using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
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

            writer.Write((byte) header);

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
    
    public static class JettReadWriteExtensions
    {
        public static void WriteArray<T>(this JettWriter writer, T[] value, int offset = 0, int count = -1) where T : unmanaged
        {
            if (value == null || value.Length == 0)
            {
                writer.Write<short>(-1);
                return;
            }

            if (value.Length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "Array length cannot exceed ushort.MaxValue");

            int length = count == -1 ? value.Length : count;

            writer.Write((short) length);

            for (int i = offset; i < length; i++)
                writer.Write(value[i]);
        }

        public static T[] ReadArray<T>(this JettReader reader) where T : unmanaged
        {
            int length = reader.Read<short>();

            if (length == -1)
                return Array.Empty<T>();

            T[] result = new T[length];

            for (int i = 0; i < length; i++)
                result[i] = reader.Read<T>();

            return result;
        }

        public static void WriteArraySegment<T>(this JettWriter writer, ArraySegment<T> segment) where T : unmanaged
        {
            WriteArray(writer, segment.Array, segment.Offset, segment.Count);
        }

        public static ArraySegment<T> ReadArraySegment<T>(this JettReader reader) where T : unmanaged
        {
            T[] array = ReadArray<T>(reader);
            return new ArraySegment<T>(array);
        }

        public unsafe static T Read<T>(this JettReader reader) where T : unmanaged
        {
            int size = sizeof(T);
            
            bool aligned = (reader.Position & 3) == 0;
            
            fixed(void* ptr = &reader.Buffer.Array[reader.Position])
            {
                if (aligned)
                {
                    T value = *(T*) ptr;
                    reader.Position += size;
                    return value;
                }
                
                T* valueBuffer = stackalloc T[1];

                Buffer.MemoryCopy(ptr, valueBuffer, size, size);

                reader.Position += size;

                return valueBuffer[0];
            }
        }

        public unsafe static void Write<T>(this JettWriter writer, T value) where T : unmanaged
        {
            int size = sizeof(T);
            
            bool aligned = (writer.Position & 3) == 0;
            
            fixed(void* ptr = &writer.Buffer.Array[writer.Position])
            {
                if (aligned)
                {
                    *(T*)ptr = value;
                    
                    writer.Position += size;
                    
                    return;
                }
                
                T* valueBuffer = stackalloc T[1]{value};

                Buffer.MemoryCopy(valueBuffer, ptr, size, size);

                writer.Position += size;
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
    }
}