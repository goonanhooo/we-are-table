using System.Collections;
using UnityEngine;

/// <summary>The table thinks about each leg before the interactive movement lessons.</summary>
public class HallwayLegIntroduction : MonoBehaviour
{
    public int FocusSlot { get; private set; } = -1;
    public bool Running { get; private set; }
    public string Thought { get; private set; } = "";
    public float Alpha { get; private set; }
    Camera cam; Transform table, focus; LegColorXray xray;
    ArtLegController[] art; LegController[] hinged; bool[] artEnabled, hingedEnabled;
    float originalFov;
    static GUIStyle legHeading, legCaption, keyLabel;
    // Physical keyboard order: one key above the left / down / right row.
    static readonly string[][] Keycaps = {
        new[] { "W", "A", "S", "D" },
        new[] { "T", "F", "G", "H" },
        new[] { "I", "J", "K", "L" },
        new[] { "[", ";", "'", "Enter" }
    };

    public IEnumerator Play(Transform body)
    {
        cam=GetComponent<Camera>();table=body;Running=true;originalFov=cam.fieldOfView;
        ThoughtBubbleUI.Prewarm();
        xray=body.root.GetComponentInChildren<LegColorXray>(true);
        art=body.root.GetComponentsInChildren<ArtLegController>(true);artEnabled=new bool[art.Length];
        hinged=body.root.GetComponentsInChildren<LegController>(true);hingedEnabled=new bool[hinged.Length];
        for(int i=0;i<art.Length;i++){artEnabled[i]=art[i].enabled;art[i].enabled=false;}
        for(int i=0;i<hinged.Length;i++){hingedEnabled[i]=hinged[i].enabled;hinged[i].enabled=false;}
        Vector3 home=cam.transform.position;Quaternion homeRot=cam.transform.rotation;
        if(xray!=null)xray.SetTutorialMask(0);
        focus=body;
        yield return Say(L.T("책상에게 가고 싶다..", "I want to reach that table.."),3.5f);
        for(int slot=0;slot<4 && !CheatMode.Active;slot++)
        {
            FocusSlot=slot;focus=LegLayout.Find(body.root,slot);
            if(focus==null)continue;
            if(xray!=null)xray.SetTutorialMask(1<<slot);
            Vector3 target=LegLayout.Center(focus)+Vector3.up*.1f;
            // Four corner views; the highlighted leg stays on the right of the composition.
            float side=slot%2==0?-1f:1f,front=slot<2?-1f:1f;
            // Keep the lens inside the corridor and use an oblique view: the distant
            // chair is offset from the leg, rather than lining up behind its outline.
            Vector3 pos=body.position+body.TransformDirection(new Vector3(side*2.05f,.0f,front*3.05f));
            pos.y=Mathf.Max(.7f,target.y+.5f);
            Quaternion aim=Quaternion.LookRotation(target-pos,Vector3.up);
            Quaternion rotation=Quaternion.LookRotation(target-(aim*Vector3.right)*.48f-pos,Vector3.up);
            yield return Move(pos,rotation,48f,1.65f);
            yield return Say(L.F("내 {0}는\n{1} 네 방향키로 움직이고.", "My {0} moves\nwith the four {1} keys.", LegLayout.Name(slot), LegLayout.Keys(slot)),3.6f);
        }
        FocusSlot=-1;focus=body;if(xray!=null)xray.SetTutorialMask(0);
        yield return Move(home,homeRot,originalFov,2f);
        if(!CheatMode.Active)yield return Say(L.T("의자에게 가볼까?", "Shall we go to the chair?"),2.8f);
        Restore();
    }
    IEnumerator Say(string line,float seconds)
    {
        Thought=line;float t=0;
        while(t<seconds && !CheatMode.Active)
        {
            if(!LetterOverlay.IsOpen)t+=Time.deltaTime;
            Alpha=Mathf.Min(Mathf.Clamp01(t/.28f),Mathf.Clamp01((seconds-t)/.28f));
            yield return null;
        }
        Alpha=0;
    }
    IEnumerator Move(Vector3 p,Quaternion r,float fov,float seconds)
    {
        var p0=cam.transform.position;var r0=cam.transform.rotation;float f0=cam.fieldOfView,t=0;
        while(t<seconds)
        {
            if(!LetterOverlay.IsOpen)t+=Time.deltaTime;
            float u=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/seconds));
            cam.transform.SetPositionAndRotation(OrbitPosition(p0,p,u),HallwayStage.BlendCameraRotation(r0,r,u));
            cam.fieldOfView=Mathf.Lerp(f0,fov,u);yield return null;
        }
        cam.transform.SetPositionAndRotation(p,r);cam.fieldOfView=fov;
    }
    Vector3 OrbitPosition(Vector3 from,Vector3 to,float u)
    {
        Vector3 a=table.InverseTransformPoint(from),b=table.InverseTransformPoint(to);
        // Elliptical orbit fits the narrow hallway. Polar interpolation never cuts
        // through the table when moving between opposing legs.
        const float rx=2.05f,rz=3.55f;
        float a0=Mathf.Atan2(a.x/rx,a.z/rz)*Mathf.Rad2Deg,a1=Mathf.Atan2(b.x/rx,b.z/rz)*Mathf.Rad2Deg;
        float angle=(a0+Mathf.DeltaAngle(a0,a1)*u)*Mathf.Deg2Rad;
        float radius=Mathf.Lerp(new Vector2(a.x/rx,a.z/rz).magnitude,new Vector2(b.x/rx,b.z/rz).magnitude,u);
        Vector3 local=new Vector3(Mathf.Clamp(Mathf.Sin(angle)*rx*radius,-2.32f,2.32f),Mathf.Lerp(a.y,b.y,u),Mathf.Cos(angle)*rz*radius);
        if(u<=0)return from;if(u>=1)return to;
        return table.TransformPoint(local);
    }
    void Restore()
    {
        if(art!=null)for(int i=0;i<art.Length;i++)if(art[i]!=null)art[i].enabled=artEnabled[i];
        if(hinged!=null)for(int i=0;i<hinged.Length;i++)if(hinged[i]!=null)hinged[i].enabled=hingedEnabled[i];
        if(cam!=null)cam.fieldOfView=originalFov;
        if(xray!=null)xray.SetTutorialMask(0);
        Running=false;Alpha=0;
    }
    void OnDisable(){if(Running)Restore();}
    void OnGUI()
    {
        if(!Running||Alpha<=0||CheatMode.Active||Time.timeScale<=0||LetterOverlay.IsOpen||cam==null)return;
        ThoughtBubbleUI.Begin();
        var rect=new Rect(100,165,780,ThoughtBubbleUI.WideHeight(780));
        var point=focus!=null?LegLayout.Center(focus):table.position;
        if(FocusSlot<0)point=table.position-cam.transform.right*1.15f+Vector3.down*.4f;
        if(ThoughtBubbleUI.GuiPoint(cam,point,out var anchor))
        {
            if(FocusSlot>=0&&focus!=null)
            {
                var bounds=ThoughtTailLayout.TargetBounds(cam,focus);anchor=bounds.center;
                rect=ThoughtBubbleUI.FitTail(rect,bounds);
            }
            else rect=ThoughtBubbleUI.FitTail(rect,anchor);
            ThoughtBubbleUI.DrawPointingTail(rect,anchor,Alpha);
        }
        if(FocusSlot>=0)DrawLegBubble(rect,FocusSlot,Alpha);
        else ThoughtBubbleUI.DrawWideBubble(rect,Alpha,L.Text(Thought),54);
        ThoughtBubbleUI.End();
    }

    public static void DrawLegBubble(Rect rect,int slot,float alpha)
    {
        if(Event.current.type!=EventType.Repaint||alpha<=0f||slot<0||slot>=Keycaps.Length)return;
        ThoughtBubbleUI.DrawWideBubble(rect,alpha);
        if(legHeading==null)
        {
            legHeading=HandUI.Style(32,Color.black,bold:true);legHeading.wordWrap=true;
            legCaption=HandUI.Style(28,Color.black,bold:true);legCaption.wordWrap=true;
            keyLabel=HandUI.Style(30,Color.black,bold:true);keyLabel.wordWrap=false;
        }
        // Reserve separate rows inside the cloud's safe text area, including at smaller resolutions.
        Rect content=ThoughtBubbleUI.WideContent(rect);
        const float headingHeight=34f,captionHeight=30f,rowGap=5f,keyGap=6f;
        float size=Mathf.Min(48f,(content.height-headingHeight-captionHeight-rowGap*2f-keyGap)*.5f);
        float top=content.y+headingHeight+rowGap;
        var old=GUI.color;GUI.color=new Color(1,1,1,alpha);
        ThoughtBubbleUI.FittedLabel(new Rect(content.x,content.y,content.width,headingHeight),
            L.F("내 {0}는", "My {0}", LegLayout.Name(slot)),legHeading,32);
        keyLabel.font=LocaleFonts.Body;
        for(int i=0;i<4;i++)
        {
            float x=content.center.x-size*.5f+(i==0?0:(i-2)*(size+keyGap));
            var keyRect=new Rect(x,top+(i==0?0:size+keyGap),size,size);
            Color tint=Color.Lerp(Color.white,LegPalette.BySlot[slot],.16f);tint.a=alpha;
            UiKit.Fill(keyRect,new Color(0,0,0,alpha));
            UiKit.Fill(new Rect(x+1.5f,keyRect.y+1.5f,size-3f,size-3f),tint);
            Color stripe=LegPalette.BySlot[slot];stripe.a=alpha;
            UiKit.Fill(new Rect(x+3f,keyRect.yMax-5f,size-6f,3f),stripe);
            keyLabel.fontSize=i==3&&slot==3?14:30;
            GUI.Label(keyRect,Keycaps[slot][i],keyLabel);
        }
        ThoughtBubbleUI.FittedLabel(new Rect(content.x,content.yMax-captionHeight,content.width,captionHeight),
            L.T("이 네 방향키로 움직이고.", "moves with these four keys."),legCaption,28);
        GUI.color=old;
    }
}
