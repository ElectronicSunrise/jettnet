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

        public bool Connected = false;

        public JettClient(Socket socket = null, Logger logger = null)
        {
            _logger = logger ?? new Logger();
            _socket = socket ?? new KcpSocket();
        }

        public void Connect(string address, ushort port)
        {
            new Thread(() => ConnectInternal(address, port)).Start();
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

        public void Send(ArraySegment<byte> data, int channel)
        {
            _socket.ClientSend(data, channel);
        }

        public void Disconnect()
        {
            _socket.StopClient();
        }

        private void ClientConnected()
        {
            _logger.Log("We connected to a server!");
            Connected = true;
        }

        private void ClientDisconnected()
        {
            _logger.Log("We disconnected from a server");
            Connected = false;
        }

        private void DataRecv(ArraySegment<byte> segment)
        {
            using (JettReader reader = new JettReader(segment.Offset, segment))
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
