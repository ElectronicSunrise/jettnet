using System;
using jettnet.core;
using jettnet.mirage.bitpacking;

namespace jettnet
{
    public class JettWriterPool
    {
        private readonly Pool<PooledJettWriter> _writers;

        public JettWriterPool(Logger logger)
        {
            Func<PooledJettWriter> generator = () =>
                new PooledJettWriter(this, JettConstants.DefaultBufferSize, true, logger);

            _writers = new Pool<PooledJettWriter>(generator);
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

}