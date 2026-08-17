using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ★2026-08-17 액자 맵 도입부(v2 — 컷씬판): 샤프트 바닥의 테니스공 무더기에 다가가면
/// 애벌레가 무더기에서 나와 **기린 컷씬과 같은 '힘 주는 컷씬'**으로 테니스공의 힘을 전달한다:
/// 테이블 동결 + 카메라 직접 제어 → 애벌레 등장 → 대사 → 빛 구슬 전달 + 테이블 발광(공 생성) →
/// "이제 나는 갈게~" → 애벌레가 벽을 타고 올라가며 퇴장(길 안내) → 조작 복귀.
/// 이후: 홀 진입 시 테니스 회수+기린 해금, 샤프트로 떨어지면 재부여. 대사는 손글씨(로비 테마) 말풍선.
/// </summary>
public class FrameHallClimb : MonoBehaviour
{
    [Header("배치(씬 값)")]
    public Vector3 pilePos = new Vector3(7f, -12.35f, -33f);   // 애벌레 무더기(샤프트 바닥 구석)
    public float meetRadius = 5f;                               // 이 거리 안이면 컷씬 발동
    public float hallZ = -21f;                                  // 이 z를 넘으면 홀(테니스 회수+기린 해금)
    public float shaftZ = -22.5f;                               // 이 z보다 뒤면 샤프트(테니스 재부여)
    public float waveEvery = 9f;                                // (컷씬 후) 애벌레 시범 등반 간격

    [Header("컷씬 타이밍(초)")]
    public float emergeSeconds = 2.2f;
    public float talkSeconds = 2.8f;
    public float lightSeconds = 2.2f;
    public float glowSeconds = 2.2f;
    public float exitWatchSeconds = 3.2f;

    Transform table, tableTop, cam;
    Behaviour cameraFollow;
    TennisMode tennis;
    GiraffeMode giraffe;
    bool met, inHall, cutsceneDone;
    float hintUntil, hint2Until;
    string bubbleText;          // 애벌레 머리 위 손글씨 말풍선
    Vector3 bubbleAnchor;
    Transform[] chain;          // 컷씬용 수동 애벌레(공 8개)
    GUIStyle txt;

    const int ChainN = 8;
    const float Spacing = 0.30f;
    const float BallR = 0.17f;

    void Start()
    {
        var b = GameObject.Find("Table");
        if (b != null) table = b.transform;
        var tt = GameObject.Find("TableTop");
        if (tt != null) tableTop = tt.transform;
        if (Camera.main != null) { cam = Camera.main.transform; cameraFollow = Camera.main.GetComponent("CameraFollow") as Behaviour; }
        tennis = Object.FindAnyObjectByType<TennisMode>();
        giraffe = Object.FindAnyObjectByType<GiraffeMode>();
        if (giraffe != null) giraffe.locked = true;   // 샤프트 구간: 기린 잠금(붙기 키와 충돌)
        BuildPile();
    }

    Material BallMat() => tennis != null ? tennis.ballMat : null;

