using System;
using jettnet.logging;

namespace jettnet
{
    public abstract class Socket
    {
        protected readonly Logger _logger;
        
        protected Socket(Logger logger)
        {
            _logger = logger;
        }
        
        // we connected to a server
        public Action ClientConnected;

        // server sent us data
        public Action<ArraySegment<byte>> ClientDataRecv;

        // we disconnected from a server
        public Action ClientDisconnected;

        // client joins our server
        public Action<ConnectionData> ServerConnected;

        // we received client data
        public Action<int, ArraySegment<byte>> ServerDataRecv;

        // someone disconnected from out server
        public Action<ConnectionData> ServerDisconnected;

        public abstract bool TryGetConnection(int id, out ConnectionData connection);
        
        public abstract void StartClient(string address, ushort port);

        public abstract void StopClient();

        public abstract void ClientSend(ArraySegment<byte> data, int channel);

        public abstract void ServerSend(ArraySegment<byte> data, int connId, int channel);

        public abstract void StartServer(ushort port);

        public abstract void DisconnectClient(int id);

        public abstract bool AddressExists(string address);
        
        public abstract bool ServerActive();
        
        public abstract bool ClientActive();

        public abstract void StopServer();

        public abstract void FetchIncoming();

        public abstract void SendOutgoing();
    }
}