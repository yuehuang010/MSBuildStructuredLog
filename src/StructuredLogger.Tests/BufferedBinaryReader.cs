﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class BufferedBinaryReaderTest
    {
        [Fact]
        public void Test_ReadString()
        {
            var testString = new string[] { "foobar", "catbar", "dogbar" };
            var stream = new MemoryStream();

            var writer = new BinaryWriter(stream);
            foreach (string test in testString)
            {
                writer.Write(test);
            }

            stream.Position = 0;

            var reader = new BufferedBinaryReader(stream);
            foreach (string test in testString)
            {
                string result = reader.ReadString();
                Assert.Equal(test, result);
            }
        }

        /// <summary>
        /// Test ReadString support strings that are larger than the internal buffer.
        /// </summary>
        [Fact]
        public void Test_ReadString_LongString()
        {
            var testString = new string[]
            {
                "FoobarCatbarDogbarDiveBarSandBar",
                "FoobarCatbarDogbarDiveBarSandBar2",
                "FoobarCatbarDogbarDiveBarSandBar3",
            };

            var stream = new MemoryStream();

            var writer = new BinaryWriter(stream);
            foreach (string test in testString)
            {
                writer.Write(test);
            }

            stream.Position = 0;

            var reader = new BufferedBinaryReader(stream, bufferCapacity: 10);
            foreach (string test in testString)
            {
                string result = reader.ReadString();
                Assert.Equal(test, result);
            }
        }

        [Fact]
        public void Test_ReadInt64()
        {
            Int64 test = Int64.MaxValue;
            var stream = new MemoryStream();

            var writer = new BinaryWriter(stream);
            writer.Write(test);

            stream.Position = 0;

            var reader = new BufferedBinaryReader(stream);
            var result = reader.ReadInt64();

            Assert.Equal(test, result);
        }

        [Fact]
        public void Test_Read7BitEncodedInt()
        {
            int test = 100;
            var stream = new MemoryStream();

            var writer = new BinaryWriter(stream);
            writer.Write7BitEncodedInt(test);

            stream.Position = 0;

            var reader = new BufferedBinaryReader(stream);
            var result = reader.Read7BitEncodedInt();

            Assert.Equal(test, result);
        }

        [Fact]
        public void Test_Read7BitEncodedInt_VariedLength()
        {
            int[] ints = new[] { 0, 1, 10, 254, 255, 256, 500, 1024, 1025, 100_000, 100_000_000, int.MaxValue };
            var stream = new MemoryStream();

            var writer = new BinaryWriter(stream);
            foreach (int number in ints)
            {
                writer.Write7BitEncodedInt(number);
            }

            stream.Position = 0;

            int result = 0;

            var reader = new BufferedBinaryReader(stream);
            foreach (int number in ints)
            {
                result = reader.Read7BitEncodedInt();
                Assert.Equal(number, result);
            }
        }

        [Fact]
        public void Test_FillBuffer_Int64()
        {
            Int64 initialCount = 200; // a large enough value to saturate a buffer
            Int64 test = initialCount;
            var stream = new MemoryStream();

            var writer = new BinaryWriter(stream);
            while (test > 0)
            {
                writer.Write(test);
                test--;
            }

            stream.Position = 0;
            test = initialCount;
            Int64 result = 0;

            var reader = new BufferedBinaryReader(stream, bufferCapacity: 20);  // Reduced buffer size
            while (test > 0)
            {
                result = reader.ReadInt64();
                Assert.Equal(test, result);
                test--;
            }
        }

        [Fact]
        public void Test_FillBuffer_Read7Bit()
        {
            int initialCount = 200; // a large enough value to saturate a buffer
            int test = initialCount;
            var stream = new MemoryStream();

            var writer = new BinaryWriter(stream);
            while (test > 0)
            {
                writer.Write7BitEncodedInt(test);
                test--;
            }

            stream.Position = 0;
            test = initialCount;
            int result = 0;

            var reader = new BufferedBinaryReader(stream, bufferCapacity: 20);  // Reduced buffer size
            while (test > 0)
            {
                result = reader.Read7BitEncodedInt();
                Assert.Equal(test, result);
                test--;
            }
        }

        [Fact]
        public void Test_FillBuffer_ReadString()
        {
            var testString = new string[] { "foobar", "catbar", "dogbar" };
            var stream = new MemoryStream();

            var writer = new BinaryWriter(stream);
            foreach (string test in testString)
            {
                writer.Write(test);
            }

            stream.Position = 0;

            var reader = new BufferedBinaryReader(stream, bufferCapacity: 10);
            foreach (string test in testString)
            {
                string result = reader.ReadString();
                Assert.Equal(test, result);
            }
        }
    }
}
