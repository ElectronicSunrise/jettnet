using System;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using Xunit;

namespace JamesFrowen.BitPacking.Tests
{
    public class BitPackingTests : IDisposable
    {
        private JettWriter writer;
        private JettReader reader;

        public BitPackingTests()
        {
            // dont allow resizing for this test, because we test throw
            this.writer = new JettWriter(1300, false);
            this.reader = new JettReader();
        }

        public void Dispose()
        {
            this.writer.Reset();
            this.reader.Dispose();
        }

        [Fact]
        public void WritesCorrectUlongValue()
        {
            WritesCorrectUlong(0ul);
            WritesCorrectUlong(1ul);
            WritesCorrectUlong(0x_FFFF_FFFF_12Ul);
        }

        private void WritesCorrectUlong(ulong value)
        {
            this.writer.Write(value, 64);
            this.reader.Reset(this.writer.ToArray());

            ulong result = this.reader.Read(64);
            Assert.Equal(value, result);
        }

        [Fact]
        public void WritesUInt()
        {
            WritesCorrectUIntValue(0u);
            WritesCorrectUIntValue(1u);
            WritesCorrectUIntValue(0x_FFFF_FF12U);
        }

        private void WritesCorrectUIntValue(uint value)
        {
            this.writer.Write(value, 32);
            this.reader.Reset(this.writer.ToArray());

            ulong result = this.reader.Read(32);
            Assert.Equal(value, result);
        }

        [Fact]
        public void WritesCorrectVals()
        {
            WritesCorrectValues(0u, 10, 2u, 5);
            WritesCorrectValues(10u, 10, 36u, 15);
            WritesCorrectValues(1u, 1, 250u, 8);
        }

        private void WritesCorrectValues(uint value1, int bits1, uint value2, int bits2)
        {
            this.writer.Write(value1, bits1);
            this.writer.Write(value2, bits2);
            this.reader.Reset(this.writer.ToArray());

            ulong result1 = this.reader.Read(bits1);
            ulong result2 = this.reader.Read(bits2);
            Assert.Equal(value1, result1);
            Assert.Equal(value2, result2);
        }


        [Fact]
        public void CanWriteToBufferLimit()
        {
            for (int i = 0; i < 208; i++)
            {
                this.writer.Write((ulong)i, 50);
            }

            byte[] result = this.writer.ToArray();

            // written bits/8
            int expectedLength = (208 * 50) / 8;
            Assert.Equal(expectedLength, result.Length);
        }

        [Fact]
        public void WriterThrowIfWritesTooMuch()
        {
            // write 1296 up to last word
            for (int i = 0; i < 162; i++)
            {
                this.writer.Write((ulong)i, 64);
            }

            this.writer.Write(0, 63);

            bool thrown = false;
            
            try
            {
                this.writer.Write(0, 1);
            }
            catch (Exception e)
            {
                thrown = true;
            }
            
            Assert.False(thrown);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            {
                this.writer.Write(0, 1);
            });
            const int max = 162 * 64 + 64;
            Assert.Equal($"Cannot write more than {max} bits", exception.Message);
        }

        [Fact]
        public void WritesAllValueSizesCorrectly()
        {
            for (int i = 0; i < 11; i++)
            {
                for (int startPosition = 0; startPosition < 63; startPosition++)
                {
                    for (int valueBits = 0; valueBits <= 64; valueBits++)
                    {
                        ulong randomValue = ULongRandom.Next();
                        this.writer.Write(0, startPosition);

                        ulong maskedValue = randomValue & BitMask.Mask(valueBits);

                        this.writer.Write(randomValue, valueBits);
                        this.reader.Reset(this.writer.ToArray());

                        _ = this.reader.Read(startPosition);
                        ulong result = this.reader.Read(valueBits);
                        Assert.Equal(maskedValue, result);
                    }
                }
            }
        }

        [Fact]
        public void WritesAllMasksCorrectly()
        {
            // we can't use [range] args because we have to skip cases where end is over 64
            int count = 0;
            for (int start = 0; start < 64; start++)
            {
                for (int bits = 0; bits < 64; bits++)
                {
                    int end = start + bits;
                    if (end > 64)
                    {
                        continue;
                    }

                    ulong expected = SlowMask(start, end);
                    ulong actual = BitMask.OuterMask(start, end);
                    count++;
                    if (expected != actual)
                    {
                        throw new Exception($"Failed, start:{start} bits:{bits}");
                    }
                }
            }
            //UnityEngine.Debug.Log($"{count} masks tested");
        }

        /// <summary>
        /// Slower but correct mask
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SlowMask(int start, int end)
        {
            // old mask, doesn't work when bitposition before/after is multiple of 64
            //           so we need to check if values == 0 before shifting masks
            ulong mask1 = start == 0 ? 0ul : (ulong.MaxValue >> (64 - start));
            // note: new position can not be 0, so no need to worry about 
            ulong mask2 = (end & 0b11_1111) == 0 ? 0ul : (ulong.MaxValue << end /*we can use full position here as c# will mask it to just 6 bits*/);
            // mask either side of value, eg writing 4 bits at position 3: 111...111_1000_0111
            ulong mask = mask1 | mask2;
            return mask;
        }
    }

    public static class ULongRandom
    {
        static Random rand;
        static byte[] bytes;

        public static unsafe ulong Next()
        {
            if (rand == null)
            {
                rand = new System.Random();
                bytes = new byte[8];
            }

            rand.NextBytes(bytes);
            fixed (byte* ptr = &bytes[0])
            {
                return *(ulong*)ptr;
            }
        }
    }
}
