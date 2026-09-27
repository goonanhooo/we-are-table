using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class LevelSelect
{
    SteamLobby.FriendRoom[] friendRooms = Array.Empty<SteamLobby.FriendRoom>();
    CancellationTokenSource friendScan;
    bool friendSearching, friendSteamOnline=true, friendSearchFailed, friendSearchIncomplete;
    int friendPage, friendScanVersion;
    float friendRefreshAt;
    readonly JourneyWheel friendWheel = new JourneyWheel();
    const int FriendsPerPage=3;
    public static readonly Rect FriendPanelRect=new Rect(100,330,1400,276);
    public static readonly Rect FriendRefreshRect=new Rect(1195,346,275,40);
    public static readonly Rect RoomCodeRect=new Rect(140,669,940,56);
    public static readonly Rect CodeJoinRect=new Rect(1110,669,350,56);
    public static Rect FriendRowRect(int row)=>new Rect(130,400+row*64,1340,58);
    public static Rect FriendJoinRect(int row)=>new Rect(1220,410+row*64,228,38);

    void EnterFriendRooms()
    {
        CancelFriendSearch();friendRooms=Array.Empty<SteamLobby.FriendRoom>();friendPage=0;
        RefreshFriendRooms();
    }
    void CancelFriendSearch()
    {
        friendScanVersion++;friendScan?.Cancel();friendScan?.Dispose();friendScan=null;friendSearching=false;
    }
    async void RefreshFriendRooms()
    {
        if (friendSearching || !Showing || step!=2 || busy) return;
        var source=new CancellationTokenSource();friendScan=source;int version=++friendScanVersion;
        friendSearching=true;friendSearchFailed=false;friendSearchIncomplete=false;
        try
        {
            var result=await SteamLobby.FindFriendRoomsAsync(source.Token);
            if(this==null || version!=friendScanVersion || !Showing || step!=2)return;
            friendRooms=result.Rooms;friendSteamOnline=result.SteamOnline;friendSearchIncomplete=result.Incomplete;
            friendPage=Mathf.Clamp(friendPage,0,Mathf.Max(0,(friendRooms.Length-1)/FriendsPerPage));
        }
        catch(OperationCanceledException) { }
        catch(Exception e)
        {
            if(this!=null && version==friendScanVersion){friendSearchFailed=true;friendRooms=Array.Empty<SteamLobby.FriendRoom>();}
            Debug.LogWarning("[Steam friends] Room search failed: "+e.Message);
        }
        finally
        {
            if(this!=null && version==friendScanVersion){friendSearching=false;friendScan=null;friendRefreshAt=Time.unscaledTime+15;}
            source.Dispose();
        }
    }
    void UpdateFriendRooms()
    {
        if(!friendSearching && Time.unscaledTime>=friendRefreshAt)RefreshFriendRooms();
        if(Mouse.current!=null && FriendPanelRect.Contains(JourneyMenuUI.MousePosition))
            friendPage=Mathf.Clamp(friendPage+friendWheel.Read(Mouse.current.scroll.ReadValue(),Time.unscaledTime),0,Mathf.Max(0,(friendRooms.Length-1)/FriendsPerPage));
    }
    async void JoinFriendRoom(SteamLobby.FriendRoom room)
    {
        if(busy || !room.CanJoin)return;
        CancelFriendSearch();busy=true;
        busyMsg=L.T("친구의 방에 들어가는 중","Joining your friend's room","フレンドのルームに参加中","正在加入好友的房间");
        bool ok=await NetSession.JoinFriendAsync(room.LobbyId,room.HostId);
        if(this==null)return;busy=false;
        if(ok)Close();else{friendRefreshAt=Time.unscaledTime+15;RefreshFriendRooms();}
    }
    string FriendSearchMessage => !friendSteamOnline ? L.T("Steam에 로그인하면 친구의 방을 볼 수 있어요.","Sign in to Steam to see your friends' rooms.","Steamにログインするとフレンドのルームが表示されます。","登录 Steam 后即可查看好友的房间。") :
        friendSearchFailed || friendSearchIncomplete ? L.T("친구 목록을 불러오지 못했어요. 새로고침해 주세요.","Could not load all friends' rooms. Please refresh.","フレンドのルームを読み込めませんでした。更新してください。","未能加载好友房间，请刷新。") :
        L.T("지금 방을 연 친구가 없어요.","No friends are hosting a room right now.","今ルームを開いているフレンドはいません。","目前没有好友正在创建房间。");

    void DrawFriendJoin()
    {
        JourneyMenuUI.Text(new Rect(100,195,1400,60),L.T("친구 방에 참가","Join a friend","フレンドに参加","加入好友"),40);
        JourneyMenuUI.Text(new Rect(103,261,1394,42),L.T("친구의 방을 고르거나 방 코드를 입력하세요.","Choose a friend's room or enter a room code.","フレンドのルームを選ぶか、ルームコードを入力。","选择好友的房间，或输入房间代码。"),25,JourneyMenuUI.Muted);
        JourneyMenuUI.Paper(FriendPanelRect);
        JourneyMenuUI.Text(new Rect(130,345,735,43),friendSearching?L.T("Steam 친구 찾는 중...","Finding Steam friends...","Steamフレンドを検索中…","正在查找 Steam 好友…"):L.T("Steam 친구의 방","Steam friends' rooms","Steamフレンドのルーム","Steam 好友的房间"),28);
        int pages=Mathf.Max(1,Mathf.CeilToInt(friendRooms.Length/(float)FriendsPerPage));
        if(pages>1)
        {
            if(JourneyMenuUI.Button(new Rect(915,346,44,40),"‹",friendPage>0))friendPage--;
            JourneyMenuUI.Literal(new Rect(970,346,120,40),(friendPage+1)+" / "+pages,21,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
            if(JourneyMenuUI.Button(new Rect(1101,346,44,40),"›",friendPage<pages-1))friendPage++;
        }
        if(JourneyMenuUI.Button(FriendRefreshRect,L.T("새로고침","Refresh","更新","刷新"),!friendSearching))RefreshFriendRooms();
        if(friendRooms.Length==0)
        {
            JourneyMenuUI.Icon(new Rect(165,442,84,84),1);
            JourneyMenuUI.Text(new Rect(290,430,1120,115),friendSearching?L.T("친구가 열어 둔 방을 확인하고 있어요.","Checking rooms hosted by your friends.","フレンドが開いているルームを確認しています。","正在查找好友创建的房间。") : FriendSearchMessage,27,JourneyMenuUI.Muted);
        }
        for(int row=0;row<FriendsPerPage;row++)
        {
            int index=friendPage*FriendsPerPage+row;if(index>=friendRooms.Length)break;
            var room=friendRooms[index];var rect=FriendRowRect(row);bool hover=room.CanJoin&&rect.Contains(JourneyMenuUI.MousePosition);
            JourneyMenuUI.Fill(rect,hover?new Color(.92f,.95f,.91f):new Color(1,1,1,.82f));
            JourneyMenuUI.Icon(new Rect(rect.x+12,rect.y+8,43,43),1,room.CanJoin?JourneyMenuUI.Accent:JourneyMenuUI.Muted);
            JourneyMenuUI.Literal(new Rect(rect.x+76,rect.y+7,530,44),room.HostName,27,JourneyMenuUI.Ink);
            JourneyMenuUI.Text(new Rect(755,rect.y+6,292,46),room.Status,23,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
            JourneyMenuUI.Literal(new Rect(1060,rect.y+6,120,46),room.Members+" / "+room.Capacity,23,JourneyMenuUI.Ink,TextAnchor.MiddleCenter);
            bool join=JourneyMenuUI.Button(FriendJoinRect(row),L.T("참가 →","Join →","参加 →","加入 →"),room.CanJoin,room.CanJoin);
            if(join||JourneyMenuUI.Click(rect,room.CanJoin)){JourneyMenuUI.BlockClick();JoinFriendRoom(room);}
        }
        JourneyMenuUI.Paper(new Rect(100,623,1400,129));
        JourneyMenuUI.Text(new Rect(140,627,680,34),L.T("방 코드로 참가","Join with a room code","ルームコードで参加","通过房间代码加入"),25);
        JourneyMenuUI.Text(new Rect(850,627,610,34),L.T("바로 입력 · 붙여넣기 가능 · Enter로 참가","Type or paste a code · Enter to join","入力・貼り付けできます · Enterで参加","直接输入或粘贴代码 · Enter 加入"),20,JourneyMenuUI.Muted,TextAnchor.MiddleRight);
        JourneyMenuUI.Fill(RoomCodeRect,Color.white);JourneyMenuUI.Border(RoomCodeRect,new Color(.57f,.65f,.59f),2);
        JourneyMenuUI.Literal(new Rect(RoomCodeRect.x+18,RoomCodeRect.y+3,RoomCodeRect.width-36,RoomCodeRect.height-6),codeInput+(Time.unscaledTime%1<.55?"|":" "),32,JourneyMenuUI.Ink);
        if(JourneyMenuUI.Button(CodeJoinRect,L.T("참가 →","Join →","参加 →","加入 →"),RoomCodeInput.IsValid(codeInput),true))JoinRoom();
        if(!string.IsNullOrEmpty(NetSession.Status))JourneyMenuUI.Text(new Rect(325,774,1175,77),NetSession.Status,23,JourneyMenuUI.Muted);
    }
}
