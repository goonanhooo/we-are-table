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
    public static readonly Rect TableRect=new Rect(450,302,700,496);
    public static readonly Rect NextRect=new Rect(1180,821,340,54);
    public static readonly Rect LeaveRect=new Rect(80,821,280,54);
    public static readonly Rect CopyRect=new Rect(525,223,255,44),InviteRect=new Rect(800,223,275,44);
    public static Rect LegButton(int i)=>new Rect(i%2==0?80:1200,i<2?604:371,320,146);
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
                    if(Mouse.current!=null&&new Rect(100,322,1400,346).Contains(JourneyMenuUI.MousePosition))move+=mapWheel.Read(Mouse.current.scroll.ReadValue(),Time.unscaledTime);
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
    void OnGUI()
    {
        if(!InSession||OverlayBlocked||(!visible&&!InLobby))return;
        JourneyMenuUI.Begin();try{if(InLobby)DrawLobby();else DrawMini();}finally{JourneyMenuUI.End();}
    }
    void DrawLobby()
    {
        if(phoneInvites){DrawPhoneInvites();return;}
        JourneyMenuUI.Fill(new Rect(0,0,1600,900),new Color(.978f,.974f,.951f));
        bool map=Link.ChoosingMap;
        JourneyMenuUI.Text(new Rect(80,39,1050,62),map?L.T("맵 선택","Choose a checkpoint","チェックポイント選択","选择检查点"):L.T("다리 선택","Choose your legs","操作する脚を選ぼう","选择要操控的腿"),42);
        int phones=0;for(int i=0;i<4;i++)if((Link.PhoneMask&(1<<i))!=0)phones++;
        JourneyMenuUI.Text(new Rect(1150,55,370,40),L.T("PC ","PC ","PC ","电脑 ")+Link.LobbyPlayerCount+" / 4   "+L.T("폰 ","phone ","スマホ ","手机 ")+phones+" / 4",23,JourneyMenuUI.Muted,TextAnchor.MiddleRight);
        DrawSteps(map);DrawRoom();
        if(map)DrawMap();else DrawLegs();
        if(JourneyMenuUI.BackButton(LeaveRect,map?L.T("다리 선택","Choose legs","脚の選択","选择腿"):L.T("뒤로","Back","戻る","返回"),!startPending))HandleBack();
    }
    void DrawSteps(bool map)
    {
        JourneyMenuUI.Text(new Rect(570,101,190,32),L.T("1  다리 선택","1  Choose legs","1  脚の選択","1  选择腿"),21,map?JourneyMenuUI.Muted:JourneyMenuUI.Accent,TextAnchor.MiddleCenter);
        JourneyMenuUI.Line(new Vector2(779,117),new Vector2(821,117),new Color(.64f,.66f,.60f),2);
        JourneyMenuUI.Text(new Rect(840,101,190,32),L.T("2  맵 선택","2  Checkpoint","2  地点の選択","2  选择地图"),21,map?JourneyMenuUI.Accent:JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        JourneyMenuUI.Line(new Vector2(map?865:590,136),new Vector2(map?1005:740,136),JourneyMenuUI.Accent,3);
    }
    void DrawRoom()
    {
        string code=string.IsNullOrEmpty(NetSession.JoinCode)?L.T("로컬 방","Local room","ローカルルーム","本地房间"):NetSession.JoinCode;
        JourneyMenuUI.Text(new Rect(470,147,660,28),L.T("방 코드","Room code","ルームコード","房间代码"),20,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        JourneyMenuUI.Literal(new Rect(330,176,940,40),code,31,JourneyMenuUI.Ink,TextAnchor.MiddleCenter);
        if(JourneyMenuUI.Button(CopyRect,L.T("코드 복사","Copy code","コードをコピー","复制代码"),!string.IsNullOrEmpty(NetSession.JoinCode))){GUIUtility.systemCopyBuffer=NetSession.JoinCode;status=L.T("방 코드를 복사했어요","Room code copied","コードをコピーしました","已复制房间代码");statusUntil=Time.unscaledTime+2;}
        if(JourneyMenuUI.Button(InviteRect,L.T("친구 초대","Invite friends","フレンド招待","邀请好友"),NM.IsHost&&SteamLobby.InLobby))SteamLobby.OpenInviteOverlay();
        if(JourneyMenuUI.Button(new Rect(1100,223,230,44),L.T("폰 QR","Phone QR","スマホQR","手机二维码"),NM.IsHost&&PhoneControllerBridge.Instance!=null&&PhoneControllerBridge.Instance.Connected))phoneInvites=true;
        if(Time.unscaledTime<statusUntil)JourneyMenuUI.Text(new Rect(480,273,640,28),status,18,JourneyMenuUI.Accent,TextAnchor.MiddleCenter);
    }
    void DrawPhoneInvites()
    {
        JourneyMenuUI.Fill(new Rect(0,0,1600,900),new Color(.978f,.974f,.951f));
        JourneyMenuUI.Text(new Rect(100,210,1400,58),L.T("폰으로 다리 조작","Control legs with phones","スマホで脚を操作","用手机控制桌腿"),40);
        var bridge=PhoneControllerBridge.Instance;
        if(bridge!=null)bridge.DrawInvite(new Rect(100,320,1400,350));
        if(bridge!=null&&bridge.WatchCode.Length==8)
            JourneyMenuUI.Text(new Rect(100,711,1400,40),L.T("웹 화면 보기","Watch on web","ウェブで見る","网页观看")+"  "+bridge.PublicOrigin+"/watch   "+bridge.WatchCode,19,JourneyMenuUI.Muted);
        if(JourneyMenuUI.BackButton(LeaveRect,L.T("다리 선택","Choose legs","脚の選択","选择腿")))phoneInvites=false;
    }
    void DrawLegs()
    {
        int hover=-1;var mouse=JourneyMenuUI.MousePosition;
        for(int i=0;i<4;i++)if(LegButton(i).Contains(mouse)||(tablePreview.Ready&&tablePreview.LegBounds(i,TableRect).Contains(mouse)))hover=i;
        tablePreview.Draw(TableRect,Link,hover);
        for(int i=0;i<4;i++)
        {
            var r=LegButton(i);long owner=Link.OwnerOf(i);bool mine=Link.IsMine(i),open=owner==-2;Color c=LegPalette.BySlot[i];
            if(tablePreview.Ready)
            {
                var target=tablePreview.LegPoint(i,TableRect);var from=new Vector2(i%2==0?r.xMax:r.x,r.center.y);
                var bend=new Vector2(i%2==0?from.x+30:from.x-30,from.y);
                Color line=new Color(c.r*.72f,c.g*.72f,c.b*.72f,mine?.95f:.55f);
                JourneyMenuUI.Line(from,bend,line,2);JourneyMenuUI.Line(bend,target,line,2);JourneyMenuUI.Dot(target,4,c);
            }
            JourneyMenuUI.Fill(r,new Color(1,1,1,.87f));JourneyMenuUI.Fill(new Rect(r.x,r.y,5,r.height),c);
            if(owner!=-2)JourneyMenuUI.Border(r,c,mine?2.5f:1.5f);
            JourneyMenuUI.HoverText(new Rect(r.x+20,r.y+9,r.width-40,32),LegLayout.Name(i),r,23,mine||open);
            JourneyMenuUI.Literal(new Rect(r.x+20,r.y+44,r.width-40,38),LegLayout.Keys(i),30,JourneyMenuUI.Ink);
            string who=open?L.T("선택하기","Click to choose","クリックで選択","点击选择"):Link.DisplayNameOf(owner)+(mine?L.T(" · 나"," · you"," · 自分"," · 我"):"");
            JourneyMenuUI.Literal(new Rect(r.x+20,r.y+91,r.width-40,38),who,22,owner==-2?JourneyMenuUI.Muted:JourneyMenuUI.Accent);
            bool hit=JourneyMenuUI.Click(r,mine||open);
            if(!hit&&tablePreview.Ready)hit=JourneyMenuUI.Click(tablePreview.LegBounds(i,TableRect),mine||open);
            if(hit){JourneyMenuUI.BlockClick();Link.ClaimLegRpc((byte)i);}
        }
        string hint=NM.IsHost?L.T("빈 다리는 호스트가 조종해요","The host controls unclaimed legs","空席の脚はホストが操作します","空位由房主操控"):L.T("PC 다리를 선택하거나 폰으로 접속하세요","Choose a PC leg or connect by phone","PCの脚を選ぶかスマホで参加","选择电脑桌腿或用手机连接");
        JourneyMenuUI.Text(new Rect(395,823,745,48),hint,22,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        if(JourneyMenuUI.Button(NextRect,L.T("다음","Next","次へ","下一步"),NM.IsHost&&Link.CanChooseMap,true)){JourneyMenuUI.BlockClick();Link.ChooseMap();}
    }
    void DrawMap()
    {
        journey.Select(Mathf.Max(0,Link.MapIndex));journey.Draw(new Rect(100,320,1400,335),Link.CheckpointMask,NM.IsHost);
        if(NM.IsHost&&journey.Selected!=Link.MapIndex){if(Unlocked(journey.Selected))Link.SetMap(journey.Selected);else journey.Select(Mathf.Max(0,Link.MapIndex));}
        if(JourneyMenuUI.Button(new Rect(100,658,65,46),"‹",NM.IsHost))ChangePoint(-1);
        if(JourneyMenuUI.Button(new Rect(1435,658,65,46),"›",NM.IsHost))ChangePoint(1);
        JourneyMenuUI.Text(new Rect(645,658,310,40),(Link.MapIndex+1).ToString("00")+" / 16",22,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        JourneyMenuUI.Text(new Rect(230,726,220,54),L.T("난이도","Difficulty","難易度","难度"),25,JourneyMenuUI.Muted);
        for(int i=0;i<2;i++)
        {
            var mode=i==0?Difficulty.Mode.Coward:Difficulty.Mode.Normal;Rect r=new Rect(475+i*390,720,360,62);bool chosen=Link.DifficultyIndex==(int)mode;
            JourneyMenuUI.Paper(r,chosen);JourneyMenuUI.HoverText(new Rect(r.x+14,r.y,r.width-28,r.height),Difficulty.Name(mode,true),r,26,NM.IsHost,TextAnchor.MiddleCenter);
            if(JourneyMenuUI.Click(r,NM.IsHost))Link.SetDifficulty((int)mode);
        }
        if(!NM.IsHost)JourneyMenuUI.Text(new Rect(390,821,740,54),L.T("호스트가 맵과 난이도를 선택하고 시작해요","The host chooses the checkpoint and starts","ホストが地点と難易度を選んで開始します","由房主选择地图和难度并开始"),22,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        if(JourneyMenuUI.Button(NextRect,L.T("시작하기","Start","スタート","开始"),NM.IsHost&&Link.CanChooseMap&&Unlocked(Link.MapIndex)&&!startPending,true))
        {
            Link.StartGame();if(Link.Started){startPending=true;CheckpointRuntime.Launch(Link.MapIndex);}
        }
    }
    void DrawMini()
    {
        Rect r=new Rect(1240,15,345,120);JourneyMenuUI.Paper(r);string mine="";
        if(Link!=null)for(int i=0;i<4;i++)if(Link.IsMine(i))mine+=(mine.Length>0?", ":"")+LegLayout.Keys(i);
        if(mine.Length==0)mine=NM.IsHost?L.T("빈 다리 전체","Unclaimed legs","空席の脚","所有空位的腿"):L.T("선택한 다리 없음","No legs selected","脚が未選択","未选择腿");
        JourneyMenuUI.Text(new Rect(r.x+12,r.y+7,r.width-24,52),mine,22);
        if(JourneyMenuUI.BackButton(new Rect(r.x+12,r.y+70,r.width-24,37),L.T("메뉴로 나가기","Leave to menu","メニューに戻る","返回菜单")))LeaveAll();
    }
}
