using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ★2026-07-14 멀티 다리 닉네임 태그 — 테이블 루트(ArtSceneReset 옆)에 부착.
/// 멀티 세션 중 주인이 있는 다리 위에 P1/P2… 빌보드 라벨을 다리 색(LegPalette)으로 띄운다.
/// 싱글/로컬 플레이에서는 아무것도 안 보임. 다리 탐색은 LegColorXray와 같은 방식(자식→전역).
/// </summary>
public class LegNameTags : MonoBehaviour
{
    [Tooltip("다리 피벗 기준 라벨 높이(m)")] public float height = 0.55f;

    // 설정 메뉴 '닉네임 표시' — 끄면 모든 테이블의 이름표가 사라진다(저장됨, 기본 켜짐)
    const string PreferenceKey = "nameTags";
    static int visible = -1;
    public static bool Visible
    {
        get { if (visible < 0) visible = PlayerPrefs.GetInt(PreferenceKey, 1); return visible != 0; }
        set { visible = value ? 1 : 0; PlayerPrefs.SetInt(PreferenceKey, visible); PlayerPrefs.Save(); }
    }

    // 로비 슬롯 순서(0=WASD 뒤왼, 1=TFGH 뒤오, 2=IJKL 앞왼, 3=Arrows 앞오)
    static readonly string[] LegBySlot = LegLayout.MeshNames;

    readonly Transform[] legs = new Transform[4];
    readonly TextMesh[] tags = new TextMesh[4];
    NetLegLink link;
    Camera cam;
    float linkFindAt;

    void Start()
    {
        var font = Resources.Load<Font>("Fonts/CJK/NotoSansCJKjp-Regular");
        for (int i = 0; i < 4; i++)
        {
            legs[i] = FindLeg(LegBySlot[i]);
            if (legs[i] == null) continue;

            var go = new GameObject("NameTag_" + LegBySlot[i]);
            go.transform.SetParent(transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 72;
            tm.characterSize = 0.016f;
            tm.fontStyle = FontStyle.Bold;
            tm.richText = false;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = i < LegPalette.BySlot.Length ? LegPalette.BySlot[i] : Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tags[i] = tm;
            go.SetActive(false);
        }
    }

    void LateUpdate()
    {
        var nm = NetworkManager.Singleton;
        bool inSession = nm != null && nm.IsListening;
        if (inSession && link == null && Time.unscaledTime > linkFindAt)
        {
            link = FindAnyObjectByType<NetLegLink>();
            linkFindAt = Time.unscaledTime + 2f;
        }
        if (cam == null) cam = Camera.main;
        bool shown = Visible;

        for (int i = 0; i < 4; i++)
        {
            if (tags[i] == null) continue;
            long owner = inSession && link != null ? link.OwnerOf(i) : -2;
            bool show = shown && inSession && owner != -2 && link.Started;
            if (tags[i].gameObject.activeSelf != show) tags[i].gameObject.SetActive(show);
            if (!show || legs[i] == null) continue;

            tags[i].text = link.NameOfLeg(i);
            var t = tags[i].transform;
            t.position = legs[i].position + Vector3.up * height;
            if (cam != null)
                t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);   // 카메라를 향하는 빌보드
        }
    }

    Transform FindLeg(string n)
    {
        foreach (var t in transform.root.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        var go = GameObject.Find(n);
        return go != null ? go.transform : null;
    }
}
