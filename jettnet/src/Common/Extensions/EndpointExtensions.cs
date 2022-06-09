using System.Net;
using jettnet.mirage.bitpacking;

namespace jettnet.mirage.extensions
{
    public static class EndpointExtensions
    {
        public static void WriteEndpoint(this JettWriter writer, IPEndPoint ep)
        {
            writer.WriteString(ep.Address.ToString());
            writer.WriteUInt16((ushort)ep.Port);
        }
        
        public static IPEndPoint ReadEndpoint(this JettReader reader)
        {
            IPAddress ip = IPAddress.Parse(reader.ReadString());
            ushort port = reader.ReadUInt16();
            return new IPEndPoint(ip, port);
        }
    }
}