    /// <summary>무더기: 테니스공 언덕(시각 전용) — 애벌레들이 사는 곳.</summary>
    void BuildPile()
    {
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
            if (BallMat() != null) r.sharedMaterial = BallMat();
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

        // ① 무더기 접근 → 힘 주는 컷씬
        if (!met && (table.position - pilePos).sqrMagnitude < meetRadius * meetRadius)
        {
            met = true;
            StartCoroutine(GrantCutscene());
        }
        if (!cutsceneDone) return;

        // ② 홀 진입 = 테니스 회수+기린 해금 / 샤프트로 떨어지면 다시 테니스
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

    // ───────────────────────── 컷씬 ─────────────────────────

    IEnumerator GrantCutscene()
    {
        FreezeTable(true);
        if (cameraFollow != null) cameraFollow.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;

        // 카메라: 테이블 어깨 너머로 무더기를 봄
        Vector3 toTable = table.position - pilePos; toTable.y = 0f;
        Vector3 dir = toTable.sqrMagnitude > 0.01f ? toTable.normalized : Vector3.forward;
        Vector3 camP = pilePos + dir * 5.5f + Vector3.up * 2.4f;
        Vector3 lookP = pilePos + Vector3.up * 0.6f;
        SetCam(camP, lookP);

        // 1) 애벌레 등장: 무더기 속 → 테이블 앞 1.8m 지점
        Vector3 stop = pilePos + dir * Mathf.Min(2.6f, toTable.magnitude - 1.8f);
        stop.y = pilePos.y;
        var path = new Vector3[]
        {
            pilePos + Vector3.down * 0.3f,
            pilePos + Vector3.up * BallR,
            Vector3.Lerp(pilePos, stop, 0.5f) + Vector3.up * BallR,
            stop + Vector3.up * BallR,
        };
        chain = SpawnChain();
        yield return MoveChain(path, emergeSeconds, camP, lookP);

        // 2) 대사 1 + 힘 전달(빛 구슬 + 테이블 발광, 도중 공 생성=active)
        bubbleAnchor = stop + Vector3.up * 0.5f;
        bubbleText = L.T("테이블아! 테니스공의 힘을 나눠줄게", "hey table! let me share the tennis-ball power");
        yield return Wiggle(talkSeconds, camP, lookP);
        bubbleText = null;

        var orb = MakeOrb();
        Vector3 from = stop + Vector3.up * 0.4f;
        Vector3 to = (tableTop != null ? tableTop.position : table.position) + Vector3.up * 0.2f;
        for (float t = 0; t < lightSeconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / lightSeconds);
            orb.transform.position = Vector3.Lerp(from, to, u);
            orb.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 0.12f, u);
            WiggleChainStep();
            SetCam(camP, lookP); yield return null;
        }
        Object.Destroy(orb);
        if (tennis != null) tennis.unlocked = true;
        yield return FlashTable(camP, lookP);   // 발광 도중 tennis.active=true → 다리 끝 공 등장

        // 3) 대사 2 + 벽 타고 퇴장(길 안내)
        bubbleText = L.T("붙었다 뗐다 하며 올라오면 돼 — 이제 나는 갈게~", "stick and release to climb up — off I go now~");
        yield return Wiggle(talkSeconds, camP, lookP);
        bubbleText = null;

        foreach (var c in chain) if (c != null) Object.Destroy(c.gameObject);
        chain = null;
        float wallZ = -22.45f;
        var exitPath = new Vector3[]
        {
            stop + Vector3.up * BallR,
            new Vector3(stop.x * 0.4f, pilePos.y + 0.5f, wallZ),
            new Vector3(0f, -6f, wallZ),
            new Vector3(0f, 0.5f, wallZ),
            new Vector3(0f, 0.35f, -20f),
            new Vector3(0f, 0.35f, -17f),
        };
        var go = new GameObject("GoodbyeCaterpillar");
        go.transform.position = exitPath[0];
        var cat = go.AddComponent<TennisCaterpillar>();
        cat.BuildAndGo(exitPath, BallMat(), 3.2f, TennisCaterpillar.Gait.Wave);

        // 카메라가 벽 등반을 올려다보며 배웅
        Vector3 camW = new Vector3(stop.x * 0.5f, pilePos.y + 3.5f, -29f);
        Vector3 lookW = new Vector3(0f, -4f, -22.5f);
        yield return CamLerp(camP, lookP, camW, lookW, 1.2f);
        yield return Hold(exitWatchSeconds, camW, lookW);

