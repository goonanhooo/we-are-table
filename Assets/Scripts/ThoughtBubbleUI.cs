using UnityEngine;

/// <summary>
/// Shared thought-bubble skin: empty PNG body + three original cutscene puffs.
/// Text and keycaps are independent live UI; no words or controls are baked into the art.
/// Resource contract: Resources/ThoughtBubbles/{BodyEmpty,Near,Mid,Tip,Layout}.
/// </summary>
public static class ThoughtBubbleUI
{
    public const float VW=1920f,VH=1080f;
    [System.Serializable] class Part { public string name; public float x,y,w,h; }
    [System.Serializable] class Layout { public Part[] parts; }
    static Texture2D[] textures;
    static Rect[] uvs;
    static Texture2D wide; static Rect wideUv;
    static Matrix4x4 savedMatrix;
    static GUIStyle text;
    static Material outline;
    public const float OutlineWidth = 2.8f;
    static void LoadOutline()
    {
        if(outline!=null)return;
        var shader=Resources.Load<Shader>("ThoughtBubbles/UniformOutline");
        if(shader!=null)outline=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
    }
    static void DrawSkin(Rect rect,Texture2D texture,Rect uv,float alpha)
    {
        LoadOutline();
        if(outline==null){var color=GUI.color;GUI.color=new Color(1,1,1,alpha);GUI.DrawTextureWithTexCoords(rect,texture,uv,true);GUI.color=color;return;}
        outline.SetVector("_StrokeUV",new Vector4(OutlineWidth*uv.width/rect.width,OutlineWidth*uv.height/rect.height,0,0));
        outline.SetFloat("_Opacity",alpha);
        Graphics.DrawTexture(rect,texture,uv,0,0,0,0,Color.white,outline);
    }
    static void Load()
    {
        if(textures!=null)return;
        var data=Resources.Load<TextAsset>("ThoughtBubbles/Layout");
        if(data==null)return;
        var layout=JsonUtility.FromJson<Layout>(data.text);
        textures=new Texture2D[layout.parts.Length];uvs=new Rect[textures.Length];
        for(int i=0;i<textures.Length;i++){var p=layout.parts[i];textures[i]=Resources.Load<Texture2D>("ThoughtBubbles/"+p.name);uvs[i]=new Rect(p.x,p.y,p.w,p.h);}
    }
    public static bool AssetsReady { get { Load();return textures!=null&&textures.Length==4&&textures[0]!=null&&textures[1]!=null&&textures[2]!=null&&textures[3]!=null; } }
    static void LoadWide()
    {
        if(wide!=null)return;
        wide=Resources.Load<Texture2D>("ThoughtBubbles/BodyWide");
        var metadata=Resources.Load<TextAsset>("ThoughtBubbles/WideLayout");
        if(metadata!=null){var p=JsonUtility.FromJson<Part>(metadata.text);wideUv=new Rect(p.x,p.y,p.w,p.h);}
        else wideUv=new Rect(0,0,1,1);
    }
    public static void Prewarm() { Load();LoadWide();LoadOutline(); }
    // One reference width keeps the smaller puffs proportional when a bubble grows.
    const float ReferenceBodyWidth=543f;
    public static float TailScale(Rect bubble)=>bubble.width/ReferenceBodyWidth;
    public static bool WideReady { get { LoadWide();return wide!=null; } }
    public static float WideHeight(float width) { LoadWide();return wide!=null?width*(wideUv.height*wide.height)/(wideUv.width*wide.width):width*.38f; }
    public static Rect WideContent(Rect r)=>new Rect(r.x+r.width*.13f,r.y+r.height*.21f,r.width*.74f,r.height*.59f);
    public static void FittedLabel(Rect rect,string value,GUIStyle style,int maxSize,int minSize=14)
    {
        style.fontSize=maxSize;
        value=L.Text(value);
        var content=new GUIContent(value);
        while(style.fontSize>minSize && style.CalcHeight(content,rect.width)>rect.height)style.fontSize--;
        GUI.Label(rect,content,style);
    }
    public static void DrawWideBubble(Rect rect,float alpha,string body="",int fontSize=26)
    {
        if(Event.current.type!=EventType.Repaint || alpha<=0f)return;
        LoadWide();if(wide==null){DrawBubble(rect,alpha,body,fontSize);return;}
        // Fit uniformly to the new art's native UV aspect. Never stretch the original cloud.
        float width=Mathf.Min(rect.width,rect.height/WideHeight(1));
        var fitted=new Rect(0,0,width,WideHeight(width));fitted.center=rect.center;
        var old=GUI.color;GUI.color=new Color(1,1,1,alpha);
        DrawSkin(fitted,wide,wideUv,alpha);
        if(!string.IsNullOrEmpty(body))
        {
            if(text==null){text=HandUI.Style(fontSize,Color.black,bold:true);text.wordWrap=true;}
            FittedLabel(WideContent(fitted),body,text,fontSize);
        }
        GUI.color=old;
    }
    public static float Scale=>Mathf.Min(Screen.width/VW,Screen.height/VH);
    public static void Begin(){savedMatrix=GUI.matrix;float s=Scale;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-VW*s)*.5f,0,0),Quaternion.identity,Vector3.one*s);}
    public static void End(){GUI.matrix=savedMatrix;}
    public static bool GuiPoint(Camera cam,Vector3 world,out Vector2 gui)
    {
        gui=Vector2.zero;if(cam==null)return false;var sp=cam.WorldToScreenPoint(world);if(sp.z<=0)return false;
        float s=Scale;gui=new Vector2((sp.x-(Screen.width-VW*s)*.5f)/s,(Screen.height-sp.y)/s);return true;
    }
    public static Rect PlaceRect(Vector2 anchor,float w,float h,bool right=true,float dx=110f,float dy=100f)
    {
        var r=right?new Rect(anchor.x+dx,anchor.y-dy-h,w,h):new Rect(anchor.x-dx-w,anchor.y-dy-h,w,h);
        r.x=Mathf.Clamp(r.x,24,VW-w-24);r.y=Mathf.Clamp(r.y,130,VH-h-60);return r;
    }
    public static float EaseOut(float v){float i=1-Mathf.Clamp01(v);return 1-i*i*i;}
    public static Rect ScaleFromCenter(Rect r,float k){var c=r.center;r.width*=k;r.height*=k;r.center=c;return r;}
    static void DrawPart(int index,Rect rect,float alpha)
    {
        if(Event.current.type!=EventType.Repaint||alpha<=0||rect.width<=0||rect.height<=0)return;
        Load();if(textures==null||textures[index]==null)return;
        var old=GUI.color;
        GUI.color=new Color(1,1,1,alpha);
        DrawSkin(rect,textures[index],uvs[index],alpha);GUI.color=old;
    }
    public static void DrawPuff(Vector2 center,float size,float alpha,int variant=0)
    {
        Load();int index=Mathf.Clamp(variant,0,2)+1;
        float ratio=textures!=null&&textures[index]!=null?(uvs[index].height*textures[index].height)/(uvs[index].width*textures[index].width):1;
        // Use the original ink once: size, stroke and fade now share the body's scale.
        DrawPart(index,new Rect(center.x-size*.5f,center.y-size*ratio*.5f,size,size*ratio),alpha);
    }
    public static void DrawBubble(Rect rect,float alpha,string body,int fontSize=34)
    {
        DrawPart(0,rect,alpha);if(string.IsNullOrEmpty(body))return;
        if(text==null){text=HandUI.Style(fontSize,Color.black,bold:true);text.wordWrap=true;}
        var old=GUI.color;GUI.color=new Color(1,1,1,alpha);
        FittedLabel(new Rect(rect.x+rect.width*.16f,rect.y+rect.height*.23f,rect.width*.68f,rect.height*.54f),body,text,fontSize);
        GUI.color=old;
    }
    public static Rect FitTail(Rect bubble,Vector2 target)=>ThoughtTailLayout.Fit(bubble,new Rect(target.x-32,target.y-32,64,64));
    public static Rect FitTail(Rect bubble,Rect targetBounds)=>ThoughtTailLayout.Fit(bubble,targetBounds);
    public static Rect TailPuffRect(Rect bubble,Vector2 target,int index)=>ThoughtTailLayout.Build(bubble,target)[Mathf.Clamp(index,0,2)];
    public static void DrawPointingTail(Rect bubble,Vector2 target,float alpha)
    {
        for(int i=0;i<3;i++){var r=TailPuffRect(bubble,target,i);DrawPuff(r.center,r.width,alpha,i);}
    }
    public static void DrawTail(Rect bubble,Vector2 anchor,float alpha)=>DrawPointingTail(bubble,anchor,alpha);
}
