﻿using System;
using jettnet.logging;
using System.Threading;
using jettnet.sockets;

namespace jettnet
{
    public class JettServer
    {
        private Socket _socket;
        private Logger _logger;
        private JettMessenger _messenger;
        private ushort _port;

        public Action<ConnectionData> ClientConnectedToServer;
        public Action<ConnectionData> ClientDisconnectedFromServer;

        public bool Active = false;

        public bool AllowMultipleConnectionsOneAddress = true;

        private Thread _recvSendThread;

        public JettServer(ushort port = 7777, Socket socket = null, Logger logger = null)
        {
            _socket = socket ?? new KcpSocket();
            _logger = logger ?? new Logger();
            _port = port;

            _messenger = new JettMessenger(_socket, _logger, true);
        }

        public void Start()
        {
            _recvSendThread = new Thread(() => StartInternal());
            _recvSendThread.Start();
            Active = true;
        }

        public void Shutdown()
        {
            _recvSendThread.Abort();
            _socket.StopServer();
        }

        public void Stop()
        {
            _socket.StopServer();
            Active = false;
        }

        public void Send(IJettMessage msg, int connectionId, int channel = JettChannels.Reliable)
        {
            _messenger.SendToClient(msg, connectionId, channel);
        }

        public void RegisterMessage<T>(Action<T> msgHandler) where T : struct, IJettMessage<T>
        {
            _messenger.RegisterInternal(msgHandler);
        }

        public void SendTo(ArraySegment<byte> data, int channel, int connId)
        {
            _socket?.ServerSend(data, connId, channel);
        }

        private void StartInternal()
        {
            _socket.ServerDataRecv = DataRecv;
            _socket.ServerConnected = ServerConnected;
            _socket.ServerDisconnected = ServerDisconnected;

            _socket.StartServer(_port);

            while (true)
            {
                _socket.FetchIncoming();
                _socket.SendOutgoing();
            }
        }

        private void ServerConnected(ConnectionData data)
        {
            _logger.Log("Client connected : " + data.ClientId);

            if (!AllowMultipleConnectionsOneAddress && _socket.AddressExists(data.Address))
            {
                _socket.DisconnectClient(data.ClientId);
                return;
            }

            ClientConnectedToServer?.Invoke(data);
        }

        private void ServerDisconnected(ConnectionData data)
        {
            _logger.Log("Client disconnected : " + data.ClientId);
            ClientDisconnectedFromServer?.Invoke(data);
        }

        private void DataRecv(int connId, ArraySegment<byte> segment)
        {
            using(PooledJettReader reader = JettReaderPool.Get(segment.Offset, segment))
            {
                var msgId = (Messages)reader.ReadByte();

                switch (msgId)
                {
                    case Messages.Message:
                        _messenger.HandleIncomingMessage(reader);
                        break;
                }
            }
        }
    }
}
