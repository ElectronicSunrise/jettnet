using Steamworks;
using System;
using System.Collections.Generic;

namespace jettnet.steamworks.core
{
    public class SteamworksServer
    {

        private jettnet.core.Logger logger;
        private Callback<SteamNetConnectionStatusChangedCallback_t> OnConnectionReceivedCallback;
        private int nextConnectionID = 0;
        private HSteamListenSocket listenSocket;

        public System.Action<SteamNetConnectionStatusChangedCallback_t> OnConnectionReceived;
        public System.Action<HSteamNetConnection> OnConnectionClosed;
        public System.Action<HSteamNetConnection> OnConnectionAccepted;
        public System.Action<SteamworksConnection> OnConnectionEstablished;
        public System.Action<EResult> OnConnectionAcceptError;
        public System.Action<SteamworksConnection> OnClientDisconnected;
        public System.Action<int, byte[], int> OnReceivedData;
        /// <summary>
        /// List of current connections. Key is connection ID and Value is corresponding SteamworksConnection
        /// </summary>
        public Dictionary<int, SteamworksConnection> Connections { get; private set; }
        public bool AcceptingConnections { get; set; } = true;
        public int MaxConnections { get; private set; }
        public bool UsingSteamGameServices { get; private set; }

        public SteamworksServer(jettnet.core.Logger logger, int maxConnections, bool usingSteamGameServices)
        {
            SteamworksUtils.InitRelay(UsingSteamGameServices);

            this.logger = logger;
            this.MaxConnections = maxConnections;
            this.UsingSteamGameServices = usingSteamGameServices;

            Connections = new Dictionary<int, SteamworksConnection>();

            OnConnectionReceivedCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionReceivedInternal);
        }

