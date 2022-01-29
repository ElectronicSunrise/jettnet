using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using jettnet.logging;
using jettnet.sockets;

namespace jettnet.punching
{
    public struct PunchOptions
    {
        public readonly IPEndPoint Peer;
        public readonly IPEndPoint Proxy;

        public readonly ushort ListeningPort;

        public readonly int Retries;
        public readonly int RetryInterval;

        public PunchOptions(ushort listeningPort,
                            IPEndPoint proxy, IPEndPoint peer,
                            int retries, int retryInterval)
        {
            ListeningPort = listeningPort;
            Proxy         = proxy;
            Peer          = peer;
            Retries       = retries;
            RetryInterval = retryInterval;
        }
    }

    public class PuncherProxy
    {
        private readonly int _attempts;
        private readonly int _interval;

        private readonly bool _isServer;

        private readonly KcpSocket  _kcp;
        private readonly UdpClient  _proxy;
        private readonly IPEndPoint _internalKcpEndPoint;
        private readonly IPEndPoint _peerEndPoint;
        private readonly Logger     _logger;

        private IPEndPoint _recvEndpoint = new IPEndPoint(IPAddress.Any, 0);

        private static readonly byte[] _punchData = {69, 169};

        private bool _punchSuccess;

        private bool IsPunchData(byte[] data) => data.Length == 2
                                              && data[0] == _punchData[0]
                                              && data[1] == _punchData[1];

        private bool IsFromInternalKcpEndPoint(IPEndPoint endpoint) => endpoint.Equals(_internalKcpEndPoint);
        private bool IsFromPeerEndPoint(IPEndPoint endpoint) => endpoint.Equals(_peerEndPoint);

        public PuncherProxy(PunchOptions options, KcpSocket kcp, bool isServer, Logger logger)
        {
            _isServer = isServer;
            _kcp      = kcp;
            _logger   = logger;

            _attempts = options.Retries;
            _interval = options.RetryInterval;

            _internalKcpEndPoint = options.Proxy;
            _peerEndPoint        = options.Peer;

            _proxy = new UdpClient(options.ListeningPort);

            _proxy.BeginReceive(DataReceived, _proxy);
        }

        private void DataReceived(IAsyncResult result)
        {
            byte[] data = _proxy.EndReceive(result, ref _recvEndpoint);
            _proxy.BeginReceive(DataReceived, _proxy);

            bool isPunchData = IsPunchData(data);
            bool isFromPeer  = IsFromPeerEndPoint(_recvEndpoint);

            // game data, and we have successfully punched
            if (!isPunchData && _punchSuccess)
            {
                // normal game data, pass it onto kcp
                _proxy.Send(data, data.Length, _internalKcpEndPoint);

                return;
            }

            // not yet punched, and we have received a punch packet
            if (isFromPeer && !_punchSuccess)
            {
                _punchSuccess = true;
                _logger.Log($"Punch successful to peer {_peerEndPoint.Address}");
                
                // try to connect
                if(!_isServer)
                    _kcp.StartClient(_peerEndPoint.Address.ToString(), (ushort)_peerEndPoint.Port);

                return;
            }

            // punched and kcp is sending data, relay it to the peer
            if (IsFromInternalKcpEndPoint(_recvEndpoint) && _punchSuccess)
            {
                _proxy.Send(data, data.Length, _peerEndPoint);

                return;
            }
        }

        public void SendPunch()
        {
            if (_punchSuccess)
                throw new Exception("Punch already successful, why are you trying to send it again?");

            Task.Factory.StartNew(async () =>
            {
                for (int i = 0; i < _attempts; i++)
                {
                    _proxy.Send(_punchData, _punchData.Length, _peerEndPoint);

                    await Task.Delay(_interval);
                }
            });
        }
    }
}