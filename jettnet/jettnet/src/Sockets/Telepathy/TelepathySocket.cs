using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using jettnet.logging;
using Telepathy;

namespace jettnet.sockets
{
    public class TelepathySocket : Socket
    {
        private Server _server;
        private Client _client;

        private readonly int _maxMessageSize;
        private readonly int _processLimit;

        private readonly List<ConnectionData> _connections = new List<ConnectionData>();

        public TelepathySocket(Logger logger, int maxMessageSize = 1200, int processLimit = 100) : base(logger)
        {
            _maxMessageSize = maxMessageSize;
            _processLimit   = processLimit;
        }
        
        public override bool TryGetConnection(int id, out ConnectionData connection)
        {
            IPEndPoint clientEndPoint = _server.GetClientEndpoint(id);

            bool endpointExists = clientEndPoint != null;

            connection = endpointExists ?  new ConnectionData(id, clientEndPoint.Address.ToString(), (ushort) clientEndPoint.Port)
                                        : default;

            return endpointExists;
        }

        public override void StartClient(string address, ushort port)
        {
            _client = new Client(_maxMessageSize);

            _client.OnConnected    = () => ClientConnected?.Invoke();
            _client.OnDisconnected = () => ClientDisconnected?.Invoke();
            _client.OnData         = (data) => ClientDataRecv?.Invoke(data);

            _client.Connect(address, port);
        }

        public override void StopClient()
        {
            _connections.Clear();
            _client.Disconnect();
            _client = null;
        }

        public override void ClientSend(ArraySegment<byte> data, int channel)
        {
            _client?.Send(data);
        }

        public override void ServerSend(ArraySegment<byte> data, int connId, int channel)
        {
            _server?.Send(connId, data);
        }

        public override void StartServer(ushort port)
        {
            _server = new Server(_maxMessageSize)
            {
                OnConnected = (id) =>
                {
                    ConnectionData connection = TelepathyIdToConnection(id);
                    
                    _connections.Add(connection);

                    ServerConnected?.Invoke(connection);
                },
                OnDisconnected = (id) =>
                {
                    ConnectionData connection = TelepathyIdToConnection(id);

                    _connections.Remove(connection);
                    
                    ServerDisconnected?.Invoke(connection);
                },
                OnData = (id, data) => ServerDataRecv?.Invoke(id, data)
            };

            _server.Start(port);
        }

        private ConnectionData TelepathyIdToConnection(int id)
        {
            IPEndPoint ep = _server.GetClientEndpoint(id);
            
            ConnectionData connection = new ConnectionData(id, ep.Address.ToString(), (ushort) ep.Port);
            return connection;
        }

        public override void DisconnectClient(int id)
        {
            _server?.Disconnect(id);
        }

        public override bool AddressExists(string address)
        {
            bool isServer = _server != null;
            bool isClient = _client != null;

            if (isServer)
            {
                return _connections.Select(x => x.Address).Contains(address);
            }

            if (isClient)
            {
                throw new Exception("Clients don't have access to all addresses");
            }

            return false;
        }

        public override bool ServerActive()
        {
            return _server != null && _server.Active;
        }

        public override bool ClientActive()
        {
            return _client != null && _client.Connected;
        }

        public override void StopServer()
        {
            _connections.Clear();
            _server?.Stop();
            _server = null;
        }

        public override void FetchIncoming()
        {
            Tick();
        }
        
        public override void SendOutgoing()
        {
            Tick();
        }

        private void Tick()
        {
            _server?.Tick(_processLimit);
            _client?.Tick(_processLimit);
        }
    }
}