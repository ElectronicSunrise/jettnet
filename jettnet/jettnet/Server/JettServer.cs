using System;
using jettnet.logging;
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

        public JettServer(ushort port = 7777, Socket socket = null, Logger logger = null, params string[] extraMessageAsms)
        {
            _socket = socket ?? new KcpSocket();
            _logger = logger ?? new Logger();
            _port = port;

            _messenger = new JettMessenger(_socket, _logger, true, extraMessageAsms);
        }

        #region Sending

        public void Send(IJettMessage msg, int connectionId, Action msgReceivedCallback = null, int channel = JettChannels.Reliable)
        {
            _messenger.SendMessage(msg, connectionId, true, msgReceivedCallback, channel);
        }

        public void Send(string msgName, int clientId, Action<JettWriter> writeDelegate, Action msgReceivedCallback = null, int channel = JettChannels.Reliable)
        {
            _messenger.SendDelegate(msgName.ToID(), writeDelegate, true, clientId, msgReceivedCallback, channel);
        }

        public void Send(int msgId, int clientId, Action<JettWriter> writeDelegate, Action msgReceivedCallback = null, int channel = JettChannels.Reliable)
        {
            _messenger.SendDelegate(msgId, writeDelegate, true, clientId, msgReceivedCallback, channel);
        }

        #endregion

        #region Registering

        public void Register<T>(Action<T, ConnectionData> msgHandler) where T : struct, IJettMessage<T>
        { 
            _messenger.RegisterInternal(msgHandler);
        }

        public void Register(int msgId, Action<JettReader, ConnectionData> readMethod)
        {
            _messenger.RegisterDelegateInternal(msgId, readMethod);
        }

        public void Register(string msgName, Action<JettReader, ConnectionData> readMethod)
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
            _socket.ServerDataRecv = DataRecv;
            _socket.ServerConnected = ServerConnected;
            _socket.ServerDisconnected = ServerDisconnected;

            _socket.StartServer(_port);
        }

        public void PollData()
        {
            if (Active)
            {
                _socket.FetchIncoming();

                _messenger.InvokeCallbacks();

                _socket.SendOutgoing();
            }
        }

        #endregion

        #region Callbacks

        private void ServerConnected(ConnectionData data)
        {
            _logger.Log("Client connected : " + data.ClientId);

            if (!AllowMultipleConnectionsOneAddress && _socket.AddressExists(data.Address))
            {
                _socket.DisconnectClient(data.ClientId);
                return;
            }

            _messenger.QueueServerCallback(new ServerCallback { Data = data, Method = ClientConnectedToServer });
        }

        private void ServerDisconnected(ConnectionData data)
        {
            _logger.Log("Client disconnected : " + data.ClientId);

            _messenger.QueueServerCallback(new ServerCallback { Data = data, Method = ClientDisconnectedFromServer });
        }

        private void DataRecv(int connId, ArraySegment<byte> segment)
        {
            using(PooledJettReader reader = JettReaderPool.Get(segment.Offset, segment))
            {
                var msgId = (JettHeader)reader.ReadByte();

                switch (msgId)
                {
                    case JettHeader.Message:
                        _messenger.HandleIncomingMessage(reader, _socket.GetDataForClient(connId));
                        break;
                    case JettHeader.MessageReceived:
                        _messenger.HandleIncomingResponseMessage(reader, _socket.GetDataForClient(connId));
                        break;
                }
            }
        }

        #endregion
    }
}
