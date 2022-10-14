using Steamworks;
using System.Runtime.InteropServices;
using System;

namespace jettnet.steamworks.core
{
    public static class SteamworksUtils
    {

        public const int MAX_MESSAGES = 256;

        public static EResult SendData(HSteamNetConnection steamConnection, byte[] data, int channelID)
        {
            Array.Resize(ref data, data.Length + 1);
            data[data.Length - 1] = (byte)channelID;

            int sendFlag;

            switch (channelID)
            {
                case 0:
                    //Reliable
                    sendFlag = Constants.k_nSteamNetworkingSend_Reliable;
                    break;
                case 1:
                    //Unreliable
                    sendFlag = Constants.k_nSteamNetworkingSend_Unreliable;
                    break;
                default:
                    //Unknown, default to reliable
                    
                    sendFlag = 0;
                    break;
            }

            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr addressOfArray = pinnedArray.AddrOfPinnedObject();

            EResult sendResult;

#if UNITY_SERVER
            sendResult = SteamGameServerNetworkingSockets.SendMessageToConnection(steamConnection, addressOfArray, (uint)data.Length, sendFlag, out _);
#else
            sendResult = SteamNetworkingSockets.SendMessageToConnection(steamConnection, addressOfArray, (uint)data.Length, sendFlag, out _);
#endif

            pinnedArray.Free();

            return sendResult;
        }

        public static (byte[], int) ProcessData(IntPtr pointerToData)
        {
            SteamNetworkingMessage_t networkMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(pointerToData);
            byte[] managedArray = new byte[networkMessage.m_cbSize];
            Marshal.Copy(networkMessage.m_pData, managedArray, 0, networkMessage.m_cbSize);
            SteamNetworkingMessage_t.Release(pointerToData);

            int channel = managedArray[managedArray.Length - 1];
            Array.Resize(ref managedArray, managedArray.Length - 1);
            return (managedArray, channel);
        }

        public static void InitRelay()
        {
#if UNITY_SERVER
            SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
#else
            SteamNetworkingUtils.InitRelayNetworkAccess();
#endif
        }
    }
}