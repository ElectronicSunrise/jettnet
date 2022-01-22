using Xunit;

namespace JamesFrowen.BitPacking.Tests.Packers
{
    public class BitHelperTest
    {
        [Fact]
        public void ReturnCorrectBitCountForMaxPrecision()
        {
            Assert.Equal(7, BitHelper.BitCount(2, 0.05f));
            Assert.Equal(11, BitHelper.BitCount(100, 0.1f));
            Assert.Equal(10, BitHelper.BitCount(.707f, 0.002f));
            Assert.Equal(15, BitHelper.BitCount(1000, 0.1f));
            Assert.Equal(19, BitHelper.BitCount(2000, 0.01f));
            Assert.Equal(11, BitHelper.BitCount(1023, 1));
            Assert.Equal(7, BitHelper.BitCount(16, 0.5f));
        }

        [Fact]
        public void ReturnCorrectBitCountForRange()
        {
            Assert.Equal(1, BitHelper.BitCount(0b1UL));
            Assert.Equal(2, BitHelper.BitCount(0b10UL));
            Assert.Equal(2, BitHelper.BitCount(0b11UL));
            Assert.Equal(3, BitHelper.BitCount(0b100UL));
            Assert.Equal(3, BitHelper.BitCount(0b101UL));
            Assert.Equal(3, BitHelper.BitCount(0b110UL));
            Assert.Equal(3, BitHelper.BitCount(0b111UL));
            Assert.Equal(4, BitHelper.BitCount(8UL));
            Assert.Equal(4, BitHelper.BitCount(15UL));
            Assert.Equal(5, BitHelper.BitCount(16UL));
            Assert.Equal(5, BitHelper.BitCount(31UL));
            Assert.Equal(6, BitHelper.BitCount(32UL));
            Assert.Equal(6, BitHelper.BitCount(63UL));
            Assert.Equal(8, BitHelper.BitCount(255UL));
            Assert.Equal(9, BitHelper.BitCount(256UL));
        }
    }
}
