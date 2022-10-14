using jettnet;
using Steamworks;
using System.Collections;
using System.Collections.Generic;

public struct SteamworksConnection
{

    public HSteamNetConnection SteamConnectionHandle;
    public SteamNetConnectionInfo_t SteamConnectionInfo;
    public CSteamID SteamID;

    public SteamworksConnection(HSteamNetConnection steamConnectionHandle, SteamNetConnectionInfo_t steamConnectionInfo)
    {
        this.SteamConnectionHandle = steamConnectionHandle;
        this.SteamConnectionInfo = steamConnectionInfo;

        this.SteamID = SteamConnectionInfo.m_identityRemote.GetSteamID();
    }
}
