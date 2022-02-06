using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace jettnet
{
    public static class IdExtensions
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
}