using jettnet.logging;
using jettnet.sockets;
using System;

namespace jettnet
{
    public class JettClient
    {
        private Socket _socket;
        private Logger _logger;

        private JettMessenger _messenger;

        public bool Connected = false;
        private bool _active = false;

        public Action OnConnect;
        public Action OnDisconnect;

        public JettClient(Socket socket = null, Logger logger = null, params string[] extraMessageAsms)
        {
            _logger = logger ?? new Logger();
            _socket = socket ?? new KcpSocket();

            _messenger = new JettMessenger(_socket, _logger, extraMessageAsms);
        }

        public void Shutdown()
        {
            _socket.StopClient();
            Connected = false;
            _active = false;
        }

#region Sending

        public void Send(IJettMessage msg, int channel = JettChannels.Reliable)
        {
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

            _socket.ClientConnected = ClientConnected;
            _socket.ClientDisconnected = ClientDisconnected;
            _socket.ClientDataRecv = DataRecv;

            _socket.StartClient(address, port);
        }

        public void FetchIncoming()
        {
            if (_active)
            {
                _socket.FetchIncoming();
            }
        }

        public void SendOutgoing()
        {
            if (_active)
            {
                _socket.SendOutgoing();
            }
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
            _active = false;

            OnDisconnect?.Invoke();
        }

        private void DataRecv(ArraySegment<byte> segment)
        {
            using (PooledJettReader reader = JettReaderPool.Get(segment.Offset, segment))
            {
                var msgId = (JettHeader)reader.ReadByte();

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
