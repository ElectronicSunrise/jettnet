using System;
using Xunit;

namespace JamesFrowen.BitPacking.Tests
{
    public class BitPackingProperties : IDisposable
    {
        private readonly JettWriter writer = new JettWriter(1300);
        private readonly JettReader reader = new JettReader();

        readonly byte[] sampleData    = new byte[10] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};
        const    int    BITS_PER_BYTE = 8;

        public void Dispose()
        {
            this.writer.Reset();
            this.reader.Dispose();
        }

        [Fact]
        public void WriterBitPositionStartsAtZero()
        {
            Assert.Equal(0, this.writer.BitPosition);
        }

        [Fact]
        public void WriterByteLengthStartsAtZero()
        {
            Assert.Equal(0, this.writer.ByteLength);
        }

        [Fact]
        public void ReaderBitPositionStartsStartsAtZero()
        {
            this.reader.Reset(this.sampleData);
            Assert.Equal(0, this.reader.BitPosition);
        }

        [Fact]
        public void ReaderBytePositionStartsStartsAtZero()
        {
            this.reader.Reset(this.sampleData);
            Assert.Equal(0, this.reader.BytePosition);
        }

        [Fact]
        public void ReaderBitLengthStartsStartsAtArrayLength()
        {
            this.reader.Reset(this.sampleData);

            Assert.Equal(this.sampleData.Length * BITS_PER_BYTE, this.reader.BitLength);
        }

        [Fact]
        public void WriterBitPositionIncreasesAfterWriting()
        {
            this.writer.Write(0, 15);
            Assert.Equal(15, this.writer.BitPosition);

            this.writer.Write(0, 50);
            Assert.Equal(65, this.writer.BitPosition);
        }

        [Fact]
        public void WriterByteLengthIncreasesAfterWriting_ShouldRoundUp()
        {
            this.writer.Write(0, 15);
            Assert.Equal(2, this.writer.ByteLength);

            this.writer.Write(0, 50);
            Assert.Equal(9, this.writer.ByteLength);
        }

        [Fact]
        public void ReaderBitPositionIncreasesAfterReading()
        {
            this.reader.Reset(this.sampleData);
            _ = this.reader.Read(15);
            Assert.Equal(15, this.reader.BitPosition);

            _ = this.reader.Read(50);
            Assert.Equal(65, this.reader.BitPosition);
        }

        [Fact]
        public void ReaderBytePositionIncreasesAfterReading_ShouldRoundUp()
        {
            this.reader.Reset(this.sampleData);
            _ = this.reader.Read(15);
            Assert.Equal(2, this.reader.BytePosition);

            _ = this.reader.Read(50);
            Assert.Equal(9, this.reader.BytePosition);
        }

        [Fact]
        public void ReaderBitLengthDoesnotIncreasesAfterReading()
        {
            this.reader.Reset(this.sampleData);
            _ = this.reader.Read(15);
            Assert.Equal(this.sampleData.Length * BITS_PER_BYTE, this.reader.BitLength);

            _ = this.reader.Read(50);
            Assert.Equal(this.sampleData.Length * BITS_PER_BYTE, this.reader.BitLength);
        }
    }
}