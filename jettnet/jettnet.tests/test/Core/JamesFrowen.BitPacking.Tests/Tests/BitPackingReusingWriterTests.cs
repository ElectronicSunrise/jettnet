using System;
using Xunit;

namespace JamesFrowen.BitPacking.Tests
{
    public class BitPackingResizeTest : IDisposable
    {
        private JettWriter writer;
        private JettReader reader;

        public BitPackingResizeTest()
        {
            this.writer = new JettWriter(1300, true);
            this.reader = new JettReader();
        }

        public void Dispose()
        {
            // we have to clear these each time so that capactity doesn't effect other tests
            this.writer.Reset();
            this.writer = null;
            this.reader.Dispose();
            this.reader = null;
        }

        [Fact]
        public void ResizesIfWritingOverCapacity()
        {
            int overCapacity = (1300 / 8) + 10;
            Assert.Equal(1304, this.writer.ByteCapacity);
            for (int i = 0; i < overCapacity; i++)
            {
                this.writer.WriteUInt64((ulong)i);
            }

            Assert.Equal((1304 * 2), this.writer.ByteCapacity);
        }


        [Fact]
        public void WillResizeMultipleTimes()
        {
            int overCapacity = ((1300 / 8) + 10) * 10; // 1720 * 8 = 13760 bytes

            Assert.Equal(1304, this.writer.ByteCapacity);
            
            for (int i = 0; i < overCapacity; i++)
            {
                this.writer.WriteUInt64((ulong)i);
            }

            Assert.Equal(20_864, this.writer.ByteCapacity);
        }

        [Fact]
        public void ResizedArrayContainsAllData()
        {
            int overCapacity = (1300 / 8) + 10;
            for (int i = 0; i < overCapacity; i++)
            {
                this.writer.WriteUInt64((ulong)i);
            }


            var segment = this.writer.ToArraySegment();
            this.reader.Reset(segment);
            for (int i = 0; i < overCapacity; i++)
            {
                Assert.Equal((ulong)i, this.reader.ReadUInt64());
            }
        }
    }
    
    public class BitPackingReusingWriterTests : IDisposable, IClassFixture<BitPackingReusingWriterTests>
    {
        private readonly JettWriter writer;
        private readonly JettReader reader;
        
        public BitPackingReusingWriterTests(JettWriter writer, JettReader reader)
        {
            this.writer = writer;
            this.reader = reader;
        }

        public void Dispose()
        {
            this.writer.Reset();
            this.reader.Dispose();
        }

        [Fact]
        public void WriteUShortAfterReset()
        {
            ushort value1 = 0b0101;
            ushort value2 = 0x1000;

            // write first value
            this.writer.WriteUInt16(value1);

            this.reader.Reset(this.writer.ToArray());
            ushort out1 = this.reader.ReadUInt16();
            Assert.Equal(value1, out1);

            // reset and write 2nd value
            this.writer.Reset();

            this.writer.WriteUInt16(value2);

            this.reader.Reset(this.writer.ToArray());
            ushort out2 = this.reader.ReadUInt16();
            Assert.Equal(value2, out2);
        }

        [Fact]
        public void WriteULongAfterReset()
        {
            UlongReset(0b0101ul, 0x1000ul);
            UlongReset(0xffff_0000_ffff_fffful, 0x0000_ffff_1111_0000ul);
        }

        private void UlongReset(ulong value1, ulong value2)
        {
            // write first value
            this.writer.WriteUInt64(value1);

            this.reader.Reset(this.writer.ToArray());
            ulong out1 = this.reader.ReadUInt64();
            Assert.Equal(value1, out1);

            // reset and write 2nd value
            this.writer.Reset();

            this.writer.WriteUInt64(value2);

            this.reader.Reset(this.writer.ToArray());
            ulong out2 = this.reader.ReadUInt64();

            Assert.Equal(value2, out2);
        }

        [Fact]
        public void WriteULongWriteBitsAfterReset()
        {
            UlongWriteReset(0b0101ul, 0x1000ul);
            UlongWriteReset(0xffff_0000_ffff_fffful, 0x0000_ffff_1111_0000ul);
        }

        private void UlongWriteReset(ulong value1, ulong value2)
        {
            // write first value
            this.writer.Write(value1, 64);

            this.reader.Reset(this.writer.ToArray());
            ulong out1 = this.reader.Read(64);
            Assert.Equal(value1, out1);

            // reset and write 2nd value
            this.writer.Reset();

            this.writer.Write(value2, 64);

            this.reader.Reset(this.writer.ToArray());
            ulong out2 = this.reader.Read(64);
            Assert.Equal(value2, out2);
        }
    }
}
