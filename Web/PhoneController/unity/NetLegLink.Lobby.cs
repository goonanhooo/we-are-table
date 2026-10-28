using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

public partial class NetLegLink
{
    public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
    {
        public ulong clientId;
        public FixedString128Bytes name;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter {s.SerializeValue(ref clientId);s.SerializeValue(ref name);}
        public bool Equals(LobbyPlayer other)=>clientId==other.clientId&&name.Equals(other.name);
    }
    NetworkList<LobbyPlayer> lobbyPlayers;
    readonly NetworkVariable<byte> lobbyPage=new(0,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
    static readonly Dictionary<ulong,string> savedNames=new();
    string sentName;
    public bool ChoosingMap=>lobbyPage.Value==1;
    public int LobbyPlayerCount=>lobbyPlayers==null?0:lobbyPlayers.Count;
    void Awake(){lobbyPlayers=new NetworkList<LobbyPlayer>();}

    public static string CleanPlayerName(string raw)
    {
        var result=new StringBuilder();var chars=StringInfo.GetTextElementEnumerator(raw??"");int count=0;
        while(chars.MoveNext()&&count<28)
        {
            string c=chars.GetTextElement();bool skip=false;
            foreach(char ch in c)if(char.IsControl(ch)||ch=='<'||ch=='>'||ch=='\u202a'||ch=='\u202b'||ch=='\u202d'||ch=='\u202e'||ch=='\u202c'||ch=='\u2066'||ch=='\u2067'||ch=='\u2068'||ch=='\u2069')skip=true;
            if(skip)continue;if(Encoding.UTF8.GetByteCount(result.ToString()+c)>100)break;
            result.Append(c);count++;
        }
        return result.ToString().Trim();
    }
    static string FallbackName(ulong id)=>"P"+(id+1);
    const string NicknameKey="nickname";
    /// <summary>이 컴퓨터의 닉네임: 직접 정한 이름(설정 → 다리 다시 고르기) → 없으면 Steam 이름.</summary>
    public static string LocalNickname
    {
        get { string own=CleanPlayerName(UnityEngine.PlayerPrefs.GetString(NicknameKey,""));return string.IsNullOrEmpty(own)?CleanPlayerName(SteamManager.PlayerName):own; }
    }
    /// <summary>내 닉네임을 바꾸고 저장 — 방 안의 모든 사람에게 바로 보인다.</summary>
    public void SetMyName(string name)
    {
        name=CleanPlayerName(name);UnityEngine.PlayerPrefs.SetString(NicknameKey,name);UnityEngine.PlayerPrefs.Save();
        sentName=string.IsNullOrEmpty(name)?LocalNickname:name;if(string.IsNullOrEmpty(sentName))sentName=FallbackName(NetworkManager.LocalClientId);
        if(IsServer)SetPlayerName(NetworkManager.LocalClientId,sentName);else RegisterNameRpc(new FixedString128Bytes(sentName));
    }
    void SpawnLobby()
    {
        if(IsServer)
        {
            foreach(var id in NetworkManager.ConnectedClientsIds)AddLobbyPlayer(id);
            NetworkManager.OnClientConnectedCallback+=LobbyConnected;
            if(Started)lobbyPage.Value=1;
        }
        sentName=LocalNickname;
        if(string.IsNullOrEmpty(sentName))sentName=FallbackName(NetworkManager.LocalClientId);
        if(IsServer)SetPlayerName(NetworkManager.LocalClientId,sentName);else RegisterNameRpc(new FixedString128Bytes(sentName));
    }
    void DespawnLobby(){if(NetworkManager!=null)NetworkManager.OnClientConnectedCallback-=LobbyConnected;}
    void LobbyConnected(ulong id)
    {
        if(!IsServer)return;AddLobbyPlayer(id);
        // A late arrival must choose a leg before the shared group advances.
        if(!Started)lobbyPage.Value=0;
    }
    void AddLobbyPlayer(ulong id)
    {
        for(int i=0;i<lobbyPlayers.Count;i++)if(lobbyPlayers[i].clientId==id)return;
        string name=savedNames.TryGetValue(id,out var old)?old:FallbackName(id);
        lobbyPlayers.Add(new LobbyPlayer{clientId=id,name=new FixedString128Bytes(name)});
    }
    void RemoveLobbyPlayer(ulong id)
    {
        for(int i=lobbyPlayers.Count-1;i>=0;i--)if(lobbyPlayers[i].clientId==id)lobbyPlayers.RemoveAt(i);
        savedNames.Remove(id);
    }
    void SetPlayerName(ulong id,string name)
    {
        name=CleanPlayerName(name);if(string.IsNullOrEmpty(name))name=FallbackName(id);
        AddLobbyPlayer(id);savedNames[id]=name;
        for(int i=0;i<lobbyPlayers.Count;i++)if(lobbyPlayers[i].clientId==id){lobbyPlayers[i]=new LobbyPlayer{clientId=id,name=new FixedString128Bytes(name)};break;}
    }
    [Rpc(SendTo.Server)]
    public void RegisterNameRpc(FixedString128Bytes name,RpcParams rpc=default)
    {
        if(!IsServer||!NetworkManager.ConnectedClients.ContainsKey(rpc.Receive.SenderClientId))return;
        SetPlayerName(rpc.Receive.SenderClientId,name.ToString());
    }
    public string DisplayNameOf(long owner)
    {
        if(owner==-2)return "";
        if(owner==-3)return L.T("폰","phone","スマホ","手机");
        ulong id=owner==-1?Unity.Netcode.NetworkManager.ServerClientId:(ulong)owner;
        if(lobbyPlayers!=null)for(int i=0;i<lobbyPlayers.Count;i++)if(lobbyPlayers[i].clientId==id)return lobbyPlayers[i].name.ToString();
        return savedNames.TryGetValue(id,out var name)?name:FallbackName(id);
    }
    public bool ClientHasLeg(ulong id)
    {
        for(int leg=0;leg<4;leg++)if(owners.Value.Get(leg)==id)return true;return false;
    }
    public bool CanChooseMap
    {
        get
        {
            if(!IsServer||Started||NetworkManager.ConnectedClientsIds.Count==0)return false;
            // A Unity client may only watch the shared game while controlling a leg by phone.
            // Unclaimed legs remain playable on the host keyboard.
            return true;
        }
    }
    public bool ChooseMap()
    {
        if(!CanChooseMap)return false;
        if(!CheckpointProgress.IsUnlocked(MapIndex))for(int i=0;i<16;i++)if(CheckpointProgress.IsUnlocked(i)){SetMap(i);break;}
        lobbyPage.Value=1;return true;
    }
    [Rpc(SendTo.Server)]
    public void ReturnToLegsRpc(RpcParams rpc=default)
    {
        if(!IsServer||Started||!NetworkManager.ConnectedClients.ContainsKey(rpc.Receive.SenderClientId))return;
        lobbyPage.Value=0;
    }
}
