using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 편지를 화면 가득 띄워 읽는 오버레이. LetterOverlay.Show(id)로 호출(어느 씬에서든).
/// 열려 있는 동안 게임 일시정지(timeScale 0) + 커서 표시. 클릭 또는 ESC로 닫기.
/// PauseMenu는 IsOpen을 확인해 ESC 충돌을 피한다.
/// </summary>
public class LetterOverlay : MonoBehaviour
{
    static LetterOverlay inst;
    public static bool IsOpen => inst != null && inst.tex != null;

    Texture2D tex;
    string title;
    int openedFrame;
    float prevTimeScale = 1f;
    GUIStyle titleStyle, hintStyle;

    public static void Show(string letterId)
    {
        if (inst == null)
        {
            var go = new GameObject("LetterOverlay");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<LetterOverlay>();
        }
        inst.tex = LetterSystem.LoadImage(letterId);
        inst.title = LetterSystem.TitleOf(letterId);
        inst.openedFrame = Time.frameCount;
        inst.prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (inst.tex == null)
            Debug.LogWarning("[LetterOverlay] Resources/Letters/" + letterId + " 텍스처 없음");
    }

    void Close()
    {
        tex = null;
        Time.timeScale = prevTimeScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (tex == null) return;
        if (Time.frameCount <= openedFrame + 1) return;   // 여는 클릭이 곧바로 닫지 않게
        var kb = Keyboard.current; var m = Mouse.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
            (m != null && m.leftButton.wasPressedThisFrame)) Close();
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (tex == null) return;
        if (titleStyle == null)
        {
            Color ink = new Color(0.08f, 0.08f, 0.08f, 1f);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = ink;
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        }
        // 흰 배경(통일 스타일 — 편지가 종이 위에 놓인 느낌)
        Color pc = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.94f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = pc;

        GUI.Label(new Rect(0, 14f, Screen.width, 40f), title, titleStyle);

        // 편지 이미지(비율 유지, 화면에 맞춤)
        float top = 64f, bottom = 54f;
        float availH = Screen.height - top - bottom;
        float availW = Screen.width * 0.9f;
        float ar = (float)tex.width / tex.height;
        float h = availH, w = h * ar;
        if (w > availW) { w = availW; h = w / ar; }
        GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, top + (availH - h) * 0.5f, w, h), tex, ScaleMode.ScaleToFit, true);

        GUI.Label(new Rect(0, Screen.height - 44f, Screen.width, 30f), "클릭 또는 ESC 로 닫기", hintStyle);
    }
}
