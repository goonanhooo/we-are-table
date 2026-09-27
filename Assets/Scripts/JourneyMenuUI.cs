using UnityEngine;
using UnityEngine.InputSystem;

// Shared paper cards. All hit tests use the same 1600 x 900 canvas as rendering.
public static class JourneyMenuUI
{
    public static readonly Color Ink = new Color(.035f,.045f,.045f);
    public static readonly Color Accent = new Color(.055f,.16f,.12f);
    public static readonly Color Muted = new Color(.16f,.18f,.17f);
    public static float Scale => Mathf.Min(Screen.width/1600f, Screen.height/900f);
    public static Vector2 Origin => new Vector2((Screen.width-1600*Scale)/2,(Screen.height-900*Scale)/2);
    public static Vector2 MousePosition => Mouse.current == null ? Vector2.one*-9999 : (new Vector2(Mouse.current.position.ReadValue().x,Screen.height-Mouse.current.position.ReadValue().y)-Origin)/Scale;
    static int usedFrame = -1;
    static Matrix4x4 previous;
    static Color previousColor,previousContent;
    static bool previousEnabled;
    static int polledFrame=-1;
    static bool pendingClick;
    static bool wasBlocked;
    static Vector2 pressedPosition;
    static GUIStyle textStyle;
    public static void BlockClick(){usedFrame=Time.frameCount;pendingClick=false;}
    public static void PollInput()
    {
        if(polledFrame==Time.frameCount)return;polledFrame=Time.frameCount;
        pendingClick=false;
        bool blocked=PauseMenu.InputBlocked||LetterOverlay.IsOpen;
        if(blocked||wasBlocked){pendingClick=false;wasBlocked=blocked;return;}
        // Sample in Update, then consume once across this frame's IMGUI passes.
        // A disabled button must never retain a click for a later menu state.
        if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&usedFrame!=Time.frameCount)
        {pendingClick=true;pressedPosition=MousePosition;}
    }
    public static bool Click(Rect r, bool enabled=true)
    {
        if (!enabled || !pendingClick || usedFrame == Time.frameCount || !r.Contains(pressedPosition)) return false;
        usedFrame=Time.frameCount;pendingClick=false;return true;
    }
    public static void Begin()
    {
        UiKit.ApplyKoreanFont(); previous=GUI.matrix;previousColor=GUI.color;previousContent=GUI.contentColor;previousEnabled=GUI.enabled;
        GUI.color=GUI.contentColor=Color.white;GUI.enabled=true;
        GUI.matrix=Matrix4x4.TRS(Origin,Quaternion.identity,Vector3.one*Scale);
    }
    public static void End(){GUI.matrix=previous;GUI.color=previousColor;GUI.contentColor=previousContent;GUI.enabled=previousEnabled;}
    public static void Text(Rect r,string text,int size=28,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
    {
        if(textStyle==null)textStyle=HandUI.Style(size,Ink);
        var st=textStyle;st.fontSize=size;st.alignment=align;st.normal.textColor=color??Ink;st.wordWrap=true;
        st.hover.textColor=st.active.textColor=st.focused.textColor=st.normal.textColor;
        ThoughtBubbleUI.FittedLabel(r,text,st,size,Mathf.Min(size,16));
    }
    public static void Fill(Rect r,Color c) => UiKit.Fill(r,c);
    public static void Border(Rect r,Color c,float width=2)
    {
        Fill(new Rect(r.x,r.y,r.width,width),c);Fill(new Rect(r.x,r.yMax-width,r.width,width),c);
        Fill(new Rect(r.x,r.y,width,r.height),c);Fill(new Rect(r.xMax-width,r.y,width,r.height),c);
    }
    public static void Paper(Rect r,bool selected=false,bool hover=false)
    {
        Fill(new Rect(r.x+4,r.y+7,r.width,r.height),new Color(.1f,.15f,.12f,.07f));
        Fill(r,new Color(.99f,.985f,.965f));
        Border(r,selected?Accent:hover?new Color(.47f,.53f,.49f):new Color(.76f,.77f,.71f),selected?3:1.5f);
    }
    public static bool Button(Rect r,string text,bool enabled=true,bool primary=false)
    {
        bool hover=enabled&&r.Contains(MousePosition);
        Fill(r,primary&&enabled?Accent:new Color(1,1,1,.8f));
        Border(r,enabled?(hover?Accent:new Color(.63f,.67f,.61f)):new Color(.8f,.81f,.78f),hover?2:1);
        Text(r,text,27,enabled?(primary?Color.white:Ink):Muted,TextAnchor.MiddleCenter);
        if(hover)Underline(r,text,27,TextAnchor.MiddleCenter,primary?Color.white:Ink);
        return Click(r,enabled);
    }
    public static void HoverText(Rect r,string text,Rect hit,int size=28,bool enabled=true,TextAnchor align=TextAnchor.MiddleLeft)
    {
        Text(r,text,size,Ink,align);
        if(enabled&&!PauseMenu.InputBlocked&&!LetterOverlay.IsOpen&&hit.Contains(MousePosition))Underline(r,text,size,align,Ink);
    }
    static void Underline(Rect r,string text,int size,TextAnchor align,Color color)
    {
        if(textStyle==null)return;
        float w=Mathf.Min(r.width-12,textStyle.CalcSize(new GUIContent(text)).x);
        float x=align==TextAnchor.MiddleCenter?r.center.x-w/2:r.x;
        float y=r.center.y+Mathf.Min(size,textStyle.fontSize)*.53f;
        var line=new Rect(x,y,w,8);
        if(HandUI.Underline!=null&&color!=Color.white){var old=GUI.color;GUI.color=color;GUI.DrawTexture(line,HandUI.Underline,ScaleMode.StretchToFill);GUI.color=old;}
        else Fill(new Rect(x,y,w,2),color);
    }
    public static void Line(Vector2 a,Vector2 b,Color c,float width=2)
    {
        // Axis-aligned strokes stay in the current GUI clip group (journey cards).
        if(Mathf.Abs(a.y-b.y)<.001f){Fill(new Rect(Mathf.Min(a.x,b.x),a.y-width/2,Mathf.Abs(b.x-a.x),width),c);return;}
        if(Mathf.Abs(a.x-b.x)<.001f){Fill(new Rect(a.x-width/2,Mathf.Min(a.y,b.y),width,Mathf.Abs(b.y-a.y)),c);return;}
        var m=GUI.matrix;GUI.matrix=m*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
        Fill(new Rect(0,-width/2,Vector2.Distance(a,b),width),c);GUI.matrix=m;
    }
    static Texture2D disc;
    public static void Dot(Vector2 p,float radius,Color c)
    {
        if(disc==null){disc=new Texture2D(64,64,TextureFormat.RGBA32,false);disc.hideFlags=HideFlags.HideAndDontSave;var pixels=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))));disc.SetPixels(pixels);disc.Apply();}
        var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),disc);GUI.color=old;
    }
    public static void Icon(Rect r,int kind,Color? tint=null)
    {
        var c=tint??Accent;Vector2 p=r.center;float s=Mathf.Min(r.width,r.height)/100f;
        void L(float ax,float ay,float bx,float by)=>Line(p+new Vector2(ax,ay)*s,p+new Vector2(bx,by)*s,c,3*s);
        if(kind==0){ // table, with four readable feet
            L(-35,-19,16,-19);L(16,-19,35,-3);L(35,-3,-16,-3);L(-16,-3,-35,-19);
            L(-35,-19,-35,16);L(16,-19,16,16);L(-16,-3,-16,32);L(35,-3,35,32);
        }else if(kind==1){ // create a room
            L(-37,-2,0,-32);L(0,-32,37,-2);L(-28,-9,-28,34);L(-28,34,28,34);L(28,34,28,-9);
            L(-10,9,10,9);L(0,-1,0,19);
        }else if(kind==2){ // enter a room
            L(5,-34,32,-34);L(32,-34,32,34);L(32,34,5,34);L(-38,0,15,0);L(15,0,0,-15);L(15,0,0,15);
        }else{ // lock
            Border(new Rect(p.x-23*s,p.y-5*s,46*s,38*s),c,3*s);
            L(-14,-5,-14,-24);L(-14,-24,14,-24);L(14,-24,14,-5);Dot(p+new Vector2(0,11)*s,3*s,c);
        }
    }
    public static string Back => L.T("뒤로","Back","戻る","返回");
    public static void BackArrow(Rect r,Color color)
    {
        float x=r.x+14,y=r.center.y,w=28;
        Line(new Vector2(x+w,y),new Vector2(x,y),color,3.5f);
        Line(new Vector2(x,y),new Vector2(x+11,y-10),color,3.5f);
        Line(new Vector2(x,y),new Vector2(x+11,y+10),color,3.5f);
    }
    public static bool BackButton(Rect r,string label=null,bool enabled=true)
    {
        bool hover=enabled&&r.Contains(MousePosition);Fill(r,new Color(1,1,1,.86f));Border(r,hover?Accent:new Color(.63f,.67f,.61f),hover?2:1);
        var color=enabled?Ink:Muted;BackArrow(r,color);var text=new Rect(r.x+54,r.y,r.width-64,r.height);
        Text(text,label??Back,27,color,TextAnchor.MiddleCenter);if(hover)Underline(text,label??Back,27,TextAnchor.MiddleCenter,color);
        return Click(r,enabled);
    }
    static GUIStyle literalStyle;
    public static void Literal(Rect r,string text,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft)
    {
        if(literalStyle==null)literalStyle=new GUIStyle(GUI.skin.label){font=Resources.Load<Font>("Fonts/CJK/NotoSansCJKjp-Regular"),richText=false,wordWrap=false};
        literalStyle.fontSize=size;literalStyle.alignment=align;literalStyle.normal.textColor=color;
        while(literalStyle.fontSize>14&&literalStyle.CalcSize(new GUIContent(text)).x>r.width)literalStyle.fontSize--;
        while(text.Length>1&&literalStyle.CalcSize(new GUIContent(text)).x>r.width){var parts=System.Globalization.StringInfo.ParseCombiningCharacters(text);if(parts.Length<2)break;text=text.Substring(0,parts[parts.Length-2])+"…";}
        GUI.Label(r,text,literalStyle);
    }
}

