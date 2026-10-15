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
    public static readonly Rect BackRect=new Rect(100,790,180,52);

    bool CanOpen()
    {
        var nm=Unity.Netcode.NetworkManager.Singleton;
        return nm==null||(!nm.IsHost&&!nm.IsConnectedClient);
    }
    void Show(int next,bool direct)
    {
        if(!CanOpen())return;
        Showing=true;step=next;directStage=direct;codeInput="";
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
    void OnGUI()
    {
        if(!Showing||PauseMenu.InputBlocked||LetterOverlay.IsOpen)return;
        JourneyMenuUI.Begin();
        try{
            if(busy){JourneyMenuUI.Icon(new Rect(110,355,110,110),step==2?2:1);JourneyMenuUI.Text(new Rect(250,365,1000,65),L.Text(busyMsg)+new string('.',(int)(Time.unscaledTime*2)%4),40);JourneyMenuUI.Text(new Rect(250,435,1000,40),L.T("잠시만 기다려 주세요","Just a moment","しばらくお待ちください","请稍候"),26,JourneyMenuUI.Muted);return;}
            if(step==0)DrawModeSelect();else if(step==1)DrawStageSelect();else if(step==3)DrawDifficulty();else if(step==4)DrawWebLobby();else DrawJoin();
            if(JourneyMenuUI.BackButton(BackRect))Back();
        }finally{JourneyMenuUI.End();}
    }
    void Heading(string title,string description="")
    {
        JourneyMenuUI.Text(new Rect(100,276,1390,58),title,40);
        JourneyMenuUI.Text(new Rect(103,337,1390,40),description,25,JourneyMenuUI.Muted);
    }
    void DrawModeSelect()
    {
        Heading(L.T("함께하기","Play together","いっしょに遊ぶ","一起玩"));
        for(int i=0;i<2;i++){
            Rect r=new Rect(100+i*710,410,670,305);bool hover=r.Contains(JourneyMenuUI.MousePosition);
            JourneyMenuUI.Paper(r,false,hover);JourneyMenuUI.Icon(new Rect(r.x+28,r.y+35,82,82),i+1);
            string title=i==0?L.T("방 만들기","Host a room","ルームを作る","创建房间"):L.T("친구 방에 참가","Join a friend","フレンドに参加","加入好友");
            JourneyMenuUI.HoverText(new Rect(r.x+125,r.y+38,510,55),title,r,32);
            JourneyMenuUI.Text(new Rect(r.x+30,r.y+132,610,80),i==0?L.T("PC와 폰이 한 방에서 함께","PCs and phones in one room","PCとスマホで一緒に","电脑和手机同房间"):L.T("PC 방코드로 접속","Join with a PC room code","PCのルームコードで参加","输入电脑房间代码"),26,JourneyMenuUI.Muted);
            JourneyMenuUI.Text(new Rect(r.x+30,r.y+240,610,42),L.T("열기 →","Open →","開く →","打开 →"),28,JourneyMenuUI.Accent);
            if(JourneyMenuUI.Click(r)){if(i==0)HostRoom();else Show(2,false);}
        }
        if(!string.IsNullOrEmpty(NetSession.Status))JourneyMenuUI.Text(new Rect(310,784,1180,68),NetSession.Status,23,JourneyMenuUI.Muted);
    }
    void DrawWebLobby()
    {
        Heading(L.T("웹 다리 방","Web leg room","ウェブの脚ルーム","网页桌腿房间"));
        var bridge=PhoneControllerBridge.Instance;
        string[] names={L.T("노랑","Yellow","黄","黄"),L.T("파랑","Blue","青","蓝"),L.T("초록","Green","緑","绿"),L.T("빨강","Red","赤","红")};
        for(int i=0;i<4;i++)
        {
            Rect r=new Rect(100+i*350,385,320,325);
            JourneyMenuUI.Paper(r,false,r.Contains(JourneyMenuUI.MousePosition));
            JourneyMenuUI.Text(new Rect(r.x+20,r.y+15,280,42),(i+1).ToString("00")+" · "+names[i]+(bridge!=null&&bridge.HasPlayer(i)?" ●":""),27);
            var qr=bridge!=null?bridge.Qr(i):null;
            if(qr!=null) GUI.DrawTexture(new Rect(r.x+79,r.y+69,162,162),qr,ScaleMode.ScaleToFit);
            string code=bridge!=null?bridge.Code(i):"";
            if(code.Length==8)code=code.Substring(0,4)+" "+code.Substring(4);
            JourneyMenuUI.Text(new Rect(r.x+18,r.y+259,284,40),code,28,JourneyMenuUI.Accent,TextAnchor.MiddleCenter);
        }
        if(bridge==null||!bridge.Connected)JourneyMenuUI.Text(new Rect(100,735,800,35),L.T("연결 대기","Connecting","接続中","连接中"),22,JourneyMenuUI.Muted);
        else if(bridge.WatchCode.Length==8)
            JourneyMenuUI.Text(new Rect(100,739,1200,38),L.T("같은 화면 보기","Watch together","同じ画面を見る","观看同一画面")+"  "+bridge.WatchCode.Substring(0,4)+" "+bridge.WatchCode.Substring(4)+"   "+bridge.PublicOrigin+"/watch",21,JourneyMenuUI.Muted);
        if(JourneyMenuUI.Button(new Rect(1070,790,430,52),L.T("맵 고르기 →","Choose map →","マップを選ぶ →","选择地图 →"),true,true))Show(3,false);
    }
    void DrawDifficulty()
    {
        Heading(L.T("난이도 선택","Choose difficulty","難易度選択","选择难度"));
        for(int i=0;i<2;i++){
            var mode=i==0?Difficulty.Mode.Coward:Difficulty.Mode.Normal;
            var r=new Rect(100+i*710,420,670,270);bool selected=Difficulty.Local==mode;
            JourneyMenuUI.Paper(r,selected,r.Contains(JourneyMenuUI.MousePosition));JourneyMenuUI.Icon(new Rect(r.x+30,r.y+65,105,105),0);
            JourneyMenuUI.HoverText(new Rect(r.x+164,r.y+62,472,68),Difficulty.Name(mode,false),r,37);
            JourneyMenuUI.Text(new Rect(r.x+164,r.y+137,472,90),Difficulty.Desc(mode),26,JourneyMenuUI.Muted);
            if(selected)JourneyMenuUI.Text(new Rect(r.x+20,r.y+11,620,35),L.T("현재 선택","Selected","選択中","已选择"),21,JourneyMenuUI.Accent,TextAnchor.MiddleRight);
            // The selection badge persists; the underline exists only under the pointer.
            if(JourneyMenuUI.Click(r)){Difficulty.Local=mode;step=1;}
        }
    }
    void DrawStageSelect()
    {
        Heading(L.T("맵 선택","Select checkpoint","チェックポイント選択","选择检查点"));
        uint mask=CheckpointProgress.Mask;journey.Draw(new Rect(100,387,1400,335),mask);
        if(JourneyMenuUI.Button(new Rect(100,724,65,48),"‹",journey.Selected>0))journey.Select(journey.Selected-1);
        if(JourneyMenuUI.Button(new Rect(1435,724,65,48),"›",journey.Selected<15))journey.Select(journey.Selected+1);
        JourneyMenuUI.Text(new Rect(650,728,300,38),(journey.Selected+1).ToString("00")+" / 16",23,JourneyMenuUI.Muted,TextAnchor.MiddleCenter);
        bool open=(mask&(1u<<journey.Selected))!=0;
        if(JourneyMenuUI.Button(new Rect(1070,790,430,52),open?L.T("여기서 시작 →","Start here →","ここから出発 →","从这里出发 →"):L.T("아직 도착하지 않은 곳","Not reached yet","まだたどり着いていません","尚未到达"),open&&!loadPending,true)){
            int id=journey.Selected;loadPending=true;Time.timeScale=1;Close();PhoneControllerBridge.WebPlayActive=webRoute;CheckpointRuntime.Launch(id);
        }
    }
    void DrawJoin()=>DrawFriendJoin();
}
