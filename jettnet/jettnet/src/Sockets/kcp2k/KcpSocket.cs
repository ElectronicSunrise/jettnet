using kcp;
using System;
using System.Collections.Generic;
using System.Net;

namespace jettnet.sockets
{
    public class KcpSocket : Socket
    {
        private KcpServer _server;
        private KcpClient _client;

        private Dictionary<int, ConnectionData> _connectionsByID = new Dictionary<int, ConnectionData>();
        private Dictionary<string, ConnectionData> _connectionsByAddress = new Dictionary<string, ConnectionData>();

        public override void StartClient(string address, ushort port)
        {
            _client = new KcpClient(ClientConnected, 
                                    ClientDataRecv, 
                                    ClientDisconnected);

            _client.Connect(address, port, true, 10, 0, false, 4096, 4096, 5000);
        }

        public override void StartServer(ushort port)
        {
            _server = new KcpServer(ServerConnect,
                                    ServerDataRecv,
                                    ServerDisconnect,
                                    true, true, 10, 0, false, 4096, 4096, 5000);

            _server.Start(port);
        }

        public override bool AddressExists(string addr)
        {
            return _connectionsByAddress.ContainsKey(addr);
        }

        public override void DisconnectClient(int id)
        {
            _server?.Disconnect(id);
        }

        public override ConnectionData GetDataForClient(int id)
        {
            return _connectionsByID[id];
        }

        private void ServerConnect(int id)
        {
            var ep = _server.connections[id].GetRemoteEndPoint() as IPEndPoint;
            var addr = ep.Address.ToString();

            var data = new ConnectionData
            {
                Address = addr,
                Port = (ushort)ep.Port,
                ClientId = id
            };

            _connectionsByID.Add(id, data);
            _connectionsByAddress.Add(addr, data);

            ServerConnected?.Invoke(data);
        }

        private void ServerDisconnect(int id)
        {
            ServerDisconnected?.Invoke(_connectionsByID[id]);
            _connectionsByAddress.Remove(_connectionsByID[id].Address);
            _connectionsByID.Remove(id);
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
                default:
                case 0:
                    _client?.Send(data, KcpChannel.Reliable);
                    break;
                case 1:
                    _client?.Send(data, KcpChannel.Unreliable);
                    break;
            }
        }

        public override void ServerSend(ArraySegment<byte> data, int connId, int channel)
        {
            switch (channel)
            {
                default:
                case 0:
                    _server?.Send(connId, data, KcpChannel.Reliable);
                    break;
                case 1:
                    _server?.Send(connId, data, KcpChannel.Unreliable);
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
