using System;

namespace JamesFrowen.BitPacking
{
    public static class LogHelper
    {
        public static Action<string> Log = Console.WriteLine;

        public static void LogBits(uint n)
        {
            var builder = new System.Text.StringBuilder();
            for (int i = sizeof(uint) * 8 - 1; i >= 0; i--)
            {
                uint shift = n >> i;
                uint masked = shift & 0b1;
                builder.Append(masked);
            }

            Log(builder.ToString());
        }

        public static void LogBits(ulong n)
        {
            var builder = new System.Text.StringBuilder();
            for (int i = sizeof(ulong) * 8 - 1; i >= 0; i--)
            {
                ulong shift = n >> i;
                ulong masked = shift & 0b1;
                builder.Append(masked);
            }

            Log(builder.ToString());
        }

        public static void LogHex(ArraySegment<byte> segment)
            => LogHex(segment.Array, segment.Offset, segment.Count);

        public static void LogHex(byte[] bytes, int? offset = null, int? count = null)
        {
            Log(BitConverter.ToString(bytes, offset ?? 0, count ?? bytes.Length));
        }
    }
}
