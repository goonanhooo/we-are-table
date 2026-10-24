using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Home submenu: difficulty, checkpoint journey, host and room-code join.</summary>
public partial class LevelSelect : MonoBehaviour
{
    // Keep serialized/public compatibility with existing scene builders and preview components.
    public bool requireProgress;
    [System.Serializable] public class Level { public string label, scene, thumb, spawn; }
    public Level[] levels = {};
    public Level[] wipLevels = {};
    public MapMiniatures miniatures;
    public MapSelectPreview preview;
    public bool Showing { get; private set; }
    public bool Busy => busy;
    public int Step => step;
    int step;
    bool directStage, busy, loadPending, webRoute;
    string codeInput="", busyMsg="";
    readonly CheckpointJourney journey=new CheckpointJourney();
    public int SelectedCheckpoint => journey.Selected;
    public static readonly Rect BackRect=new Rect(80,780,280,JourneyMenuUI.HS);

    bool CanOpen()
    {
        var nm=Unity.Netcode.NetworkManager.Singleton;
        return nm==null||(!nm.IsHost&&!nm.IsConnectedClient);
    }
    void Show(int next,bool direct)
    {
        if(!CanOpen())return;
        if(!Showing)shownAt=0;Showing=true;step=next;directStage=direct;codeInput="";
        JourneyMenuUI.BlockClick();
        if(preview!=null)preview.HideAll();
        if(next==2)EnterFriendRooms();
        if(next==3){int latest=0;for(int i=0;i<16;i++)if((CheckpointProgress.Mask&(1u<<i))!=0)latest=i;journey.Select(latest,true);}
    }
    public void Open(){webRoute=false;PhoneControllerBridge.WebPlayActive=false;Show(0,false);}
    public void OpenMulti(){webRoute=false;PhoneControllerBridge.WebPlayActive=false;Show(0,false);}
    public void OpenStage(){webRoute=false;PhoneControllerBridge.WebPlayActive=false;Show(3,true);}
    public void GoTo(int s,bool wip=false)=>Show(Mathf.Clamp(s,0,3),false);
    public void Close(){CancelFriendSearch();Showing=false;JourneyMenuUI.BlockClick();if(preview!=null)preview.HideAll();}
    public void Back()
    {
        // An in-flight session request owns its loading screen until it resolves.
        if(!Showing||busy||loadPending)return;
        JourneyMenuUI.BlockClick();
        if(step==1)step=3;
        else if(step==3){if(directStage)Close();else step=webRoute?4:0;}
        else if(step==4){webRoute=false;step=0;}
        else if(step==2){CancelFriendSearch();step=0;}
        else Close();
    }
    void Update()
    {
        JourneyMenuUI.PollInput();
        if(!Showing||busy||PauseMenu.InputBlocked||LetterOverlay.IsOpen)return;
        if(step==1)journey.Update();
        if(step!=2)return;
        UpdateFriendRooms();
        var kb=Keyboard.current;if(kb==null)return;
        codeInput=RoomCodeInput.Read(codeInput,kb);
        if(kb.enterKey.wasPressedThisFrame&&RoomCodeInput.IsValid(codeInput))JoinRoom();
    }
    async void HostRoom()
    {
        if(busy)return;busy=true;busyMsg=L.T("방을 만들고 있어요","Creating your room","ルームを作成しています","正在创建房间");
        NetLegLink.ResetSessionCache();
        bool ok=await NetSession.HostAsync();
        if(this==null)return;busy=false;
        if(ok){PhoneControllerBridge.WebPlayActive=true;Time.timeScale=1;Close();}
    }
    async void JoinRoom()
    {
        if(busy||!RoomCodeInput.IsValid(codeInput))return;CancelFriendSearch();busy=true;busyMsg=L.T("친구의 방에 들어가는 중","Joining your friend's room","フレンドのルームに参加中","正在加入好友的房间");
        bool ok=await NetSession.JoinAsync(codeInput);
        if(this==null)return;busy=false;if(ok)Close();
    }
    void OnDisable(){CancelFriendSearch();if(preview!=null)preview.HideAll();}
    float shownAt;
    void OnGUI()
    {
        if(!Showing||PauseMenu.InputBlocked||LetterOverlay.IsOpen)return;
        if(Event.current.type==EventType.Layout&&shownAt<=0)shownAt=Time.unscaledTime;
        // 바탕은 미끄러지지 않고 한 번만 살짝 나타남, 내용은 화면이 바뀔 때마다 옆에서 들어옴.
        JourneyMenuUI.Begin();JourneyMenuUI.Backdrop(Mathf.Clamp01((Time.unscaledTime-shownAt)/.18f));JourneyMenuUI.End();
        JourneyMenuUI.Begin("levelselect",busy?"busy":"step"+step);
        try{
            if(busy){DrawBusy();return;}
            if(step==0)DrawModeSelect();else if(step==1)DrawStageSelect();else if(step==3)DrawDifficulty();else if(step==4)DrawWebLobby();else DrawJoin();
            if(JourneyMenuUI.BackButton(BackRect))Back();
        }finally{JourneyMenuUI.End();}
    }
    public static readonly Rect ActionRect=new Rect(1120,780,400,JourneyMenuUI.HS);
    void DrawBusy()
    {
        var r=new Rect(480,288,640,300);JourneyMenuUI.Box(r,JourneyMenuUI.Kind.Sheet);
        JourneyMenuUI.Icon(new Rect(r.center.x-48,r.y+JourneyMenuUI.S4,96,96),step==2?2:1);
        JourneyMenuUI.Text(new Rect(r.x+JourneyMenuUI.S4,r.y+152,r.width-2*JourneyMenuUI.S4,52),L.Text(busyMsg)+new string('.',(int)(Time.unscaledTime*2)%4),JourneyMenuUI.TL,null,TextAnchor.MiddleCenter);
        JourneyMenuUI.Text(new Rect(r.x+JourneyMenuUI.S4,r.y+212,r.width-2*JourneyMenuUI.S4,40),L.T("잠시만 기다려 주세요","Just a moment","しばらくお待ちください","请稍候"),JourneyMenuUI.TS,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
    }
    void Heading(string title,string description="")=>JourneyMenuUI.Heading(title,description);
    void Status()
    {
        if(!string.IsNullOrEmpty(NetSession.Status))JourneyMenuUI.Note(new Rect(BackRect.xMax+JourneyMenuUI.S4,BackRect.y,ActionRect.x-BackRect.xMax-2*JourneyMenuUI.S4,BackRect.height),NetSession.Status);
    }
    void DrawModeSelect()
    {
        Heading(L.T("함께하기","Play together","いっしょに遊ぶ","一起玩"),L.T("방을 만들거나 친구의 방에 들어가요.","Host a room or join a friend.","ルームを作るか、フレンドに参加します。","创建房间，或加入好友。"));
        for(int i=0;i<2;i++){
            var r=new Rect(80+i*736,208,704,440);
            string title=i==0?L.T("방 만들기","Host a room","ルームを作る","创建房间"):L.T("친구 방에 참가","Join a friend","フレンドに参加","加入好友");
            string desc=i==0?L.T("PC와 폰이 한 방에서 함께 다리를 나눠 맡아요.","PCs and phones share the legs in one room.","PCとスマホが一つのルームで脚を分担します。","电脑和手机在同一房间分担桌腿。"):L.T("Steam 친구의 방을 고르거나 방 코드를 입력해요.","Pick a Steam friend's room or type a room code.","Steamフレンドのルームを選ぶか、コードを入力します。","选择 Steam 好友的房间，或输入房间代码。");
            string action=i==0?L.T("방 만들기","Create room","作成する","创建"):L.T("방 찾기","Find rooms","ルームを探す","查找房间");
            if(JourneyMenuUI.ChoiceCard(r,i+1,title,desc,action)){if(i==0)HostRoom();else Show(2,false);}
        }
        Status();
    }
    void DrawWebLobby()
    {
        Heading(L.T("웹 다리 방","Web leg room","ウェブの脚ルーム","网页桌腿房间"),L.T("폰으로 QR을 찍어 다리를 하나씩 맡아요.","Scan the QR with a phone to take a leg.","スマホでQRを読み取り、脚を担当します。","用手机扫二维码，各自负责一条腿。"));
        var bridge=PhoneControllerBridge.Instance;
        if(bridge!=null)bridge.DrawInvite(new Rect(80,208,1440,440));
        string note=bridge==null||!bridge.Connected?L.T("연결 대기","Connecting","接続中","连接中"):
            bridge.WatchCode.Length==8?L.T("같은 화면 보기","Watch together","同じ画面を見る","观看同一画面")+"  "+bridge.WatchCode.Substring(0,4)+" "+bridge.WatchCode.Substring(4)+"   "+bridge.PublicOrigin+"/watch":"";
        JourneyMenuUI.Note(new Rect(80,672,1440,40),note);
        if(JourneyMenuUI.Button(ActionRect,L.T("맵 고르기","Choose map","マップを選ぶ","选择地图"),true,true))Show(3,false);
    }
    void DrawDifficulty()
    {
        Heading(L.T("난이도 선택","Choose difficulty","難易度選択","选择难度"),L.T("언제든 다시 고를 수 있어요.","You can change this any time.","いつでも変更できます。","随时可以更改。"));
        for(int i=0;i<2;i++){
            var mode=i==0?Difficulty.Mode.Coward:Difficulty.Mode.Normal;bool selected=Difficulty.Local==mode;
            var r=new Rect(80+i*736,208,704,440);
            if(JourneyMenuUI.ChoiceCard(r,0,Difficulty.Name(mode,false),Difficulty.Desc(mode),L.T("이 난이도로","Choose this","これにする","选择此难度"),true,selected,selected?L.T("현재 선택","Selected","選択中","已选择"):null)){Difficulty.Local=mode;step=1;JourneyMenuUI.BlockClick();}
        }
    }
    void DrawStageSelect()
    {
        Heading(L.T("어디서 시작할까요","Where to start","どこから始める？","从哪里开始"),L.T("도착한 곳에서만 시작할 수 있어요. ← → 키나 휠로 넘겨요.","You can start only from places you have reached. Use ← → or the wheel.","たどり着いた場所からだけ始められます。← → かホイールで移動。","只能从已到达的地点开始。用 ← → 或滚轮切换。"));
        uint mask=CheckpointProgress.Mask;journey.Draw(new Rect(80,192,1440,368),mask);
        if(JourneyMenuUI.ArrowButton(new Rect(80,592,88,JourneyMenuUI.HS),-1,journey.Selected>0))journey.Select(journey.Selected-1);
        if(JourneyMenuUI.ArrowButton(new Rect(1432,592,88,JourneyMenuUI.HS),1,journey.Selected<15))journey.Select(journey.Selected+1);
        bool open=(mask&(1u<<journey.Selected))!=0;
        JourneyMenuUI.Text(new Rect(200,592,1200,40),(journey.Selected+1).ToString("00")+"  "+CheckpointCatalog.Label(journey.Selected,mask),JourneyMenuUI.TM,open?JourneyMenuUI.Ink:JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        JourneyMenuUI.Text(new Rect(200,632,1200,32),(journey.Selected+1).ToString("00")+" / 16",JourneyMenuUI.TXS,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        if(JourneyMenuUI.Button(ActionRect,open?L.T("여기서 시작","Start here","ここから出発","从这里出发"):L.T("아직 도착하지 않은 곳","Not reached yet","まだたどり着いていません","尚未到达"),open&&!loadPending,true)){
            int id=journey.Selected;loadPending=true;Time.timeScale=1;Close();PhoneControllerBridge.WebPlayActive=webRoute;CheckpointRuntime.Launch(id);
        }
    }
    void DrawJoin()=>DrawFriendJoin();
}