public sealed class CheckpointJourney
{
    public int Selected {get;private set;}
    float scroll;
    readonly JourneyWheel wheel=new JourneyWheel();
    readonly Texture2D[] shots=new Texture2D[16];
    public void Select(int id,bool immediate=false){Selected=Mathf.Clamp(id,0,15);if(immediate)scroll=Selected;}
    public void Update(bool interactive=true)
    {
        if(interactive){var kb=Keyboard.current;int move=0;if(kb!=null){if(kb.leftArrowKey.wasPressedThisFrame)move--;if(kb.rightArrowKey.wasPressedThisFrame)move++;}
            if(Mouse.current!=null)move+=wheel.Read(Mouse.current.scroll.ReadValue(),Time.unscaledTime);Select(Selected+move);}
        else wheel.Reset();
        scroll=Mathf.Lerp(scroll,Selected,1-Mathf.Exp(-Time.unscaledDeltaTime*12));
    }
    public static Texture2D Thumbnail(int id)=>Resources.Load<Texture2D>("CheckpointThumbs/"+id.ToString("00"));
    public void Draw(Rect viewport,uint mask,bool interactive=true)
    {
        // Hit tests remain in canvas coordinates; clipping only affects the artwork.
        float stride=360, cw=326, ch=258;
        GUI.BeginGroup(viewport);
        for(int i=0;i<16;i++){
            float cx=viewport.width/2+(i-scroll)*stride;
            if(cx<-cw||cx>viewport.width+cw)continue;
            bool open=(mask&(1u<<i))!=0, selected=i==Selected;
            float y=selected?12:32;
            var card=new Rect(cx-cw/2,y,cw,ch);
            JourneyMenuUI.Line(new Vector2(cx,viewport.height-27),new Vector2(cx+stride,viewport.height-27),new Color(.68f,.71f,.64f),3);
            JourneyMenuUI.Dot(new Vector2(cx,viewport.height-27),selected?10:6,open?JourneyMenuUI.Accent:new Color(.72f,.73f,.7f));
            JourneyMenuUI.Paper(card,selected);
            Rect photo=new Rect(card.x+8,card.y+8,cw-16,174);
            if(open){if(shots[i]==null)shots[i]=Thumbnail(i);if(shots[i]!=null)GUI.DrawTexture(photo,shots[i],ScaleMode.ScaleAndCrop);else JourneyMenuUI.Icon(photo,0);}
            else {JourneyMenuUI.Fill(photo,new Color(.91f,.92f,.88f));JourneyMenuUI.Icon(new Rect(photo.center.x-38,photo.center.y-38,76,76),3,new Color(.58f,.63f,.58f));}
            JourneyMenuUI.Text(new Rect(card.x+15,card.y+192,35,45),(i+1).ToString("00"),21,JourneyMenuUI.Muted);
            JourneyMenuUI.Text(new Rect(card.x+55,card.y+184,cw-68,65),CheckpointCatalog.Label(i,mask),27);
        }
        GUI.EndGroup();
        for(int i=0;i<16;i++){
            float cx=viewport.center.x+(i-scroll)*stride;
            var r=new Rect(cx-cw/2,viewport.y+(i==Selected?12:32),cw,ch);
            if(interactive&&viewport.Contains(JourneyMenuUI.MousePosition)&&JourneyMenuUI.Click(r))Select(i);
        }
    }
}

// Accumulate trackpad deltas, and limit repeated moves during a single gesture.
public sealed class JourneyWheel
{
    float accumulated,lastMove=-10f,lastInput=-10f;
    public void Reset(){accumulated=0;lastInput=-10f;lastMove=-10f;}
    public int Read(Vector2 delta,float now)
    {
        float value=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?delta.x:-delta.y;
        if(Mathf.Abs(value)<.01f)return 0;
        if(now-lastInput>.22f || accumulated*value<0)accumulated=0;
        lastInput=now;accumulated+=value;
        if(Mathf.Abs(accumulated)<1f || now-lastMove<.16f)return 0;
        int direction=accumulated>0?1:-1;accumulated=0;lastMove=now;return direction;
    }
}
