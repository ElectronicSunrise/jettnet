using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using jettnet.mirage.bitpacking;
using jettnet.logging;

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

namespace jettnet // v1.5
{
    public static class JettChannels
    {
        public const int Reliable   = 0;
        public const int Unreliable = 1;
    }

    public static class JettConstants
    {
        public const int DefaultBufferSize = 1200 - OVERHEAD;

        private const int OVERHEAD = sizeof(int);
    }

    public readonly struct JettConnection : IEquatable<JettConnection>
    {
        public readonly int ClientId;

        public readonly string Address;
        public readonly ushort Port;

        public JettConnection(int clientId, string address, ushort port)
        {
            ClientId = clientId;
            Address = address;
            Port = port;
        }
        
        private sealed class EqualityComparer : IEqualityComparer<JettConnection>
        {
            public bool Equals(JettConnection x, JettConnection y)
            {
                return x.ClientId == y.ClientId &&
                       x.Address == y.Address &&
                       x.Port == y.Port;
            }

            public int GetHashCode(JettConnection obj)
            {
                unchecked
                {
                    return (obj.ClientId * 397) ^ obj.Port;
                }
            }
        }

        public static IEqualityComparer<JettConnection> Comparer { get; } = new EqualityComparer();

        public bool Equals(JettConnection other)
        {
            return ClientId == other.ClientId && Address == other.Address && Port == other.Port;
        }

        public override bool Equals(object obj)
        {
            return obj is JettConnection other && Equals(other);
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

        public static int ToId(this string s)
        {
            if (_cache.TryGetValue(s, out int id))
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
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
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
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }
        
        // from mirage
        void IDisposable.Dispose() => Dispose(true);
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // only put back into the pool is Dispose was called
            // => dont put it back for finalize
            if (disposing)
            {
                _pool.Return(this);
            }
        }
    }
}