using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using StructuredLogViewer;
using Xunit;

namespace StructuredLogger.Tests
{
    public class CommandLineDiffTests
    {
        public CommandLineDiffTests()
        {
            CultureInfo.DefaultThreadCurrentCulture = new("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new("en-US");
        }

        public class CommandLineDiffer
        {
            private static bool IsSwitchToken(char token)
            {
                switch (token)
                {
                    case '/':
                    case '-':
                        return true;
                    default:
                        break;
                }

                return false;
            }

            private static char GetNextLetter(string text, int startIndex, out int endIndex)
            {
                char c = char.MinValue;

                while (startIndex < text.Length)
                {
                    c = text[startIndex];
                    if (char.IsWhiteSpace(c))
                    {
                        startIndex++;
                        continue;
                    }

                    break;
                }

                endIndex = startIndex;
                return c;
            }

            public static List<string> ParseParameters(string cmdLine, int startIndex)
            {
                var parameters = new List<string>();
                bool inQuotes = false;
                char escapeChar = '\\';
                var current = new List<char>();

                for (int i = startIndex; i < cmdLine.Length; i++)
                {
                    char c = cmdLine[i];

                    if (c == '"' && (i == 0 || cmdLine[i - 1] != escapeChar))
                    {
                        inQuotes = !inQuotes;
                    }
                    else if (char.IsWhiteSpace(c) && !inQuotes)
                    {
                        if (current.Count > 0)
                        {
                            parameters.Add(new string(current.ToArray()));
                            current.Clear();
                        }
                    }
                    else
                    {
                        if (c == escapeChar && i + 1 < cmdLine.Length && cmdLine[i + 1] == '"')
                        {
                            i++; // Skip the escape character
                            current.Add('"');
                        }
                        else
                        {
                            current.Add(c);
                        }
                    }
                }

                if (current.Count > 0)
                {
                    parameters.Add(new string(current.ToArray()));
                }

                return parameters;
            }

            public static bool TryParseExe(string commandLine, out string program)
            {
                program = "";

                char c = GetNextLetter(commandLine, 0, out int startIndex);


                if (c == '\"')
                {
                    int endIndex = commandLine.IndexOf('\"', startIndex + 1);
                    program = commandLine.Substring(startIndex + 1, endIndex - 1 - startIndex);
                    return true;
                }

                {
                    int endIndex = commandLine.IndexOf(".exe");
                    if (endIndex != -1)
                    {
                        program = commandLine.Substring(startIndex, (endIndex + 4) - startIndex);
                        return true;
                    }
                }

                return false;
            }

            public static bool TryParseCommandLine(string commandLine, out List<string> result)
            {
                result = new List<string>();
                int startIndex = 0;

                if (TryParseExe(commandLine, out string program))
                {
                    startIndex = program.Length + 1;
                    result.Add(program);
                }

                var paramResult = ParseParameters(commandLine, startIndex);
                result.AddRange(paramResult);

                return true;
            }

            private class ParameterEntry
            {
                public string Parameter { get; set; }

                public string Prefix { get; set; } = string.Empty;

                public bool Matched { get; set; } = false;

                public bool ParameterMatched { get; set; } = false;

                public static List<ParameterEntry> ToList(List<string> paramList)
                {
                    List<ParameterEntry> parameterEntries = new List<ParameterEntry>(paramList.Count);
                    string lastSwitchParam = string.Empty;

                    foreach (string param in paramList)
                    {
                        ParameterEntry paraEntry = new ParameterEntry()
                        {
                            Parameter = param,
                        };

                        if (IsSwitchToken(param[0]))
                        {
                            // Note: generally switches don't have prefix.
                            lastSwitchParam = param;
                        }
                        else if (!string.IsNullOrEmpty(lastSwitchParam))
                        {
                            // Note: generally a switch only have one argument, but there might be exceptions.
                            paraEntry.Prefix = lastSwitchParam;
                            lastSwitchParam = string.Empty;
                        }

                        parameterEntries.Add(paraEntry);
                    }

                    return parameterEntries;
                }
            }

            public static bool TryCompare(string left, string right, out List<string> leftRemainder, out List<string> rightRemainder)
            {
                leftRemainder = new List<string>();
                rightRemainder = new List<string>();

                if (!TryParseCommandLine(left, out var cmdLeft) || !TryParseCommandLine(right, out var cmdRight))
                {
                    return false;
                }

                // First pass: Matches with the same index.

                var leftParams = ParameterEntry.ToList(cmdLeft);
                var rightParams = ParameterEntry.ToList(cmdRight);

                for (int i = 0; i < leftParams.Count; i++)
                {
                    if (leftParams[i].Parameter == rightParams[i].Parameter)
                    {
                        if (leftParams[i].Prefix == rightParams[i].Prefix)
                        {
                            leftParams[i].Matched = true;
                            rightParams[i].Matched = true;
                        }
                    }
                }

                // Second pass: Find n^2 all matches.

                var leftParamRemainder = leftParams.Where(p => !p.Matched).ToList();
                var rightParamRemainder = rightParams.Where(p => !p.Matched).ToList();

                for (int i = 0; i < leftParamRemainder.Count; i++)
                {
                    for (int j = 0; j < rightParamRemainder.Count; j++)
                    {
                        if (!rightParamRemainder[j].Matched &&
                            leftParamRemainder[i].Parameter == rightParamRemainder[j].Parameter)
                        {
                            if (leftParamRemainder[i].Prefix == rightParamRemainder[j].Prefix)
                            {
                                leftParamRemainder[i].Matched = true;
                                rightParamRemainder[j].Matched = true;
                            }
                        }
                    }
                }

                // Third Pass: Find standalone switches.

                for (int i = 0; i < leftParamRemainder.Count; i++)
                {
                    for (int j = 0; j < rightParamRemainder.Count; j++)
                    {
                        if (!rightParamRemainder[j].Matched &&
                            leftParamRemainder[i].Parameter == rightParamRemainder[j].Parameter)
                        {
                            leftParamRemainder[i].Matched = true;
                            rightParamRemainder[j].Matched = true;
                        }
                    }
                }

                // Populate output remainder

                foreach (var param in leftParamRemainder)
                {
                    if (!param.Matched)
                    {
                        leftRemainder.Add(param.Parameter);
                    }
                }

                foreach (var param in rightParamRemainder)
                {
                    if (!param.Matched)
                    {
                        rightRemainder.Add(param.Parameter);
                    }
                }

                return true;
            }
        }

        [Fact]
        public void CompareSame_Test()
        {
            var Test = (string s) =>
            {
                Assert.True(CommandLineDiffer.TryCompare(s, s, out var lr, out var rr));
                Assert.Empty(lr);
                Assert.Empty(rr);
            };

            Test("");
            Test("csc.exe");
            Test("csc.exe -help");
            Test("csc.exe --help");
            Test("csc.exe /help");
            Test("csc.exe -pathmap:file1.txt=file2.txt");
            Test("csc.exe -out:program.exe file.cs");
        }

        [Fact]
        public void CompareDifferent_Test()
        {
            Compare_Helper(@"", @"");
            Compare_Helper(@"csc.exe", @"csc.exe");
            Compare_Helper(@"csc.exe source.cs", @"csc.exe -out:file.exe source.cs", @"-out:file.exe");
            Compare_Helper(@"csc.exe -out:file.exe source.cs", @"csc.exe -out:file.exe -embed source.cs", @"-embed");
        }

        [Fact]
        public void ParseCommandLine_Test()
        {
            ParseCommandLine_Helper(@"csc.exe --switch", @"csc.exe", @"--switch");
            ParseCommandLine_Helper(@"csc.exe --switch file.txt", @"csc.exe", @"--switch", @"file.txt");
            ParseCommandLine_Helper(@"csc.exe --switch1 ""--switch2""", @"csc.exe", @"--switch1", @"--switch2");
        }

        [Fact]
        public void ParseParameters_Test()
        {
            ParseParameters_Helper(@"one", @"one");
            ParseParameters_Helper(@"""one""", @"one");
            ParseParameters_Helper(@"""one two""", @"one two");
            ParseParameters_Helper(@"one two", @"one", @"two");
        }

        [Fact]
        public void ProgramPaths_Test()
        {
            TryParseExe_Helper(@"csc.exe", true, "csc.exe");
            TryParseExe_Helper(@"""folder\csc.exe""", true, @"folder\csc.exe");
            TryParseExe_Helper(@"C:\Program Folder(x86)\folder\csc.exe", true, @"C:\Program Folder(x86)\folder\csc.exe");
            TryParseExe_Helper(@"""C:\Program Folder(x86)\folder\csc.exe""", true, @"C:\Program Folder(x86)\folder\csc.exe");
        }

        private void Compare_Helper(string leftString, string rightString, params string[] expected)
        {
            var result = CommandLineDiffer.TryCompare(leftString, rightString, out var leftRemainder, out var rightRemainder);
            Assert.True(result);
            Assert.Equal(expected.Length, leftRemainder.Count + rightRemainder.Count);

            var actual = leftRemainder.Concat(rightRemainder).ToList();

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        private void ParseCommandLine_Helper(string testString, params string[] expected)
        {
            var result = CommandLineDiffer.TryParseCommandLine(testString, out var actual);
            Assert.True(result);
            Assert.Equal(expected.Length, actual.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        private void ParseParameters_Helper(string testString, params string[] expected)
        {
            var actual = CommandLineDiffer.ParseParameters(testString, 0);

            Assert.Equal(expected.Length, actual.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        private void TryParseExe_Helper(string testString, bool expected, string outputString)
        {
            bool actual = CommandLineDiffer.TryParseExe(testString, out string outputValue);
            Assert.True(actual);
            Assert.Equal(outputString, outputValue);
        }
    }
}
