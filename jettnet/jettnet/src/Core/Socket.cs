﻿using System;

namespace jettnet
{
    public abstract class Socket
    {
        // we received client data
        public Action<int, ArraySegment<byte>> ServerDataRecv;

        // server sent us data
        public Action<ArraySegment<byte>> ClientDataRecv;
        
        // we connected to a server
        public Action ClientConnected;

        // we disconnected from a server
        public Action ClientDisconnected;

        // client joins our server
        public Action<ConnectionData> ServerConnected;

        // someone disconnected from out server
        public Action<ConnectionData> ServerDisconnected;

        // client encounters an error
        public Action<Exception> ClientError;

        // server encounters an error caused by a client
        public Action<int, Exception> OnServerError;

        public abstract ConnectionData GetDataForClient(int id);

        public abstract void StartClient(string address, ushort port);

        public abstract void StopClient();

        public abstract void ClientSend(ArraySegment<byte> data, int channel);

        public abstract void ServerSend(ArraySegment<byte> data, int connId, int channel);

        public abstract void StartServer(ushort port);

        public abstract void DisconnectClient(int id);

        public abstract bool AddressExists(string addr);

        public abstract void StopServer();

        public abstract void FetchIncoming();
        
        public abstract void SendOutgoing();
    }
}