        public void Host()
        {
            SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[0];

            logger.Log($"Hosting with CSteamID: {SteamUser.GetSteamID().m_SteamID.ToString()}", jettnet.core.LogLevel.Info);

            if (UsingSteamGameServices)
            {
                listenSocket = SteamGameServerNetworkingSockets.CreateListenSocketP2P(0, options.Length, options);
            }
            else
            {
                listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, options.Length, options);
            }
        }

        private void OnConnectionReceivedInternal(SteamNetConnectionStatusChangedCallback_t connectionStatus)
        {
            ESteamNetworkingConnectionState connectionState = connectionStatus.m_info.m_eState;
            HSteamNetConnection steamConnectionHandle = connectionStatus.m_hConn;
            SteamNetConnectionInfo_t steamConnectionInfo = connectionStatus.m_info;

            logger.Log($"Server received connection state: {connectionState}", jettnet.core.LogLevel.Info);

            if (connectionState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                //Connection attempt

                OnConnectionReceived?.Invoke(connectionStatus);

                AttemptAcceptConnection(steamConnectionHandle);
            }
            else if (connectionState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                //Successfully connected

                OnConnectionEstablishedInternal(steamConnectionInfo, steamConnectionHandle);
            }
            else if (connectionState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer || connectionState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                logger.Log("Server Connection closed by peer or local error occured.", jettnet.core.LogLevel.Info);

                int clientID = ConvertSteamIDToClientID(steamConnectionInfo.m_identityRemote.GetSteamID());

                if (Connections.ContainsKey(clientID))
                {
                    DisconnectClient(clientID);
                }
            }
        }

        public void FlushDataOnAllClients()
        {
            //Manually flush socket data

            foreach (KeyValuePair<int, SteamworksConnection> connection in Connections)
            {
                if (UsingSteamGameServices)
                {
                    SteamGameServerNetworkingSockets.FlushMessagesOnConnection(connection.Value.SteamConnectionHandle);
                }
                else
                {
                    SteamNetworkingSockets.FlushMessagesOnConnection(connection.Value.SteamConnectionHandle);
                }
            }
        }

        public bool AddressExists(string address)
        {
            if (ulong.TryParse(address, out ulong parsedAddress))
            {
                CSteamID steamID = new CSteamID(parsedAddress);

                int clientID = ConvertSteamIDToClientID(steamID);

                if (clientID == -1)
                    return false;
                else
                    return true;
            }
            else
            {
                throw new InvalidOperationException("Incompatible CSteamID address format.");
            }
        }

        private int ConvertSteamIDToClientID(CSteamID steamID)
        {
            //May cache if overhead becomes extreme

            foreach (KeyValuePair<int, SteamworksConnection> connection in Connections)
            {
                if (connection.Value.SteamID == steamID)
                    return connection.Key;
            }

            return -1;
        }

        private void OnConnectionEstablishedInternal(SteamNetConnectionInfo_t clientConnectionInfo, HSteamNetConnection clientConnectionHandle)
        {
            SteamworksConnection newSteamworksConnection = RegisterConnection(clientConnectionHandle, clientConnectionInfo);

            OnConnectionEstablished?.Invoke(newSteamworksConnection);
        }

        private SteamworksConnection RegisterConnection(HSteamNetConnection connectionHandle, SteamNetConnectionInfo_t steamConnectionInfo)
        {
            SteamworksConnection steamworksConnection = new SteamworksConnection(connectionHandle, steamConnectionInfo);

            Connections.Add(nextConnectionID++, steamworksConnection);

            return steamworksConnection;
        }

        private void AttemptAcceptConnection(HSteamNetConnection clientConnection)
        {
            //Attemps to accept connection from external client. NOTE: Cannot be localhost.
            if (!AcceptingConnections)
            {
                logger.Log("Connection received but server not accepting connections. Refusing connection.", jettnet.core.LogLevel.Info);
                CloseConnection(clientConnection, "Not accepting connections");
                return;
            }

            if (Connections.Count >= MaxConnections)
            {
                logger.Log("Connection received but hit max connections. Refusing connection", jettnet.core.LogLevel.Info);
                CloseConnection(clientConnection, "Max connections");
                return;
            }

            //Accept connection

            EResult acceptResult;

            if (UsingSteamGameServices)
            {
                acceptResult = SteamGameServerNetworkingSockets.AcceptConnection(clientConnection);
            }
            else
            {
                acceptResult = SteamNetworkingSockets.AcceptConnection(clientConnection);
            }

            if (acceptResult == EResult.k_EResultOK)
            {
                logger.Log("Accepted connection.", jettnet.core.LogLevel.Info);
                OnConnectionAccepted?.Invoke(clientConnection);
            }
            else
            {
                logger.Log($"Could not accept connection. This should be okay since the connection state may change. Result: {acceptResult}", jettnet.core.LogLevel.Info);
                OnConnectionAcceptError?.Invoke(acceptResult);
            }
        }

        private void CloseConnection(HSteamNetConnection connection, string disconnectReason) {
            if (UsingSteamGameServices)
            {
                SteamGameServerNetworkingSockets.CloseConnection(connection, 0, disconnectReason, false);
            }
            else
            {
                SteamNetworkingSockets.CloseConnection(connection, 0, disconnectReason, false);
            }

            logger.Log("Connection closed.", jettnet.core.LogLevel.Info);

            OnConnectionClosed?.Invoke(connection);
        }

        private void DisconnectClientInternal(int clientID)
        {
            if (!Connections.ContainsKey(clientID))
            {
                logger.Log("Cannot close connection that does not exist.", jettnet.core.LogLevel.Error);
                return;
            }

            SteamworksConnection steamConnection = Connections[clientID];

            CloseConnection(steamConnection.SteamConnectionHandle, "Explicit disconnect");

            Connections.Remove(clientID);
        }

        public void DisconnectClient(JettConnection connection)
        {
            DisconnectClientInternal(connection.ClientId);
        }

        public void DisconnectClient(int clientID)
        {
            DisconnectClientInternal(clientID);
        }

        public void Shutdown()
        {
            //CloseListenSocket automatically ungracefully disconnects all connections.

            if (UsingSteamGameServices)
            {
                SteamGameServerNetworkingSockets.CloseListenSocket(listenSocket);
            }
            else
            {
                SteamNetworkingSockets.CloseListenSocket(listenSocket);
            }

            Connections.Clear();
            nextConnectionID = 0;

            Dispose();
        }

        public void Send(int connectionID, byte[] data, int channelID)
        {
            if (!Connections.ContainsKey(connectionID))
                throw new NullReferenceException("Cannot send data to unknown connection");

            SteamworksConnection clientConnection = Connections[connectionID];

            EResult dataSendResult = SteamworksUtils.SendData(clientConnection.SteamConnectionHandle, data, channelID, UsingSteamGameServices);

            if (dataSendResult == EResult.k_EResultNoConnection || dataSendResult == EResult.k_EResultInvalidParam)
            {
                logger.Log("Connection was lost to client.", jettnet.core.LogLevel.Info);
                DisconnectClientInternal(connectionID);
            }
            else if (dataSendResult != EResult.k_EResultOK)
            {
                logger.Log("An error occured while sending data.", jettnet.core.LogLevel.Error);
                return;
            }
        }

        public void ReceiveData()
        {
            foreach (KeyValuePair<int, SteamworksConnection> clientConnection in Connections)
            {
                IntPtr[] ptrs = new IntPtr[SteamworksUtils.MAX_MESSAGES];
                int messageCount;

                if ((messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(clientConnection.Value.SteamConnectionHandle, ptrs, SteamworksUtils.MAX_MESSAGES)) > 0)
                {
                    //Received messages from endpoint

                    for (int i=0; i<messageCount; i++)
                    {
                        (byte[] data, int channel) = SteamworksUtils.ProcessData(ptrs[i]);

                        OnReceivedData?.Invoke(clientConnection.Key, data, channel);
                    }
                }
            }
        }

        private void Dispose()
        {
            if (OnConnectionReceivedCallback != null)
            {
                OnConnectionReceivedCallback.Dispose();
                OnConnectionReceivedCallback = null;
            }
        }
    }
}