        // 4) 복귀 + 조작 힌트
        if (cameraFollow != null) cameraFollow.enabled = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        FreezeTable(false);
        hintUntil = Time.time + 10f;
        cutsceneDone = true;
        StartCoroutine(WaveLoop());
    }

    /// <summary>(컷씬 후) 애벌레들이 이따금 무더기에서 나와 벽을 타고 올라감 — 길 안내 앰비언트.</summary>
    IEnumerator WaveLoop()
    {
        var wait = new WaitForSeconds(waveEvery);
        while (true)
        {
            yield return wait;
            if (inHall) continue;
            float wallZ = -22.45f;
            float lat = Random.Range(-3f, 3f);
            var path = new Vector3[]
            {
                pilePos + Vector3.down * 0.3f,
                pilePos + Vector3.up * BallR,
                new Vector3(lat, pilePos.y + 0.5f, -25f),
                new Vector3(lat, pilePos.y + 0.6f, wallZ),
                new Vector3(lat * 0.4f, -6f, wallZ),
                new Vector3(lat * 0.15f, 0.5f, wallZ),
                new Vector3(lat * 0.15f, 0.35f, -20f),
                new Vector3(lat * 0.15f, 0.35f, -17f),
            };
            var go = new GameObject("ClimbCaterpillar");
            go.transform.position = path[0];
            var cat = go.AddComponent<TennisCaterpillar>();
            cat.BuildAndGo(path, BallMat(), Random.Range(1.1f, 1.6f), TennisCaterpillar.Gait.Wave);
        }
    }

    // ── 컷씬용 수동 애벌레(등장·대사 동안 제자리 꿈틀 — TennisCaterpillar는 도착 시 자멸이라 직접 제어) ──

    Transform[] SpawnChain()
    {
        var arr = new Transform[ChainN];
        for (int i = 0; i < ChainN; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(s.GetComponent<Collider>());
            s.name = "CutsceneCat_" + i;
            s.transform.localScale = Vector3.one * (BallR * 2f);
            var r = s.GetComponent<MeshRenderer>();
            if (BallMat() != null) r.sharedMaterial = BallMat();
            s.transform.position = pilePos + Vector3.down * 0.3f;
            arr[i] = s.transform;
        }
        return arr;
    }

    static Vector3 Sample(Vector3[] pts, float s)
    {
        if (s <= 0f) return pts[0];
        for (int i = 0; i < pts.Length - 1; i++)
        {
            float seg = Vector3.Distance(pts[i], pts[i + 1]);
            if (s <= seg || i == pts.Length - 2)
                return Vector3.Lerp(pts[i], pts[i + 1], seg > 1e-5f ? Mathf.Clamp01(s / seg) : 1f);
            s -= seg;
        }
        return pts[pts.Length - 1];
    }

    IEnumerator MoveChain(Vector3[] path, float dur, Vector3 cp, Vector3 cl)
    {
        float total = 0f;
        for (int i = 0; i < path.Length - 1; i++) total += Vector3.Distance(path[i], path[i + 1]);
        float endS = total + (ChainN - 1) * Spacing;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float headS = Mathf.SmoothStep(0f, 1f, t / dur) * endS;
            PoseChain(path, headS);
            SetCam(cp, cl); yield return null;
        }
        PoseChain(path, endS);
        lastPath = path; lastHeadS = endS;
    }

    Vector3[] lastPath; float lastHeadS;

    void PoseChain(Vector3[] path, float headS)
    {
        float t = Time.time;
        for (int i = 0; i < ChainN; i++)
        {
            float phase = t * 2.2f * Mathf.PI * 2f - i * 0.95f;
            Vector3 p = Sample(path, headS - i * Spacing + Mathf.Sin(phase) * 0.07f);
            p.y += Mathf.Max(0f, Mathf.Sin(phase)) * 0.06f;
            if (chain[i] != null) chain[i].position = p;
        }
    }

    void WiggleChainStep() { if (chain != null && lastPath != null) PoseChain(lastPath, lastHeadS); }

    IEnumerator Wiggle(float dur, Vector3 cp, Vector3 cl)
    { for (float t = 0; t < dur; t += Time.deltaTime) { WiggleChainStep(); SetCam(cp, cl); yield return null; } }

    IEnumerator Hold(float dur, Vector3 cp, Vector3 cl)
    { for (float t = 0; t < dur; t += Time.deltaTime) { SetCam(cp, cl); yield return null; } }

    IEnumerator CamLerp(Vector3 p0, Vector3 l0, Vector3 p1, Vector3 l1, float dur)
    {
        Quaternion r0 = Quaternion.LookRotation(l0 - p0, Vector3.up), r1 = Quaternion.LookRotation(l1 - p1, Vector3.up);
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / dur);
            if (cam != null) { cam.position = Vector3.Lerp(p0, p1, u); cam.rotation = Quaternion.Slerp(r0, r1, u); }
            yield return null;
        }
    }

    // 테이블 발광 — 도중(40%)에 테니스공 생성(GiraffeCutscene.FlashTable 축약판)
    IEnumerator FlashTable(Vector3 cp, Vector3 cl)
    {
        var rends = table != null ? table.root.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
        var mpb = new MaterialPropertyBlock();
        bool swapped = false;
        for (float t = 0; t < glowSeconds; t += Time.deltaTime)
        {
            float u = t / glowSeconds;
            float k = Mathf.Pow(Mathf.Clamp01(u / 0.18f) * Mathf.Clamp01((1f - u) / 0.28f), 0.6f);
            if (!swapped && tennis != null && u >= 0.4f) { tennis.active = true; swapped = true; }
            Color emi = new Color(1.1f, 1.5f, 0.7f) * (k * 3.2f);   // 테니스 연두빛
            foreach (var r in rends)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;
                if (m != null && !m.IsKeywordEnabled("_EMISSION"))
                { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", emi); r.SetPropertyBlock(mpb);
            }
            WiggleChainStep();
            SetCam(cp, cl); yield return null;
        }
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", Color.black); r.SetPropertyBlock(mpb);
        }
    }

    GameObject MakeOrb()
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "TennisOrb";
        var c = s.GetComponent<Collider>(); if (c != null) Object.Destroy(c);
        s.transform.localScale = Vector3.one * 0.5f;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.color = new Color(0.75f, 1f, 0.45f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.9f, 1.5f, 0.5f) * 3f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        s.GetComponent<Renderer>().sharedMaterial = m;
        var pl = s.AddComponent<Light>();
        pl.type = LightType.Point; pl.color = new Color(0.8f, 1f, 0.5f); pl.range = 8f; pl.intensity = 3.5f;
        return s;
    }

    void SetCam(Vector3 pos, Vector3 look)
    { if (cam != null) { cam.position = pos; cam.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up); } }

    void FreezeTable(bool freeze)
    {
        if (table == null) return;
        foreach (var rb in table.root.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) rb.isKinematic = freeze;
        foreach (var ab in table.root.GetComponentsInChildren<ArticulationBody>(true))
            if (ab != null && ab.isRoot) { ab.immovable = freeze; if (freeze) { ab.linearVelocity = Vector3.zero; ab.angularVelocity = Vector3.zero; } }
    }

    // ───────────────────────── 손글씨 말풍선/힌트(로비 테마) ─────────────────────────

    void OnGUI()
    {
        if (Time.timeScale == 0f) return;
        UiKit.ApplyKoreanFont();
        if (txt == null) txt = HandUI.Style(26, HandUI.Ink, TextAnchor.MiddleCenter);

        // 컷씬 말풍선: 애벌레 머리 위(월드 앵커 → 스크린)
        if (bubbleText != null && Camera.main != null)
        {
            Vector3 sp = Camera.main.WorldToScreenPoint(bubbleAnchor);
            if (sp.z > 0f)
            {
                var sz = Measure(bubbleText);
                var r = new Rect(sp.x - sz.x * 0.5f, Screen.height - sp.y - sz.y - 26f, sz.x, sz.y);
                r.x = Mathf.Clamp(r.x, 8f, Screen.width - sz.x - 8f);
                r.y = Mathf.Clamp(r.y, 8f, Screen.height - sz.y - 8f);
                DrawHand(r, bubbleText, 0.9f);
            }
        }

        // 컷씬 후 조작 힌트 / 홀 진입 힌트(하단 중앙)
        string s = null; float until = 0f;
        if (Time.time < hintUntil)
        {
            s = L.T("신축 키(E·Y·O·])로 공을 벽에 붙이고, 반대 키(Q·R·U·P)로 떼요\n다리를 번갈아 붙이며 벽을 올라가세요!",
                    "stick a ball to the wall with E·Y·O·], release with Q·R·U·P\nclimb by sticking one leg at a time!");
            until = hintUntil;
        }
        else if (Time.time < hint2Until)
        {
            s = L.T("테니스공의 힘은 여기까지 — 이제 기린의 힘(2)을 쓸 수 있어요",
                    "the tennis power stays behind — the giraffe power (2) is yours now");
            until = hint2Until;
        }
        if (s == null) return;
        float a = Mathf.Clamp01(until - Time.time);
        var sz2 = Measure(s);
        var r2 = new Rect((Screen.width - sz2.x) * 0.5f, Screen.height - sz2.y - 46f, sz2.x, sz2.y);
        DrawHand(r2, s, a * 0.55f, a);
    }

    Vector2 Measure(string text)
    {
        float w = 0f;
        foreach (var line in text.Split('\n'))
            w = Mathf.Max(w, txt.CalcSize(new GUIContent(line)).x);
        w += 44f;
        float h = txt.CalcHeight(new GUIContent(text), w) + 20f;
        return new Vector2(w, h);
    }

    void DrawHand(Rect r, string text, float bgAlpha, float textAlpha = 1f)
    {
        var pc = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, textAlpha);
        UiKit.Fill(r, new Color(1f, 1f, 1f, bgAlpha));
        GUI.Label(r, text, txt);
        if (HandUI.Underline != null)
        {
            float tw = Mathf.Min(r.width * 0.62f, 430f);
            GUI.DrawTexture(new Rect(r.center.x - tw * 0.5f, r.yMax - 9f, tw, 12f), HandUI.Underline, ScaleMode.StretchToFill);
        }
        GUI.color = pc;
    }
}
