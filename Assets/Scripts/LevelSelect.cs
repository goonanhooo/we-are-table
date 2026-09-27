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
    bool directStage, busy, loadPending;
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
    public void Open()=>Show(0,false);
    public void OpenMulti()=>Show(0,false);
    public void OpenStage()=>Show(3,true);
    public void GoTo(int s,bool wip=false)=>Show(Mathf.Clamp(s,0,3),false);
    public void Close(){CancelFriendSearch();Showing=false;JourneyMenuUI.BlockClick();if(preview!=null)preview.HideAll();}
    public void Back()
    {
        // An in-flight session request owns its loading screen until it resolves.
        if(!Showing||busy||loadPending)return;
        JourneyMenuUI.BlockClick();
        if(step==1)step=3;
        else if(step==3){if(directStage)Close();else step=0;}
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
        if(ok){Time.timeScale=1;Close();}
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
            if(step==0)DrawModeSelect();else if(step==1)DrawStageSelect();else if(step==3)DrawDifficulty();else DrawJoin();
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
            JourneyMenuUI.Paper(r,false,hover);JourneyMenuUI.Icon(new Rect(r.x+38,r.y+35,110,110),i+1);
            JourneyMenuUI.HoverText(new Rect(r.x+172,r.y+34,460,58),i==0?L.T("방 만들기","Host a room","ルームを作る","创建房间"):L.T("친구 방에 참가","Join a friend","フレンドに参加","加入好友"),r,37);
            JourneyMenuUI.Text(new Rect(r.x+172,r.y+100,452,88),i==0?L.T("친구를 초대하고\n다리와 출발 지점을 골라요.","Invite friends, then choose\nyour legs and starting point.","フレンドを招待して、\n脚とスタート地点を選ぼう。","邀请朋友，选择各自的腿\n和旅程的起点。"):L.T("Steam 친구의 방을 고르거나\n방 코드로 참가해요.","Find a Steam friend’s room\nor join with a room code.","Steamフレンドのルームを選ぶか、\nコードで参加しよう。","选择 Steam 好友的房间，\n或通过房间代码加入。"),27,JourneyMenuUI.Muted);
            JourneyMenuUI.Text(new Rect(r.x+38,r.y+227,580,46),i==0?L.T("호스트로 시작 →","Create room →","ホストになる →","成为房主 →"):L.T("친구 방 찾기 →","Find a friend’s room →","フレンドのルームを探す →","查找好友房间 →"),28,JourneyMenuUI.Accent);
            if(JourneyMenuUI.Click(r)){if(i==0)HostRoom();else Show(2,false);}
        }
        if(!string.IsNullOrEmpty(NetSession.Status))JourneyMenuUI.Text(new Rect(310,784,1180,68),NetSession.Status,23,JourneyMenuUI.Muted);
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
            int id=journey.Selected;loadPending=true;Time.timeScale=1;Close();CheckpointRuntime.Launch(id);
        }
    }
    void DrawJoin()=>DrawFriendJoin();
}
