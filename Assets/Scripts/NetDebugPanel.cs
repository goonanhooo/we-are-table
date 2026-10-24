using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A shared lobby: choose real table legs, then choose the journey's starting point.</summary>
public class NetDebugPanel : MonoBehaviour
{
    bool visible=true,startPending,wasMap,phoneInvites;string status="";float statusUntil;
    readonly CheckpointJourney journey=new();readonly JourneyWheel mapWheel=new();
    readonly LobbyTablePreview tablePreview=new();
    LevelSelect select;NetLegLink cachedLink;
    NetworkManager NM=>NetworkManager.Singleton;
    NetLegLink Link {get{if(cachedLink==null)cachedLink=FindAnyObjectByType<NetLegLink>();return cachedLink;}}
    bool InSession=>NM!=null&&(NM.IsHost||NM.IsConnectedClient);
    bool InLobby=>InSession&&Link!=null&&!Link.Started;
    public static bool LobbyOpen {get;private set;}
    public bool MapPage=>InLobby&&Link.ChoosingMap;
    public bool TablePreviewReady=>tablePreview.Ready;
    public Vector2 PreviewLegPoint(int slot)=>tablePreview.LegPoint(slot,TableRect);
    // 캔버스 단위(지우개 대소동 규칙 ×2): 방 정보 줄 → 내용 → 아래 줄(뒤로 · 안내 · 다음).
    public static readonly Rect RoomRect=new Rect(80,168,1440,104);
    public static readonly Rect TableRect=new Rect(448,296,704,464);
    public static readonly Rect NextRect=new Rect(1120,780,400,JourneyMenuUI.HS);
    public static readonly Rect LeaveRect=new Rect(80,780,280,JourneyMenuUI.HS);
    public static readonly Rect CopyRect=new Rect(800,184,224,JourneyMenuUI.HS),InviteRect=new Rect(1040,184,224,JourneyMenuUI.HS),QrRect=new Rect(1280,184,224,JourneyMenuUI.HS);
    public static Rect LegButton(int i)=>new Rect(i%2==0?80:1184,i<2?552:296,336,208);
    bool OverlayBlocked {get{if(select==null)select=FindAnyObjectByType<LevelSelect>();return PauseMenu.InputBlocked||LetterOverlay.IsOpen||(select!=null&&select.Showing);}}
    void Start(){if(NM!=null)NM.OnClientDisconnectCallback+=OnDisconnected;}
    void OnDestroy(){LobbyOpen=false;tablePreview.Dispose();if(NM!=null)NM.OnClientDisconnectCallback-=OnDisconnected;}
    void OnDisable(){LobbyOpen=false;phoneInvites=false;tablePreview.SetVisible(false);}
    void OnDisconnected(ulong id)
    {
        if(NM==null||NM.IsServer||id!=NM.LocalClientId)return;
        Difficulty.NetOverride=-1;if(!NetScene.IntentionalLeave)NetSessionRecovery.Begin();
    }
    void Update()
    {
        JourneyMenuUI.PollInput();LobbyOpen=InLobby;
        bool legs=InLobby&&!Link.ChoosingMap&&!OverlayBlocked;
        if(legs)tablePreview.Initialize(Link.transform);
        tablePreview.SetVisible(legs);
        if(!InSession)return;
        if(Link!=null&&Link.Started)startPending=false;
        bool map=MapPage;
        if(map)
        {
            if(!wasMap)journey.Select(Mathf.Max(0,Link.MapIndex),true);
            if(!OverlayBlocked)
            {
                if(NM.IsHost)
                {
                    int move=0;var kb=Keyboard.current;if(kb!=null){if(kb.leftArrowKey.wasPressedThisFrame)move--;if(kb.rightArrowKey.wasPressedThisFrame)move++;}
                    if(Mouse.current!=null&&MapViewport.Contains(JourneyMenuUI.MousePosition))move+=mapWheel.Read(Mouse.current.scroll.ReadValue(),Time.unscaledTime);
                    if(move!=0)ChangePoint(move);
                }
                journey.Select(Mathf.Max(0,Link.MapIndex));journey.Update(false);
            }
        }
        else mapWheel.Reset();wasMap=map;
        if(Keyboard.current!=null&&Keyboard.current.f9Key.wasPressedThisFrame)visible=!visible;
    }
    public bool HandleBack()
    {
        if(!InLobby)return false;
        if(!OverlayBlocked&&!startPending){JourneyMenuUI.BlockClick();if(Link.ChoosingMap)Link.ReturnToLegsRpc();else LeaveAll();}
        return true;
    }
    void LeaveAll()
    {
        if(startPending)return;LobbyOpen=false;JourneyMenuUI.BlockClick();NetLegLink.ResetSessionCache();Time.timeScale=1;NetScene.LeaveToLocal("StartScreen");
    }
    bool Unlocked(int id)=>CheckpointCatalog.Valid(id)&&(Link.CheckpointMask&(1u<<id))!=0;
    void ChangePoint(int direction)
    {
        int next=Link.MapIndex;for(int n=0;n<16;n++){next=(next+(direction>0?1:-1)+16)%16;if(Unlocked(next)){Link.SetMap(next);return;}}
    }
    static readonly Rect MapViewport=new Rect(80,288,1440,344);
    void OnGUI()
    {
        if(!InSession||OverlayBlocked||(!visible&&!InLobby))return;
        if(InLobby){JourneyMenuUI.Begin();JourneyMenuUI.Backdrop();JourneyMenuUI.End();}
        JourneyMenuUI.Begin("lobby",!InLobby?"mini":phoneInvites?"phone":Link.ChoosingMap?"map":"legs");
        try{if(InLobby)DrawLobby();else DrawMini();}finally{JourneyMenuUI.End();}
    }
    void DrawLobby()
    {
        if(phoneInvites){DrawPhoneInvites();return;}
        bool map=Link.ChoosingMap;
        JourneyMenuUI.Heading(map?L.T("맵 선택","Choose a checkpoint","チェックポイント選択","选择检查点"):L.T("다리 선택","Choose your legs","操作する脚を選ぼう","选择要操控的腿"),"",64);
        DrawSteps(map);DrawRoom();
        if(map)DrawMap();else DrawLegs();
        if(JourneyMenuUI.BackButton(LeaveRect,map?L.T("다리 선택","Choose legs","脚の選択","选择腿"):L.T("나가기","Leave","退出","离开"),!startPending))HandleBack();
    }
    void DrawSteps(bool map)
    {
        // 오른쪽 위 단계 표시: 지금 단계 = 잉크 글씨 + 손그림 밑줄, 다른 단계 = 회색
        var a=new Rect(1120,56,184,40);var b=new Rect(1336,56,184,40);
        JourneyMenuUI.Text(a,L.T("1  다리 선택","1  Legs","1  脚の選択","1  选择腿"),JourneyMenuUI.TXS,map?JourneyMenuUI.Muted:JourneyMenuUI.Ink,TextAnchor.MiddleCenter);
        JourneyMenuUI.Text(b,L.T("2  맵 선택","2  Checkpoint","2  地点の選択","2  选择地图"),JourneyMenuUI.TXS,map?JourneyMenuUI.Ink:JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        JourneyMenuUI.Line(new Vector2(a.xMax+JourneyMenuUI.S1,a.center.y),new Vector2(b.x-JourneyMenuUI.S1,b.center.y),JourneyMenuUI.LineSoft,2);
        var on=map?b:a;JourneyMenuUI.SketchRule(on.x+JourneyMenuUI.S2,on.yMax+JourneyMenuUI.S1,on.width-2*JourneyMenuUI.S2,JourneyMenuUI.Ink,true);
    }
    void DrawRoom()
    {
        var r=RoomRect;JourneyMenuUI.Box(r,JourneyMenuUI.Kind.Panel);
        string code=string.IsNullOrEmpty(NetSession.JoinCode)?L.T("로컬 방","Local room","ローカルルーム","本地房间"):NetSession.JoinCode;
        JourneyMenuUI.Text(new Rect(r.x+JourneyMenuUI.S4,r.y+JourneyMenuUI.S2,320,32),L.T("방 코드","Room code","ルームコード","房间代码"),JourneyMenuUI.TXS,JourneyMenuUI.Muted);
        JourneyMenuUI.Literal(new Rect(r.x+JourneyMenuUI.S4,r.y+48,352,44),code,JourneyMenuUI.TM,JourneyMenuUI.Ink);   // 긴 스팀 로비 번호는 글씨만 작아짐
        int phones=0;for(int i=0;i<4;i++)if((Link.PhoneMask&(1<<i))!=0)phones++;
        string info=Time.unscaledTime<statusUntil?status:L.T("PC ","PC ","PC ","电脑 ")+Link.LobbyPlayerCount+"/4    "+L.T("폰 ","Phone ","スマホ ","手机 ")+phones+"/4";
        JourneyMenuUI.Literal(new Rect(r.x+400,r.y,CopyRect.x-r.x-400-JourneyMenuUI.S4,r.height),info,JourneyMenuUI.TXS,JourneyMenuUI.Muted,TextAnchor.MiddleRight);   // 한 줄(넘치면 글씨만 작아짐)
        if(JourneyMenuUI.Button(CopyRect,L.T("코드 복사","Copy code","コードをコピー","复制代码"),!string.IsNullOrEmpty(NetSession.JoinCode))){GUIUtility.systemCopyBuffer=NetSession.JoinCode;status=L.T("방 코드를 복사했어요","Room code copied","コードをコピーしました","已复制房间代码");statusUntil=Time.unscaledTime+2;}
        if(JourneyMenuUI.Button(InviteRect,L.T("친구 초대","Invite friends","フレンド招待","邀请好友"),NM.IsHost&&SteamLobby.InLobby))SteamLobby.OpenInviteOverlay();
        if(JourneyMenuUI.Button(QrRect,L.T("폰 QR","Phone QR","スマホQR","手机二维码"),NM.IsHost&&PhoneControllerBridge.Instance!=null&&PhoneControllerBridge.Instance.Connected))phoneInvites=true;
    }
    void DrawPhoneInvites()
    {
        JourneyMenuUI.Heading(L.T("폰으로 다리 조작","Control legs with phones","スマホで脚を操作","用手机控制桌腿"));
        var bridge=PhoneControllerBridge.Instance;
        if(bridge!=null)bridge.DrawInvite(new Rect(80,192,1440,456));
        if(bridge!=null&&bridge.WatchCode.Length==8)
            JourneyMenuUI.Note(new Rect(80,672,1440,40),L.T("웹 화면 보기","Watch on web","ウェブで見る","网页观看")+"   "+bridge.PublicOrigin+"/watch   "+bridge.WatchCode);
        if(JourneyMenuUI.BackButton(LeaveRect,L.T("다리 선택","Choose legs","脚の選択","选择腿")))phoneInvites=false;
    }
    void DrawLegs()
    {
        int hover=-1;var mouse=JourneyMenuUI.MousePosition;
        for(int i=0;i<4;i++)if(LegButton(i).Contains(mouse)||(tablePreview.Ready&&tablePreview.LegBounds(i,TableRect).Contains(mouse)))hover=i;
        tablePreview.Draw(TableRect,Link,hover);
        for(int i=0;i<4;i++)
        {
            var r=LegButton(i);long owner=Link.OwnerOf(i);bool mine=Link.IsMine(i),open=owner==-2,can=mine||open;Color c=LegPalette.BySlot[i];
            if(tablePreview.Ready)
            {
                // 카드 → 미리보기 다리: 회색 선(내 다리 = 잉크), 끝점만 다리 색
                var target=tablePreview.LegPoint(i,TableRect);var from=new Vector2(i%2==0?r.xMax:r.x,r.center.y);
                var bend=new Vector2(i%2==0?from.x+JourneyMenuUI.S4:from.x-JourneyMenuUI.S4,from.y);
                Color line=mine?JourneyMenuUI.Ink:JourneyMenuUI.LineSoft;
                JourneyMenuUI.Line(from,bend,line,2);JourneyMenuUI.Line(bend,target,line,2);JourneyMenuUI.Dot(target,5,c);
            }
            JourneyMenuUI.BeginCard(r,can,mine);
            DrawLegCard(r,i,owner,mine,open,can);
            JourneyMenuUI.EndCard();
            bool hit=JourneyMenuUI.Click(r,can);
            if(!hit&&tablePreview.Ready)hit=JourneyMenuUI.Click(tablePreview.LegBounds(i,TableRect),can);
            if(hit){JourneyMenuUI.BlockClick();Link.ClaimLegRpc((byte)i);}
        }
        if(JourneyMenuUI.Button(NextRect,L.T("다음","Next","次へ","下一步"),NM.IsHost&&Link.CanChooseMap,true)){JourneyMenuUI.BlockClick();Link.ChooseMap();}
    }
    /// <summary>다리 카드 = [머리 줄: 다리 색 점 + 다리 이름 | 맡은 사람 칸] + [자판 판: 키캡 위 1 + 아래 3].</summary>
    void DrawLegCard(Rect r,int i,long owner,bool mine,bool open,bool can)
    {
        const float S1=JourneyMenuUI.S1,S2=JourneyMenuUI.S2,S3=JourneyMenuUI.S3;
        var head=new Rect(r.x+S3,r.y+S2,r.width-2*S3,40);
        // 맡은 사람 칸: 내 다리 = 검정, 남의 다리 = 흰 칸, 빈 자리 = 연한 칸
        string who=open?L.T("빈 자리","Free","空き","空位"):mine?L.T("나","Me","自分","我"):Link.DisplayNameOf(owner);
        float bw=Mathf.Clamp(JourneyMenuUI.TextWidth(who,JourneyMenuUI.TXS)+2*S2,88,150);
        var badge=new Rect(head.xMax-bw,head.y,bw,head.height);
        JourneyMenuUI.Box(badge,mine?JourneyMenuUI.Kind.Primary:open?JourneyMenuUI.Kind.Soft:JourneyMenuUI.Kind.Card);
        JourneyMenuUI.Literal(badge,who,JourneyMenuUI.TXS,mine?JourneyMenuUI.White:open?JourneyMenuUI.Muted:JourneyMenuUI.Ink,TextAnchor.MiddleCenter);
        JourneyMenuUI.Dot(new Vector2(head.x+8,head.center.y),8,LegPalette.BySlot[i]);   // 다리 색 = 어느 다리인지
        JourneyMenuUI.Text(new Rect(head.x+16+S2,head.y,badge.x-head.x-16-S2-S1,head.height),L.Title(LegLayout.Name(i)),JourneyMenuUI.TXS,can?JourneyMenuUI.Ink:JourneyMenuUI.Muted);
        // 자판 판: 연한 상자 안에 키캡
        var plate=new Rect(r.x+S2,head.yMax+S2,r.width-2*S2,r.yMax-S2-(head.yMax+S2));
        JourneyMenuUI.Box(plate,JourneyMenuUI.Kind.Soft);
        JourneyMenuUI.KeyCluster(new Rect(plate.x,plate.y+S1,plate.width,plate.height-2*S1),i,can);
    }
    void DrawMap()
    {
        journey.Select(Mathf.Max(0,Link.MapIndex));journey.Draw(MapViewport,Link.CheckpointMask,NM.IsHost);
        if(NM.IsHost&&journey.Selected!=Link.MapIndex){if(Unlocked(journey.Selected))Link.SetMap(journey.Selected);else journey.Select(Mathf.Max(0,Link.MapIndex));}
        float ry=MapViewport.yMax+JourneyMenuUI.S2;
        if(JourneyMenuUI.ArrowButton(new Rect(80,ry,88,JourneyMenuUI.HS),-1,NM.IsHost))ChangePoint(-1);
        if(JourneyMenuUI.ArrowButton(new Rect(1432,ry,88,JourneyMenuUI.HS),1,NM.IsHost))ChangePoint(1);
        int id=Mathf.Max(0,Link.MapIndex);
        JourneyMenuUI.Text(new Rect(200,ry,1200,JourneyMenuUI.HS),(id+1).ToString("00")+" / 16",JourneyMenuUI.TXS,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        // 아래 줄 가운데: 난이도 두 칸(호스트만 바꿈)
        var area=new Rect(LeaveRect.xMax+JourneyMenuUI.S4,LeaveRect.y,NextRect.x-LeaveRect.xMax-2*JourneyMenuUI.S4,LeaveRect.height);
        JourneyMenuUI.Note(new Rect(area.x,area.y-40,area.width,32),L.T("난이도","Difficulty","難易度","难度"));
        float w=(area.width-JourneyMenuUI.S2)*.5f;
        for(int i=0;i<2;i++)
        {
            var mode=i==0?Difficulty.Mode.Coward:Difficulty.Mode.Normal;var r=new Rect(area.x+i*(w+JourneyMenuUI.S2),area.y,w,area.height);bool chosen=Link.DifficultyIndex==(int)mode;
            JourneyMenuUI.BeginCard(r,NM.IsHost||chosen,chosen);
            JourneyMenuUI.Text(new Rect(r.x+JourneyMenuUI.S2,r.y,r.width-2*JourneyMenuUI.S2,r.height),Difficulty.Name(mode,true),JourneyMenuUI.TXS,NM.IsHost||chosen?JourneyMenuUI.Ink:JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
            JourneyMenuUI.EndCard();
            if(JourneyMenuUI.Click(r,NM.IsHost))Link.SetDifficulty((int)mode);
        }
        if(JourneyMenuUI.Button(NextRect,L.T("시작하기","Start","スタート","开始"),NM.IsHost&&Link.CanChooseMap&&Unlocked(Link.MapIndex)&&!startPending,true))
        {
            Link.StartGame();if(Link.Started){startPending=true;CheckpointRuntime.Launch(Link.MapIndex);}
        }
    }
    void DrawMini()
    {
        // 게임 중 오른쪽 위 작은 창: 내 다리 + 메뉴로 나가기
        var r=new Rect(1184,16,400,152);JourneyMenuUI.Box(r,JourneyMenuUI.Kind.Panel);string mine="";
        if(Link!=null)for(int i=0;i<4;i++)if(Link.IsMine(i))mine+=(mine.Length>0?", ":"")+LegLayout.Keys(i);
        if(mine.Length==0)mine=NM.IsHost?L.T("빈 다리 전체","Unclaimed legs","空席の脚","所有空位的腿"):L.T("선택한 다리 없음","No legs selected","脚が未選択","未选择腿");
        JourneyMenuUI.Text(new Rect(r.x+JourneyMenuUI.S2,r.y+JourneyMenuUI.S2,r.width-2*JourneyMenuUI.S2,40),mine,JourneyMenuUI.TXS);
        if(JourneyMenuUI.BackButton(new Rect(r.x+JourneyMenuUI.S2,r.y+64,r.width-2*JourneyMenuUI.S2,JourneyMenuUI.HS),L.T("메뉴로 나가기","Leave to menu","メニューに戻る","返回菜单")))LeaveAll();
    }
}
