using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_STANDALONE || UNITY_EDITOR
using Steamworks;
using Steamworks.Data;
#endif

public static partial class SteamLobby
{
    public enum FriendRoomState { Open, Full, Playing, DifferentVersion }
    public sealed class FriendRoom
    {
        public readonly ulong LobbyId, HostId;
        public readonly string HostName;
        public readonly int Members, Capacity;
        public readonly FriendRoomState State;
        public bool CanJoin => State == FriendRoomState.Open;
        public FriendRoom(ulong lobbyId, ulong hostId, string hostName, int members, int capacity, FriendRoomState state)
        { LobbyId=lobbyId; HostId=hostId; HostName=NetLegLink.CleanPlayerName(hostName); Members=members; Capacity=capacity; State=state; }
        public string Status => State == FriendRoomState.Playing ? L.T("게임 중", "In game", "プレイ中", "游戏中") :
            State == FriendRoomState.Full ? L.T("가득 찼어요", "Full", "満員", "已满") :
            State == FriendRoomState.DifferentVersion ? L.T("버전이 달라요", "Different version", "バージョンが異なります", "版本不同") :
            L.T("참가 가능", "Joinable", "参加できます", "可加入");
    }
    public sealed class FriendRoomSearch
    {
        public readonly FriendRoom[] Rooms;
        public readonly bool SteamOnline, Incomplete;
        public FriendRoomSearch(FriendRoom[] rooms, bool online, bool incomplete=false)
        { Rooms=rooms; SteamOnline=online; Incomplete=incomplete; }
    }
    public static string FriendRoomGone => L.T("친구의 방에 참가할 수 없어요. 목록을 새로고침해 주세요.", "This friend's room is no longer available. Refresh the list.", "フレンドのルームに参加できません。一覧を更新してください。", "无法加入好友的房间，请刷新列表。");

#if UNITY_STANDALONE || UNITY_EDITOR
    const string RoomStateKey = "wat_state";

    // Query each friend's advertised lobby directly. This avoids the public lobby list's
    // result cap and does not interfere with a concurrent room-code query.
    public static async Task<FriendRoomSearch> FindFriendRoomsAsync(CancellationToken cancellation)
    {
        if (!Available) return new FriendRoomSearch(Array.Empty<FriendRoom>(), false);
        cancellation.ThrowIfCancellationRequested();
        var pending = new List<Task<FriendRoom>>();
        bool incomplete = false;
        foreach (var friend in SteamFriends.GetFriends())
        {
            var game = friend.GameInfo;
            if (!friend.IsFriend || !friend.IsPlayingThisGame || !game.HasValue || !game.Value.Lobby.HasValue) continue;
            var lobby = game.Value.Lobby.Value;
            async Task<FriendRoom> Read(Friend person, Lobby room)
            {
                if (!await RefreshFriendLobbyAsync(room, cancellation)) { incomplete=true; return null; }
                cancellation.ThrowIfCancellationRequested();
                // Presence can change while Steam fetches the lobby metadata.
                var now = person.GameInfo;
                if (!person.IsFriend || !person.IsPlayingThisGame || !now.HasValue || !now.Value.Lobby.HasValue ||
                    now.Value.Lobby.Value.Id.Value != room.Id.Value) return null;
                return ReadFriendRoom(room, person.Id.Value, person.Name);
            }
            pending.Add(Read(friend, lobby));
        }
        var results = await Task.WhenAll(pending);
        cancellation.ThrowIfCancellationRequested();
        if (!SteamManager.LoggedOn) return new FriendRoomSearch(Array.Empty<FriendRoom>(), false);
        var rooms = new List<FriendRoom>(); var seen = new HashSet<ulong>();
        foreach (var room in results) if (room != null && seen.Add(room.LobbyId)) rooms.Add(room);
        rooms.Sort((a,b) => a.CanJoin != b.CanJoin ? (a.CanJoin ? -1 : 1) : StringComparer.OrdinalIgnoreCase.Compare(a.HostName,b.HostName));
        return new FriendRoomSearch(rooms.ToArray(), true, incomplete);
    }

    static FriendRoom ReadFriendRoom(Lobby lobby, ulong friendId, string name)
    {
        // GetLobbyOwner is unavailable before joining. The host's metadata identifies
        // which friend actually hosts; ownership is verified after joining as well.
        if (lobby.GetData(GameKey) != GameValue || !ulong.TryParse(lobby.GetData("wat_host"), out ulong host) || host != friendId)
            return null;
        string state = lobby.GetData(RoomStateKey);
        if (state == "preparing" || state == "closed" || !RoomCodeInput.IsSteamCode(lobby.GetData(CodeKey))) return null;
        int members=lobby.MemberCount, capacity=lobby.MaxMembers;
        if (members <= 0 || capacity <= 0) return null;
        var status = lobby.GetData("wat_protocol") != Protocol ? FriendRoomState.DifferentVersion :
            state == "playing" ? FriendRoomState.Playing : members >= capacity ? FriendRoomState.Full : FriendRoomState.Open;
        return new FriendRoom(lobby.Id.Value, friendId, name, members, capacity, status);
    }

    static async Task<bool> RefreshFriendLobbyAsync(Lobby lobby, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (current.HasValue && current.Value.Id.Value == lobby.Id.Value) return true; // Own lobby is always current.
        var received = new TaskCompletionSource<bool>();
        void Changed(Lobby room) { if (room.Id.Value == lobby.Id.Value) received.TrySetResult(true); }
        SteamMatchmaking.OnLobbyDataChanged += Changed;
        try
        {
            if (!lobby.Refresh()) return false;
            var done = await Task.WhenAny(received.Task, Task.Delay(8000, cancellation));
            cancellation.ThrowIfCancellationRequested();
            return done == received.Task && await received.Task;
        }
        finally { SteamMatchmaking.OnLobbyDataChanged -= Changed; }
    }

    public static async Task<bool> JoinFriendRoomAsync(ulong lobbyId, ulong hostId)
    {
        Hook(); LastError="";
        var friend = new Friend(hostId);
        var game = friend.GameInfo;
        if (!friend.IsFriend || !friend.IsPlayingThisGame || !game.HasValue || !game.Value.Lobby.HasValue ||
            game.Value.Lobby.Value.Id.Value != lobbyId) { LastError=FriendRoomGone; return false; }
        var lobby = game.Value.Lobby.Value;
        if (!await RefreshFriendLobbyAsync(lobby, CancellationToken.None)) { LastError=FriendRoomGone; return false; }
        var room = ReadFriendRoom(lobby, hostId, friend.Name);
        if (room == null || !room.CanJoin) { LastError=FriendRoomGone; return false; }
        return await JoinByIdAsync(lobbyId, lobby.GetData(CodeKey), hostId);
    }
#else
    public static Task<FriendRoomSearch> FindFriendRoomsAsync(CancellationToken cancellation) =>
        Task.FromResult(new FriendRoomSearch(Array.Empty<FriendRoom>(), false));
    public static Task<bool> JoinFriendRoomAsync(ulong lobbyId, ulong hostId) => Task.FromResult(false);
#endif
}
