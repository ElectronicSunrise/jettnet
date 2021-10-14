using jettnet.logging;
using jettnet.sockets;
using System;
using System.Threading;

namespace jettnet
{
    public class JettClient
    {
        private Socket _socket;
        private Logger _logger;

        private JettMessenger _messenger;

        public bool Connected = false;

        public Action OnConnect;
        public Action OnDisconnect;

        private Thread _recvSendThread;

        public JettClient(Socket socket = null, Logger logger = null)
        {
            _logger = logger ?? new Logger();
            _socket = socket ?? new KcpSocket();

            _messenger = new JettMessenger(_socket, _logger, false);
        }

        public void Shutdown()
        {
            _recvSendThread.Abort();
            _socket.StopClient();
        }

        #region Sending

        public void Send(IJettMessage msg, int channel = JettChannels.Reliable, Action msgReceivedCallback = null)
        {
            _messenger.SendMessage(msg, -1, false, msgReceivedCallback, channel);
        }

        public void Send(string msgName, Action<JettWriter> writeDelegate, int channel = JettChannels.Reliable, Action msgReceivedCallback = null)
        {
            _messenger.SendDelegate(msgName.ToID(), writeDelegate, false, -1, msgReceivedCallback, channel);
        }

        public void Send(int msgId, Action<JettWriter> writeDelegate, int channel = JettChannels.Reliable, Action msgReceivedCallback = null)
        {
            _messenger.SendDelegate(msgId, writeDelegate, false, -1, msgReceivedCallback, channel);
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
            _messenger.RegisterInternal(msgHandler);
        }

        #endregion

        #region Connection

        public void Connect(string address, ushort port)
        {
            _recvSendThread = new Thread(() => ConnectInternal(address, port));
            _recvSendThread.Start();
        }

        private void ConnectInternal(string address, ushort port)
        {
            _socket.ClientConnected = ClientConnected;
            _socket.ClientDisconnected = ClientDisconnected;
            _socket.ClientDataRecv = DataRecv;

            _socket.StartClient(address, port);

            while (true)
            {
                _socket.FetchIncoming();
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
            OnDisconnect?.Invoke();
        }

        private void DataRecv(ArraySegment<byte> segment)
        {
            using (PooledJettReader reader = JettReaderPool.Get(segment.Offset, segment))
            {
                var msgId = (Messages)reader.ReadByte();

                switch (msgId)
                {
                    case Messages.Message:
                        // this is client side, client will always receive data from server so no need to pass in data
                        _messenger.HandleIncomingMessage(reader, new ConnectionData());
                        break;
                    case Messages.MessageReceived:
                        _messenger.HandleIncomingMessageReceived(reader);
                        break;
                }
            }
        }

        #endregion
    }
}
