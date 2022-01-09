using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using kcp;

namespace jettnet.sockets
{
    public class KcpSocket : Socket
    {
        private readonly List<IPEndPoint> _connections = new List<IPEndPoint>();

        private readonly Dictionary<int, ConnectionData> _connectionsById = new Dictionary<int, ConnectionData>();
        private          KcpClient                       _client;
        private          KcpServer                       _server;

        public override void StartClient(string address, ushort port)
        {
            _client = new KcpClient(ClientConnected,
                                    ClientDataRecv,
                                    ClientDisconnected);

            // wrapper handles logging
            // so kcp can stfu,
            // unless error related
            Log.Warning = _ => { };
            Log.Info    = _ => { };

            _client.Connect(address, port, true, 10, 0, false, 4096, 4096, 5000);
        }

        public override void StartServer(ushort port)
        {
            _server = new KcpServer(ServerConnect,
                                    ServerDataRecv,
                                    ServerDisconnect,
                                    true, true, 10, 0, false, 4096, 4096, 5000);

            Log.Error   = _ => { };
            Log.Warning = _ => { };
            Log.Info    = _ => { };

            _server.Start(port);
        }
        
        public override bool ClientActive()
        {
            return _client != null && _client.connected;
        }
        
        public override bool ServerActive()
        {
            return _server != null && _server.IsActive();
        }

        public override bool AddressExists(string address)
        {
            return _connections.FirstOrDefault(x => x.Address.ToString() == address) != null;
        }

        public override void DisconnectClient(int id)
        {
            _server?.Disconnect(id);
        }

        public override bool TryGetConnection(int id, out ConnectionData connection)
        {
            if (_connectionsById.TryGetValue(id, out ConnectionData data))
            {
                connection = data;
                return true;
            }

            connection = default;
            return false;
        }

        private void ServerConnect(int id)
        {
            if (!(_server.connections[id].GetRemoteEndPoint() is IPEndPoint remoteEndPoint)) return;
            string address = remoteEndPoint.Address.ToString();

            ConnectionData data = new ConnectionData(id, address, (ushort) remoteEndPoint.Port);

            _connectionsById.Add(id, data);
            _connections.Add(remoteEndPoint);

            ServerConnected?.Invoke(data);
        }

        private void ServerDisconnect(int id)
        {
            ServerDisconnected?.Invoke(_connectionsById[id]);
            _connections.Remove(_server.connections[id].GetRemoteEndPoint() as IPEndPoint);
            _connectionsById.Remove(id);
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