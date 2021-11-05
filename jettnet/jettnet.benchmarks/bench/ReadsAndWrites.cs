using BenchmarkDotNet.Attributes;

namespace jettnet.benchmarks.bench
{
    [MemoryDiagnoser]
    public class ReadsAndWrites
    {
        private readonly JettReader _reader;

        private readonly JettWriter _writer;

        //[Params(1000, 10000)]
        [Params(1000)] public int TestIterations;

        public ReadsAndWrites()
        {
            _reader = new JettReader();
            _writer = new JettWriter();

            _reader.Buffer = _writer.Buffer;
        }

        #region Int

        [Benchmark]
        public void WriteInt()
        {
            for (int i = 0; i < TestIterations; i++)
            {
                _writer.Position = 0;

                for (int j = 0; j < _writer.Buffer.Count / sizeof(int); j++) _writer.WriteInt(4369);
            }
        }

        [Benchmark]
        public void ReadInt()
        {
            int dummy;
            for (int i = 0; i < TestIterations; i++)
            {
                _reader.Position = 0;

                for (int j = 0; j < _reader.Buffer.Count / sizeof(int); j++) dummy = _reader.ReadInt();
            }
        }

        #endregion

        #region Byte

        [Benchmark]
        public void WriteByte()
        {
            for (int i = 0; i < TestIterations; i++)
            {
                _writer.Position = 0;

                for (int j = 0; j < _writer.Buffer.Count / sizeof(byte); j++)
                    // 1 in each nibble
                    _writer.WriteByte(17);
            }
        }

        [Benchmark]
        public void ReadByte()
        {
            byte dummy;
            for (int i = 0; i < TestIterations; i++)
            {
                _reader.Position = 0;
                for (int j = 0; j < _reader.Buffer.Count / sizeof(byte); j++) dummy = _reader.ReadByte();
            }
        }

        #endregion

        #region Bool

        [Benchmark]
        public void WriteBool()
        {
            for (int i = 0; i < TestIterations; i++)
            {
                _writer.Position = 0;

                for (int j = 0; j < _writer.Buffer.Count / sizeof(bool); j++)
                    // 1 in each nibble
                    _writer.WriteBool(true);
            }
        }

        [Benchmark]
        public void ReadBool()
        {
            bool dummy;
            for (int i = 0; i < TestIterations; i++)
            {
                _reader.Position = 0;
                for (int j = 0; j < _reader.Buffer.Count / sizeof(bool); j++) dummy = _reader.ReadBool();
            }
        }

        #endregion

        #region String

        [Benchmark]
        public void WriteString()
        {
            // Better off setup in the constructor, but shouldn't matter much
            string dummy                 = "the quick brown fox jumped over the fence";
            int    sizeOfString          = dummy.Length * sizeof(char);
            int    sizeOfStringAndLength = sizeOfString + sizeof(int);

            for (int i = 0; i < TestIterations; i++)
            {
                _writer.Position = 0;

                for (int j = 0; j < _writer.Buffer.Count / sizeOfStringAndLength; j++) _writer.WriteString(dummy);
            }
        }

        [Benchmark]
        public void ReadString()
        {
            int sizeOfString          = "the quick brown fox jumped over the fence".Length * sizeof(char);
            int sizeOfStringAndLength = sizeOfString + sizeof(int);

            string dummy = default;

            for (int i = 0; i < TestIterations; i++)
            {
                _reader.Position = 0;
                for (int j = 0; j < _reader.Buffer.Count / sizeOfStringAndLength; j++) dummy = _reader.ReadString();
            }
        }

        #endregion

        #region Bytes

        [Benchmark]
        public void WriteBytes()
        {
            // Should probably declare this in the class constructor
            // Leave room for the length byte that gets written first
            byte[] byteArray = new byte[_writer.Buffer.Count - sizeof(int)];
            for (int i = 0; i < TestIterations; i++)
            {
                _writer.Position = 0;
                _writer.WriteBytes(byteArray);
            }
        }

        [Benchmark]
        public void ReadBytes()
        {
            byte[] dummy;
            for (int i = 0; i < TestIterations; i++)
            {
                _reader.Position = 0;
                dummy            = _reader.ReadBytes();
            }
        }

        #endregion

        #region Char

        [Benchmark]
        public void WriteChar()
        {
            for (int i = 0; i < TestIterations; i++)
            {
                _writer.Position = 0;

                for (int j = 0; j < _writer.Buffer.Count / sizeof(char); j++) _writer.WriteChar('a');
            }
        }

        [Benchmark]
        public void ReadChar()
        {
            char dummy;
            for (int i = 0; i < TestIterations; i++)
            {
                _reader.Position = 0;
                for (int j = 0; j < _reader.Buffer.Count / sizeof(char); j++) dummy = _reader.ReadChar();
            }
        }

        #endregion

        #region Unmanaged Struct

        [Benchmark]
        public void WriteUnmanagedStruct()
        {
            UnmanagedStruct dummy = new();

            for (int i = 0; i < TestIterations; i++)
            {
                _writer.Position = 0;
                _writer.WriteUnmanagedStruct(ref dummy);
            }
        }

        [Benchmark]
        public void ReadUnmanagedStruct()
        {
            UnmanagedStruct dummy = new();
            for (int i = 0; i < TestIterations; i++)
            {
                _reader.Position = 0;
                _reader.ReadUnmanagedStruct(ref dummy);
            }
        }

        public unsafe struct UnmanagedStruct
        {
            // 300 is _writer.buffer.count / sizeof(int).  This is true unless the buffer size changes, but given MTU things, it probably won't
            public fixed int intArray[300];
        }

        #endregion
    }
}