using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{

    /// <summary>
    /// Combines BufferedStream, BinaryReader, and TransparentReadStream into a single optimized class.
    /// </summary>
    /// <remarks>BinaryReader calls ReadByte() many times that results in a high overhead.
    /// This improvement is noticeable in Read7BitEncodedInt() as it can't peek ahead.</remarks>
    internal class BufferedBinaryReader : IDisposable
    {
        private Stream baseStream;
        private int baseStreamPosition = 0;  // virtual Position of the base stream.
        private long maxAllowedPosition = long.MaxValue;
        private int bufferCapacity;
        private byte[] buffer;
        private int bufferOffset = 0;
        private int bufferLength = 0;
        private Encoding encoding;

        public BufferedBinaryReader(Stream stream, Encoding encoding = null, int bufferCapacity = 32768)
        {
            this.baseStream = stream;
            this.bufferCapacity = bufferCapacity;  // Note: bufferSize must be large enough for an Read operation.
            this.encoding = encoding ?? new UTF8Encoding();
            this.buffer = new byte[this.bufferCapacity];
        }

        public long Position => baseStreamPosition;

        public int? BytesCountAllowedToRead
        {
            set => maxAllowedPosition = value.HasValue ? baseStreamPosition + value.Value : long.MaxValue;
        }

        public int BytesCountAllowedToReadRemaining => maxAllowedPosition == long.MaxValue ? 0 : (int)(maxAllowedPosition - baseStreamPosition);

        public int ReadInt32()
        {
            FillBuffer(4);

            var result = (int)(buffer[bufferOffset] | buffer[bufferOffset + 1] << 8 | buffer[bufferOffset + 2] << 16 | buffer[bufferOffset + 3] << 24);
            this.bufferOffset += 4;
            this.baseStreamPosition += 4;
            return result;
        }

        // Reusable StringBuilder for ReadString().
        private StringBuilder cachedBuilder;

        // Reusable char[] for ReadString().
        private char[] charBuffer;


        public string ReadString()
        {
            int stringLength = Read7BitEncodedInt();
            int stringOffsetPos = 0;
            int readChunk = 0;

            if (stringLength == 0)
            {
                return string.Empty;
            }

            if (stringLength < 0)
            {
                throw new Exception();
            }

            if (charBuffer == null)
            {
                charBuffer = new char[this.bufferCapacity + 1];
            }

            int charRead = 0;

            if (this.bufferLength > 0)
            {
                // Read content in the buffer.
                readChunk = stringLength < (this.bufferLength - this.bufferOffset) ? stringLength : this.bufferLength - this.bufferOffset;
                charRead = this.encoding.GetChars(this.buffer, this.bufferOffset, readChunk, charBuffer, 0);
                this.bufferOffset += readChunk;
                this.baseStreamPosition += readChunk;
                if (stringLength == readChunk)
                {
                    // if the string is fits in the buffer, then cast to string without using string builder.
                    return new string(charBuffer, 0, charRead);
                }
                else
                {
                    cachedBuilder ??= new StringBuilder();
                    cachedBuilder.Append(charBuffer, 0, charRead);
                }
            }

            cachedBuilder ??= new StringBuilder();
            stringOffsetPos += readChunk;

            do
            {
                // Read up to bufferCapacity;
                readChunk = Math.Min(stringLength - stringOffsetPos, this.bufferCapacity);
                FillBuffer(readChunk);
                charRead = this.encoding.GetChars(this.buffer, this.bufferOffset, readChunk, charBuffer, 0);
                this.bufferOffset += readChunk;
                this.baseStreamPosition += readChunk;
                cachedBuilder.Append(charBuffer, 0, charRead);
                stringOffsetPos += readChunk;
            } while (stringOffsetPos < stringLength);

            string result = cachedBuilder.ToString();
            cachedBuilder.Clear();
            return result;
        }

        public long ReadInt64()
        {
            FillBuffer(8);
            uint lo = (uint)(buffer[bufferOffset + 0] | buffer[bufferOffset + 1] << 8 |
                             buffer[bufferOffset + 2] << 16 | buffer[bufferOffset + 3] << 24);
            uint hi = (uint)(buffer[bufferOffset + 4] | buffer[bufferOffset + 5] << 8 |
                             buffer[bufferOffset + 6] << 16 | buffer[bufferOffset + 7] << 24);
            var result = (long)((ulong)hi) << 32 | lo;
            this.bufferOffset += 8;
            this.baseStreamPosition += 8;
            return result;
        }

        public bool ReadBoolean()
        {
            FillBuffer(1);
            var result = (buffer[bufferOffset] != 0);
            bufferOffset++;
            this.baseStreamPosition++;
            return result;
        }

        public byte[] ReadBytes(int count)
        {
            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            FillBuffer(count);
            if (this.bufferLength == 0)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[count];
            Array.Copy(this.buffer, bufferOffset, result, 0, count);
            this.bufferOffset += count;
            this.baseStreamPosition += count;
            return result;
        }

        public byte ReadByte()
        {
            FillBuffer(1);
            return InternalReadByte();
        }

        public int Read7BitEncodedInt()
        {
            FillBuffer(5);
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                b = InternalReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return count;
        }

        public void Seek(int count, SeekOrigin current)
        {
            if (current != SeekOrigin.Current || count < 0)
            {
                throw new NotSupportedException("Only seeking from SeekOrigin.Current and forward.");
            }

            if (count == 0)
            {
                return;
            }

            // TODO: optimized to avoid writing to the buffer.
            FillBuffer(count);
            this.bufferOffset += count;
            this.baseStreamPosition += count;
        }

        public Stream Slice(int numBytes)
        {
            // create a memory stream of this number of bytes.
            if (numBytes == 0)
            {
                return Stream.Null;
            }

            if (numBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numBytes));
            }

            this.FillBuffer(numBytes);
            MemoryStream memoryStream = new MemoryStream(numBytes);
            memoryStream.Write(this.buffer, this.bufferOffset, numBytes);
            memoryStream.Position = 0;

            this.bufferOffset += numBytes;
            this.baseStreamPosition += numBytes;

            return memoryStream;
        }

        public void Dispose()
        {
            ((IDisposable)baseStream).Dispose();
        }

        /// <summary>
        /// Pre fill the buffer.
        /// </summary>
        /// <param name="numBytes">Number of bytes to prefill.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillBuffer(int numBytes)
        {
            if (this.bufferLength - this.bufferOffset >= numBytes)
            {
                return;  // enough space in the current buffer;
            }

            LoadBuffer(numBytes);
        }

        private void LoadBuffer(int numBytes)
        {
            numBytes = this.bufferCapacity;  // fill as much of the buffer as possible.
            int bytesRead = 0;
            int offset = this.bufferLength - this.bufferOffset;

            // Copy the remainder to the start.
            if (offset > 0)
            {
                Array.Copy(this.buffer, this.bufferOffset, this.buffer, 0, offset);
                bytesRead = offset;
            }

            do
            {
                offset = this.baseStream.Read(this.buffer, bytesRead, numBytes - bytesRead);
                if (offset == 0)
                {
                    break;
                }
                bytesRead += offset;
            } while (bytesRead < numBytes);

            this.bufferLength = bytesRead;
            this.bufferOffset = 0;
        }

        /// <summary>
        /// Inlined ReadByte that assumes that there is enough space created by FillBuffer().
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte InternalReadByte()
        {
            this.baseStreamPosition++;
            return buffer[bufferOffset++];
        }
    }
}
