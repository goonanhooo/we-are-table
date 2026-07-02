using UnityEngine;

/// <summary>
/// 수집형 '편지' 시스템의 공용 저장소/로더.
/// - 수집 여부는 PlayerPrefs("letter_&lt;id&gt;")로 영구 저장(세션·씬 넘어 유지).
/// - 이미지는 Resources/Letters/&lt;id&gt;.png 에서 로드 — 사용자가 같은 이름으로 파일만 교체하면 됨.
/// - 편지 목록(Ids/Titles)에 새 편지를 추가하면 메뉴에 자동 반영.
/// </summary>
public static class LetterSystem
{
    /// <summary>알려진 편지 id(=Resources/Letters/ 안의 텍스처 이름)와 메뉴 표시 제목.</summary>
    public static readonly string[] Ids = { "Letter1", "Letter2" };
    public static readonly string[] Titles = { "편지 1 — 정글", "편지 2 — 빙산" };

    public static bool IsCollected(string id) => PlayerPrefs.GetInt("letter_" + id, 0) == 1;

    public static void Collect(string id)
    {
        PlayerPrefs.SetInt("letter_" + id, 1);
        PlayerPrefs.Save();
    }

    public static Texture2D LoadImage(string id) => Resources.Load<Texture2D>("Letters/" + id);

    public static string TitleOf(string id)
    {
        for (int i = 0; i < Ids.Length; i++) if (Ids[i] == id) return Titles[i];
        return id;
    }
}
