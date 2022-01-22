using System;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace JamesFrowen.BitPacking.Tests.Packers
{
    public class FloatPackerCreateTest : PackerTestBase
    {
        [Fact]
        public void CreateUsingPrecsion()
        {
            Assert.Equal(8, PrecisionCreate(1));
            Assert.Equal(11, PrecisionCreate(0.1f));
            Assert.Equal(15, PrecisionCreate(0.01f));
        }

        private int PrecisionCreate(float precision)
        {
            writer.Reset();

            var packer = new FloatPacker(100, precision);

            packer.Pack(this.writer, 1f);
            
            return this.writer.BitPosition;
        }

        [Fact]
        public void PackFromBitCountPacksToCorrectCount()
        {
            for (int bitCount = 1; bitCount <= 30; bitCount++)
            {
                writer.Reset();
                var packer = new FloatPacker(100, bitCount);

                packer.Pack(this.writer, 1f);

                Assert.Equal(bitCount, this.writer.BitPosition);
            }
        }

        [Fact]
        public void ThrowsIfBitCountIsLessThan1()
        {
            for (int bitCount = -10; bitCount <= 0; bitCount++)
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                {
                    _ = new FloatPacker(10, bitCount);
                });

                var expected = new ArgumentException("Bit count is too low, bit count should be between 1 and 30", "bitCount");
             
                Assert.Equal(expected.Message, exception.Message);
            }
        }

        [Fact]
        public void ThrowsIfBitCountIsGreaterThan30()
        {
            for (int bitCount = 31; bitCount < 41; bitCount++)
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                {
                    _ = new FloatPacker(10, bitCount);
                });

                var expected = new ArgumentException("Bit count is too high, bit count should be between 1 and 30", "bitCount");
                Assert.Equal(expected.Message, exception.Message);
            }
        }

        [Fact]
        public void ThrowsIfMaxIsZero()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = new FloatPacker(0, 1);
            });

            var expected = new ArgumentException("Max can not be 0", "max");
            Assert.Equal(expected.Message, exception.Message);
        }
    }
}
