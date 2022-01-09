﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Telepathy;

namespace jettnet.sockets
{
    public class TelepathySocket : Socket
    {
        private Server _server;
        private Client _client;

        private int _maxMessageSize = 1200;

        private readonly List<ConnectionData> _connections = new List<ConnectionData>();

        private int _processLimit = 100;

        public override bool TryGetConnection(int id, out ConnectionData connection)
        {
            throw new NotImplementedException();
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
            _server = new Server(_maxMessageSize);

            _server.OnConnected = (id) =>
            {
                ConnectionData connection = TelepathyIdToConnection(id);

                ServerConnected?.Invoke(connection);
            };

            _server.OnDisconnected = (id) =>
            {
                ConnectionData connection = TelepathyIdToConnection(id);

                ServerDisconnected?.Invoke(connection);
            };

            _server.OnData = (id, data) => { ServerDataRecv?.Invoke(id, data); };

            _server.Start(port);
        }

        private ConnectionData TelepathyIdToConnection(int id)
        {
            IPEndPoint ep = _server.GetClientEndpoint(id);

            ushort clientPort = (ushort) ep.Port;
            string clientIp   = ep.Address.ToString();

            ConnectionData connection = new ConnectionData(id, clientIp, clientPort);
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