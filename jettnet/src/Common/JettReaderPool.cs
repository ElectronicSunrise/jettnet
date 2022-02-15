using System;
using jettnet.core;
using jettnet.mirage.bitpacking;

namespace jettnet
{
    public class JettReaderPool
    {
        private readonly Pool<PooledJettReader> _readers;

        public JettReaderPool(Logger logger)
        {
            Func<PooledJettReader> generator = () => new PooledJettReader(this, logger);
            _readers = new Pool<PooledJettReader>(generator);
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