using System;
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
        public void ReadWrite_UShort_Generic()
        {
            ushort value = 69;
            
            JettWriter writer = new JettWriter();
            writer.Write(value);
            
            JettReader reader = new JettReader();
            reader.Buffer = writer.Buffer;
            
            ushort result = reader.Read<ushort>();
            
            Assert.Equal(value, result);
        }

        [Fact]
        public void JettReaderWriter_ArraySegmentByte()
        {
            // write

            JettWriter writer = new JettWriter();
            byte[]     value  = {69, 4, 2, 0, 69};

            var segment = new ArraySegment<byte>(value, 0, value.Length);

            writer.WriteByteArraySegment(segment);

            using (new AssertionScope())
            {
                byte[] expectedInt = BitConverter.GetBytes(value.Length);

                writer.Buffer.Should().NotBeEmpty().And.StartWith(expectedInt);

                writer.Position.Should().Be(sizeof(int) + segment.Count);
            }

            // read

            JettReader reader = new JettReader
            {
                Buffer = writer.Buffer
            };

            var readData = reader.ReadByteArraySegment();

            using (new AssertionScope())
            {
                Assert.Equal(segment, readData);

                reader.Position.Should().Be(sizeof(int) + segment.Count);
            }
        }

        [Fact]
        public void JettWriter_WriteByte()
        {
            JettWriter writer = new JettWriter();
            const byte value  = 69;
            writer.WriteByte(value);

            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                      .And.StartWith(value);
                writer.Position.Should().Be(1);
            }
        }

        [Fact]
        public void JettWriter_WriteBytes()
        {
            JettWriter writer = new JettWriter();
            byte[]     value  = {69, 4, 2, 0, 69};
            writer.WriteArray(value);

            int expectedLength = sizeof(int) + value.Length;

            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                      .And.StartWith(new byte[] {0x05, 0x00, 0x00, 0x00, 0x45, 0x04, 0x02, 0x00, 0x45});
                writer.Position.Should().Be(expectedLength);
            }
        }

        [Fact]
        public void JettWriter_WriteBool()
        {
            JettWriter writer = new JettWriter();
            writer.WriteBool(false);

            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                      .And.StartWith(0x00);
                writer.Position.Should().Be(1);
            }
        }

        [Fact]
        public void JettReader_ReadBool()
        {
            JettWriter writer = new JettWriter();
            JettReader reader = new JettReader();

            writer.WriteBool(false);

            reader.Buffer   = writer.Buffer;
            reader.Position = 0;

            using (new AssertionScope())
            {
                reader.ReadBool().Should().BeFalse();
            }
        }

        [Fact]
        public void JettReadWrite_UShort()
        {
            JettReader reader = new JettReader();
            JettWriter writer = new JettWriter();

            const ushort value = 69;
            writer.WriteUShort(value);

            reader.Buffer   = writer.Buffer;
            reader.Position = 0;

            using (new AssertionScope())
            {
                reader.ReadUShort().Should().Be(value);
                reader.Position.Should().Be(sizeof(ushort));

                writer.Position.Should().Be(sizeof(ushort));
            }
        }

        [Fact]
        public void JettReadWrite_String()
        {
            JettWriter writer = new JettWriter();
            JettReader reader = new JettReader();

            const string value = "deez nuts";
            writer.WriteString(value);

            reader.Buffer = writer.Buffer;
            reader.Position = 0;

            using (new AssertionScope())
            {
                writer.Position.Should().Be((value.Length * 2) + sizeof(int));

                reader.ReadString().Should().Be(value);
            }
        }

        [Fact]
        public void JettWriter_WriteChar()
        {
            JettWriter writer = new JettWriter();
            char       value  = '7';
            writer.WriteChar(value);

            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                      .And.StartWith(0x37);
                writer.Position.Should().Be(2);
            }
        }

        [Fact]
        public void JettWriter_WriteInt()
        {
            JettWriter writer = new JettWriter();
            int        value  = 6942069;
            writer.WriteInt(value);

            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                      .And.StartWith(new byte[] {0x75, 0xED, 0x69});
                writer.Position.Should().Be(4);
            }
        }

        [Fact]
        public void JettWriter_WriteUnmanagedStruct()
        {
            JettWriter  writer = new JettWriter();
            ValueStruct value  = new ValueStruct('y');

            writer.WriteUnmanagedStruct(ref value);
            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                      .And.StartWith(0x79);
                writer.Position.Should().Be(2);
            }
        }

        private struct ValueStruct
        {
            public ValueStruct(char c)
            {
                _c = c;
            }

            private char _c;
        }
    }
}