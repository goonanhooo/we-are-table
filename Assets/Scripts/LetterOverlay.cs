using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Localized letter text and photograph; online readers never pause shared physics.</summary>
public class LetterOverlay : MonoBehaviour
{
    static LetterOverlay inst;
    public static bool IsOpen => LetterSystem.Enabled && inst != null && !string.IsNullOrEmpty(inst.letterId);
    string letterId;
    Texture2D photo;
    int openedFrame;
    float prevTimeScale=1;
    bool pausedTime;
    GUIStyle titleStyle,bodyStyle,hintStyle;
    public static void Show(string id)
    {
        if (!LetterSystem.Enabled) return;
        if(inst==null){var go=new GameObject("LetterOverlay");DontDestroyOnLoad(go);inst=go.AddComponent<LetterOverlay>();}
        if(!IsOpen)inst.prevTimeScale=Time.timeScale;
        inst.letterId=id;inst.photo=LetterSystem.LoadImage(id);inst.openedFrame=Time.frameCount;
        var nm=Unity.Netcode.NetworkManager.Singleton;
        inst.pausedTime=nm==null||!nm.IsListening;
        if(inst.pausedTime)Time.timeScale=0;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    void Close(){letterId=null;if(pausedTime)Time.timeScale=prevTimeScale;pausedTime=false;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
    void Update()
    {
        if (!LetterSystem.Enabled && !string.IsNullOrEmpty(letterId)) { Close(); return; }
        if(!IsOpen||Time.frameCount<=openedFrame+1)return;
        var k=Keyboard.current;var m=Mouse.current;
        if((k!=null&&k.escapeKey.wasPressedThisFrame)||(m!=null&&m.leftButton.wasPressedThisFrame))Close();
    }
    void OnGUI()
    {
        if(!IsOpen)return;GUI.depth=-300;GUI.color=Color.white;GUI.contentColor=Color.white;LocaleFonts.Apply();
        if(titleStyle==null){titleStyle=HandUI.Style(28,HandUI.Ink);bodyStyle=HandUI.Style(28,new Color(.27f,.22f,.17f));bodyStyle.wordWrap=true;hintStyle=HandUI.Style(18,HandUI.InkSoft);}
        UiKit.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.97f,.96f,.92f,1f));
        GUI.Label(new Rect(0,8,Screen.width,45),LetterSystem.TitleOf(letterId),titleStyle);
        float h=Screen.height-120,w=Mathf.Min(Screen.width*.9f,h*.86f);
        Rect paper=new Rect((Screen.width-w)/2,60,w,h);
        UiKit.Fill(paper,new Color(.96f,.935f,.865f));
        Color border=new Color(.70f,.65f,.54f);float pad=12;
        UiKit.Fill(new Rect(paper.x+pad,paper.y+pad,w-2*pad,1),border);UiKit.Fill(new Rect(paper.x+pad,paper.yMax-pad,w-2*pad,1),border);
        UiKit.Fill(new Rect(paper.x+pad,paper.y+pad,1,h-2*pad),border);UiKit.Fill(new Rect(paper.xMax-pad,paper.y+pad,1,h-2*pad),border);
        bool withPhoto=(letterId=="Letter1"||letterId=="Letter2")&&photo!=null;
        var content=new Rect(paper.x+w*.1f,paper.y+h*.065f,w*.8f,h*(withPhoto?.38f:.76f));
        ThoughtBubbleUI.FittedLabel(content,LetterContents.Body(letterId),bodyStyle,Mathf.RoundToInt(Mathf.Clamp(w*.048f,18,34)),13);
        if(withPhoto)
        {
            // Keep the original photograph as a texture region; the original text is outside the UV region.
            Rect image=new Rect(paper.x+w*.13f,paper.y+h*.47f,w*.74f,h*.44f);
            GUI.DrawTextureWithTexCoords(image,photo,new Rect(.17f,.115f,.63f,.52f));
        }
        var sign=new Rect(paper.x+w*.12f,paper.yMax-h*.10f,w*.76f,h*.055f);
        if(withPhoto)sign.y=paper.y+h*.405f;
        ThoughtBubbleUI.FittedLabel(sign,L.T("— 의자가 —","— The chair —"),bodyStyle,Mathf.RoundToInt(w*.043f));
        GUI.Label(new Rect(0,Screen.height-45,Screen.width,30),L.T("클릭 또는 ESC로 닫기","Click or press ESC to close"),hintStyle);
    }
    void OnDestroy(){if(pausedTime)Time.timeScale=prevTimeScale;}
}
