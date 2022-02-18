using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace jettnet
{
    public static class IdExtensions
    {
        public static int ToId(this string s)
        {
            return GetHash(s);
        }

        private static int GetHash(string input)
        {
            // from mirror
            unchecked
            {
                int hash = 23;

                for (int index = 0; index < input.Length; index++)
                {
                    char c = input[index];
                    hash = hash * 31 + c;
                }

                return hash;
            }
        }
    }
}