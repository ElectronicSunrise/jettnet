using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace jettnet.steamworks.core
{
    public class SteamworksClient
    {

        private jettnet.core.Logger logger;
        private Callback<SteamNetConnectionStatusChangedCallback_t> OnConnectionStatusChanged;
        private List<System.Action> dataReceivedBeforeConnectionBuffer;

        public System.Action<HSteamNetConnection> OnConnectionAttempted;
        public System.Action<SteamworksConnection> OnConnectionEstablished;
        public System.Action<HSteamNetConnection> OnDisconnected;
        public System.Action<byte[], int> OnReceivedData;

        public SteamworksClient(jettnet.core.Logger logger)
        {
            SteamworksUtils.InitRelay();

            this.logger = logger;

            dataReceivedBeforeConnectionBuffer = new List<Action>();

            OnConnectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChangedInternal);
        }

        private void OnConnectionStatusChangedInternal(SteamNetConnectionStatusChangedCallback_t connectionStatus)
        {
            SteamworksConnection steamworksConnection = new SteamworksConnection(connectionStatus.m_hConn, connectionStatus.m_info);

            if (connectionStatus.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                //Connected to server
                logger.Log("Client Connection established.", jettnet.core.LogLevel.Info);

                Connected = true;
                OnConnectionEstablished?.Invoke(steamworksConnection);

                if (dataReceivedBeforeConnectionBuffer.Count > 0) {
                    logger.Log("Client Processing data received before connection established", jettnet.core.LogLevel.Info);

                    foreach (Action dataToProcess in dataReceivedBeforeConnectionBuffer)
                    {
                        dataToProcess();
                    }
                }
            }
            else if (connectionStatus.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer || connectionStatus.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                logger.Log("Client Connection status either closed by peer or problem occured. Connection terminated.", jettnet.core.LogLevel.Info);

                Disconnect();
            }
            else
            {
                logger.Log($"Client Connection status: {connectionStatus.m_info.m_eState}", jettnet.core.LogLevel.Info);
            }
        }

        private HSteamNetConnection hostConnection;

        public bool Connected { get; private set; }

        public void Connect(string address)
        {
            if (Connected)
                throw new InvalidOperationException("Network client already connected!");

            //Parse string address into CSteamID ulong
            if (ulong.TryParse(address, out ulong parsedAddress))
            {
                CSteamID hostSteamID = new CSteamID(parsedAddress);

                SteamNetworkingIdentity serverNetworkIdentity = new SteamNetworkingIdentity();

                serverNetworkIdentity.SetSteamID(hostSteamID);

                SteamNetworkingConfigValue_t[] configOptions = new SteamNetworkingConfigValue_t[0];

                logger.Log($"Client connecting to server with CSteamID: {hostSteamID}");
                hostConnection = SteamNetworkingSockets.ConnectP2P(ref serverNetworkIdentity, 0, configOptions.Length, configOptions);

                OnConnectionAttempted?.Invoke(hostConnection);
            }
            else
            {
                throw new InvalidOperationException("Incompatible CSteamID address format.");
            }
        }

        private void InternalDisconnect(string disconnectReason)
        {
            if (hostConnection.m_HSteamNetConnection != 0)
            {
                logger.Log("Disconnecting.", jettnet.core.LogLevel.Info);

                SteamNetworkingSockets.CloseConnection(hostConnection, 0, disconnectReason, false);

                OnDisconnected?.Invoke(hostConnection);

                hostConnection.m_HSteamNetConnection = 0;

                Connected = false;
            }
        }

        public void Disconnect()
        {
            InternalDisconnect("Explicit disconnect request");
        }

        public void Dispose()
        {
            if (OnConnectionStatusChanged != null)
            {
                OnConnectionStatusChanged.Dispose();
                OnConnectionStatusChanged = null;
            }
        }

        public void ReceiveData()
        {
            IntPtr[] ptrs = new IntPtr[SteamworksUtils.MAX_MESSAGES];
            int messageCount;

            if ((messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(hostConnection, ptrs, SteamworksUtils.MAX_MESSAGES)) > 0)
            {
                for (int i = 0; i < messageCount; i++)
                {
                    (byte[] data, int channel) = SteamworksUtils.ProcessData(ptrs[i]);

                    if (Connected)
                    {
                        //Connection established and ready to process data
                        OnReceivedDataInternal(data, channel);
                    }
                    else
                    {
                        //No connection established yet. Put data into buffer
                        dataReceivedBeforeConnectionBuffer.Add(() => OnReceivedDataInternal(data, channel));
                    }
                }
            }
        }

        public void Send(byte[] data, int channelID)
        {
            EResult sendResult = SteamworksUtils.SendData(hostConnection, data, channelID);

            if (sendResult == EResult.k_EResultNoConnection || sendResult == EResult.k_EResultInvalidParam)
            {
                logger.Log("Connection to server was unexpectedly lost.", jettnet.core.LogLevel.Warning);
                InternalDisconnect("Lost connection to server");
            }
            else if (sendResult != EResult.k_EResultOK)
            {
                logger.Log("Unexpected error occured. Cannot send packet data.", jettnet.core.LogLevel.Warning);
                return;
            }
        }

        public void Stop()
        {
            InternalDisconnect("Client stopped.");
            Dispose();
        }

        public void FlushData()
        {
            SteamNetworkingSockets.FlushMessagesOnConnection(hostConnection);
        }

        private void OnReceivedDataInternal(byte[] data, int channel)
        {
            OnReceivedData?.Invoke(data, channel);
        }
    }
}