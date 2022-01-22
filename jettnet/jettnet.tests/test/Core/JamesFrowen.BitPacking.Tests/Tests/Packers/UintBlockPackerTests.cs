using System;
using Xunit;
using Random = System.Random;

namespace JamesFrowen.BitPacking.Tests.Packers
{
    public class UintBlockPackerTests : PackerTestBase
    {
        readonly Random random = new Random();
        readonly int    blockSize;

        public UintBlockPackerTests(int blockSize)
        {
            this.blockSize = blockSize;
        }
        
        ulong GetRandonUlongBias()
        {
            return (ulong) (Math.Abs(this.random.NextDouble() - this.random.NextDouble()) * ulong.MaxValue);
        }

        uint GetRandonUintBias()
        {
            return (uint) (Math.Abs(this.random.NextDouble() - this.random.NextDouble()) * uint.MaxValue);
        }

        ushort GetRandonUshortBias()
        {
            return (ushort) (Math.Abs(this.random.NextDouble() - this.random.NextDouble()) * ushort.MaxValue);
        }

        [Fact]
        public void UnpacksCorrectUlongValue()
        {
            for (int i = 0; i <= 1000; i++)
            {
                ulong start = this.GetRandonUlongBias();
                VariableBlockPacker.Pack(this.writer, start, this.blockSize);
                ulong unpacked = VariableBlockPacker.Unpack(this.GetReader(), this.blockSize);

                Assert.Equal(start, unpacked);
            }
        }

        [Fact]
        public void UnpacksCorrectUintValue()
        {
            for (int i = 0; i <= 1000; i++)
            {
                uint start = this.GetRandonUintBias();
                VariableBlockPacker.Pack(this.writer, start, this.blockSize);
                ulong unpacked = VariableBlockPacker.Unpack(this.GetReader(), this.blockSize);

                Assert.Equal(start, unpacked);
            }
        }

        [Fact]
        public void UnpacksCorrectUshortValue()
        {
            for (int i = 0; i <= 1000; i++)
            {
                ushort start = this.GetRandonUshortBias();
                VariableBlockPacker.Pack(this.writer, start, this.blockSize);
                ulong unpacked = VariableBlockPacker.Unpack(this.GetReader(), this.blockSize);

                Assert.Equal(start, unpacked);
            }
        }

        [Fact]
        public void WritesNplus1BitsPerBlock()
        {
            uint zero = 0u;
            VariableBlockPacker.Pack(this.writer, zero, this.blockSize);
            Assert.Equal(this.blockSize + 1, this.writer.BitPosition);

            ulong unpacked = VariableBlockPacker.Unpack(this.GetReader(), this.blockSize);
            Assert.Equal(zero, unpacked);
        }

        [Fact]
        public void WritesNplus1BitsPerBlock_bigger()
        {
            uint aboveBlockSize = (1u << this.blockSize) + 1u;
            VariableBlockPacker.Pack(this.writer, aboveBlockSize, this.blockSize);
            Assert.Equal(2*(this.blockSize + 1), this.writer.BitPosition);

            ulong unpacked = VariableBlockPacker.Unpack(this.GetReader(), this.blockSize);
            Assert.Equal(aboveBlockSize, unpacked);
        }
    }
}