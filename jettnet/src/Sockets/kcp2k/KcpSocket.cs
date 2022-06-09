using System;
using System.Net;
using jettnet.core;
using kcp2p;

namespace jettnet.sockets
{
    public class KcpSocket : Socket
    {
        private KcpClient _client;
        private KcpServer _server;

        private static readonly byte[] ARBITRARY_PUNCH_BYTES = { 0x0, 0x0 };

        public KcpSocket(Logger logger) : base(logger)
        {
        }

        public void SendArbitrary(int count, IPEndPoint ep)
        {
            System.Net.Sockets.Socket sock = _client != null ? _client.connection.socket : _server.socket;
            
            for (int i = 0; i < count; i++)
            {
                sock.SendTo(ARBITRARY_PUNCH_BYTES, ep);
            }
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

        private JettConnection KcpIdToJettConnection(int id)
        {
            IPEndPoint endPoint = _server.GetClientEndpoint(id);

            return endPoint == null
                ? default
                : new JettConnection(id, endPoint.Address.ToString(), (ushort) endPoint.Port);
        }

        public override bool ClientActive()
        {
            return _client is { connected: true };
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

        public override void DisconnectClient(JettConnection connection)
        {
            _server?.Disconnect(connection.ClientId);
        }

        public override bool TryGetConnection(int id, out JettConnection jettConnection)
        {
            if (_server == null)
                throw new InvalidOperationException("You may only get connections as the server!");

            JettConnection conn = KcpIdToJettConnection(id);

            jettConnection = conn;

            return !conn.Equals(default);
        }

        private void ServerConnect(int id)
        {
            if (!(_server.connections[id].GetRemoteEndPoint() is IPEndPoint remoteEndPoint))
                return;

            JettConnection data =
                new JettConnection(id, remoteEndPoint.Address.ToString(), (ushort) remoteEndPoint.Port);

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