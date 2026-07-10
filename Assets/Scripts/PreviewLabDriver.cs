using UnityEngine;

/// <summary>
/// ★ PreviewLab 씬 드라이버(2026-07-10) — 맵 선택 경험 전체를 독립 씬에서 구동하는 쇼케이스.
/// StartScreen과 무관하게: 미니어처 RT 타일 그리드 + 호버=3D 프리뷰 등장/퇴장 + 클릭=확정 연출.
/// 클릭 확정은 실제 씬 로드 대신 로그(랩 씬이므로) — loadScenes=true면 실제 로드.
/// </summary>
public class PreviewLabDriver : MonoBehaviour
{
    public MapMiniatures miniatures;
    public MapSelectPreview preview;
    [Tooltip("클릭 확정 시 실제 씬 로드(기본 꺼짐 — 랩에서는 로그만)")]
    public bool loadScenes = false;

    static readonly string[] Names = { "시작", "정글", "눈 덮인 산", "우주", "트랙", "달", "모험", "등반", "경주", "축구" };
    static readonly string[] Scenes = { "Hallway", "Jungle", "IceMountain", "Space", "Track", "Moon", "Adventure", "Climb", "Race", "Soccer" };

    int hoverIdx = -1;
    bool confirming;
    GUIStyle label;

    void OnGUI()
    {
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = new Color(0.08f, 0.08f, 0.08f);
        }
        if (miniatures != null) miniatures.MarkVisible();

        // 하단 타일 그리드(라이브 RT) — 호버=프리뷰, 클릭=확정
        int n = Names.Length;
        float tw = Mathf.Min(150f, (Screen.width - 40f) / n - 10f);
        float th = tw * 0.625f;
        float total = n * (tw + 10f) - 10f;
        float x0 = (Screen.width - total) * 0.5f;
        float y = Screen.height - th - 58f;

        for (int i = 0; i < n; i++)
        {
            float x = x0 + i * (tw + 10f);
            var r = new Rect(x, y, tw, th);
            bool hover = r.Contains(Event.current.mousePosition);
            UiKit.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), hover ? Color.black : new Color(0f, 0f, 0f, 0.55f));
            var rt = miniatures != null ? miniatures.RT(i) : null;
            if (rt != null) GUI.DrawTexture(r, rt, ScaleMode.ScaleAndCrop);
            else UiKit.Fill(r, new Color(0.9f, 0.9f, 0.9f));
            GUI.Label(new Rect(x, y + th + 4f, tw, 22f), Names[i], label);

            if (hover && preview != null && !confirming && i != hoverIdx)
            {
                int dir = (hoverIdx >= 0 && i < hoverIdx) ? -1 : 1;
                preview.Show(i, dir);
                hoverIdx = i;
            }
            if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !confirming && preview != null && !preview.Busy)
            {
                confirming = true;
                int captured = i;
                preview.Show(i, 1);
                preview.Confirm(() =>
                {
                    confirming = false;
                    Debug.Log("[PreviewLab] 확정: " + Names[captured] + " (" + Scenes[captured] + ")");
                    if (loadScenes) UnityEngine.SceneManagement.SceneManager.LoadScene(Scenes[captured]);
                    else if (preview != null) preview.Show(captured, 1);   // 랩: 확정 후 다시 보여줌
                });
            }
        }
    }
}
