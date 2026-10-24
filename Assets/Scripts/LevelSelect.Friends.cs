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
    // 캔버스 단위(지우개 대소동 규칙 ×2): 간격 8·16·24·32·48, 높이 72·88.
    public static readonly Rect FriendPanelRect=new Rect(80,160,1440,376);
    public static readonly Rect FriendRefreshRect=new Rect(1248,176,240,JourneyMenuUI.HS);
    public static readonly Rect RoomCodePanelRect=new Rect(80,560,1440,160);
    public static readonly Rect RoomCodeRect=new Rect(112,632,1000,JourneyMenuUI.HS);
    public static readonly Rect CodeJoinRect=new Rect(1136,632,352,JourneyMenuUI.HS);
    public static Rect FriendRowRect(int row)=>new Rect(112,264+row*88,1376,JourneyMenuUI.HS);
    public static Rect FriendJoinRect(int row)=>FriendRowRect(row);

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
        Heading(L.T("친구 방에 참가","Join a friend","フレンドに参加","加入好友"));
        var panel=FriendPanelRect;JourneyMenuUI.Box(panel,JourneyMenuUI.Kind.Panel);
        JourneyMenuUI.Text(new Rect(panel.x+JourneyMenuUI.S4,panel.y+JourneyMenuUI.S2,640,JourneyMenuUI.HS),friendSearching?L.T("Steam 친구 찾는 중...","Finding Steam friends...","Steamフレンドを検索中…","正在查找 Steam 好友…"):L.T("Steam 친구의 방","Steam friends' rooms","Steamフレンドのルーム","Steam 好友的房间"),JourneyMenuUI.TM);
        int pages=Mathf.Max(1,Mathf.CeilToInt(friendRooms.Length/(float)FriendsPerPage));
        if(pages>1)
        {
            float px=FriendRefreshRect.x-JourneyMenuUI.S4-88-120-88;
            if(JourneyMenuUI.ArrowButton(new Rect(px,FriendRefreshRect.y,88,JourneyMenuUI.HS),-1,friendPage>0))friendPage--;
            JourneyMenuUI.Literal(new Rect(px+88,FriendRefreshRect.y,120,JourneyMenuUI.HS),(friendPage+1)+" / "+pages,JourneyMenuUI.TXS,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
            if(JourneyMenuUI.ArrowButton(new Rect(px+208,FriendRefreshRect.y,88,JourneyMenuUI.HS),1,friendPage<pages-1))friendPage++;
        }
        if(JourneyMenuUI.Button(FriendRefreshRect,L.T("새로고침","Refresh","更新","刷新"),!friendSearching))RefreshFriendRooms();
        JourneyMenuUI.SketchRule(panel.x+JourneyMenuUI.S4,panel.y+JourneyMenuUI.S2+JourneyMenuUI.HS+JourneyMenuUI.S1,panel.width-2*JourneyMenuUI.S4);
        if(friendRooms.Length==0)
        {
            JourneyMenuUI.Icon(new Rect(panel.x+JourneyMenuUI.S5,panel.y+152,96,96),1,JourneyMenuUI.Muted);
            JourneyMenuUI.Text(new Rect(panel.x+JourneyMenuUI.S5+96+JourneyMenuUI.S4,panel.y+136,panel.width-260,128),friendSearching?L.T("친구가 열어 둔 방을 확인하고 있어요.","Checking rooms hosted by your friends.","フレンドが開いているルームを確認しています。","正在查找好友创建的房间。") : FriendSearchMessage,JourneyMenuUI.TS,JourneyMenuUI.Muted);
        }
        for(int row=0;row<FriendsPerPage;row++)
        {
            int index=friendPage*FriendsPerPage+row;if(index>=friendRooms.Length)break;
            var room=friendRooms[index];var rect=FriendRowRect(row);
            JourneyMenuUI.BeginCard(rect,room.CanJoin);
            JourneyMenuUI.Icon(new Rect(rect.x+JourneyMenuUI.S3,rect.y+12,48,48),1,room.CanJoin?JourneyMenuUI.Ink:JourneyMenuUI.Muted);
            JourneyMenuUI.Literal(new Rect(rect.x+96,rect.y,520,rect.height),room.HostName,JourneyMenuUI.TS,room.CanJoin?JourneyMenuUI.Ink:JourneyMenuUI.Muted);
            JourneyMenuUI.Text(new Rect(rect.x+640,rect.y,320,rect.height),room.Status,JourneyMenuUI.TXS,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
            JourneyMenuUI.Literal(new Rect(rect.x+976,rect.y,120,rect.height),room.Members+" / "+room.Capacity,JourneyMenuUI.TXS,JourneyMenuUI.Ink,TextAnchor.MiddleCenter);
            var join=new Rect(rect.xMax-JourneyMenuUI.S2-200,rect.y+JourneyMenuUI.S1,200,rect.height-2*JourneyMenuUI.S1);
            JourneyMenuUI.Box(join,room.CanJoin?JourneyMenuUI.Kind.Primary:JourneyMenuUI.Kind.Disabled);
            JourneyMenuUI.Text(join,L.T("참가","Join","参加","加入"),JourneyMenuUI.TXS,room.CanJoin?JourneyMenuUI.White:JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
            JourneyMenuUI.EndCard();
            if(JourneyMenuUI.Click(rect,room.CanJoin)){JourneyMenuUI.BlockClick();JoinFriendRoom(room);}
        }
        var code=RoomCodePanelRect;JourneyMenuUI.Box(code,JourneyMenuUI.Kind.Panel);
        JourneyMenuUI.Text(new Rect(code.x+JourneyMenuUI.S4,code.y+JourneyMenuUI.S2,640,40),L.T("방 코드로 참가","Join with a room code","ルームコードで参加","通过房间代码加入"),JourneyMenuUI.TS);
        JourneyMenuUI.Field(RoomCodeRect,codeInput,L.T("방 코드","Room code","ルームコード","房间代码"));
        if(JourneyMenuUI.Button(CodeJoinRect,L.T("참가","Join","参加","加入"),RoomCodeInput.IsValid(codeInput),true))JoinRoom();
        Status();
    }
}
