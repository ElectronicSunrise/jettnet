using kcp2k;
using System;
using System.Linq;

namespace jettnet.sockets
{
    public class KcpSocket : Socket
    {
        private KcpServer _server;
        private KcpClient _client;

        public override void StartClient(string address, ushort port)
        {
            _client = new KcpClient(ClientConnected, 
                                    ClientDataRecv, 
                                    ClientDisconnected);

            _client.Connect(address, port, true, 10, 0, false, 4096, 4096, 5000);
        }

        public override void StartServer(ushort port)
        {
            _server = new KcpServer(ServerConnected,
                                    ServerDataRecv,
                                    ServerDisconnected,
                                    true, true, 10, 0, false, 4096, 4096, 5000);

            _server.Start(port);
        }

        public override void FetchIncoming()
        {
            _server?.TickIncoming();
            _client?.TickIncoming();
        }

        public override void SendOutgoing()
        {
            _server?.TickOutgoing();
            _client?.TickOutgoing();
        }

        public override void ClientSend(ArraySegment<byte> data, int channel)
        {
            switch (channel)
            {
                case 0:
                    _client?.Send(data, KcpChannel.Reliable);
                    break;
                case 1:
                    _client?.Send(data, KcpChannel.Unreliable);
                    break;

                default:
                    _client?.Send(data, KcpChannel.Reliable);
                    break;
            }
        }

        public override void ServerSend(ArraySegment<byte> data, int connId, int channel)
        {
            switch (channel)
            {
                case 0:
                    _server?.Send(connId, data, KcpChannel.Reliable);
                    break;
                case 1:
                    _server?.Send(connId, data, KcpChannel.Unreliable);
                    break;

                    // for testing, send to first client
                case -1:
                    _server?.Send(_server.connections.FirstOrDefault().Key, data, KcpChannel.Reliable);
                    break;

                default:
                    _server?.Send(connId, data, KcpChannel.Reliable);
                    break;
            }
        }

        public override void StopClient()
        {
            _client?.Disconnect();
        }

        public override void StopServer()
        {
            _server?.Stop();
        }
    }
}
