using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>Creates an independent Moon variant. Only the house and ending are replaced.</summary>
public static class MoonCozyKitchenBuilder
{
    public const string ScenePath="Assets/Scenes/Moon_CozyKitchen.unity";
    const string Dir="Assets/Guide/MoonCozyKitchen";
    static Material cream,wood,oak,sage,dark,brass,linen,clay,milk,leaf,glow;
    static Transform house;
    static readonly Vector2[] Feet={new(-.58f,.58f),new(.58f,.58f),new(-.58f,-.58f),new(.58f,-.58f)};
    static Material Mat(string name,Color c,float shine=.15f,Color? emission=null)
    {
        var path=Dir+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",shine);
        if(emission.HasValue){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
        EditorUtility.SetDirty(m);return m;
    }
    static Transform Group(string name,Transform parent,Vector3 p)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
    static GameObject Prim(PrimitiveType type,string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool col=false,Vector3? rot=null)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;
        if(rot.HasValue)g.transform.localRotation=Quaternion.Euler(rot.Value);
        g.GetComponent<Renderer>().sharedMaterial=mat;if(!col)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static GameObject Box(string name,Transform p,Vector3 at,Vector3 size,Material mat,bool col=false,Vector3? rot=null)=>Prim(PrimitiveType.Cube,name,p,at,size,mat,col,rot);
    static GameObject Cyl(string name,Transform p,Vector3 at,Vector3 size,Material mat,Vector3? rot=null)=>Prim(PrimitiveType.Cylinder,name,p,at,size,mat,false,rot);
    static GameObject Ball(string name,Transform p,Vector3 at,Vector3 size,Material mat)=>Prim(PrimitiveType.Sphere,name,p,at,size,mat);
    static void Bar(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material mat)
    {var g=Cyl(name,parent,(a+b)*.5f,new Vector3(radius,(b-a).magnitude*.5f,radius),mat);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static void Ring(string name,Transform p,Vector3 at,float radius,float tube,Material mat,Vector3 rotation)
    {
        var group=Group(name,p,at);group.localRotation=Quaternion.Euler(rotation);
        for(int i=0;i<20;i++){float a=i*Mathf.PI*2/20,b=(i+1)*Mathf.PI*2/20;Bar("Arc",group,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0),tube,mat);}
    }
    static Light LightAt(string name,Transform p,Vector3 at,Color color,float strength,float range)
    {var g=Group(name,p,at);var l=g.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=strength;l.range=range;l.shadows=LightShadows.Soft;return l;}
    static void Cup(Transform p,Vector3 at,Material mat)
    {
        var g=Group("Mug",p,at);Cyl("Ceramic",g,new Vector3(0,.09f,0),new Vector3(.16f,.09f,.16f),mat);
        Cyl("Tea",g,new Vector3(0,.181f,0),new Vector3(.13f,.004f,.13f),dark);
        Ring("Handle",g,new Vector3(.105f,.1f,0),.055f,.015f,mat,Vector3.zero);
    }
    static void Plant(Transform p,Vector3 at,float scale)
    {
        var g=Group("Herbs",p,at);g.localScale=Vector3.one*scale;
        Cyl("Terracotta",g,new Vector3(0,.12f,0),new Vector3(.29f,.12f,.29f),clay);
        for(int i=0;i<6;i++){float a=i*2.4f;Vector3 top=new Vector3(Mathf.Cos(a)*.16f,.4f+(i%2)*.12f,Mathf.Sin(a)*.16f);Bar("Stem",g,new Vector3(0,.2f,0),top,.014f,leaf);var b=Ball("Leaf",g,top,new Vector3(.21f,.07f,.12f),leaf);b.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,25);}
    }
    static void Cabinet(Transform p,Vector3 at,Vector3 size)
    {
        var g=Group("Cupboard",p,at);Box("Carcass",g,Vector3.zero,size,sage,true);
        Box("DoorInset",g,new Vector3(0,0,size.z*.5f+.02f),new Vector3(size.x-.12f,size.y-.12f,.035f),cream);
        Box("DoorPanel",g,new Vector3(0,0,size.z*.5f+.042f),new Vector3(size.x-.22f,size.y-.22f,.022f),sage);
        Ball("Knob",g,new Vector3(size.x*.3f,size.y*.18f,size.z*.5f+.08f),Vector3.one*.065f,brass);
    }
    static void Gable(float z)
    {
        var mesh=new Mesh{name="CozyGable"};mesh.vertices=new[]{new Vector3(-5.3f,3.65f,z),new Vector3(5.3f,3.65f,z),new Vector3(0,5.8f,z)};
        mesh.triangles=new[]{0,1,2,2,1,0};mesh.RecalculateNormals();var path=Dir+"/Gable"+(z>0?"Front":"Back")+".asset";
        AssetDatabase.CreateAsset(mesh,path);var g=Group("Gable",house,Vector3.zero);g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=cream;
    }
    static void Floor(MoonKitchenEnding ending)
    {
        var floor=Group("RecessedPlankFloor",house,Vector3.zero);
        var xs=new SortedSet<float>{-5.2f,-3.4f,-1.7f,1.7f,3.4f,5.2f};var zs=new SortedSet<float>{-4.2f,4.2f};
        for(float z=-3.8f;z<4.1f;z+=.48f)zs.Add(z);
        foreach(var foot in Feet){xs.Add(foot.x-.2f);xs.Add(foot.x+.2f);zs.Add(foot.y-.2f);zs.Add(foot.y+.2f);}
        var ax=new List<float>(xs);var az=new List<float>(zs);
        var oaks=new[]{oak,Mat("OakHoney",new Color(.57f,.37f,.19f)),Mat("OakSoft",new Color(.64f,.45f,.26f))};
        for(int x=0;x<ax.Count-1;x++)for(int z=0;z<az.Count-1;z++)
        {
            float px=(ax[x]+ax[x+1])*.5f,pz=(az[z]+az[z+1])*.5f;bool hole=false;
            foreach(var foot in Feet)if(Mathf.Abs(px-foot.x)<.2f&&Mathf.Abs(pz-foot.y)<.2f)hole=true;
            if(!hole)Box("FloorPlank",floor,new Vector3(px,.07f,pz),new Vector3(ax[x+1]-ax[x]-.006f,.22f,az[z+1]-az[z]-.006f),oaks[(x+z*2)%3],true);
        }
        Box("Foundation",house,new Vector3(0,-.25f,0),new Vector3(10.5f,.42f,8.5f),wood,true);
        for(int i=0;i<4;i++)
        {
            Vector2 f=Feet[i];var s=Group("FootSocket_"+i,floor,new Vector3(f.x,.12f,f.y));ending.sockets[i]=s;
            Box("SocketBottom",s,new Vector3(0,-.045f,0),new Vector3(.4f,.01f,.4f),dark,true);
            for(int edge=0;edge<4;edge++)
            {
                bool along=edge<2;var rim=Box("SocketRim",s,new Vector3(along?0:(edge==2?-.22f:.22f),.063f,along?(edge==0?-.22f:.22f):0),along?new Vector3(.48f,.013f,.035f):new Vector3(.035f,.013f,.48f),brass);
                if(edge==0)ending.socketRims[i]=rim.GetComponent<Renderer>();
            }
        }
    }
    static void Shell()
    {
        Box("WallBack",house,new Vector3(0,1.9f,-4.2f),new Vector3(10.5f,3.5f,.22f),cream,true);
        Box("WallLeft",house,new Vector3(-5.2f,1.9f,0),new Vector3(.22f,3.5f,8.5f),cream,true);
        Box("WallRight",house,new Vector3(5.2f,1.9f,0),new Vector3(.22f,3.5f,8.5f),cream,true);
        // A real opening: no opaque wall or luminous pane behind the window.
        Box("FrontDoorPier",house,new Vector3(-5.05f,1.9f,4.2f),new Vector3(.3f,3.5f,.22f),cream,true);
        Box("FrontMiddlePier",house,new Vector3(-1.9f,1.9f,4.2f),new Vector3(.4f,3.5f,.22f),cream,true);
        Box("FrontRightPier",house,new Vector3(4.92f,1.9f,4.2f),new Vector3(.56f,3.5f,.22f),cream,true);
        Box("WindowLowerWall",house,new Vector3(1.47f,.43f,4.2f),new Vector3(6.34f,.5f,.22f),cream,true);
        Box("WindowUpperWall",house,new Vector3(1.47f,3.45f,4.2f),new Vector3(6.34f,.4f,.22f),cream,true);
        Box("DoorLintel",house,new Vector3(-3.5f,3.18f,4.2f),new Vector3(2.8f,.92f,.22f),cream,true);
        for(int i=0;i<2;i++)Box("WindowSideFrame",house,new Vector3(i==0?-1.7f:4.64f,1.97f,4.2f),new Vector3(.1f,2.6f,.34f),wood);
        Box("WindowSill",house,new Vector3(1.47f,.71f,4.2f),new Vector3(6.6f,.1f,.64f),wood);
        Box("WindowTop",house,new Vector3(1.47f,3.26f,4.2f),new Vector3(6.5f,.12f,.32f),wood);
        Box("WindowMullion",house,new Vector3(2.05f,1.97f,4.2f),new Vector3(.045f,2.45f,.15f),wood);
        Box("CurtainRod",house,new Vector3(1.47f,3.34f,3.94f),new Vector3(6.8f,.045f,.045f),brass);
        for(int side=0;side<2;side++)for(int fold=0;fold<5;fold++)
        {
            float x=(side==0?-1.43f:4.0f)+fold*.085f;
            Box("LinenCurtainFold",house,new Vector3(x,2.05f,3.94f+(fold%2)*.045f),new Vector3(.1f,2.43f,.08f),linen);
        }
        for(int side=0;side<2;side++)
        {
            float x=side==0?-5.07f:5.07f;Box("WallWainscot",house,new Vector3(x,.58f,0),new Vector3(.07f,.78f,8.2f),sage);
            Box("ChairRail",house,new Vector3(x,1.0f,0),new Vector3(.1f,.07f,8.2f),wood);
        }
        Box("BackWainscot",house,new Vector3(0,.58f,-4.06f),new Vector3(10.2f,.78f,.07f),sage);
        for(int x=-4;x<=4;x+=2)Box("CeilingBeam",house,new Vector3(x,3.68f,0),new Vector3(.14f,.2f,8.7f),wood);
        float half=5.6f,rise=2.3f;float angle=Mathf.Atan2(rise,half)*Mathf.Rad2Deg,length=Mathf.Sqrt(half*half+rise*rise);
        var roof=Mat("RoofTerracotta",new Color(.36f,.16f,.12f));
        Box("RoofL",house,new Vector3(-2.8f,4.76f,0),new Vector3(length+.15f,.16f,9.1f),roof,true,new Vector3(0,0,angle));
        Box("RoofR",house,new Vector3(2.8f,4.76f,0),new Vector3(length+.15f,.16f,9.1f),roof,true,new Vector3(0,0,-angle));Gable(4.2f);Gable(-4.2f);
        Box("EntryStep",house,new Vector3(-3.5f,.03f,4.65f),new Vector3(3.15f,.14f,1f),oak,true);
        var hinge=Group("OpenDoor",house,new Vector3(-4.9f,.18f,4.2f));hinge.localRotation=Quaternion.Euler(0,-106,0);
        Box("DoorLeaf",hinge,new Vector3(1.35f,1.3f,0),new Vector3(2.65f,2.6f,.09f),sage,true);
        for(int y=0;y<2;y++)Box("DoorPanel",hinge,new Vector3(1.35f,.7f+y*1.15f,-.052f),new Vector3(2.25f,.95f,.025f),cream);
        Ball("DoorHandle",hinge,new Vector3(2.4f,1.3f,-.1f),Vector3.one*.09f,brass);
    }
    static void Kitchen(MoonKitchenEnding ending)
    {
        var k=Group("Kitchen",house,Vector3.zero);
        for(int i=0;i<5;i++)Cabinet(k,new Vector3(-3.8f+i*1.15f,.66f,-3.62f),new Vector3(1.1f,.96f,.88f));
        Box("Worktop",k,new Vector3(-1.5f,1.18f,-3.62f),new Vector3(5.85f,.1f,1.0f),oak,true);
        // Hand-set ceramic backsplash tiles, gentle alternating cream tones.
        for(int x=0;x<16;x++)for(int y=0;y<3;y++)Box("BacksplashTile",k,new Vector3(-4.3f+x*.35f,1.43f+y*.25f,-4.065f),new Vector3(.34f,.24f,.018f),(x+y)%3==0?linen:milk);
        Box("SinkLip",k,new Vector3(-2.95f,1.24f,-3.58f),new Vector3(.94f,.03f,.64f),milk);
        Box("SinkBasin",k,new Vector3(-2.95f,1.257f,-3.58f),new Vector3(.78f,.015f,.49f),dark);
        Bar("Tap",k,new Vector3(-2.95f,1.27f,-3.93f),new Vector3(-2.95f,1.65f,-3.93f),.044f,brass);
        Bar("TapSpout",k,new Vector3(-2.95f,1.65f,-3.93f),new Vector3(-2.95f,1.65f,-3.58f),.044f,brass);
        Box("Cooktop",k,new Vector3(.15f,1.245f,-3.62f),new Vector3(1.05f,.035f,.8f),dark);
        Cyl("Burner",k,new Vector3(.05f,1.28f,-3.62f),new Vector3(.4f,.01f,.4f),brass);
        Ball("Kettle",k,new Vector3(.05f,1.48f,-3.62f),new Vector3(.45f,.39f,.43f),clay);
        Cyl("KettleLid",k,new Vector3(.05f,1.69f,-3.62f),new Vector3(.25f,.025f,.25f),brass);
        Ball("LidKnob",k,new Vector3(.05f,1.75f,-3.62f),Vector3.one*.065f,wood);
        Bar("Spout",k,new Vector3(.23f,1.5f,-3.62f),new Vector3(.4f,1.68f,-3.62f),.055f,clay);
        Ring("KettleHandle",k,new Vector3(-.12f,1.65f,-3.62f),.21f,.025f,wood,Vector3.zero);
        var smokeMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Guide/Moon/HouseSmoke.mat");
        var steam=Group("KettleSteam",k,new Vector3(.4f,1.7f,-3.62f));steam.localRotation=Quaternion.Euler(-90,0,0);var ps=steam.gameObject.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=2.6f;main.startSpeed=.14f;main.startSize=new ParticleSystem.MinMaxCurve(.07f,.15f);main.startColor=new Color(1,.94f,.8f,.18f);main.maxParticles=50;var emission=ps.emission;emission.rateOverTime=5;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=10;shape.radius=.025f;ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=smokeMat;ending.steam=ps;
        Box("BreadBoard",k,new Vector3(-1.47f,1.255f,-3.57f),new Vector3(.78f,.035f,.48f),wood);
        Ball("Bread",k,new Vector3(-1.52f,1.37f,-3.57f),new Vector3(.58f,.22f,.29f),oak);
        for(int i=0;i<3;i++)Box("BreadScore",k,new Vector3(-1.7f+i*.17f,1.478f,-3.56f),new Vector3(.025f,.008f,.2f),cream,false,new Vector3(0,20,0));
        Cup(k,new Vector3(-.68f,1.245f,-3.48f),milk);Cup(k,new Vector3(-.91f,1.245f,-3.63f),sage);
        Plant(k,new Vector3(-4.05f,1.25f,-3.65f),.85f);
        for(int shelf=0;shelf<2;shelf++)
        {
            float y=2.2f+shelf*.65f;Box("OpenShelf",k,new Vector3(-2.5f,y,-3.89f),new Vector3(3.65f,.075f,.4f),wood);
            for(int j=0;j<5;j++){var jar=Group("Jar",k,new Vector3(-3.8f+j*.64f,y+.05f,-3.85f));Cyl("Pottery",jar,new Vector3(0,.15f,0),new Vector3(.22f,.15f,.22f),j%2==0?clay:milk);Cyl("Lid",jar,new Vector3(0,.31f,0),new Vector3(.235f,.025f,.235f),wood);}
        }
        Cabinet(k,new Vector3(3.6f,1.27f,-3.63f),new Vector3(1.9f,2.2f,.84f));
        Box("HutchShelf",k,new Vector3(3.6f,2.44f,-3.61f),new Vector3(2f,.09f,.93f),oak);
        for(int i=0;i<3;i++)Cyl("Plate",k,new Vector3(3.1f+i*.48f,2.77f,-3.9f),new Vector3(.39f,.025f,.39f),milk,new Vector3(90,0,0));
        Box("SideRunner",k,new Vector3(2.65f,.192f,.05f),new Vector3(1.65f,.015f,3.2f),linen);
        for(int i=0;i<9;i++)Box("RunnerStripe",k,new Vector3(2.65f,.202f,-1.45f+i*.36f),new Vector3(1.58f,.005f,.034f),clay);
        var lamp=Group("Pendant",k,new Vector3(0,0,0));Bar("Cord",lamp,new Vector3(0,3.65f,0),new Vector3(0,2.35f,0),.022f,dark);
        Cyl("Shade",lamp,new Vector3(0,2.42f,0),new Vector3(.66f,.16f,.66f),linen);Ball("Bulb",lamp,new Vector3(0,2.24f,0),Vector3.one*.16f,glow);
        ending.warmLights=new[]{LightAt("TableLamplight",k,new Vector3(0,2.18f,0),new Color(1,.76f,.47f),3.1f,8),LightAt("WorktopWarmth",k,new Vector3(-1.8f,2.55f,-2.6f),new Color(1,.84f,.64f),2.1f,6),LightAt("WindowSpill",k,new Vector3(1.4f,2.65f,3.3f),new Color(1,.8f,.52f),2.0f,7)};
        // Small lived-in details retain the game's simple, tactile geometry.
        Plant(k,new Vector3(4.5f,.18f,-1.55f),1.6f);
        Box("TeaTowel",k,new Vector3(-3.25f,.94f,-3.13f),new Vector3(.35f,.48f,.025f),linen);
        for(int i=0;i<3;i++)Box("TowelStripe",k,new Vector3(-3.35f+i*.1f,.94f,-3.112f),new Vector3(.018f,.46f,.006f),clay);
        var clock=Group("WallClock",k,new Vector3(-5.065f,2.5f,-1.5f));clock.localRotation=Quaternion.Euler(0,90,0);
        Cyl("ClockRim",clock,Vector3.zero,new Vector3(.56f,.04f,.56f),wood,new Vector3(90,0,0));Cyl("ClockFace",clock,new Vector3(0,0,-.045f),new Vector3(.49f,.012f,.49f),milk,new Vector3(90,0,0));
        Bar("Hour",clock,new Vector3(0,0,-.066f),new Vector3(-.11f,.09f,-.066f),.012f,dark);Bar("Minute",clock,new Vector3(0,0,-.066f),new Vector3(.04f,.18f,-.066f),.009f,dark);
    }
    static Transform CloneVisual(Transform src,Transform parent,string name)
    {
        if(src==null)throw new System.InvalidOperationException("Missing original actor: "+name);
        var g=Object.Instantiate(src.gameObject);g.name=name;SceneManager.MoveGameObjectToScene(g,house.gameObject.scene);g.transform.SetParent(parent,true);
        foreach(var b in g.GetComponentsInChildren<NetworkBehaviour>(true))Object.DestroyImmediate(b);
        foreach(var b in g.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(b);
        foreach(var j in g.GetComponentsInChildren<Joint>(true))Object.DestroyImmediate(j);
        foreach(var c in g.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
        foreach(var rb in g.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(rb);
        foreach(var a in g.GetComponentsInChildren<AudioSource>(true))Object.DestroyImmediate(a);
        foreach(var ps in g.GetComponentsInChildren<ParticleSystem>(true)){ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;}
        g.SetActive(true);foreach(var r in g.GetComponentsInChildren<Renderer>(true))if(!(r is ParticleSystemRenderer))r.enabled=true;return g.transform;
    }
    static void PlaceActor(Transform actor,Vector3 at,float height,float yaw)
    {
        actor.SetParent(house.Find("WindowFriends"),true);actor.localRotation=Quaternion.Euler(0,yaw,0);
        Bounds b=new Bounds();bool first=true;foreach(var r in actor.GetComponentsInChildren<Renderer>())if(!(r is ParticleSystemRenderer)){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}
        if(!first){actor.localScale*=height/Mathf.Max(.01f,b.size.y);first=true;foreach(var r in actor.GetComponentsInChildren<Renderer>())if(!(r is ParticleSystemRenderer)){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}var bottom=new Vector3(b.center.x,b.min.y,b.center.z);actor.position+=house.TransformPoint(at)-bottom;}
        var idle=actor.gameObject.AddComponent<KitchenFriendIdle>();idle.phase=at.x;idle.lift=actor.name.Contains("Squid")?.07f:.01f;idle.sway=actor.name.Contains("Snowman")?.25f:.55f;
    }
    static void Friends(MoonKitchenEnding ending)
    {
        var p=Group("WindowFriends",house,Vector3.zero);ending.friends=p.gameObject;var target=house.gameObject.scene;
        var jungle=EditorSceneManager.OpenScene("Assets/Scenes/Jungle.unity",OpenSceneMode.Additive);
        Transform giraffe=null;foreach(var root in jungle.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="Giraffe")giraffe=t;
        var g=CloneVisual(giraffe,p,"GiraffeFriend");EditorSceneManager.CloseScene(jungle,true);SceneManager.SetActiveScene(target);PlaceActor(g,new Vector3(-1.9f,.05f,5.2f),3.15f,185);
        var cave=EditorSceneManager.OpenScene("Assets/Scenes/IceCavern.unity",OpenSceneMode.Additive);SnowmanCutscene snow=null;FrameHallClimb climb=null;
        foreach(var root in cave.GetRootGameObjects()){if(snow==null)snow=root.GetComponentInChildren<SnowmanCutscene>(true);if(climb==null)climb=root.GetComponentInChildren<FrameHallClimb>(true);}
        var snowGroup=Group("SnowmanFriend",p,Vector3.zero);
        var body=CloneVisual(snow.body,snowGroup,"CompleteBody");var head=CloneVisual(snow.head,snowGroup,"CompleteHead");body.localPosition=Vector3.zero;body.localRotation=Quaternion.identity;head.localPosition=Vector3.up*snow.assembledHeadOffsetY;head.localRotation=Quaternion.identity;
        var caterpillar=CloneVisual(climb.cutsceneChain,p,"TennisCaterpillarFriend");
        EditorSceneManager.CloseScene(cave,true);SceneManager.SetActiveScene(target);PlaceActor(snowGroup,new Vector3(3.35f,.02f,5.05f),2.45f,180);
        caterpillar.SetParent(p,false);caterpillar.localPosition=new Vector3(.7f,.92f,4.65f);caterpillar.localRotation=Quaternion.identity;caterpillar.localScale=Vector3.one;
        for(int i=0;i<caterpillar.childCount;i++){var t=caterpillar.GetChild(i);t.localPosition=new Vector3((i-3.5f)*.24f,.04f*Mathf.Sin(i*.8f),0);t.localScale=Vector3.one*.34f;}
        caterpillar.gameObject.AddComponent<KitchenFriendIdle>().lift=.025f;
        var space=EditorSceneManager.OpenScene("Assets/Scenes/Space.unity",OpenSceneMode.Additive);SpaceIntroCutscene intro=null;foreach(var root in space.GetRootGameObjects())if(intro==null)intro=root.GetComponentInChildren<SpaceIntroCutscene>(true);
        var squid=CloneVisual(intro.squid,p,"SpaceSquidFriend");EditorSceneManager.CloseScene(space,true);SceneManager.SetActiveScene(target);PlaceActor(squid,new Vector3(3.4f,2.5f,5.3f),.82f,180);
        p.gameObject.SetActive(false);
    }
    static void Shots(MoonKitchenEnding ending)
    {
        var p=Group("CinematicShots",house,Vector3.zero);
        var positions=new[]{new Vector3(-2.8f,1.62f,3.4f),new Vector3(2.25f,1.38f,1.85f),new Vector3(-1.3f,1.65f,-2.38f),new Vector3(-4.3f,1.95f,-1.7f),new Vector3(.8f,1.28f,3.75f),new Vector3(1.8f,2.65f,13.2f)};
        var ends=new[]{new Vector3(-2.3f,1.5f,2.9f),new Vector3(2.05f,1.31f,1.65f),new Vector3(-1.03f,1.57f,-2.65f),new Vector3(-3.8f,1.8f,-1.35f),new Vector3(.95f,1.3f,3.9f),new Vector3(1.5f,2.75f,14.0f)};
        var looks=new[]{new Vector3(0,.7f,0),new Vector3(0,.68f,0),new Vector3(.02f,1.47f,-3.62f),new Vector3(.4f,.94f,.3f),new Vector3(.1f,.58f,0),new Vector3(1.1f,1.5f,3.3f)};
        var names=new[]{"A_place_to_rest","The_table_and_chair","Tea_is_ready","Quiet_kitchen","Stay_a_little_longer","Everyone_is_home"};var secs=new[]{10f,13f,12f,14f,12f,21f};var fovs=new[]{49f,43f,44f,52f,42f,49f};ending.shots=new MoonKitchenEnding.Shot[6];
        for(int i=0;i<6;i++)ending.shots[i]=new MoonKitchenEnding.Shot{label=names[i],from=Group(names[i]+"_From",p,positions[i]),to=Group(names[i]+"_To",p,ends[i]),lookAt=Group(names[i]+"_Look",p,looks[i]),seconds=secs[i],fov=fovs[i]};
    }
    [MenuItem("Tools/Moon/Create Cozy Kitchen Variant")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new System.InvalidOperationException("Save current scene edits first.");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)throw new System.InvalidOperationException("Variant already exists; refusing to overwrite it.");
        if(!AssetDatabase.IsValidFolder(Dir))AssetDatabase.CreateFolder("Assets/Guide","MoonCozyKitchen");
        if(!AssetDatabase.CopyAsset("Assets/Scenes/Moon.unity",ScenePath))throw new System.InvalidOperationException("Could not copy Moon scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath);var old=GameObject.Find("House");var pos=old.transform.position;var rot=old.transform.rotation;
        var oldEnd=Object.FindAnyObjectByType<MoonHomeEnding>();var chairCopy=Object.Instantiate(oldEnd.chair.gameObject);chairCopy.name="WaitingChair";chairCopy.transform.SetParent(null);
        Object.DestroyImmediate(old);Object.DestroyImmediate(oldEnd.gameObject);
        cream=Mat("CreamPlaster",new Color(.88f,.82f,.69f));wood=Mat("WarmWalnut",new Color(.29f,.17f,.095f));oak=Mat("HoneyOak",new Color(.61f,.41f,.22f));sage=Mat("SagePaint",new Color(.35f,.45f,.32f));dark=Mat("DeepWarmShadow",new Color(.09f,.075f,.05f));brass=Mat("AgedBrass",new Color(.57f,.39f,.14f),.4f,Color.black);linen=Mat("OatLinen",new Color(.83f,.72f,.52f));clay=Mat("Terracotta",new Color(.58f,.26f,.14f));milk=Mat("MilkCeramic",new Color(.92f,.88f,.76f),.35f);leaf=Mat("HerbGreen",new Color(.24f,.38f,.15f));glow=Mat("LampGlow",new Color(1,.87f,.59f),.2f,new Color(2.8f,1.8f,.7f));
        house=new GameObject("House_CozyKitchen").transform;house.SetPositionAndRotation(pos,rot);
        var ending=house.gameObject.AddComponent<MoonKitchenEnding>();Floor(ending);Shell();Kitchen(ending);
        ending.tablePlace=Group("TablePlace",house,new Vector3(0,.08f,0));ending.chairSeat=Group("ChairSeat",house,new Vector3(0,.18f,1.04f));ending.chair=chairCopy.transform;ending.chair.SetParent(house,false);ending.chair.localPosition=new Vector3(.65f,.18f,1.65f);ending.chair.localRotation=Quaternion.Euler(0,-15,0);
        MoonHouseCeilingUpgrade.ApplyToHouse(house);
        Friends(ending);Shots(ending);LightAt("FriendsWindowBounce",house,new Vector3(1.5f,2.1f,5.1f),new Color(1,.77f,.5f),.85f,6);
        var scenes=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("[MoonCozyKitchen] Created separate scene, real foot recesses, six wordless shots (82 sec), four original friends. Original Moon untouched.");
    }
}
