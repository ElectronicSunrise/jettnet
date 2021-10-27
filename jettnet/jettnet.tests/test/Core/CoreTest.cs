using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace jettnet.tests.test.Core
{
    public class CoreTest
    {
        [Fact]
        public void ToID_generatesId()
        {
            string instance = "I'm getting bricked up writing these unit tests";
            var expected = 1895833957;
            
            Assert.Equal(expected, instance.ToID());
        }

        [Fact]
        public void JettWriter_WriteByte()
        {
            JettWriter writer = new JettWriter();
            byte value = 69;
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
            byte[] value = {69, 4, 2, 0, 69};
            writer.WriteBytes(value);

            var expectedLength = 4 + 5; // int length plus data
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
        public void JettWriter_WriteString()
        {
            JettWriter writer = new JettWriter();
            string value = "yo yo yo yo yo";
            writer.WriteString(value);

            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                    .And.StartWith(0x0E);
                writer.Position.Should().Be(32);
            }
        }
        
        [Fact]
        public void JettWriter_WriteChar()
        {
            JettWriter writer = new JettWriter();
            char value = '7';
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
            int value = 6942069;
            writer.WriteInt(value);

            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                    .And.StartWith(new byte[] {0x75, 0xED, 0x69});
                writer.Position.Should().Be(4);
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
        
        [Fact]
        public void JettWriter_WriteUnmanagedStruct()
        {
            JettWriter writer = new JettWriter();
            ValueStruct value = new ValueStruct('y');

            writer.WriteUnmanagedStruct(ref value);
            using (new AssertionScope())
            {
                writer.Buffer.Should().NotBeEmpty()
                    .And.StartWith(0x79);
                writer.Position.Should().Be(2);
            }
        }
    }
}