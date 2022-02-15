/*
MIT License

Copyright (c) 2021 James Frowen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Runtime.CompilerServices;

namespace jettnet.mirage.bitpacking
{
    public sealed class VariableIntPacker
    {
        // todo needs doc comments
        // todo need attribute to validate large bits based on pack type (eg if packing ushort, make sure largebits is 16 or less)

        readonly int smallBitCount;
        readonly int mediumBitsCount;
        readonly int largeBitsCount;

        readonly ulong smallValue;
        readonly ulong mediumValue;
        readonly ulong largeValue;

        readonly bool throwIfOverLarge;

        public VariableIntPacker(ulong smallValue, ulong mediumValue)
            : this(BitHelper.BitCount(smallValue), BitHelper.BitCount(mediumValue), 64, false) { }
        public VariableIntPacker(ulong smallValue, ulong mediumValue, ulong largeValue, bool throwIfOverLarge = true)
            : this(BitHelper.BitCount(smallValue), BitHelper.BitCount(mediumValue), BitHelper.BitCount(largeValue), throwIfOverLarge) { }

        public static VariableIntPacker FromBitCount(int smallBits, int mediumBits)
            => FromBitCount(smallBits, mediumBits, 64, false);
        public static VariableIntPacker FromBitCount(int smallBits, int mediumBits, int largeBits, bool throwIfOverLarge = true)
            => new VariableIntPacker(smallBits, mediumBits, largeBits, throwIfOverLarge);

        private VariableIntPacker(int smallBits, int mediumBits, int largeBits, bool throwIfOverLarge)
        {
            this.throwIfOverLarge = throwIfOverLarge;
            if (smallBits == 0) throw new ArgumentException("Small value can not be zero", nameof(smallBits));
            if (smallBits >= mediumBits) throw new ArgumentException("Medium value must be greater than small value", nameof(mediumBits));
            if (mediumBits >= largeBits) throw new ArgumentException("Large value must be greater than medium value", nameof(largeBits));
            if (largeBits > 64) throw new ArgumentException("Large bits must be 64 or less", nameof(largeBits));
            // force medium to also be 62 or less so we can use 1 write call (2 bits to say its medium + 62 value bits
            if (mediumBits > 62) throw new ArgumentException("Medium bits must be 62 or less", nameof(mediumBits));

            this.smallBitCount = smallBits;
            this.mediumBitsCount = mediumBits;
            this.largeBitsCount = largeBits;

            // mask is also max value for n bits
            this.smallValue = BitMask.Mask(smallBits);
            this.mediumValue = BitMask.Mask(mediumBits);
            this.largeValue = BitMask.Mask(largeBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackUlong(JettWriter writer, ulong value)
        {
            this.pack(writer, value, 64);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackUint(JettWriter writer, uint value)
        {
            this.pack(writer, value, 32);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackUshort(JettWriter writer, ushort value)
        {
            this.pack(writer, value, 16);
        }
        void pack(JettWriter writer, ulong value, int maxBits)
        {
            if (value <= this.smallValue)
            {
                // start with b0 to say small, then value
                writer.Write(value << 1, this.smallBitCount + 1);
            }
            else if (value <= this.mediumValue)
            {
                // start with b01 to say medium, then value
                writer.Write(value << 2 | 0b01, this.mediumBitsCount + 2);
            }
            else if (value <= this.largeValue)
            {
                // start with b11 to say large, then value
                // use 2 write calls here because bitCount could be 64
                writer.Write(0b11, 2);
                writer.Write(value, Math.Min(maxBits, this.largeBitsCount));
            }
            else
            {
                if (this.throwIfOverLarge)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"Value is over max of {this.largeValue}");
                }
                else
                {
                    // if no throw write MaxValue
                    // we dont want to write value here because it will be masked and lose some high bits
                    // need 2 write calls here because max is 64+2 bits
                    writer.Write(0b11, 2);
                    writer.Write(ulong.MaxValue, Math.Min(maxBits, this.largeBitsCount));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong UnpackUlong(JettReader reader)
        {
            return this.unpack(reader, 64);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint UnpackUint(JettReader reader)
        {
            return (uint)this.unpack(reader, 32);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort UnpackUshort(JettReader reader)
        {
            return (ushort)this.unpack(reader, 16);
        }
        ulong unpack(JettReader reader, int maxBits)
        {
            if (!reader.ReadBoolean())
            {
                return reader.Read(this.smallBitCount);
            }
            else
            {
                if (!reader.ReadBoolean())
                {
                    return reader.Read(this.mediumBitsCount);
                }
                else
                {
                    return reader.Read(Math.Min(this.largeBitsCount, maxBits));
                }
            }
        }
    }
}
