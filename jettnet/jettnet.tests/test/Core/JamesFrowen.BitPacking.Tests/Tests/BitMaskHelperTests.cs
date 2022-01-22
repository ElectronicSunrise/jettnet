using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace JamesFrowen.BitPacking.Tests
{
    public class BitMaskHelperTests
    {
        /// <summary>
        /// slow way of creating correct mask
        /// </summary>
        static ulong slowMask(int bits)
        {
            ulong mask = 0;
            for (int i = 0; i < bits; i++)
            {
                mask |= 1ul << i;
            }

            return mask;
        }

        [Fact]
        [Description("manually checking edge cases to be sure")]
        public void MaskValueIsCorrect0()
        {
            ulong mask = BitMask.Mask(0);
            Assert.Equal(0ul, mask);
        }

        [Fact]
        [Description("manually checking edge cases to be sure")]
        public void MaskValueIsCorrect63()
        {
            ulong mask = BitMask.Mask(63);
            Assert.Equal(0x7FFFFFFFFFFFFFFFul, mask);
        }

        [Fact]
        [Description("manually checking edge cases to be sure")]
        public void MaskValueIsCorrect64()
        {
            ulong mask = BitMask.Mask(64);
            Assert.Equal(0xFFFFFFFFFFFFFFFFul, mask);
        }

        [Fact]
        public void MaskValueIsCorrect()
        {
            for (int i = 0; i < 65; i++)
            {
                int bits = i;
                            
                ulong mask     = BitMask.Mask(bits);
                ulong expected = slowMask(bits);

                Assert.Equal(expected, mask);
            }
        }
    }
}
