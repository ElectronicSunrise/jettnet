using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentAssertions.Execution;
using JamesFrowen.BitPacking;
using Xunit;
using Xunit.Abstractions;

namespace jettnet.tests
{
    public class CoreTest
    {
        private readonly ITestOutputHelper output;

        public CoreTest(ITestOutputHelper output)
        {
            this.output = output;
        }
        
        [Fact]
        public void ToID_generatesId()
        {
            string instance = "I'm getting bricked up writing these unit tests";
            int    expected = 1895833957;

            Assert.Equal(expected, instance.ToID());
        }

        [Fact]
        public void SerializerTest()
        {
            JettWriter writer = new JettWriter(1200, true);
            JettReader reader = new JettReader();
            
            writer.WriteUInt32(77u);
            writer.WriteInt64(66L);
            writer.WriteUInt64(420UL);
            writer.WriteInt16(88);
            writer.WriteUInt16(1000);
            writer.WriteByte(255);
            writer.WriteSByte(43);
            writer.WriteSingle(1.00069f);
            writer.WriteDouble(30.67);
            writer.WriteBoolean(false);

            var arr = writer.ToArray();
            
  //          output.WriteLine($"writer byte length {arr.Length}, normal size {normalSize}");

            reader.Reset(writer.ToArraySegment());

            Assert.Equal(77u, reader.ReadUInt32());
            Assert.Equal(66L, reader.ReadInt64());
            Assert.Equal(420UL, reader.ReadUInt64());
            Assert.Equal(88, reader.ReadInt16());
            Assert.Equal(1000, reader.ReadUInt16());
            Assert.Equal(255, reader.ReadByte());
            Assert.Equal(43, reader.ReadSByte());
            Assert.Equal(1.00069f, reader.ReadSingle());
            Assert.Equal(30.67, reader.ReadDouble());
            Assert.False(reader.ReadBoolean());
        }
    }
}