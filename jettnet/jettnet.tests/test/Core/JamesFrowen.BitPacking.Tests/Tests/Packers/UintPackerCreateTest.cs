using System;
using Xunit;

namespace JamesFrowen.BitPacking.Tests.Packers
{
    public class UintPackerCreateTest : PackerTestBase
    {
        [Fact]
        public void TestCreateUsing2Values()
        {
            Assert.Equal((6+1), CreatesUsing2Values(0ul, 50ul, 1000ul));
            Assert.Equal((6+1), CreatesUsing2Values(60ul, 50ul, 1000ul));
            Assert.Equal((10+2), CreatesUsing2Values(100ul, 50ul, 1000ul));
            Assert.Equal((9+1), CreatesUsing2Values(100ul, 500ul, 100000ul));
            Assert.Equal((9+1), CreatesUsing2Values(501ul, 500ul, 100000ul));
            Assert.Equal((17+2), CreatesUsing2Values(5_000ul, 500ul, 100000ul));
            Assert.Equal((64+2), CreatesUsing2Values(5_000_000ul, 500ul, 100000ul));
        }
        
        public int CreatesUsing2Values(ulong inValue, ulong smallValue, ulong mediumValue)
        {
            var packer = new VariableIntPacker(smallValue, mediumValue);
            packer.PackUlong(this.writer, inValue);
            return this.writer.BitPosition;
        }


        [Fact]
        public void TestCreateUsing3Vals()
        {
            Assert.Equal((6+1), CreatesUsing3Values(0ul, 50ul, 1000ul, 50_000ul));
            Assert.Equal((6+1), CreatesUsing3Values(60ul, 50ul, 1000ul, 50_000ul));
            Assert.Equal((10+2), CreatesUsing3Values(100ul, 50ul, 1000ul, 50_000ul));
            Assert.Equal((16+2), CreatesUsing3Values(100ul, 500ul, 100_000ul, 50_000_000ul));
            Assert.Equal((9+1), CreatesUsing3Values(100ul, 500ul, 100_000ul, 50_000_000ul));
            Assert.Equal((9+1), CreatesUsing3Values(501ul, 500ul, 100_000ul, 50_000_000ul));
            Assert.Equal((17+2), CreatesUsing3Values(5_000ul, 500ul, 100_000ul, 50_000_000ul));
            Assert.Equal((26+2), CreatesUsing3Values(5_000_000ul, 500ul, 100_000ul, 50_000_000ul));
        }

        private int CreatesUsing3Values(ulong inValue, ulong smallValue, ulong mediumValue, ulong largeValue)
        {
            var packer = new VariableIntPacker(smallValue, mediumValue, largeValue);
            packer.PackUlong(this.writer, inValue);
            return this.writer.BitPosition;
        }


        [Fact]
        public void ThrowsIfSmallBitIsZero()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = VariableIntPacker.FromBitCount(0, 10);
            });
            var expected = new ArgumentException("Small value can not be zero", "smallBits");
        
            Assert.Equal(expected.Message, exception.Message);
        }
        
        [Fact]
        public void ThrowsIfMediumLessThanSmall()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = VariableIntPacker.FromBitCount(6, 5);
            });
            var expected = new ArgumentException("Medium value must be greater than small value", "mediumBits");
            Assert.Equal(expected.Message, exception.Message);
        }
        
        [Fact]
        public void ThrowsIfLargeLessThanMedium()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = VariableIntPacker.FromBitCount(4, 10, 8);
            });
            var expected = new ArgumentException("Large value must be greater than medium value", "largeBits");
            
            Assert.Equal(expected.Message, exception.Message);
        }
        
        [Fact]
        public void ThrowsIfLargeIsOver64()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = VariableIntPacker.FromBitCount(5, 10, 65);
            });
            var expected = new ArgumentException("Large bits must be 64 or less", "largeBits");
            
            Assert.Equal(expected.Message, exception.Message);
        }
        
        [Fact]
        public void ThrowsIfMediumIsOver62()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = VariableIntPacker.FromBitCount(5, 63);
            });
            var expected = new ArgumentException("Medium bits must be 62 or less", "mediumBits");
            
            Assert.Equal(expected.Message, exception.Message);
        }

        [Fact]
        public void ThrowsWhenValueIsOverLargeValue()
        {
            var packer = VariableIntPacker.FromBitCount(1, 2, 3, true);
            ArgumentOutOfRangeException exception1 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                packer.PackUlong(this.writer, 20);
            });
            ArgumentOutOfRangeException exception2 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                packer.PackUint(this.writer, 20);
            });
            ArgumentOutOfRangeException exception3 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                packer.PackUlong(this.writer, 20);
            });
            var expected = new ArgumentOutOfRangeException("value", 20, $"Value is over max of {7}");
            
            Assert.Equal(expected.Message, exception1.Message);
            Assert.Equal(expected.Message, exception2.Message);
            Assert.Equal(expected.Message, exception3.Message);
        }

        [Fact]
        public void WritesMaxIfOverLargeValue()
        {
            WriteMaxIfOverLargeValue(20ul, 3);
            WriteMaxIfOverLargeValue(260ul, 8);
            WriteMaxIfOverLargeValue(50_000ul, 10);
        }
        
        private void WriteMaxIfOverLargeValue(ulong inValue, int largeBits)
        {
            ulong max    = BitMask.Mask(largeBits);
            var   packer = VariableIntPacker.FromBitCount(1, 2, largeBits, false);
            
            bool threw = false;
            
            try
            {
                packer.PackUlong(this.writer, inValue);
                packer.PackUint(this.writer, (uint)inValue);
                packer.PackUlong(this.writer, (ushort)inValue);
            }
            catch (Exception e)
            {
                threw = true;
            }
            
            Assert.False(threw);
            
            JettReader reader  = this.GetReader();
            ulong         unpack1 = packer.UnpackUlong(reader);
            uint          unpack2 = packer.UnpackUint(reader);
            ushort        unpack3 = packer.UnpackUshort(reader);

            Assert.Equal(max, unpack1);
            Assert.Equal(max, unpack2);
            Assert.Equal(max, unpack3);
        }
        
    }
}
