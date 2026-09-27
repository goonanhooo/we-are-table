using UnityEngine;

/// <summary>Resolution-independent control artwork with live translated labels and square keys.</summary>
public static class LocalizedControlGuide
{
    static GUIStyle title, text, small, key;
    static readonly string[] high = { "E", "Y", "O", "]" }, low = { "Q", "R", "U", "P" };
    static readonly string[][] moves = { new[]{"W","A","S","D"},new[]{"T","F","G","H"},new[]{"I","J","K","L"},new[]{"[",";","'","Enter"} };
    public static int Kind(string name) => name.Contains("Giraffe") ? 1 : name.Contains("Tennis") ? 2 : name.Contains("Snowman") || name.Contains("Ice") ? 3 : name.Contains("Squid") ? 4 : 0;
    public static string Name(int kind) => kind == 1 ? L.T("기린의 힘","giraffe power") : kind == 2 ? L.T("테니스공의 힘","tennis power") : kind == 3 ? L.T("눈사람의 힘","snowman power") : kind == 4 ? L.T("우주오징어의 힘","Space squid") : L.T("기본 조작","basic moves");
    static void Label(Rect r,string s,GUIStyle style,int size) { style.font=LocaleFonts.Body; style.wordWrap=true; ThoughtBubbleUI.FittedLabel(r,s,style,size,14); }
    static void Key(Rect r,string s,Color color,int size=38)
    {
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,10);
        GUI.DrawTexture(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,Color.white,0,8);
        key.normal.textColor=color;key.font=LocaleFonts.Body;key.wordWrap=false;key.fontSize=size;
        var content=new GUIContent(s);var inset=new Rect(r.x+4,r.y+4,r.width-8,r.height-8);
        while(key.fontSize>10&&(key.CalcSize(content).x>inset.width||key.CalcSize(content).y>inset.height))key.fontSize--;
        GUI.Label(inset,content,key);
    }
    public static void Draw(Rect target,Texture2D source) => Draw(target,Kind(source!=null?source.name:"ControlsMove"));
    public static void Draw(Rect target,int kind)
    {
        if(title==null){title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};text=new GUIStyle(title);small=new GUIStyle(title){fontStyle=FontStyle.Normal};key=new GUIStyle(title);}
        Color ink=new Color(.06f,.09f,.14f), muted=new Color(.34f,.38f,.44f);
        title.normal.textColor=ink;text.normal.textColor=ink;small.normal.textColor=muted;
        const float width=1200,height=675;
        float scale=Mathf.Min(target.width/width,target.height/height);var matrix=GUI.matrix;Color old=GUI.color,oldContent=GUI.contentColor;
        GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(target.center.x-width*scale/2,target.center.y-height*scale/2),Quaternion.identity,Vector3.one*scale);GUI.color=Color.white;GUI.contentColor=Color.white;
        UiKit.Fill(new Rect(0,0,width,height),Color.white);
        Label(new Rect(60,22,1080,65),Name(kind),title,48);
        Label(new Rect(50,88,1100,44),kind==0?L.T("같은 색 키로 같은 색 다리를 움직여요","Match each key group to its leg color"):L.T("한 번에 한 가지 힘만 사용할 수 있어요","Use one power at a time"),small,24);
        if(kind>0)
        {
            Key(new Rect(395,145,72,72),(kind+1).ToString(),ink,50);
            Label(new Rect(488,145,320,72),L.T("켜기 / 끄기","ON / OFF"),title,37);
        }
        if(kind==3)
        {
            // Complete snowman silhouette, no detached shapes above its head.
            Circle(new Vector2(600,415),75,ink);Circle(new Vector2(600,319),49,ink);
            Circle(new Vector2(600,403),6,Color.white);Circle(new Vector2(600,432),6,Color.white);
            Label(new Rect(180,515,840,65),L.T("얼음이 되어 미끄러져요","Turn to ice and slide"),text,35);
        }
        else for(int i=0;i<4;i++)
        {
            float x=35+i*295;Color color=LegPalette.BySlot[i];
            Label(new Rect(x,248,275,46),LegLayout.Name(i),small,23);
            if(kind==0)
            {
                var labels=moves[i];float cx=x+137.5f;const float size=58,gap=6;
                Key(new Rect(cx-size/2,308,size,size),labels[0],color);
                for(int n=1;n<4;n++)Key(new Rect(cx-size*1.5f-gap+(n-1)*(size+gap),372,size,size),labels[n],color);
            }
            else
            {
                Key(new Rect(x+86,306,102,102),high[i],color,56);
                Label(new Rect(x+5,417,265,45),kind==1?L.T("늘리기","Stretch"):kind==2?L.T("부착","Attach"):L.T("분사","Thrust"),text,30);
                if(kind==1||kind==2)
                {
                    Key(new Rect(x+28,486,57,57),low[i],muted,32);
                    Label(new Rect(x+96,486,158,57),kind==1?L.T("줄이기","Shrink"):L.T("부착 취소","Release"),small,23);
                }
            }
        }
        Label(new Rect(45,602,1110,44),kind==0?L.T("마우스로 카메라 회전 · 휠로 확대/축소","Mouse: camera · Wheel: zoom"):L.T("같은 색 키로 같은 색 다리를 움직여요","Match each key group to its leg color"),small,24);
        GUI.matrix=matrix;GUI.color=old;GUI.contentColor=oldContent;
    }
    static void Circle(Vector2 center,float radius,Color color) => GUI.DrawTexture(new Rect(center.x-radius,center.y-radius,radius*2,radius*2),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
}
