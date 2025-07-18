using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StructuredLogger
{
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
                if (i < rightParams.Count && leftParams[i].Parameter == rightParams[i].Parameter)
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
}
