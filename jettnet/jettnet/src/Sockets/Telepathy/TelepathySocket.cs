using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using jettnet.logging;
using Telepathy2k;

namespace jettnet.sockets
{
    public class TelepathySocket : Socket
    {
        private Server _server;
        private Client _client;

        private readonly int _maxMessageSize;
        private readonly int _processLimit;

        private readonly List<JettConnection> _connections = new List<JettConnection>();

        public TelepathySocket(Logger logger, int maxMessageSize = 1200, int processLimit = 100) : base(logger)
        {
            _maxMessageSize = maxMessageSize;
            _processLimit   = processLimit;
        }
        
        public override bool TryGetConnection(int id, out JettConnection jettConnection)
        {
            IPEndPoint clientEndPoint = _server.GetClientEndpoint(id);

            bool endpointExists = clientEndPoint != null;

            jettConnection = endpointExists ?  new JettConnection(id, clientEndPoint.Address.ToString(), (ushort) clientEndPoint.Port)
                                        : default;

            return endpointExists;
        }

        public override void StartClient(string address, ushort port)
        {
            _client = new Client(_maxMessageSize)
            {
                OnConnected    = () => ClientConnected?.Invoke(),
                OnDisconnected = () => ClientDisconnected?.Invoke(),
                OnData         = (data) => ClientDataRecv?.Invoke(data)
            };

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
                    JettConnection jettConnection = TelepathyIdToConnection(id);
                    
                    _connections.Add(jettConnection);

                    ServerConnected?.Invoke(jettConnection);
                },
                OnDisconnected = (id) =>
                {
                    JettConnection jettConnection = TelepathyIdToConnection(id);

                    _connections.Remove(jettConnection);
                    
                    ServerDisconnected?.Invoke(jettConnection);
                },
                OnData = (id, data) => ServerDataRecv?.Invoke(id, data)
            };

            _server.Start(port);
        }

        private JettConnection TelepathyIdToConnection(int id)
        {
            IPEndPoint ep = _server.GetClientEndpoint(id);
            
            JettConnection jettConnection = new JettConnection(id, ep.Address.ToString(), (ushort) ep.Port);
            return jettConnection;
        }

        public override void DisconnectClient(JettConnection connection)
        {
            _server?.Disconnect(connection.ClientId);
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