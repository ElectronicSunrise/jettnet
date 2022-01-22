using System;
using Xunit;

namespace JamesFrowen.BitPacking.Tests
{
    public class BitPackingCopyFromOtherTests : IDisposable
    {
        private JettWriter writer;
        private JettWriter otherWriter;
        private JettReader reader;
        
        public BitPackingCopyFromOtherTests()
        {
            this.writer = new JettWriter(1300);
            this.otherWriter = new JettWriter(1300);
            this.reader = new JettReader();
        }

        public void Dispose()
        {
            this.writer.Reset();
            this.otherWriter.Reset();
            this.reader.Dispose();
        }

        [Fact]
        public void CopyFromOtherWriterAligned()
        {
            this.otherWriter.Write(1, 8);
            this.otherWriter.Write(2, 8);
            this.otherWriter.Write(3, 8);
            this.otherWriter.Write(4, 8);
            this.otherWriter.Write(5, 8);


            this.writer.CopyFromWriter(this.otherWriter, 0, 5 * 8);

            var segment = this.writer.ToArraySegment();
            this.reader.Reset(segment);
            
            Assert.Equal((byte)1, this.reader.Read(8));
            Assert.Equal((byte)2, this.reader.Read(8));
            Assert.Equal((byte)3, this.reader.Read(8));
            Assert.Equal((byte)4, this.reader.Read(8));
            Assert.Equal((byte)5, this.reader.Read(8));
        }

        [Fact]
        public void CopyFromOtherWriterUnAligned()
        {
            this.otherWriter.Write(1, 6);
            this.otherWriter.Write(2, 7);
            this.otherWriter.Write(3, 8);
            this.otherWriter.Write(4, 9);
            this.otherWriter.Write(5, 10);

            this.writer.Write(1, 3);

            this.writer.CopyFromWriter(this.otherWriter, 0, 40);

            var segment = this.writer.ToArraySegment();
            this.reader.Reset(segment);

            Assert.Equal((byte)1, this.reader.Read(3));
            Assert.Equal((byte)1, this.reader.Read(6));
            
            Assert.Equal((byte)2, this.reader.Read(7));
            Assert.Equal((byte)3, this.reader.Read(8));
            Assert.Equal((byte)4, this.reader.Read(9));
            Assert.Equal((byte)5, this.reader.Read(10));
        }

        [Fact]
        public void CopyFromOtherWriterUnAlignedBig()
        {
            for (int i = 0; i < 101; i++)
            {
                ulong value1 = (ulong)TestRandom.Range(0, 20000);
                ulong value2 = (ulong)TestRandom.Range(0, 20000);
                ulong value3 = (ulong)TestRandom.Range(0, 20000);
                ulong value4 = (ulong)TestRandom.Range(0, 20000);
                ulong value5 = (ulong)TestRandom.Range(0, 20000);
                this.otherWriter.Write(value1, 46);
                this.otherWriter.Write(value2, 47);
                this.otherWriter.Write(value3, 48);
                this.otherWriter.Write(value4, 49);
                this.otherWriter.Write(value5, 50);

                this.writer.WriteUInt64(5);
                this.writer.Write(1, 3);
                this.writer.WriteByte(171);

                this.writer.CopyFromWriter(this.otherWriter, 0, 240);

                var segment = this.writer.ToArraySegment();
                this.reader.Reset(segment);

                Assert.Equal(5ul, this.reader.ReadUInt64());
                Assert.Equal(1ul, this.reader.Read(3));
                Assert.Equal(171, this.reader.ReadByte());
                Assert.Equal(value1, this.reader.Read(46));
                Assert.Equal(value2, this.reader.Read(47));
                Assert.Equal(value3, this.reader.Read(48));
                Assert.Equal(value4, this.reader.Read(49));
                Assert.Equal(value5, this.reader.Read(50));
            }
        }

        [Fact]
        public void CopyFromOtherWriterUnAlignedBigOtherUnaligned()
        {
            for (int l = 0; l < 101; l++)
            {
                for (int i = 0; i < 10; i++)
                {
                    this.otherWriter.Write(12, 20);
                }
                
                ulong value1 = (ulong)TestRandom.Range(0, 20000);
                ulong value2 = (ulong)TestRandom.Range(0, 20000);
                ulong value3 = (ulong)TestRandom.Range(0, 20000);
                ulong value4 = (ulong)TestRandom.Range(0, 20000);
                ulong value5 = (ulong)TestRandom.Range(0, 20000);
                this.otherWriter.Write(value1, 46);
                this.otherWriter.Write(value2, 47);
                this.otherWriter.Write(value3, 48);
                this.otherWriter.Write(value4, 49);
                this.otherWriter.Write(value5, 50);

                this.writer.WriteUInt64(5);
                this.writer.Write(1, 3);
                this.writer.WriteByte(171);

                this.writer.CopyFromWriter(this.otherWriter, 200, 240);

                var segment = this.writer.ToArraySegment();
                this.reader.Reset(segment);
                
                Assert.Equal(5ul, this.reader.ReadUInt64());
                Assert.Equal(1ul, this.reader.Read(3));
                Assert.Equal(171, this.reader.ReadByte());
                Assert.Equal(value1, this.reader.Read(46));
                Assert.Equal(value2, this.reader.Read(47));
                Assert.Equal(value3, this.reader.Read(48));
                Assert.Equal(value4, this.reader.Read(49));
                Assert.Equal(value5, this.reader.Read(50));
            }
        }
    }
}
