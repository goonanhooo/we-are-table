using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>Gameplay acquisitions receive the reveal; cheats reveal icons without tutorials.</summary>
[DisallowMultipleComponent]
public class AbilityHud : MonoBehaviour
{
    GiraffeMode giraffe; TennisMode tennis; IcePower ice; SquidMode squid;
    ArtSceneReset pose; HallwayStage hallway;
    bool menu; int shown; float baseAlpha, clock; int presenting=-1;
    readonly Queue<int> pending = new Queue<int>();
    readonly Texture2D[] icons = new Texture2D[5];
    Texture2D halo, ring;
    readonly float[] lightLevels = new float[5];
    int lastPoseResets; float poseLightUntil;
    public float PowerLightLevel(int slot) => slot >= 0 && slot < 5 ? lightLevels[slot] : 0f;
    GUIStyle label, keyStyle;
    static int earned;
    const float FlightStart=.38f, FlightEnd=1.55f, Duration=1.8f;
    public bool IsPresenting => presenting>=0 || pending.Count>0;
    public int EarnedPowerMask => earned;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { earned=0; }
    static readonly Color[] colors = { new Color(.25f,.29f,.32f),new Color(.74f,.48f,.16f),new Color(.4f,.63f,.2f),new Color(.25f,.63f,.85f),new Color(.66f,.43f,.87f) };

