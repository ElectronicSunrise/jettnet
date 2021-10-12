using System;
using System.Collections.Generic;
using jettnet.logging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jettnet.sockets;

namespace jettnet
{
    public class JettServer
    {
        private Socket _socket;
        private Logger _logger;
        private JettMessenger _messenger;
        private ushort _port;

        public JettServer(ushort port = 7777, Socket socket = null, Logger logger = null)
        {
            _socket = socket ?? new KcpSocket();
            _logger = logger ?? new Logger();
            _port = port;

            _messenger = new JettMessenger(_socket, _logger);
        }

        public void Start()
        {
            new Thread(() => StartInternal()).Start();
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
            _socket.ServerConnected = ClientConnectedToServer;
            _socket.ServerDisconnected = ClientDisconnectedFromServer;

            _socket.StartServer(_port);

            while (true)
            {
                _socket.FetchIncoming();

                _socket.SendOutgoing();
            }
        }

        private void ClientConnectedToServer(int connId)
        {
            _logger.Log("Client connected : " + connId);
        }

        private void ClientDisconnectedFromServer(int connId)
        {
            _logger.Log("Client disconnected : " + connId);
        }

        private void DataRecv(int connId, ArraySegment<byte> segment)
        {
            using(PooledJettReader reader = JettReaderPool.Get(segment.Offset, segment))
            {
                var msgId = (Messages)reader.ReadByte();

                switch (msgId)
                {
                    case Messages.WorldUpdate:
                        break;
                    case Messages.Ping:
                        _logger.Log("ping");
                        break;
                    case Messages.Pong:
                        _logger.Log("pong");
                        break;
                }
            }
        }
    }
}
