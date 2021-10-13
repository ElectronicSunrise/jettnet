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

        public JettClient(Socket socket = null, Logger logger = null)
        {
            _logger = logger ?? new Logger();
            _socket = socket ?? new KcpSocket();

            _messenger = new JettMessenger(_socket, _logger, false);
        }

        public void Connect(string address, ushort port)
        {
            new Thread(() => ConnectInternal(address, port)).Start();
        }

        public void Send(IJettMessage msg, int channel = JettChannels.Reliable)
        {
            _messenger.SendToServer(msg, channel);
        }

        public void RegisterMessage<T>(Action<T> msgHandler) where T : struct, IJettMessage<T>
        {
            _messenger.RegisterInternal(msgHandler);
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
                        _messenger.HandleIncomingMessage(reader);
                        break;
                }
            }
        }

#if UNITY_64
        // unity keeps sockets open even when editor is done playing
        private void OnApplicationQuit() 
        {
            _socket.StopClient();
        }
#endif
    }
}
