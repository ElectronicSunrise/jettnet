using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace jettnet.tests
{
    public class CoreTest
    {
        [Fact]
        public void ToID_generatesId()
        {
            string instance = "I'm getting bricked up writing these unit tests";
            int    expected = 1895833957;

            Assert.Equal(expected, instance.ToID());
        }

        [Fact]
        public static void SerializerTest()
        {
            JettWriter writer = new JettWriter();
            JettReader reader = new JettReader();
            
            writer.Write(default(int));
            writer.Write(default(uint));
            writer.Write(default(long));
            writer.Write(default(ulong));
            writer.Write(default(short));
            writer.Write(default(ushort));
            writer.Write(default(byte));
            writer.Write(default(sbyte));
            writer.Write(default(float));
            writer.Write(default(double));
            writer.Write(default(decimal));
            writer.Write(default(bool));
            writer.Write(default(char));
            
            writer.Write(default(DateTime));
            writer.Write(default(TimeSpan));
            writer.Write(default(Guid));

            reader.Buffer = writer.Buffer;
            reader.Position = 0;
            
            Assert.Equal(default(int), reader.Read<int>());
            Assert.Equal(default(uint), reader.Read<uint>());
            Assert.Equal(default(long), reader.Read<long>());
            Assert.Equal(default(ulong), reader.Read<ulong>());
            Assert.Equal(default(short), reader.Read<short>());
            Assert.Equal(default(ushort), reader.Read<ushort>());
            Assert.Equal(default(byte), reader.Read<byte>());
            Assert.Equal(default(sbyte), reader.Read<sbyte>());
            Assert.Equal(default(float), reader.Read<float>());
            Assert.Equal(default(double), reader.Read<double>());
            Assert.Equal(default(decimal), reader.Read<decimal>());
            Assert.Equal(default(bool), reader.Read<bool>());
            Assert.Equal(default(char), reader.Read<char>());
            
            Assert.Equal(default(DateTime), reader.Read<DateTime>());
            Assert.Equal(default(TimeSpan), reader.Read<TimeSpan>());
            Assert.Equal(default(Guid), reader.Read<Guid>());
        }
        
        [Fact]
        public void JettReaderWriter_ArraySegmentByte()
        {
            // write

            JettWriter writer = new JettWriter();
            byte[]     value  = {69, 4, 2, 0, 69};

            var segment = new ArraySegment<byte>(value, 0, value.Length);

            writer.WriteArraySegment(segment);

            using (new AssertionScope())
            {
                byte[] expectedShort = BitConverter.GetBytes((short) value.Length);

                writer.Buffer.Should().NotBeEmpty().And.StartWith(expectedShort);

                writer.Position.Should().Be(sizeof(short) + segment.Count);
            }

            // read

            JettReader reader = new JettReader
            {
                Buffer = writer.Buffer
            };

            var readData = reader.ReadArraySegment<byte>();

            using (new AssertionScope())
            {
                Assert.Equal(segment, readData);

                reader.Position.Should().Be(sizeof(short) + segment.Count);
            }
        }
    }
}