using UnityEngine;

/// <summary>
/// ★ 모험 맵(Adventure) 능력 토템 — 테이블이 가까이 오면 능력 하나를 해금/발동한다.
/// SnowmanDummy(눈사람 접촉 해금) 패턴의 일반화: kind로 기린/얼음/오징어 선택.
/// - Giraffe: GiraffeMode.locked=false (켜기는 플레이어가 2번 키)
/// - Ice:     IcePower.unlocked=true (켜기는 4번 키)
/// - Squid:   SquidMode unlocked+active 즉시 발동(무중력) + 기린 잠금(E/Y/O/] 키 충돌 방지) + 얼음 해제
/// 획득 순간 상단에 UiKit 칩 안내문을 messageTime초 표시. "Orb" 자식은 봉투처럼 bob+회전 연출.
/// </summary>
public class AbilityTotem : MonoBehaviour
{
    public enum Kind { Giraffe, Ice, Squid }
    public Kind kind;
    [Tooltip("테이블(보드)이 이 XZ 반경 안에 오면 발동")]
    public float grantRadius = 6f;
    [Tooltip("획득 시 상단 안내문(비면 표시 안 함)")]
    public string message = "";
    public float messageTime = 6f;

    Transform board;
    bool granted;
    float msgUntil;
    Transform orb;
    Vector3 orbBase;

    void Start()
    {
        var t = GameObject.Find("Table");
        if (t != null) board = t.transform;
        var o = transform.Find("Orb");
        if (o != null) { orb = o; orbBase = o.localPosition; }
    }

    void Update()
    {
        if (orb != null)
        {
            orb.localPosition = orbBase + Vector3.up * (Mathf.Sin(Time.time * 1.7f) * 0.25f);
            orb.Rotate(0f, 50f * Time.deltaTime, 0f, Space.World);
        }
        if (granted || board == null) return;
        Vector3 d = board.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > grantRadius * grantRadius) return;
        granted = true;
        msgUntil = Time.time + messageTime;
        Grant();
    }

    void Grant()
    {
        switch (kind)
        {
            case Kind.Giraffe:
                var g = FindAnyObjectByType<GiraffeMode>();
                if (g != null) { g.locked = false; GiraffeMode.Unlocked = true; AbilityHud.Acquire(g, "giraffe"); }
                break;
            case Kind.Ice:
                var ice = FindAnyObjectByType<IcePower>();
                if (ice != null) { ice.unlocked = true; AbilityHud.Acquire(ice, "ice"); }
                break;
            case Kind.Squid:
                var sq = FindAnyObjectByType<SquidMode>();
                if (sq != null) { sq.unlocked = true; sq.active = true; AbilityHud.Acquire(sq, "squid"); }
                var g2 = FindAnyObjectByType<GiraffeMode>();
                if (g2 != null) { g2.active = false; g2.locked = true; }
                var ice2 = FindAnyObjectByType<IcePower>();
                if (ice2 != null && ice2.active) { ice2.active = false; ice2.Apply(); }
                break;
        }
        Debug.Log("[AbilityTotem] granted: " + kind);
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (string.IsNullOrEmpty(message) || Time.time >= msgUntil) return;
        var r = UiKit.Chip(Screen.width * 0.5f, 96f, 720f, 62f);
        GUI.Label(r, message, UiKit.Btn);
    }
}
