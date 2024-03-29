﻿using System;
using System.Collections.Generic;
using jettnet.core;
using jettnet.mirage.bitpacking;
using jettnet.sockets;

namespace jettnet
{
    public class JettServer
    {
        private readonly Logger    _logger;
        private readonly Messenger _messenger;
        private readonly ushort    _port;
        private readonly Socket    _socket;

        public bool Active;

        public bool AllowMultipleConnectionsOneAddress = true;

        public Action<JettConnection> ClientConnectedToServer;
        public Action<JettConnection> ClientDisconnectedFromServer;

        public JettServer(ushort port = 7777, Socket socket = null, Logger logger = null,
                          params string[] extraMessageAssemblies)
        {
            _logger = logger ?? new Logger();
            _socket = socket ?? new KcpSocket(_logger);
            _port   = port;

            _messenger = new Messenger(_socket, _logger, extraMessageAssemblies);
        }

        #region Sending

        public void Send(IJettMessage msg, int connectionId, int channel = JettConstants.ReliableChannel)
        {
            _messenger.SendMessage(msg, connectionId, true, channel);
        }
        
        public void Send(IJettMessage msg, IEnumerable<int> connectionIds, int channel = JettConstants.ReliableChannel)
        {
            _messenger.SendManyMessages(msg, connectionIds, channel);
        }
        
        public void Send(string msgName, IEnumerable<int> connectionIds, Action<JettWriter> writeDelegate, int channel = JettConstants.ReliableChannel)
        {
            _messenger.SendManyDelegates(msgName.ToId(), writeDelegate, connectionIds, channel);
        }

        public void Send(int msgId, IEnumerable<int> connectionIds, Action<JettWriter> writeDelegate, int channel = JettConstants.ReliableChannel)
        {
            _messenger.SendManyDelegates(msgId, writeDelegate, connectionIds, channel);
        }
        
        public void Send(string msgName, int clientId, Action<JettWriter> writeDelegate, int channel = JettConstants.ReliableChannel)
        {
            _messenger.SendDelegate(msgName.ToId(), writeDelegate, true, clientId, channel);
        }

        public void Send(int msgId, int clientId, Action<JettWriter> writeDelegate, int channel = JettConstants.ReliableChannel)
        {
            _messenger.SendDelegate(msgId, writeDelegate, true, clientId, channel);
        }

        #endregion

        #region Registering

        public void Register<T>(Action<T, JettConnection> msgHandler) where T : struct, IJettMessage<T>
        {
            _messenger.RegisterInternal(msgHandler);
        }

        public void Register(int msgId, Action<JettReader, JettConnection> readMethod)
        {
            _messenger.RegisterDelegateInternal(msgId, readMethod);
        }

        public void Register(string msgName, Action<JettReader, JettConnection> readMethod)
        {
            _messenger.RegisterDelegateInternal(msgName, readMethod);
        }

        #endregion

        #region Control

        public void Start()
        {
            StartInternal();
            Active = true;
        }

        public void Shutdown()
        {
            _socket.StopServer();
        }

        public void Stop()
        {
            _socket.StopServer();
            Active = false;
        }

        private void StartInternal()
        {
            _socket.ServerDataRecv     = DataRecv;
            _socket.ServerConnected    = ServerConnected;
            _socket.ServerDisconnected = ServerDisconnected;

            _socket.StartServer(_port);
        }

        public void FetchIncoming()
        {
            if (Active) _socket.FetchIncoming();
        }

        public void SendOutgoing()
        {
            if (Active) _socket.SendOutgoing();
        }

        public Socket GetActiveSocket()
        {
            return _socket;
        }

        #endregion

        #region Callbacks

        private void ServerConnected(JettConnection data)
        {
            _logger.Log("Client connected : " + data.ClientId);

            if (!AllowMultipleConnectionsOneAddress && _socket.AddressExists(data.Address))
            {
                _socket.DisconnectClient(data);
                return;
            }

            ClientConnectedToServer?.Invoke(data);
        }

        private void ServerDisconnected(JettConnection data)
        {
            _logger.Log("Client disconnected : " + data.ClientId);

            ClientDisconnectedFromServer?.Invoke(data);
        }

        private void DataRecv(int connId, ArraySegment<byte> segment)
        {
            using (PooledJettReader reader = _messenger.ReaderPool.Get(segment))
            {
                if (_socket.TryGetConnection(connId, out JettConnection connection))
                    _messenger.HandleIncomingMessage(reader, connection);
            }
        }

        #endregion
    }
}