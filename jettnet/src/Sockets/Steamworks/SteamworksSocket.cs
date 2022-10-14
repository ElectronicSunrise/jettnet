using System;
using jettnet.steamworks.core;

namespace jettnet.sockets
{
    public class SteamworksSocket : Socket
    {
        private SteamworksClient steamworksClient;
        private SteamworksServer steamworksServer;

        private core.Logger logger;

        public int MaxConnections { get; private set; }

        public SteamworksServer ActiveServer => steamworksServer;
        public SteamworksClient ActiveClient => steamworksClient;
        public bool UsingSteamGameServices { get; private set; }

        /// <summary>
        /// Creates a new Steamworks Socket
        /// </summary>
        /// <param name="logger">JettNet logger</param>
        /// <param name="maxConnections">Maximum amount of connections for this socket. Unused when creating clients</param>
        /// <param name="usingSteamGameServices">Determines if we are using Steam Game Services</param>
        public SteamworksSocket(core.Logger logger, int maxConnections, bool usingSteamGameServices=false) : base(logger)
        {
            this.logger = logger;
            this.MaxConnections = maxConnections;
            this.UsingSteamGameServices = usingSteamGameServices;
        }

        public override bool AddressExists(string address)
        {
            if (steamworksServer == null)
                throw new InvalidOperationException("Only servers can get addresses!");

            return steamworksServer.AddressExists(address);
        }

        public override bool ClientActive()
        {
            return steamworksClient != null;
        }

        public override void ClientSend(ArraySegment<byte> data, int channel)
        {
            if (!ClientActive())
                throw new InvalidOperationException("Cannot send data on client when no client is active!");

            //Data is tampered with in SteamworksUtils.SendData. Make copy of data array

            byte[] copiedData = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copiedData, 0, data.Count);

            steamworksClient.Send(copiedData, channel);
        }

        public override void DisconnectClient(JettConnection connection)
        {
            if (steamworksServer == null)
                throw new InvalidOperationException("Cannot disconnect client when no server exists!");

            steamworksServer.DisconnectClient(connection);
        }

        public override void FetchIncoming()
        {
            steamworksClient?.ReceiveData();
            steamworksServer?.ReceiveData();
        }

        public override void SendOutgoing()
        {
            steamworksClient?.FlushData();
            steamworksServer?.FlushDataOnAllClients();
        }

        public override bool ServerActive()
        {
            return steamworksServer != null;
        }

        public override void ServerSend(ArraySegment<byte> data, int connId, int channel)
        {
            if (!ServerActive())
                throw new InvalidOperationException("Server must be active before data can be sent!");

            //Data is tampered with in SteamworksUtils.SendData. Make copy of data array

            byte[] copiedData = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copiedData, 0, data.Count);

            steamworksServer.Send(connId, copiedData, channel);
        }

        public override void StartClient(string address, ushort port)
        {
            if (ClientActive())
                throw new InvalidOperationException("Cannot start client when one is already active! Close original client first.");

            logger.Log("Starting client.", core.LogLevel.Info);

            steamworksClient = new SteamworksClient(logger);

            steamworksClient.OnReceivedData += OnClientReceivedData;

            //Port unused (0 by default)
            steamworksClient.Connect(address);
        }

        private void OnClientReceivedData(byte[] data, int channel)
        {
            ClientDataRecv.Invoke(data);
        }

        public override void StartServer(ushort port)
        {
            if (ServerActive())
                throw new InvalidOperationException("Cannot start server when one is already active! Close original server first.");

            logger.Log("Starting server.", core.LogLevel.Info);

            steamworksServer = new SteamworksServer(logger, MaxConnections, UsingSteamGameServices);

            steamworksServer.Host();

            steamworksServer.OnReceivedData += OnServerReceivedData;
        }

        private void OnServerReceivedData(int clientID, byte[] data, int channel)
        {
            ServerDataRecv.Invoke(clientID, data);
        }

        public override void StopClient()
        {
            if (!ClientActive())
                throw new InvalidOperationException("Cannot stop client when no client is running");

            steamworksClient.OnReceivedData -= OnClientReceivedData;

            steamworksClient.Stop();

            steamworksClient = null;
        }

        public override void StopServer()
        {
            if (!ServerActive())
                throw new InvalidOperationException("Cannot stop server when no server is running.");

            steamworksServer.OnReceivedData -= OnServerReceivedData;

            steamworksServer.Shutdown(); //Automatically disposes

            steamworksServer = null;
        }

        public override bool TryGetConnection(int id, out JettConnection jettConnection)
        {
            if (steamworksServer == null)
                throw new InvalidOperationException("Only servers can get connections!");

            if (!steamworksServer.Connections.ContainsKey(id))
            {
                jettConnection = default;
                return false;
            }

            SteamworksConnection steamConnection = steamworksServer.Connections[id];

            jettConnection = new JettConnection(id, steamConnection.SteamID.m_SteamID.ToString(), 0);
            return true;
        }
    }
}