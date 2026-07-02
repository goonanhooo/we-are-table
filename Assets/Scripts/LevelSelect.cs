using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시작 화면에서 Play 표지판을 누르면 열리는 '레벨 선택' 오버레이.
/// 씬 썸네일(Resources/Thumbs/*)을 작게 띄우고, 클릭하면 그 씬으로 이동.
/// EventSystem 없이 OnGUI로 그리고 클릭 처리(표지판 클릭 방식과 일관).
/// </summary>
public class LevelSelect : MonoBehaviour
{
    [System.Serializable]
    public class Level { public string label; public string scene; public string thumb; }

    public Level[] levels =
    {
        new Level { label = "시작",      scene = "Hallway",     thumb = "Thumbs/hallway" },
        new Level { label = "정글",      scene = "Jungle",      thumb = "Thumbs/jungle"  },
        new Level { label = "눈 덮인 산", scene = "IceMountain", thumb = "Thumbs/ice"     },
    };

    public bool Showing { get; private set; }

    Texture2D[] thumbs;
    GUIStyle labelStyle, titleStyle, backStyle;
    Texture2D white;

    void Start()
    {
        thumbs = new Texture2D[levels.Length];
        for (int i = 0; i < levels.Length; i++)
            thumbs[i] = Resources.Load<Texture2D>(levels[i].thumb);
        white = Texture2D.whiteTexture;
    }

    public void Open() { Showing = true; }
    public void Close() { Showing = false; }

    void EnsureStyles()
    {
        if (labelStyle != null) return;
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        labelStyle.normal.textColor = Color.white;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = Color.white;
        backStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
    }

    void Rect2(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = p; }

    void OnGUI()
    {
        if (!Showing) return;
        EnsureStyles();

        // 어둡게 깔기
        Rect2(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

        // ESC로 닫기
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { Close(); return; }

        GUI.Label(new Rect(0, Screen.height * 0.5f - 175f, Screen.width, 50f), "스테이지 선택", titleStyle);

        int n = levels.Length;
        float gap = 36f;
        float maxW = (Screen.width - gap * (n + 1));
        float tw = Mathf.Min(300f, maxW / n);
        float th = tw * 0.625f;            // 320x200 비율
        float totalW = n * tw + (n - 1) * gap;
        float x0 = (Screen.width - totalW) * 0.5f;
        float y = (Screen.height - th) * 0.5f - 10f;

        for (int i = 0; i < n; i++)
        {
            float x = x0 + i * (tw + gap);
            Rect thumbR = new Rect(x, y, tw, th);
            bool hover = thumbR.Contains(Event.current.mousePosition);

            // 테두리(호버 시 강조)
            Rect2(new Rect(x - 4, y - 4, tw + 8, th + 8), hover ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.85f));
            // 썸네일
            if (thumbs[i] != null) GUI.DrawTexture(thumbR, thumbs[i], ScaleMode.ScaleAndCrop);
            else Rect2(thumbR, new Color(0.3f, 0.3f, 0.35f, 1f));
            // 라벨
            GUI.Label(new Rect(x, y + th + 8f, tw, 30f), levels[i].label, labelStyle);

            // 클릭 → 씬 로드
            if (GUI.Button(thumbR, GUIContent.none, GUIStyle.none))
            {
                Time.timeScale = 1f;
                NetScene.Load(levels[i].scene);   // 멀티: 호스트가 전원 씬 전환
            }
        }

        // 뒤로
        if (GUI.Button(new Rect(Screen.width * 0.5f - 60f, y + th + 56f, 120f, 40f), "뒤로", backStyle)) Close();
    }
}
