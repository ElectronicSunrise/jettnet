using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentAssertions.Execution;
using jettnet.mirage.bitpacking;
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

            writer.WriteBoolean(false);
            writer.WriteBoolean(true);
            writer.WriteBoolean(false);
            writer.WriteBoolean(true);
            writer.WriteBoolean(false);
            writer.WriteBoolean(true);
            writer.WriteBoolean(false);
            writer.WriteBoolean(true);

            var arr = writer.ToArray(); 
            
            output.WriteLine($"writer byte length {arr.Length}, normal size {sizeof(bool) * 8}");

            reader.Reset(writer.ToArraySegment());

            Assert.False(reader.ReadBoolean());
            Assert.True(reader.ReadBoolean());
            Assert.False(reader.ReadBoolean());
            Assert.True(reader.ReadBoolean());
            Assert.False(reader.ReadBoolean());
            Assert.True(reader.ReadBoolean());
            Assert.False(reader.ReadBoolean());
            Assert.True(reader.ReadBoolean());
        }
    }
}