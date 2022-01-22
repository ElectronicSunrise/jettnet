using System;

namespace JamesFrowen.BitPacking.Tests.Packers
{
    public class PackerTestBase : IDisposable
    {
        public readonly JettWriter writer = new JettWriter(1300);
        readonly JettReader reader = new JettReader();

        public void Dispose()
        {
            this.writer.Reset();
            this.reader.Dispose();
        }

        /// <summary>
        /// Gets Reader using the current data inside writer
        /// </summary>
        /// <returns></returns>
        public JettReader GetReader()
        {
            this.reader.Reset(this.writer.ToArraySegment());
            return this.reader;
        }
    }
}
