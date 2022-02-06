using System;
using System.Collections.Generic;

namespace jettnet
{

    public readonly struct JettConnection : IEquatable<JettConnection>
    {
        public readonly int ClientId;

        public readonly string Address;
        public readonly ushort Port;

        public JettConnection(int clientId, string address, ushort port)
        {
            ClientId = clientId;
            Address  = address;
            Port     = port;
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
}