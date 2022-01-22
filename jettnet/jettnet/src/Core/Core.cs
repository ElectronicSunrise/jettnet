using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml;
using JamesFrowen.BitPacking;
using jettnet.logging;
using Telepathy;

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
        public const int DefaultBufferSize = 1200 - sizeof(int);
    }

    public readonly struct ConnectionData : IEquatable<ConnectionData>
    {
        public readonly IPEndPoint EndPoint;

        public readonly int ClientId;

        public string Address => EndPoint.Address.ToString();
        public ushort Port => (ushort) EndPoint.Port;

        public ConnectionData(int clientId, IPEndPoint endPoint)
        {
            EndPoint = endPoint;
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

    public class JettWriterPool
    {
        private readonly ObjectPool<PooledJettWriter> _writers;
        
        public JettWriterPool(Logger logger)
        {
            Func<PooledJettWriter> generator = () => 
                new PooledJettWriter(this, JettConstants.DefaultBufferSize, true, logger);
            
            _writers = new ObjectPool<PooledJettWriter>(generator);
        }

        public PooledJettWriter Get()
        {
            PooledJettWriter writer = _writers.Get();
            writer.Reset();

            return writer;
        }
        
        public void Return(PooledJettWriter jettWriter)
        {
            _writers.Return(jettWriter);
        }
    }

    public class JettReaderPool
    {
        private readonly ObjectPool<PooledJettReader> _readers;

        public JettReaderPool(Logger logger)
        {
            Func<PooledJettReader> generator = () => new PooledJettReader(this, logger);
            _readers = new ObjectPool<PooledJettReader>(generator);
        }
        
        public PooledJettReader Get(ArraySegment<byte> data)
        {
            PooledJettReader reader = _readers.Get();

            reader.Reset(data);

            return reader;
        }

        public void Return(PooledJettReader jettReader)
        {
            _readers.Return(jettReader);
        }
    }

    public sealed class PooledJettWriter : JettWriter, IDisposable
    {
        private readonly JettWriterPool _pool;
        
        public PooledJettWriter(JettWriterPool pool, int startBufferSize, bool allowResize, Logger logger = null) :
            base(startBufferSize, allowResize, logger)
        {
            _pool = pool;
        }

        public void Dispose()
        {
            _pool.Return(this);
        }
    }

    public sealed class PooledJettReader : JettReader, IDisposable
    {
        private readonly JettReaderPool _pool;
        
        public PooledJettReader(JettReaderPool pool, Logger logger = null) : base(logger)
        {
            _pool = pool;
        }

        public new void Dispose()
        {
            _pool.Return(this);
        }
    }

    public static class JettReadWriteExtensions
    {
        // https://github.com/MirageNet/Mirage/blob/master/Assets/Mirage/Runtime/Serialization/StringExtensions.cs
        
        /// <summary>
        /// Defaults MTU, 1300
        /// <para>Can be changed by user if they need to</para>
        /// </summary>
        public static int MaxStringLength = JettConstants.DefaultBufferSize;

        static readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
        static readonly byte[] stringBuffer = new byte[MaxStringLength];

        /// <param name="value">string or null</param>
        public static void WriteString(this JettWriter writer, string value)
        {
            // write 0 for null support, increment real size by 1
            // (note: original HLAPI would write "" for null strings, but if a
            //        string is null on the server then it should also be null
            //        on the client)
            if (value == null)
            {
                writer.WriteUInt16(0);
                return;
            }

            // write string with same method as NetworkReader
            // convert to byte[]
            int size = encoding.GetBytes(value, 0, value.Length, stringBuffer, 0);

            // check if within max size
            if (size >= MaxStringLength)
            {
                throw new DataMisalignedException($"NetworkWriter.Write(string) too long: {size}. Limit: {MaxStringLength}");
            }

            // write size and bytes
            writer.WriteUInt16(checked((ushort)(size + 1)));
            writer.WriteBytes(stringBuffer, 0, size);
        }

        /// <returns>string or null</returns>
        /// <exception cref="ArgumentException">Throws if invalid utf8 string is received</exception>
        public static string ReadString(this JettReader reader)
        {
            // read number of bytes
            ushort size = reader.ReadUInt16();

            if (size == 0)
                return null;

            int realSize = size - 1;

            // make sure it's within limits to avoid allocation attacks etc.
            if (realSize >= MaxStringLength)
            {
                throw new EndOfStreamException($"ReadString too long: {realSize}. Limit is: {MaxStringLength}");
            }

            ArraySegment<byte> data = reader.ReadBytesSegment(realSize);

            // convert directly from buffer to string via encoding
            return encoding.GetString(data.Array, data.Offset, data.Count);
        }
    }
}