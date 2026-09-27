using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>Milestones save earned powers and world state; no scene-based ability grants.</summary>
[DefaultExecutionOrder(200)]
public class CheckpointRuntime : MonoBehaviour
{
    public static CheckpointRuntime Instance {get;private set;}
    public static CheckpointProgress.Snapshot Loading {get;private set;}
    public static int EarnedPowers {get;private set;}
    static int selectedPower, carriedPowers;
    public static bool Cheated {get;private set;}
    public static string LoadingJson=>Loading!=null?JsonUtility.ToJson(Loading):"";
    public static bool Authority=>NetworkManager.Singleton==null||!NetworkManager.Singleton.IsListening||NetworkManager.Singleton.IsServer;
    public static bool WillRestore(int from)=>Loading!=null&&Loading.id>=from&&CheckpointCatalog.Matches(Loading.id,SceneManager.GetActiveScene().name);
    ArticulationBody body; ArtSceneReset reset;
    JungleStage jungle; GiraffeGate gate; SnowmanCutscene snowman; RailRide rail; RocketLaunchCutscene rocket; Transform home;
    uint reached; CheckpointProgress.Snapshot current;
    float toastUntil; bool ready; float checkAt;
    public string ReachedJson {get;private set;}
    public int CurrentId=>current!=null?current.id:-1;
    public static bool Launch(int id)
    {
        if(!Authority||!CheckpointProgress.IsUnlocked(id))return false;
        var point=CheckpointProgress.Get(id);if(point==null)return false;
        Cheated=CheatMode.Active;EarnedPowers=point.powers;Loading=point;
        selectedPower=point.activePower;carriedPowers=0;
        Time.timeScale=1;NetScene.Load(point.scene,"checkpoint:"+id);return true;
    }
    public static void BeforeLoad(string scene,string spawn)
    {
        if(!Authority)return;
        if(spawn!=null&&spawn.StartsWith("checkpoint:"))return;
        if(Instance!=null)
        {
            selectedPower=Instance.ReadActivePower();carriedPowers=Instance.ReadPowers();
            if(!Cheated)EarnedPowers|=carriedPowers;
        }
        Loading=null;
        if(scene=="StartScreen"){EarnedPowers=0;Cheated=false;selectedPower=carriedPowers=0;}
    }
    public static void ForgetRun(){Loading=null;EarnedPowers=0;Cheated=false;selectedPower=carriedPowers=0;}
    public static void ReloadCurrent()
    {
        if(!Authority)return;
        if(Instance!=null&&Instance.current!=null)
        {
            Loading=JsonUtility.FromJson<CheckpointProgress.Snapshot>(JsonUtility.ToJson(Instance.current));
            Loading.activePower=Instance.ReadActivePower();EarnedPowers=Loading.powers;
            NetScene.Load(Loading.scene,"checkpoint:"+Loading.id);
        }
        else NetScene.Load(SceneManager.GetActiveScene().name);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Instance=null;Loading=null;EarnedPowers=0;Cheated=false;selectedPower=carriedPowers=0;SceneManager.sceneLoaded-=Loaded;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        string n=scene.name;
        if(n!="Hallway"&&n!="Jungle"&&n!="IceCavern"&&n!="Space"&&n!="Moon"&&n!="Moon_CozyKitchen")return;
        var go=new GameObject("Story checkpoints");SceneManager.MoveGameObjectToScene(go,scene);go.AddComponent<CheckpointRuntime>();
    }
    void Awake(){Instance=this;}
    System.Collections.IEnumerator Start()
    {
        reset=FindAnyObjectByType<ArtSceneReset>();
        if(reset!=null)foreach(var ab in reset.GetComponentsInChildren<ArticulationBody>(true))if(ab.isRoot){body=ab;break;}
        jungle=FindAnyObjectByType<JungleStage>();gate=FindAnyObjectByType<GiraffeGate>();snowman=FindAnyObjectByType<SnowmanCutscene>();rail=FindAnyObjectByType<RailRide>();rocket=FindAnyObjectByType<RocketLaunchCutscene>();
        var house=GameObject.Find("House_CozyKitchen")??GameObject.Find("House");home=house!=null?house.transform:null;
        if(rail!=null&&rail.carts!=null)foreach(var cart in rail.carts)if(cart!=null&&cart.GetComponent<CheckpointCartContact>()==null)cart.gameObject.AddComponent<CheckpointCartContact>();
        ready=body!=null;if(!ready)yield break;
        if(Authority)
        {
            if(Loading!=null&&CheckpointCatalog.Matches(Loading.id,gameObject.scene.name))
            {
                // Articulation roots need their first physics step before a restored teleport can stick.
                yield return new WaitForFixedUpdate();
                Apply(Loading,true);
            }
            else
            {
                ApplyPowers(EarnedPowers | (Cheated ? carriedPowers : 0),selectedPower);
                int entry=CheckpointCatalog.SceneEntry(gameObject.scene.name);if(entry>=0)Reach(entry);
            }
        }
    }
    public void Apply(CheckpointProgress.Snapshot point,bool move)
    {
        if(!ready||!CheckpointProgress.Valid(point)||!CheckpointCatalog.Matches(point.id,gameObject.scene.name))return;
        current=point;Loading=point;EarnedPowers=point.powers;reached|=1u<<point.id;
        if(point.id>=2&&gameObject.scene.name=="Jungle")
        {
            FindAnyObjectByType<CageButton>()?.RestoreCompleted();FindAnyObjectByType<GiraffeCutscene>()?.RestoreCompleted();
            if((point.world&1)!=0)gate?.RestoreCompleted();
        }
        if(point.id>=6&&gameObject.scene.name=="IceCavern")
        {
            FindAnyObjectByType<FrameHallClimb>()?.RestoreCompleted();
            if(point.id>=7)FindAnyObjectByType<CavernChairCutscene>()?.RestoreCompleted();
            if(point.id>=9)snowman?.RestoreCompleted();
            if(rail!=null&&point.carts!=null)for(int i=0;i<point.carts.Length;i++)rail.DebugSetU(i,point.carts[i]);
        }
        if(point.id>=11&&gameObject.scene.name=="Space")FindAnyObjectByType<SpaceIntroCutscene>()?.RestoreCompleted();
        if(point.id>=14)FindAnyObjectByType<MoonStage>()?.CancelArrival();
        ApplyPowers(point.powers,point.activePower);
        // Guest powers/pose are network-authoritative; local restoration is only presentation.
        if(move&&Authority&&point.hasPosition&&!CheckpointCatalog.All[point.id].SceneStart)
        {
            foreach(var tm in body.transform.root.GetComponentsInChildren<TennisMode>(true))tm.ReleaseAll();
            foreach(var leg in body.transform.root.GetComponentsInChildren<ArtLegController>(true)){leg.ResetState();leg.HoldVertical(.5f);}
            var giraffe=body.transform.root.GetComponentInChildren<GiraffeMode>();if(giraffe!=null)giraffe.ResetLegs();
            body.immovable=false;
            if(point.scene=="Space")body.TeleportRoot(point.position,Quaternion.Euler(0,point.yaw,0));
            else reset.ResetPoseAtXZ(point.position.x,point.position.z,point.position.y+.5f);
            body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;reset.SetSpawn(body.transform.position);
            if(jungle!=null)jungle.RestoreCheckpointBank(body.transform.position);
        }
        var tutorial=body.transform.root.GetComponentInChildren<KeyTutorial>();if(tutorial!=null)tutorial.RestoreKnownPowers(point.powers);
        if(point.id==15&&Authority)
        {
            var kitchen=FindAnyObjectByType<MoonKitchenEnding>();
            if(kitchen!=null)kitchen.Replay();else FindAnyObjectByType<MoonHomeEnding>()?.Play();
        }
    }
    public static void ReceiveRestore(string json)
    {
        if(string.IsNullOrEmpty(json)||Authority)return;
        var point=JsonUtility.FromJson<CheckpointProgress.Snapshot>(json);
        if(Instance!=null)Instance.Apply(point,false);
    }
    int ReadPowers()
    {
        if(body==null)return 0;var t=body.transform.root;int mask=0;
        var g=t.GetComponentInChildren<GiraffeMode>();if(g!=null&&!g.locked)mask|=1;
        var b=t.GetComponentInChildren<TennisMode>();if(b!=null&&b.unlocked)mask|=2;
        var i=t.GetComponentInChildren<IcePower>();if(i!=null&&i.unlocked)mask|=4;
        var s=t.GetComponentInChildren<SquidMode>();if(s!=null&&s.unlocked&&!s.thrustLocked)mask|=8;
        return mask;
    }
    public int ReadActivePower()
    {
        if(body==null)return 0;var t=body.transform.root;
        var s=t.GetComponentInChildren<SquidMode>();if(s!=null&&s.LookOn)return 4;
        var i=t.GetComponentInChildren<IcePower>();if(i!=null&&i.active)return 3;
        var b=t.GetComponentInChildren<TennisMode>();if(b!=null&&b.active)return 2;
        var g=t.GetComponentInChildren<GiraffeMode>();return g!=null&&g.active?1:0;
    }
    void ApplyPowers(int mask,int selected)
    {
        var t=body.transform.root;var g=t.GetComponentInChildren<GiraffeMode>();var b=t.GetComponentInChildren<TennisMode>();var i=t.GetComponentInChildren<IcePower>();var s=t.GetComponentInChildren<SquidMode>();
        if(Authority)
        {
            if(g!=null){g.locked=(mask&1)==0;g.active=false;}
            if(b!=null){b.unlocked=(mask&2)!=0;b.SetActive(false);}
            if(i!=null){i.unlocked=(mask&4)!=0;i.SetActive(false);}
            if(s!=null){s.unlocked=(mask&8)!=0;s.thrustLocked=!s.unlocked;s.active=false;}
            // Restore one selected ability only when it has actually been earned.
            if(selected>=1&&selected<=4&&(mask&(1<<(selected-1)))!=0)
            {
                if(selected==1&&g!=null){g.active=true;AbilitySelection.Select(g);}
                if(selected==2&&b!=null)b.SetActive(true);
                if(selected==3&&i!=null)i.SetActive(true);
                if(selected==4&&s!=null){s.active=true;AbilitySelection.Select(s);}
            }
        }
        var hud=t.GetComponentInChildren<AbilityHud>();if(hud!=null)hud.RestoreEarnedPowers(mask);
        GiraffeMode.Unlocked=(mask&1)!=0;TennisMode.Unlocked=(mask&2)!=0;SquidMode.Unlocked=(mask&8)!=0;
    }
    void Update()
    {
        if(!ready||!Authority)return;if(CheatMode.Active)Cheated=true;
        if(Cheated||Time.timeScale<=0||Time.time<checkAt)return;checkAt=Time.time+.2f;
        EarnedPowers|=ReadPowers();
        var p=body.transform.position;
        if(jungle!=null&&jungle.island!=null)
        {
            if(jungle.CanSaveAcrossLava)Reach(3);
            if(jungle.CanSaveAcrossLava&&gate!=null&&gate.buttons!=null)foreach(var b in gate.buttons)if(b!=null&&Vector3.Distance(p,b.transform.position)<10&&Grounded()){Reach(4);break;}
        }
        if(rail!=null&&rail.PlayerCart!=null)Reach(7);
        if(snowman!=null&&snowman.body!=null&&p.z> -17f&&p.z<35f&&Mathf.Abs(p.x-snowman.body.position.x)<18&&Mathf.Abs(p.y-snowman.body.position.y)<12&&Grounded())Reach(8);
        if(rocket!=null&&Mathf.Abs(p.y-rocket.transform.position.y)<5&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(rocket.transform.position.x,rocket.transform.position.z))<23&&Grounded())Reach(10);
        if(home!=null&&Vector3.Distance(p,home.position)<13&&Grounded())Reach(14);
    }
    bool Grounded()
    {
        if(body.immovable)return false;
        foreach(var hit in Physics.RaycastAll(body.transform.position+Vector3.up*.2f,Vector3.down,3f,~0,QueryTriggerInteraction.Ignore))
            if(hit.collider.transform.root!=body.transform.root&&hit.normal.y>.45f)return true;
        // Extended giraffe legs can support the table more than three metres above the floor.
        foreach(var col in body.transform.root.GetComponentsInChildren<Collider>())
        {
            if(!col.enabled||col.isTrigger||col.GetComponentInParent<ArtLegController>()==null)continue;
            var foot=new Vector3(col.bounds.center.x,col.bounds.min.y+.2f,col.bounds.center.z);
            foreach(var hit in Physics.RaycastAll(foot,Vector3.down,.5f,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider.transform.root!=body.transform.root&&hit.normal.y>.45f)return true;
        }
        return false;
    }
    public static void Milestone(int id){if(Instance!=null)Instance.Reach(id);}
    public bool Reach(int id)
    {
        if(!ready||!Authority||Cheated||CheatMode.Active||!CheckpointCatalog.Matches(id,gameObject.scene.name)||(reached&(1u<<id))!=0||(current!=null&&id<=current.id))return false;
        EarnedPowers|=ReadPowers();
        var point=new CheckpointProgress.Snapshot{id=id,scene=gameObject.scene.name,powers=EarnedPowers,activePower=ReadActivePower(),position=body.transform.position,yaw=body.transform.eulerAngles.y,hasPosition=true,world=gate!=null&&gate.Opened?1:0};
        // Arrival can be earned with trailing legs over lava. Save the safe bank,
        // never the partly supported crossing pose.
        if(id==3&&jungle!=null)point.position=jungle.SafeUprightAnchor;
        if(rail!=null&&rail.carts!=null){point.carts=new float[rail.carts.Length];for(int i=0;i<point.carts.Length;i++)point.carts[i]=rail.CartU(i);}
        // Restart at the safe station beside the first cart, never inside its rock load.
        if(id==7&&rail!=null&&rail.station!=null){point.position=rail.station.position+Vector3.up*2; if(point.carts!=null)for(int i=0;i<point.carts.Length;i++)if(rail.carts[i]!=null)point.carts[i]=rail.carts[i].uStart;}
        if(id==15&&current!=null&&current.id==14){point.position=current.position;point.yaw=current.yaw;}
        bool fresh=!CheckpointProgress.IsUnlocked(id);
        if(!CheckpointProgress.Store(point))return false;
        reached|=1u<<id;current=point;
        if(fresh&&id!=0)toastUntil=Time.unscaledTime+3;
        ReachedJson=JsonUtility.ToJson(point);
        FindAnyObjectByType<NetStageSync>()?.PublishCheckpoint(point);
        return true;
    }
    public static void ReceiveReached(string json)
    {
        if(Authority||string.IsNullOrEmpty(json))return;
        var point=JsonUtility.FromJson<CheckpointProgress.Snapshot>(json);if(!CheckpointProgress.Valid(point))return;
        bool fresh=!CheckpointProgress.IsUnlocked(point.id);
        if(CheckpointProgress.Store(point)&&Instance!=null){Instance.current=point;if(fresh)Instance.toastUntil=Time.unscaledTime+3;}
    }
    public bool Respawn()
    {
        if(!Authority||current==null||CheckpointCatalog.All[current.id].SceneStart||current.id==15)return false;
        Apply(current,true);return true;
    }
    void OnGUI()
    {
        if(Time.unscaledTime>toastUntil||toastUntil<=0||LetterOverlay.IsOpen)return;LocaleFonts.Apply();
        float width=Mathf.Min(430,Screen.width-40);var r=new Rect((Screen.width-width)/2,Screen.height-62,width,36);
        UiKit.Panel(r,.93f);GUI.Label(r,L.T("체크포인트 저장됨","Checkpoint saved"),UiKit.Center);
    }
    void OnDestroy(){if(Instance==this)Instance=null;}
}
