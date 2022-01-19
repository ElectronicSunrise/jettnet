using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml;

//         _        _    _                 _   
//        (_)      | |  | |               | |  
//         _   ___ | |_ | |_  _ __    ___ | |_ 
//        | | / _ \| __|| __|| '_ \  / _ \| __|
//        | ||  __/| |_ | |_ | | | ||  __/| |_ 
//        | | \___| \__| \__||_| |_| \___| \__|
//       _/ |                                  
//      |__/
//  
//                   _ooOoo_
//                  o8888888o
//                  88" . "88
//                  (| -_- |)
//                  O\  =  /O
//               ____/`---'\____
//             .'  \\|     |//  `.
//            /  \\|||  :  |||//  \
//           /  _||||| -:- |||||-  \
//           |   | \\\  -  /// |   |
//           | \_|  ''\---/''  |   |
//           \  .-\__  `-`  ___/-. /
//         ___`. .'  /--.--\  `. . __
//      ."" '<  `.___\_<|>_/___.'  >'"".
//     | | :  `- \`.;`\ _ /`;.`/ - ` : | |
//     \  \ `-.   \_ __\ /__ _/   .-` /  /
//======`-.____`-.___\_____/___.-`____.-'======
//                   `=---='
//
//^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//          佛祖保佑           永无BUG
//         God Bless        Never Crash

// [ header ] (1 byte)
// [ message id ] (4 bytes)

// [ user serialized data ] 

namespace jettnet // v1.3
{
    public static class JettChannels
    {
        public const int Reliable   = 0;
        public const int Unreliable = 1;
    }

    public static class JettConstants
    {
        public const int DefaultBufferSize = 1200;
    }

