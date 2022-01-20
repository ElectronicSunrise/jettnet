using System;
using jettnet.logging;
using jettnet.sockets;

namespace jettnet
{
    public class JettClient
    {
        private readonly Logger _logger;

        private readonly Messenger _messenger;
        private readonly Socket    _socket;
        private          bool      _active;

        public bool Connected;

        public Action OnConnect;
        public Action OnDisconnect;

        public JettClient(Socket socket = null, Logger logger = null, params string[] extraMessageAssemblies)
        {
            _logger    = logger ?? new Logger();
            _socket    = socket ?? new KcpSocket();
            _messenger = new Messenger(_socket, _logger, extraMessageAssemblies);
        }

        public void Shutdown()
        {
            _socket.StopClient();
            Connected = false;
            _active   = false;
        }

        #region Sending

        public void Send(IJettMessage msg, int channel = JettChannels.Reliable)
        {
            // -1 because server client doesnt need to specify,
            // it's always going to the server
            _messenger.SendMessage(msg, -1, false, channel);
        }

        public void Send(string msgName, Action<JettWriter> writeDelegate, int channel = JettChannels.Reliable)
        {
            _messenger.SendDelegate(msgName.ToID(), writeDelegate, false, -1, channel);
        }

        public void Send(int msgId, Action<JettWriter> writeDelegate, int channel = JettChannels.Reliable)
        {
            _messenger.SendDelegate(msgId, writeDelegate, false, -1, channel);
        }

        #endregion

        #region Registering

        public void Register(int msgId, Action<JettReader> readMethod)
        {
            _messenger.RegisterDelegateInternal(msgId, (r, _) => readMethod?.Invoke(r));
        }

        public void Register(string msgName, Action<JettReader> readMethod)
        {
            _messenger.RegisterDelegateInternal(msgName, (r, _) => readMethod?.Invoke(r));
        }

        public void Register<T>(Action<T> msgHandler) where T : struct, IJettMessage<T>
        {
            _messenger.RegisterInternal<T>((r, _) => msgHandler?.Invoke(r));
        }

        #endregion

        #region Connection

        public void Connect(string address, ushort port)
        {
            ConnectInternal(address, port);
        }

        private void ConnectInternal(string address, ushort port)
        {
            _active = true;

            _socket.ClientConnected    = ClientConnected;
            _socket.ClientDisconnected = ClientDisconnected;
            _socket.ClientDataRecv     = DataRecv;

            _socket.StartClient(address, port);
        }

        public void FetchIncoming()
        {
            if (_active) _socket.FetchIncoming();
        }

        public void SendOutgoing()
        {
            if (_active) _socket.SendOutgoing();
        }

        public void Disconnect()
        {
            _socket.StopClient();
        }

        #endregion

        #region Callbacks

        private void ClientConnected()
        {
            _logger.Log("We connected to a server!");
            Connected = true;

            OnConnect?.Invoke();
        }

        private void ClientDisconnected()
        {
            _logger.Log("We disconnected from a server");
            Connected = false;
            _active   = false;

            OnDisconnect?.Invoke();
        }

        private void DataRecv(ArraySegment<byte> segment)
        {
            using (PooledJettReader reader = JettReaderPool.Get(segment.Offset, segment))
            {
                JettHeader msgId = (JettHeader) reader.Read<byte>();

                switch (msgId)
                {
                    case JettHeader.Message:
                        // this is client side, client will always receive data from server so no need to pass in data
                        _messenger.HandleIncomingMessage(reader, default);
                        break;
                }
            }
        }

        #endregion
    }
}