    void Start()
    {
        giraffe=GetComponent<GiraffeMode>();tennis=GetComponent<TennisMode>();ice=GetComponent<IcePower>();squid=GetComponent<SquidMode>();
        pose=GetComponent<ArtSceneReset>();hallway=Object.FindAnyObjectByType<HallwayStage>();
        menu=Object.FindAnyObjectByType<StartScreenMenu>()!=null || Object.FindAnyObjectByType<NewStartScreenUI>()!=null;
        if(menu)earned=0;
        lastPoseResets=ArtSceneReset.PoseResetCount;
        if(CheatMode.Active && !menu) RevealCheatPowers();
        // A grant received before Start is still animated, rather than displayed twice.
        shown=earned;foreach(int id in pending)shown&=~(1<<id);
    }
    public void RevealCheatPowers()
    {
        giraffe=GetComponent<GiraffeMode>();tennis=GetComponent<TennisMode>();ice=GetComponent<IcePower>();squid=GetComponent<SquidMode>();
        earned|=30;shown|=30;pending.Clear();presenting=-1;clock=0;
    }
    bool LocalPlayer()
    {
        var camera=Camera.main;if(camera==null)return false;
        var follow=camera.GetComponent<CameraFollow>();
        return follow==null || follow.target==null || follow.target.root==transform.root;
    }
    public static void Acquire(Component owner,string power)
    {
        if(owner==null)return;
        var hud=owner.GetComponent<AbilityHud>();
        if(hud==null)hud=owner.gameObject.AddComponent<AbilityHud>();
        if(CheatMode.Active){hud.RevealCheatPowers();return;}
        int id=power=="giraffe"?1:power=="tennis"?2:power=="ice"?3:power=="squid"?4:-1;
        if(id<0 || !hud.LocalPlayer() || (earned&(1<<id))!=0)return;
        owner.GetComponent<KeyTutorial>()?.NotifyGranted(power);
        earned|=1<<id;hud.pending.Enqueue(id);
    }
    public static bool CanReadInput(Component owner)
    {
        if(Time.timeScale<=0f || PauseMenu.InputBlocked || LetterOverlay.IsOpen || Keyboard.current==null) return false;
        var body=owner.GetComponent<ArticulationBody>();if(body==null)body=owner.transform.root.GetComponentInChildren<ArticulationBody>();
        if(body!=null && body.immovable && !CheatMode.Active) return false;
        var camera=Camera.main; if(camera==null) return false;
        var follow=camera.GetComponent<CameraFollow>(); if(follow!=null && !follow.enabled) return false;
        return Object.FindAnyObjectByType<StartScreenMenu>()==null && Object.FindAnyObjectByType<NewStartScreenUI>()==null;
    }
    bool Available(int i)
    {
        switch(i){case 0:return pose!=null&&!pose.disableManualReset;case 1:return giraffe!=null&&!giraffe.locked;
        case 2:return tennis!=null&&(tennis.unlocked||tennis.active);case 3:return ice!=null&&ice.unlocked;
        default:return squid!=null&&!squid.thrustLocked&&(squid.unlocked||squid.active);}
    }
    bool Active(int i)
    {
        switch(i){case 0:return Time.unscaledTime < poseLightUntil;case 1:return giraffe!=null&&giraffe.active;case 2:return tennis!=null&&tennis.active;
        case 3:return ice!=null&&ice.active;default:return squid!=null&&squid.LookOn;}
    }
    static string Name(int i)=>i==0?L.T("바로서기","Stand up"):i==1?L.T("기린의 힘","Giraffe"):i==2?L.T("테니스공의 힘","Tennis"):i==3?L.T("눈사람의 힘","Snowman"):L.T("우주오징어의 힘","Space squid");
    void Update()
    {
        if(menu || !LocalPlayer() || Time.timeScale<=0f || LetterOverlay.IsOpen)return;
        if(lastPoseResets!=ArtSceneReset.PoseResetCount){lastPoseResets=ArtSceneReset.PoseResetCount;poseLightUntil=Time.unscaledTime+.65f;}
        for(int i=0;i<5;i++)lightLevels[i]=Mathf.MoveTowards(lightLevels[i],Active(i)?1f:0f,Time.unscaledDeltaTime*7f);
        bool ready=CheatMode.Active || hallway==null || hallway.IntroFinished;
        baseAlpha=Mathf.MoveTowards(baseAlpha,ready?1:0,Time.unscaledDeltaTime*2f);
        if(!ready || !CanReadInput(this))return;
        if(presenting<0 && pending.Count>0){presenting=pending.Dequeue();clock=0;}
        if(presenting>=0){clock+=Time.unscaledDeltaTime;if(clock>=FlightEnd)shown|=1<<presenting;
            if(clock>=Duration){presenting=-1;clock=0;}}
    }
    void EnsureArt()
    {
        if(label!=null)return;
        label=HandUI.Style(19,HandUI.Ink);label.wordWrap=false;
        keyStyle=HandUI.Style(20,HandUI.Ink);
        var font=Resources.Load<Font>("Fonts/NanumGothic-Regular");if(font!=null)keyStyle.font=font;
        for(int i=0;i<5;i++)icons[i]=MakeIcon(i==0?4:i-1);
        halo=Radial(false);ring=Radial(true);
    }
    Rect Slot(int id)
    {
        float x=Available(0)?108:22;
        if(id>0){if(Available(0))x+=130;for(int i=1;i<id;i++)if((shown&(1<<i))!=0)x+=130;}
        return new Rect(x,20,120,88);
    }
    void DrawCard(int i,Rect r,float alpha)
    {
        GUI.color=Color.white;
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(1,1,1,.88f*alpha),0,12);
        float lit=lightLevels[i];
        if(lit>0f){GUI.color=new Color(colors[i].r,colors[i].g,colors[i].b,alpha*lit*.65f);GUI.DrawTexture(new Rect(r.x+19,r.y-9,82,76),halo);}
        Color ink=Color.Lerp(new Color(.27f,.29f,.31f),colors[i],lit);ink.a=alpha*(Available(i)?1:.42f);GUI.color=ink;
        GUI.DrawTexture(new Rect(r.x+39,r.y+7,42,42),icons[i]);GUI.color=new Color(1,1,1,alpha);
        GUI.Label(new Rect(r.x+88,r.y+5,25,25),(i+1).ToString(),keyStyle);
        string name=Name(i);label.fontSize=19;
        while(label.fontSize>13 && label.CalcSize(new GUIContent(name)).x>110)label.fontSize--;
        GUI.Label(new Rect(r.x+3,r.y+52,114,28),name,label);
    }
    void OnGUI()
    {
        if(menu || Time.timeScale<=0f || LetterOverlay.IsOpen || Event.current.type!=EventType.Repaint || !LocalPlayer())return;
        EnsureArt();Matrix4x4 matrix=GUI.matrix;Color old=GUI.color;int depth=GUI.depth;GUI.depth=-20;
        float scale=Mathf.Min(Mathf.Clamp(Mathf.Min(Screen.width/1920f,Screen.height/1080f),.65f,1.3f),Screen.width/780f);
        GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        if(Available(0) && baseAlpha>0){GUI.color=new Color(1,1,1,baseAlpha);pose.DrawPoseCharges(new Rect(18,25,78,78));DrawCard(0,Slot(0),baseAlpha);}
        for(int i=1;i<5;i++)if((shown&(1<<i))!=0)DrawCard(i,Slot(i),baseAlpha);
        if(presenting>=0)DrawAcquisition(Screen.width/scale,Screen.height/scale);
        GUI.color=old;GUI.matrix=matrix;GUI.depth=depth;
    }
    void DrawAcquisition(float width,float height)
    {
        int id=presenting;Color color=colors[id];
        float intro=Mathf.SmoothStep(0,1,Mathf.Clamp01(clock/.18f));
        float fly=Mathf.SmoothStep(0,1,Mathf.Clamp01((clock-FlightStart)/(FlightEnd-FlightStart)));
        Vector2 origin=new Vector2(width*.5f,height*.38f),target=Slot(id).position+new Vector2(60,28);
        Vector2 center=Vector2.Lerp(origin,target,fly);center.y-=Mathf.Sin(fly*Mathf.PI)*height*.09f;
        float pop=intro*(1+.14f*Mathf.Sin(Mathf.Clamp01(clock/FlightStart)*Mathf.PI));
        float size=Mathf.Lerp(154*pop,42,fly);
        if(clock<FlightEnd)
        {
            DrawTex(halo,center,size*1.6f,new Color(1,1,.94f,.75f*intro*(1-fly)));
            DrawTex(icons[id],center+new Vector2(0,3*(1-fly)),size,new Color(.08f,.06f,.03f,.18f*intro*(1-fly)));
            color.a=intro;DrawTex(icons[id],center,size,color);
        }
        if(clock>=FlightEnd){float land=(clock-FlightEnd)/(Duration-FlightEnd);DrawTex(ring,target,48+land*25,new Color(color.r,color.g,color.b,.5f*(1-land)));}
        GUI.color=Color.white;
    }
    static void DrawTex(Texture2D tex,Vector2 center,float size,Color color){GUI.color=color;GUI.DrawTexture(new Rect(center.x-size/2,center.y-size/2,size,size),tex);GUI.color=Color.white;}
    static void Line(Vector2 a,Vector2 b,float thickness,Color color)
    {
        var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);
        GUI.color=color;GUI.DrawTexture(new Rect(a.x,a.y-thickness/2,Vector2.Distance(a,b),thickness),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=Color.white;
    }
    static Texture2D Radial(bool outline)
    {
        const int size=256;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Ability "+(outline?"ring":"glow"),filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){float r=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1).magnitude;
            float a=outline?Mathf.Clamp01(1-Mathf.Abs(r-.94f)/.012f):Mathf.Pow(Mathf.Clamp01(1-r),2.3f);pixels[y*size+x]=new Color(1,1,1,a);}
        tex.SetPixels(pixels);tex.Apply(false,true);return tex;
    }
    // Small antialiased, single-color silhouettes; no external image dependency.
    static Texture2D MakeIcon(int kind)
    {
        const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Ability logo "+kind,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            int hits=0;
            for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)if(Shape(kind,(x+(sx+.5f)/2)/size,(y+(sy+.5f)/2)/size))hits++;
            pixels[y*size+x]=new Color(1,1,1,hits/4f);
        }
        texture.SetPixels(pixels);texture.Apply(false,true);return texture;
    }
    static bool Circle(float x,float y,float cx,float cy,float radius)=>(x-cx)*(x-cx)+(y-cy)*(y-cy)<radius*radius;
    static bool Box(float x,float y,float left,float bottom,float right,float top)=>x>left&&x<right&&y>bottom&&y<top;
    static bool Shape(int kind,float x,float y)
    {
        if(kind==4)return Box(x,y,.12f,.57f,.88f,.72f)||Box(x,y,.18f,.13f,.29f,.58f)
            ||Box(x,y,.71f,.13f,.82f,.58f)||Box(x,y,.32f,.32f,.4f,.58f)||Box(x,y,.6f,.32f,.68f,.58f);
        if(kind==0)return Box(x,y,.27f,.29f,.7f,.52f)||Box(x,y,.58f,.45f,.71f,.85f)||Box(x,y,.58f,.76f,.91f,.89f)
            ||Box(x,y,.3f,.1f,.4f,.35f)||Box(x,y,.59f,.1f,.69f,.35f)||Box(x,y,.6f,.88f,.65f,.97f)||Box(x,y,.73f,.88f,.78f,.97f);
        if(kind==1){bool ball=Circle(x,y,.5f,.5f,.41f);float left=Mathf.Abs(Mathf.Sqrt((x+.10f)*(x+.10f)+(y-.5f)*(y-.5f))-.56f);float right=Mathf.Abs(Mathf.Sqrt((x-1.1f)*(x-1.1f)+(y-.5f)*(y-.5f))-.56f);return ball&&left>.035f&&right>.035f;}
        if(kind==2){bool snow=Circle(x,y,.5f,.27f,.25f)||Circle(x,y,.5f,.58f,.18f)||Box(x,y,.28f,.76f,.72f,.81f)||Box(x,y,.37f,.8f,.63f,.96f);return snow&&!Circle(x,y,.5f,.25f,.025f)&&!Circle(x,y,.5f,.36f,.025f);}
        bool head=((x-.5f)*(x-.5f)/.085f+(y-.61f)*(y-.61f)/.12f)<1f&&y>.38f;
        bool tentacles=false;for(int i=0;i<4;i++){float center=.26f+i*.16f+.035f*Mathf.Sin(y*24f+i);if(y>.08f&&y<.46f&&Mathf.Abs(x-center)<.045f)tentacles=true;}
        return (head||tentacles)&&!Circle(x,y,.39f,.58f,.045f)&&!Circle(x,y,.61f,.58f,.045f);
    }
    void OnDestroy(){foreach(var icon in icons)if(icon!=null)Destroy(icon);if(halo!=null)Destroy(halo);if(ring!=null)Destroy(ring);}
    public void RestoreEarnedPowers(int mask)
    {
        earned=(mask&15)<<1;shown=earned;pending.Clear();presenting=-1;clock=0;
    }

}
