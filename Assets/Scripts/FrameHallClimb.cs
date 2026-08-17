using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-08-17 액자 맵 도입부 개편(사용자 요청): 홀 뒤 수직 샤프트 바닥에서 스폰 —
/// 테니스공 애벌레 더미를 만나면 '테니스공의 힘'(TennisMode, 끈끈이 공 벽타기)을 받아
/// 등반면(홀 뒷벽 하단)을 올라 개구부로 홀에 진입한다.
/// 홀에 들어서면 테니스 힘 회수 + 기린 해금(화분 퍼즐용 — 신축 키가 붙기 키와 겹쳐 동시 사용 불가).
/// 애벌레는 주기적으로 더미에서 나와 벽을 타고 올라가며 길을 보여준다(TennisCaterpillar 재사용).
/// </summary>
public class FrameHallClimb : MonoBehaviour
{
    [Header("배치(씬 값)")]
    public Vector3 pilePos = new Vector3(5f, -12.35f, -32f);   // 애벌레 더미(샤프트 바닥 구석)
    public float meetRadius = 4.5f;                             // 이 거리 안에 오면 힘 부여
    public float hallZ = -21f;                                  // 이 z를 넘으면 홀(테니스 회수+기린 해금)
    public float shaftZ = -22.5f;                               // 이 z보다 뒤면 샤프트(테니스 재부여)
    public float waveEvery = 7f;                                // 애벌레 시범 등반 간격(초)

    Transform table;
    TennisMode tennis;
    GiraffeMode giraffe;
    bool met;            // 애벌레를 만났는가(힘 최초 부여)
    bool inHall;
    float hintUntil, hint2Until;
    GUIStyle txt;

    void Start()
    {
        var b = GameObject.Find("Table");
        if (b != null) table = b.transform;
        tennis = Object.FindAnyObjectByType<TennisMode>();
        giraffe = Object.FindAnyObjectByType<GiraffeMode>();
        if (giraffe != null) giraffe.locked = true;   // 샤프트 구간: 기린 잠금(키 충돌)
        BuildPile();
        StartCoroutine(WaveLoop());
    }

    /// <summary>더미: 테니스공 무더기(시각 전용) — 애벌레들이 사는 곳.</summary>
    void BuildPile()
    {
        var mat = tennis != null ? tennis.ballMat : null;
        var root = new GameObject("BallPile");
        root.transform.position = pilePos;
        Random.InitState(20260817);
        for (int i = 0; i < 14; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(s.GetComponent<Collider>());
            s.name = "PileBall_" + i;
            s.transform.SetParent(root.transform, false);
            Vector2 c = Random.insideUnitCircle * 0.75f;
            s.transform.localPosition = new Vector3(c.x, 0.17f + (i / 7) * 0.26f, c.y);
            s.transform.localScale = Vector3.one * 0.34f;
            var r = s.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
        }
    }

    /// <summary>애벌레 시범: 더미 → 벽 밑 → 등반면을 타고 올라 개구부 너머로(길 안내 연출).</summary>
    IEnumerator WaveLoop()
    {
        var wait = new WaitForSeconds(waveEvery);
        while (true)
        {
            yield return wait;
            if (inHall) continue;   // 홀에 들어갔으면 그만 보냄
            var mat = tennis != null ? tennis.ballMat : null;
            float wallZ = -22.45f;   // 등반면(샤프트 쪽 표면) 바로 앞
            float lat = Random.Range(-2.5f, 2.5f);
            var path = new Vector3[]
            {
                pilePos + Vector3.down * 0.3f,
                pilePos + Vector3.up * 0.17f,
                new Vector3(lat, pilePos.y + 0.4f, -24.5f),
                new Vector3(lat, pilePos.y + 0.6f, wallZ),
                new Vector3(lat * 0.4f, -6f, wallZ),          // 벽면 수직 등반
                new Vector3(lat * 0.15f, 0.5f, wallZ),
                new Vector3(lat * 0.15f, 0.35f, -21f),        // 개구부 넘어 홀로
                new Vector3(lat * 0.15f, 0.35f, -18f),
            };
            var go = new GameObject("ClimbCaterpillar");
            go.transform.position = path[0];
            var cat = go.AddComponent<TennisCaterpillar>();
            cat.BuildAndGo(path, mat, Random.Range(1.1f, 1.6f), TennisCaterpillar.Gait.Wave);
        }
    }

    void Update()
    {
        if (table == null)
        {
            var b = GameObject.Find("Table");
            if (b != null) table = b.transform;
            return;
        }

        // ① 애벌레 더미를 만나면 테니스공의 힘 부여
        if (!met && (table.position - pilePos).sqrMagnitude < meetRadius * meetRadius)
        {
            met = true;
            if (tennis != null) { tennis.unlocked = true; tennis.active = true; }
            hintUntil = Time.time + 10f;
        }
        if (!met) return;

        // ② 홀 진입 = 테니스 회수 + 기린 해금(화분 퍼즐) / 샤프트로 떨어지면 다시 테니스
        bool nowHall = table.position.z > hallZ && table.position.y > -1.5f;
        bool nowShaft = table.position.z < shaftZ;
        if (nowHall && !inHall)
        {
            inHall = true;
            if (tennis != null) tennis.active = false;
            if (giraffe != null) giraffe.locked = false;
            hint2Until = Time.time + 7f;
        }
        else if (nowShaft && inHall)
        {
            inHall = false;
            if (tennis != null) tennis.active = true;
            if (giraffe != null) giraffe.locked = true;
        }
    }

    // ── 손글씨 힌트(로비 테마 — HallwayTutorial과 동일 룩) ──
    void OnGUI()
    {
        if (Time.timeScale == 0f) return;
        string s = null;
        float until = 0f;
        if (Time.time < hintUntil)
        {
            s = L.T("애벌레들이 테니스공의 힘을 나눠줬어요!\n신축 키(E·Y·O·])로 공을 벽에 붙이고, 반대 키(Q·R·U·P)로 떼요\n다리를 번갈아 붙이며 벽을 올라가세요!",
                    "the caterpillars shared their tennis-ball power!\nstick a ball to the wall with E·Y·O·], release with Q·R·U·P\nclimb by sticking one leg at a time!");
            until = hintUntil;
        }
        else if (Time.time < hint2Until)
        {
            s = L.T("테니스공의 힘은 여기까지 — 이제 기린의 힘(2)을 쓸 수 있어요",
                    "the tennis power stays behind — the giraffe power (2) is yours now");
            until = hint2Until;
        }
        if (s == null) return;

        UiKit.ApplyKoreanFont();
        if (txt == null) txt = HandUI.Style(26, HandUI.Ink, TextAnchor.MiddleCenter);
        float a = Mathf.Clamp01(until - Time.time);   // 마지막 1초 페이드아웃
        var pc = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, a);

        float w = 0f;
        foreach (var line in s.Split('\n'))
            w = Mathf.Max(w, txt.CalcSize(new GUIContent(line)).x);
        w += 44f;
        float h = txt.CalcHeight(new GUIContent(s), w) + 20f;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 46f, w, h);
        UiKit.Fill(r, new Color(1f, 1f, 1f, 0.55f));
        GUI.Label(r, s, txt);
        if (HandUI.Underline != null)
        {
            float tw = Mathf.Min(r.width * 0.62f, 430f);
            GUI.DrawTexture(new Rect(r.center.x - tw * 0.5f, r.yMax - 9f, tw, 12f), HandUI.Underline, ScaleMode.StretchToFill);
        }
        GUI.color = pc;
    }
}