    public readonly struct ConnectionData : IEquatable<ConnectionData>
    {
        public readonly int    ClientId;
        public readonly string Address;
        public readonly ushort Port;

        public ConnectionData(int clientId, string address, ushort port)
        {
            Address  = address;
            Port     = port;
            ClientId = clientId;
        }

        private sealed class EqualityComparer : IEqualityComparer<ConnectionData>
        {
            public bool Equals(ConnectionData x, ConnectionData y)
            {
                return x.ClientId == y.ClientId &&
                       x.Address == y.Address &&
                       x.Port == y.Port;
            }

            public int GetHashCode(ConnectionData obj)
            {
                unchecked
                {
                    return (obj.ClientId * 397) ^ obj.Port;
                }
            }
        }

        public static IEqualityComparer<ConnectionData> Comparer { get; } = new EqualityComparer();

        public bool Equals(ConnectionData other)
        {
            return ClientId == other.ClientId && Address == other.Address && Port == other.Port;
        }

        public override bool Equals(object obj)
        {
            return obj is ConnectionData other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ClientId * 397) ^ Port;
            }
        }
    }

    public enum JettHeader : byte
    {
        Message = 4
    }

    public static class IdExtenstions
    {
        private static readonly MD5                     _crypto = MD5.Create();
        private static readonly Dictionary<string, int> _cache  = new Dictionary<string, int>();

        public static int ToID(this string s)
        {
            if (_cache.TryGetValue(s, out var id))
                return id;

            byte[] result   = _crypto.ComputeHash(Encoding.UTF8.GetBytes(s));
            int    computed = BitConverter.ToInt32(result, 0);

            _cache.Add(s, computed);

            return computed;
        }
    }

    // https://docs.microsoft.com/en-us/dotnet/standard/collections/thread-safe/how-to-create-an-object-pool
    public class ObjectPool<T>
    {
        private readonly Func<T>          _objectGenerator;
        private readonly ConcurrentBag<T> _objects;

        public ObjectPool(Func<T> objectGenerator)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects         = new ConcurrentBag<T>();
        }

        public T Get()
        {
            return _objects.TryTake(out T item) ? item : _objectGenerator();
        }

        public void Return(T item)
        {
            _objects.Add(item);
        }
    }

    public interface IJettMessage<T> : IJettMessage where T : struct
    {
        T Deserialize(JettReader reader);
    }

    public interface IJettMessage
    {
        void Serialize(JettWriter writer);
    }

    public static class JettWriterPool
    {
        private static readonly ObjectPool<PooledJettWriter> _writers =
            new ObjectPool<PooledJettWriter>(() => new PooledJettWriter());

        public static PooledJettWriter Get()
        {
            return GetInternal();
        }

        private static PooledJettWriter GetInternal()
        {
            PooledJettWriter writer = _writers.Get();
            writer.Position = 0;
            return writer;
        }

        public static PooledJettWriter Get(JettHeader header = JettHeader.Message,
                                           int bufferSize = JettConstants.DefaultBufferSize)
        {
            PooledJettWriter writer = GetInternal();

            bool customBuffer = bufferSize != JettConstants.DefaultBufferSize;

            // ensure the buffer is defaulted to 1200 (JettConstants.DefaultBufferSize),
            // if a previous use caused it to grow.

            if (customBuffer)
                writer.Buffer = new ArraySegment<byte>(new byte[bufferSize]);
            else if (writer.Buffer.Array.Length != JettConstants.DefaultBufferSize)
                writer.Buffer = new ArraySegment<byte>(new byte[JettConstants.DefaultBufferSize]);

            writer.WriteByte((byte) header);

            return writer;
        }

        public static void Return(PooledJettWriter jettWriter)
        {
            _writers.Return(jettWriter);
        }
    }

    public static class JettReaderPool
    {
        private static readonly ObjectPool<PooledJettReader> _readers =
            new ObjectPool<PooledJettReader>(() => new PooledJettReader());

        public static PooledJettReader Get(int pos, ArraySegment<byte> data)
        {
            PooledJettReader reader = _readers.Get();

            reader.Position = pos;
            reader.Buffer   = data;

            return reader;
        }

        public static void Return(PooledJettReader jettReader)
        {
            _readers.Return(jettReader);
        }
    }

    public sealed class PooledJettWriter : JettWriter, IDisposable
    {
        public void Dispose()
        {
            JettWriterPool.Return(this);
        }
    }

    public sealed class PooledJettReader : JettReader, IDisposable
    {
        public void Dispose()
        {
            JettReaderPool.Return(this);
        }
    }

    public class JettWriter
    {
        public ArraySegment<byte> Buffer = new ArraySegment<byte>(new byte[JettConstants.DefaultBufferSize]);
        public int                Position;
    }

    public class JettReader
    {
        public ArraySegment<byte> Buffer;
        public int                Position;
    }

    public class ArraySerializer<T> : IArraySerializer
    {
        public Func<JettReader, T>   Read;
        public Action<JettWriter, T> Write;
    }

    public interface IArraySerializer
    {
    }

    public static class JettReadWriteExtensions
    {
        private static readonly Dictionary<Type, IArraySerializer> _serializers = new Dictionary<Type, IArraySerializer>
        {
            {typeof(int), new ArraySerializer<int> {Read       = ReadInt, Write    = WriteInt}},
            {typeof(bool), new ArraySerializer<bool> {Read     = ReadBool, Write   = WriteBool}},
            {typeof(string), new ArraySerializer<string> {Read = ReadString, Write = WriteString}},
            {typeof(byte), new ArraySerializer<byte> {Read     = ReadByte, Write   = WriteByte}},
            {typeof(ushort), new ArraySerializer<ushort> {Read = ReadUShort, Write = WriteUShort}},
            {typeof(char), new ArraySerializer<char> {Read     = ReadChar, Write   = WriteChar}},
            {typeof(float), new ArraySerializer<float> {Read   = ReadFloat, Write  = WriteFloat}},
            {typeof(short), new ArraySerializer<short> {Read   = ReadShort, Write  = WriteShort}},
            {typeof(double), new ArraySerializer<double> {Read = ReadDouble, Write = WriteDouble}},
        };

        public static void WriteArray<T>(this JettWriter writer, T[] value, int offset = 0, int count = -1)
        {
            Type type = typeof(T);

            if (!_serializers.TryGetValue(type, out IArraySerializer serializer))
                throw new NotSupportedException($"Type {type} is not supported");

            ArraySerializer<T> typedSerializer = (ArraySerializer<T>) serializer;

            if (value == null || value.Length == 0)
            {
                writer.WriteShort(-1);
                return;
            }

            if (value.Length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "Array length cannot exceed ushort.MaxValue");

            int length = count == -1 ? value.Length : count;

            writer.WriteShort((short) length);

            for (int i = offset; i < length; i++)
                typedSerializer.Write(writer, value[i]);
        }

        public static T[] ReadArray<T>(this JettReader reader)
        {
            Type type = typeof(T);

            if (!_serializers.TryGetValue(type, out IArraySerializer serializer))
                throw new NotSupportedException($"Type {type} is not supported");

            ArraySerializer<T> typedSerializer = (ArraySerializer<T>) serializer;

            int length = reader.ReadShort();

            if (length == -1)
                return Array.Empty<T>();

            T[] result = new T[length];

            for (int i = 0; i < length; i++)
                result[i] = typedSerializer.Read(reader);

            return result;
        }

        public static void WriteArraySegment<T>(this JettWriter writer, ArraySegment<T> segment)
        {
            WriteArray(writer, segment.Array, segment.Offset, segment.Count);
        }

        public static ArraySegment<T> ReadArraySegment<T>(this JettReader reader)
        {
            T[] array = ReadArray<T>(reader);
            return new ArraySegment<T>(array);
        }

        public static void WriteByteArraySegment(this JettWriter writer, ArraySegment<byte> segment)
        {
            writer.WriteInt(segment.Count);

            for (int i = segment.Offset; i < segment.Count; i++) writer.WriteByte(segment.Array[i]);
        }

        public static ArraySegment<byte> ReadByteArraySegment(this JettReader reader)
        {
            int count = reader.ReadInt();

            byte[] dest = new byte[count];

            Buffer.BlockCopy(reader.Buffer.Array, reader.Position, dest, 0, count);

            reader.Position += count;

            return new ArraySegment<byte>(dest);
        }

        public unsafe static T Read<T>(this JettReader reader) where T : unmanaged
        {
            int memoryOffset = reader.Position & 3;
            int truePosition = reader.Position - memoryOffset;

            int offset = 0xFF << (memoryOffset * 2);

            fixed (int* dataPtr = &reader.Buffer.Array[truePosition + offset])
            {
                T result = *(T*) &dataPtr;

                reader.Position += sizeof(T);

                return result;
            }
        }

        public unsafe static void Write<T>(this JettWriter writer, T value) where T : unmanaged
        {
            int memoryOffset = writer.Position & 3;
            int truePosition = writer.Position - memoryOffset;

            fixed (int* dataPtr = &writer.Buffer.Array[truePosition])
            {
                writer.Position += sizeof(T);

                *(T*) dataPtr = value;
            }
        }

        public static void WriteByte(this JettWriter writer, byte value)
        {
            writer.Buffer.Array[writer.Position] =  value;
            writer.Position                      += sizeof(byte);
        }

        public static byte ReadByte(this JettReader reader)
        {
            byte value = reader.Buffer.Array[reader.Position];
            reader.Position += sizeof(byte);
            return value;
        }

        public static void WriteBool(this JettWriter writer, bool value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    bool* valuePtr = (bool*) dataPtr;
                    *valuePtr       =  value;
                    writer.Position += sizeof(bool);
                }
            }
        }

        public static bool ReadBool(this JettReader reader)
        {
            unsafe
            {
                fixed (byte* dataPtr = &reader.Buffer.Array[reader.Position])
                {
                    bool value = *(bool*) dataPtr;
                    reader.Position += sizeof(bool);

                    return value;
                }
            }
        }

        public static void WriteShort(this JettWriter writer, short value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    short* valuePtr = (short*) dataPtr;
                    *valuePtr       =  value;
                    writer.Position += sizeof(short);
                }
            }
        }

        public static short ReadShort(this JettReader reader)
        {
            unsafe
            {
                fixed (byte* dataPtr = &reader.Buffer.Array[reader.Position])
                {
                    short value = *(short*) dataPtr;
                    reader.Position += sizeof(short);

                    return value;
                }
            }
        }

        public static ushort ReadUShort(this JettReader reader)
        {
            unsafe
            {
                fixed (byte* dataPtr = &reader.Buffer.Array[reader.Position])
                {
                    reader.Position += (sizeof(ushort));
                    return *dataPtr;
                }
            }
        }

        public static void WriteUShort(this JettWriter writer, ushort value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    *dataPtr        =  (byte) value;
                    writer.Position += sizeof(ushort);
                }
            }
        }

        public static void WriteString(this JettWriter writer, string value)
        {
            WriteArray(writer, value.ToCharArray());
        }

        public static string ReadString(this JettReader reader)
        {
            return new string(ReadArray<char>(reader));
        }

        public static void WriteChar(this JettWriter writer, char value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    char* valuePtr = (char*) dataPtr;
                    *valuePtr       =  value;
                    writer.Position += sizeof(char);
                }
            }
        }

        public static char ReadChar(this JettReader reader)
        {
            unsafe
            {
                fixed (byte* dataPtr = &reader.Buffer.Array[reader.Position])
                {
                    char value = *(char*) dataPtr;
                    reader.Position += sizeof(char);

                    return value;
                }
            }
        }

        public static void WriteInt(this JettWriter writer, int value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    int* valuePtr = (int*) dataPtr;
                    *valuePtr = value;

                    writer.Position += sizeof(int);
                }
            }
        }

        public static int ReadInt(this JettReader reader)
        {
            unsafe
            {
                fixed (byte* dataPtr = &reader.Buffer.Array[reader.Position])
                {
                    int value = *(int*) dataPtr;
                    reader.Position += sizeof(int);

                    return value;
                }
            }
        }

        public static void WriteFloat(this JettWriter writer, float value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    float* valuePtr = (float*) dataPtr;
                    *valuePtr = value;

                    writer.Position += sizeof(float);
                }
            }
        }

        public static float ReadFloat(this JettReader reader)
        {
            unsafe
            {
                fixed (byte* dataPtr = &reader.Buffer.Array[reader.Position])
                {
                    float value = *(float*) dataPtr;
                    reader.Position += sizeof(float);

                    return value;
                }
            }
        }

        public static void WriteDouble(this JettWriter writer, double value)
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    double* valuePtr = (double*) dataPtr;
                    *valuePtr = value;

                    writer.Position += sizeof(double);
                }
            }
        }

        public static double ReadDouble(this JettReader reader)
        {
            unsafe
            {
                fixed (byte* dataPtr = &reader.Buffer.Array[reader.Position])
                {
                    double value = *(double*) dataPtr;
                    reader.Position += sizeof(double);

                    return value;
                }
            }
        }

        public static void WriteUnmanagedStruct<T>(this JettWriter writer, ref T unmanagedStruct) where T : unmanaged
        {
            unsafe
            {
                fixed (byte* dataPtr = &writer.Buffer.Array[writer.Position])
                {
                    fixed (void* unmanagedStructPtr = &unmanagedStruct)
                    {
                        int sizeOfStructure   = sizeof(T);
                        int freeBytesInBuffer = writer.Buffer.Array.Length - writer.Position;

                        if (freeBytesInBuffer < sizeOfStructure)
                            throw new Exception("Buffer too small.  Bytes available: " + freeBytesInBuffer +
                                                " size of struct: " + sizeOfStructure);

                        Buffer.MemoryCopy(unmanagedStructPtr, dataPtr, freeBytesInBuffer, sizeOfStructure);

                        writer.Position += sizeOfStructure;
                    }
                }
            }
        }

        public static void ReadUnmanagedStruct<T>(this JettReader reader, ref T unmanagedStruct) where T : unmanaged
        {
            unsafe
            {
                fixed (byte* dataPtr = &reader.Buffer.Array[reader.Position])
                {
                    fixed (void* unmanagedPtr = &unmanagedStruct)
                    {
                        Buffer.MemoryCopy(dataPtr, unmanagedPtr, sizeof(T), sizeof(T));
                    }
                }

                reader.Position += sizeof(T);
            }
        }
    }
}