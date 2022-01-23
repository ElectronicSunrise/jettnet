using System;
using System.Net;
using jettnet.logging;
using kcp2k;

namespace jettnet.sockets
{
    public class KcpSocket : Socket
    {
        private KcpClient _client;
        private KcpServer _server;

        public KcpSocket(Logger logger) : base(logger)
        {
        }

        public override void StartClient(string address, ushort port)
        {
            _client = new KcpClient(ClientConnected,
                                    (data, channel) => ClientDataRecv.Invoke(data),
                                    ClientDisconnected);
            ConfigureLogger();

            _client.Connect(address, port, true, 10, 0, false, 4096, 4096, 5000);
        }

        private void ConfigureLogger()
        {
            Log.Info    = (msg) => _logger.Log(msg, LogLevel.Info);
            Log.Warning = (msg) => _logger.Log(msg, LogLevel.Warning);
            Log.Error   = (msg) => _logger.Log(msg, LogLevel.Error);
        }

        public override void StartServer(ushort port)
        {
            _server = new KcpServer(ServerConnect,
                                    (id, data, channel) => ServerDataRecv.Invoke(id, data),
                                    ServerDisconnect,
                                    true, true, 10, 0, false, 4096, 4096, 5000);

            ConfigureLogger();

            _server.Start(port);
        }

        private ConnectionData KcpIdToJettConnection(int id)
        {
            IPEndPoint endPoint = _server.GetClientEndpoint(id);

            return endPoint == null
                ? default
                : new ConnectionData(id, endPoint.Address.ToString(), (ushort) endPoint.Port);
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
            if (_server == null)
                throw new InvalidOperationException("You may only check address's as the server!");

            foreach (var kvp in _server.connections)
            {
                if (_server.GetClientEndpoint(kvp.Key).Address.ToString() == address)
                    return true;
            }

            return false;
        }

        public override void DisconnectClient(int id)
        {
            _server?.Disconnect(id);
        }

        public override bool TryGetConnection(int id, out ConnectionData connection)
        {
            if (_server == null)
                throw new InvalidOperationException("You may only get connections as the server!");

            ConnectionData conn = KcpIdToJettConnection(id);

            connection = conn;

            return !conn.Equals(default);
        }

        private void ServerConnect(int id)
        {
            if (!(_server.connections[id].GetRemoteEndPoint() is IPEndPoint remoteEndPoint))
                return;

            ConnectionData data =
                new ConnectionData(id, remoteEndPoint.Address.ToString(), (ushort) remoteEndPoint.Port);

            ServerConnected?.Invoke(data);
        }

        private void ServerDisconnect(int id)
        {
            ServerDisconnected?.Invoke(KcpIdToJettConnection(id));
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