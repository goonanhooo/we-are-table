using System.Collections;
using UnityEngine;

/// <summary>The table remembers how to move before the interactive movement lessons.
/// ★2026-11-01 다리별 카메라 소개를 없애고 "내가 어떻게 움직였더라...?" 다음에 큰 조작법(키보드) 그림을 바로 띄운다.</summary>
public class HallwayLegIntroduction : MonoBehaviour
{
    public int FocusSlot { get; private set; } = -1;
    public bool Running { get; private set; }
    public string Thought { get; private set; } = "";
    public float Alpha { get; private set; }
    Camera cam; Transform table; LegColorXray xray;
    ArtLegController[] art; LegController[] hinged; bool[] artEnabled, hingedEnabled;
    float originalFov;

    public IEnumerator Play(Transform body)
    {
        cam=GetComponent<Camera>();table=body;Running=true;originalFov=cam.fieldOfView;
        ThoughtBubbleUI.Prewarm();
        xray=body.root.GetComponentInChildren<LegColorXray>(true);
        art=body.root.GetComponentsInChildren<ArtLegController>(true);artEnabled=new bool[art.Length];
        hinged=body.root.GetComponentsInChildren<LegController>(true);hingedEnabled=new bool[hinged.Length];
        for(int i=0;i<art.Length;i++){artEnabled[i]=art[i].enabled;art[i].enabled=false;}
        for(int i=0;i<hinged.Length;i++){hingedEnabled[i]=hinged[i].enabled;hinged[i].enabled=false;}
        if(xray!=null)xray.SetTutorialMask(0);
        yield return Say(L.T("책상에게 가고 싶다..", "I want to reach that table..", "あの机のところへ行きたい..", "好想去那张桌子那里.."),3.5f);
        if(!CheatMode.Active)
        {
            yield return Say(L.T("내가 어떻게 움직였더라...?", "How did I move again...?", "どうやって動くんだっけ...?", "我是怎么动的来着...？"),3f);
            // 큰 조작법 그림(PauseMenu 조작법 화면) — 닫을 때까지 기다림
            var pause=body.root.GetComponentInChildren<PauseMenu>(true);
            if(pause!=null){pause.OpenControls();while(pause.IsOpen)yield return null;}
            yield return Say(L.T("의자에게 가볼까?", "Shall we go to the chair?", "椅子のところへ行こうか?", "去椅子那边吧？"),2.8f);
        }
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
        var point=table.position-cam.transform.right*1.15f+Vector3.down*.4f;
        if(ThoughtBubbleUI.GuiPoint(cam,point,out var anchor))
        {
            rect=ThoughtBubbleUI.FitTail(rect,anchor);
            ThoughtBubbleUI.DrawPointingTail(rect,anchor,Alpha);
        }
        ThoughtBubbleUI.DrawWideBubble(rect,Alpha,L.Text(Thought),54);
        ThoughtBubbleUI.End();
    }
}
