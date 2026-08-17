using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// ★2026-08-17 액자 맵 도입부(v3 — 공 수집판, 사용자 요청): 밝은 샤프트 바닥에 테니스공 4개가 흩어져 있고,
/// **공이 없는 다리 발끝이 공에 닿으면(후한 판정) 그 공이 발끝에 붙는다.** 네 다리 모두 붙으면
/// 반대편 벽 쥐구멍에서 애벌레가 나와 기린 컷씬 문법으로 "잘했어... / 나의 힘을 줄게..." →
/// 초록 빛 전달 + 테이블 발광 → TennisMode 부여 → 조작 설명 이미지(ControlsTennis) 표시 →
/// 애벌레는 등반벽을 타고 퇴장(길 안내). 벽 위 애벌레는 표면에서 공 반지름만큼 띄워 파묻힘 방지.
/// 홀 진입 시 테니스 회수+기린 해금, 샤프트로 떨어지면 재부여(v2와 동일).
/// </summary>
public class FrameHallClimb : MonoBehaviour
{
    [Header("배치(씬 값)")]
    public Vector3 holePos = new Vector3(14.99f, -12f, -31f);   // 쥐구멍(오른쪽 벽 바닥)
    public float grabRadius = 0.7f;                              // 공 줍기 판정(후하게)
    public float hallZ = -21f;
    public float shaftZ = -22.5f;
    public float waveEvery = 9f;
    public Texture2D guideTex;                                   // ControlsTennis.png(씬에서 할당)

    [Header("컷씬 타이밍(초)")]
    public float emergeSeconds = 2.0f;
    public float talkSeconds = 2.6f;
    public float lightSeconds = 2.0f;
    public float glowSeconds = 2.2f;
    public float exitWatchSeconds = 3.0f;

    // 흩뿌린 공 4개(샤프트 바닥, 반지름 0.17 → y = -12 + 0.17)
    static readonly Vector3[] BallSpots =
    {
        new Vector3(-9f, -11.83f, -26.5f),
        new Vector3( 8f, -11.83f, -25.5f),
        new Vector3(-5f, -11.83f, -36.5f),
        new Vector3(10f, -11.83f, -37f),
    };
    const float BallR = 0.17f;
    const float WallZ = -22.75f;    // 등반면 표면(-22.5)에서 공 반지름+여유만큼 앞 — 파묻힘 방지

    Transform table, tableTop, cam;
    Behaviour cameraFollow;
    TennisMode tennis;
    GiraffeMode giraffe;
    ArtLegController[] legs;        // 발끝 계산용(TennisMode.Tip과 동일 규칙)
    Transform[] legMesh;
    Transform[] carried;            // 다리별 붙은 공(없으면 null)
    Transform[] loose;              // 바닥의 공
    bool cutsceneStarted, cutsceneDone, inHall;
    bool showGuide; float guideShownAt;
    float hint2Until;
    string bubbleText; Vector3 bubbleAnchor;
    Transform[] chain;
    GUIStyle txt;

    const int ChainN = 8;
    const float Spacing = 0.30f;

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

        if (table != null)
        {
            legs = table.root.GetComponentsInChildren<ArtLegController>(true);
            legMesh = new Transform[legs.Length];
            carried = new Transform[legs.Length];
            for (int i = 0; i < legs.Length; i++)
                legMesh[i] = legs[i].transform.childCount > 0 ? legs[i].transform.GetChild(0) : legs[i].transform;
        }

        // 공 4개 흩뿌리기(시각 전용 — 판정은 거리 검사)
        var mat = tennis != null ? tennis.ballMat : null;
        loose = new Transform[BallSpots.Length];
        for (int i = 0; i < BallSpots.Length; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(s.GetComponent<Collider>());
            s.name = "LooseBall_" + i;
            s.transform.position = BallSpots[i];
            s.transform.localScale = Vector3.one * (BallR * 2f);
            if (mat != null) s.GetComponent<MeshRenderer>().sharedMaterial = mat;
            loose[i] = s.transform;
        }
    }

    Vector3 Tip(int i) => legMesh[i].TransformPoint(0f, -0.5f, 0f);
    Material BallMat() => tennis != null ? tennis.ballMat : null;

    void Update()
    {
        if (table == null || legs == null) return;

        // ── 공 줍기: 빈 다리 발끝이 바닥 공에 닿으면 부착 ──
        if (!cutsceneStarted)
        {
            for (int i = 0; i < legs.Length; i++)
            {
                if (carried[i] != null) continue;
                Vector3 tip = Tip(i);
                for (int k = 0; k < loose.Length; k++)
                {
                    if (loose[k] == null) continue;
                    if ((loose[k].position - tip).sqrMagnitude < grabRadius * grabRadius)
                    {
                        carried[i] = loose[k];
                        loose[k] = null;
                        break;
                    }
                }
            }
            int have = 0;
            for (int i = 0; i < carried.Length; i++) if (carried[i] != null) have++;
            if (have >= 4) { cutsceneStarted = true; StartCoroutine(GrantCutscene()); }
        }

        // 붙은 공은 발끝 추종(테니스 모드와 같은 위치)
        for (int i = 0; i < carried.Length; i++)
            if (carried[i] != null) carried[i].SetPositionAndRotation(Tip(i), legMesh[i].rotation);

        if (!cutsceneDone) return;

        // ── 홀 진입 = 테니스 회수+기린 해금 / 샤프트로 떨어지면 재부여 ──
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

    /// <summary>[치트/검증용] 남은 공을 강제로 전부 부착.</summary>
    public void DebugCollectAll()
    {
        for (int i = 0; i < carried.Length; i++)
        {
            if (carried[i] != null) continue;
            for (int k = 0; k < loose.Length; k++)
                if (loose[k] != null) { carried[i] = loose[k]; loose[k] = null; break; }
        }
    }

    // ───────────────────────── 컷씬: 쥐구멍 애벌레의 힘 전달 ─────────────────────────

    IEnumerator GrantCutscene()
    {
        FreezeTable(true);
        if (cameraFollow != null) cameraFollow.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;

        // 카메라: 쥐구멍을 비스듬히 봄
        Vector3 camP = holePos + new Vector3(-5.5f, 2.6f, 1.5f);
        Vector3 lookP = holePos + Vector3.up * 0.4f;
        SetCam(camP, lookP);
        yield return Hold(0.5f, camP, lookP);

        // 1) 애벌레가 쥐구멍에서 나옴
        Vector3 stop = holePos + new Vector3(-3.2f, 0.17f, 0f);
        var path = new Vector3[]
        {
            holePos + new Vector3(0.6f, 0.17f, 0f),      // 구멍 안
            holePos + new Vector3(-0.4f, 0.17f, 0f),
            Vector3.Lerp(holePos, stop, 0.55f) + Vector3.up * 0.17f,
            stop,
        };
        chain = SpawnChain(path[0]);
        yield return MoveChain(path, emergeSeconds, camP, lookP);

        // 2) 대사 2줄(기린 컷씬 문법)
        bubbleAnchor = stop + Vector3.up * 0.5f;
        bubbleText = L.T("잘했어...", "well done...");
        yield return Wiggle(talkSeconds, camP, lookP);
        bubbleText = L.T("나의 힘을 줄게...", "take my power...");
        yield return Wiggle(talkSeconds, camP, lookP);
        bubbleText = null;

        // 3) 초록 빛 구슬 전달 + 테이블 발광(도중 TennisMode ON — 모은 공 4개가 '진짜 힘'이 됨)
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
        yield return FlashTable(camP, lookP);

        // 4) 애벌레 퇴장 — 등반벽을 타고 올라 홀 개구부 너머로(길 안내)
        foreach (var c in chain) if (c != null) Object.Destroy(c.gameObject);
        chain = null;
        var exitPath = new Vector3[]
        {
            stop,
            new Vector3(stop.x * 0.5f, -11.8f, -25f),
            new Vector3(2f, -11.7f, WallZ),
            new Vector3(1f, -6f, WallZ),
            new Vector3(0.5f, 0.5f, WallZ),
            new Vector3(0.4f, 0.35f, -20f),
            new Vector3(0.4f, 0.35f, -17f),
        };
        var go = new GameObject("GoodbyeCaterpillar");
        go.transform.position = exitPath[0];
        var cat = go.AddComponent<TennisCaterpillar>();
        cat.BuildAndGo(exitPath, BallMat(), 3.4f, TennisCaterpillar.Gait.Wave);

        Vector3 camW = new Vector3(4f, -8f, -30f);
        Vector3 lookW = new Vector3(0f, -4f, -22.5f);
        yield return CamLerp(camP, lookP, camW, lookW, 1.2f);
        yield return Hold(exitWatchSeconds, camW, lookW);

        // 5) 복귀 + 조작 설명 이미지
        if (cameraFollow != null) cameraFollow.enabled = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        FreezeTable(false);
        cutsceneDone = true;
        if (guideTex != null) { showGuide = true; guideShownAt = Time.time; }
        StartCoroutine(WaveLoop());
    }

    /// <summary>발광 도중(40%) TennisMode ON — 모은 공 제거(모드 공으로 교체됨).</summary>
    IEnumerator FlashTable(Vector3 cp, Vector3 cl)
    {
        var rends = table != null ? table.root.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
        var mpb = new MaterialPropertyBlock();
        bool swapped = false;
        for (float t = 0; t < glowSeconds; t += Time.deltaTime)
        {
            float u = t / glowSeconds;
            float k = Mathf.Pow(Mathf.Clamp01(u / 0.18f) * Mathf.Clamp01((1f - u) / 0.28f), 0.6f);
            if (!swapped && tennis != null && u >= 0.4f)
            {
                tennis.active = true;
                for (int i = 0; i < carried.Length; i++)
                    if (carried[i] != null) { Object.Destroy(carried[i].gameObject); carried[i] = null; }
                swapped = true;
            }
            Color emi = new Color(1.1f, 1.5f, 0.7f) * (k * 3.2f);
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

    /// <summary>(컷씬 후) 애벌레들이 이따금 쥐구멍에서 나와 벽을 타고 올라감 — 표면에서 띄워 파묻힘 방지.</summary>
    IEnumerator WaveLoop()
    {
        var wait = new WaitForSeconds(waveEvery);
        while (true)
        {
            yield return wait;
            if (inHall) continue;
            float lat = Random.Range(-3f, 3f);
            var path = new Vector3[]
            {
                holePos + new Vector3(0.6f, 0.17f, 0f),
                holePos + new Vector3(-1.5f, 0.17f, 0f),
                new Vector3(lat, -11.7f, -26f),
                new Vector3(lat, -11.6f, WallZ),
                new Vector3(lat * 0.4f, -6f, WallZ),
                new Vector3(lat * 0.15f, 0.5f, WallZ),
                new Vector3(lat * 0.15f, 0.35f, -20f),
                new Vector3(lat * 0.15f, 0.35f, -17f),
            };
            var go = new GameObject("ClimbCaterpillar");
            go.transform.position = path[0];
            var cat = go.AddComponent<TennisCaterpillar>();
            cat.BuildAndGo(path, BallMat(), Random.Range(1.1f, 1.6f), TennisCaterpillar.Gait.Wave);
        }
    }

    // ── 컷씬용 수동 애벌레 ──

    Transform[] SpawnChain(Vector3 at)
    {
        var arr = new Transform[ChainN];
        for (int i = 0; i < ChainN; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(s.GetComponent<Collider>());
            s.name = "CutsceneCat_" + i;
            s.transform.localScale = Vector3.one * (BallR * 2f);
            if (BallMat() != null) s.GetComponent<MeshRenderer>().sharedMaterial = BallMat();
            s.transform.position = at;
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

    Vector3[] lastPath; float lastHeadS;

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

    // ───────────────────────── 손글씨 말풍선 + 설명 이미지 오버레이 ─────────────────────────

    void OnGUI()
    {
        if (Time.timeScale == 0f) return;
        UiKit.ApplyKoreanFont();
        if (txt == null) txt = HandUI.Style(26, HandUI.Ink, TextAnchor.MiddleCenter);

        // 조작 설명 이미지(흰 화면 + 16:9 맞춤 — ControlsGuide 룩, 클릭/이동키로 닫기)
        if (showGuide)
        {
            UiKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, 0.97f));
            float top = 24f, bottom = Screen.height - 60f;
            float availH = bottom - top, availW = Screen.width - 48f;
            float ar = 16f / 9f;
            float w = availW, h = w / ar;
            if (h > availH) { h = availH; w = h * ar; }
            GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, top + (availH - h) * 0.5f, w, h), guideTex, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(0f, Screen.height - 52f, Screen.width, 30f),
                L.T("클릭하면 시작!", "click to start!"), txt);
            if (Time.time - guideShownAt > 0.6f && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                showGuide = false;
            return;
        }

        // 컷씬 말풍선(애벌레 머리 위)
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

        // 홀 진입 힌트
        if (Time.time < hint2Until)
        {
            string s = L.T("테니스공의 힘은 여기까지 — 이제 기린의 힘(2)을 쓸 수 있어요",
                           "the tennis power stays behind — the giraffe power (2) is yours now");
            float a = Mathf.Clamp01(hint2Until - Time.time);
            var sz2 = Measure(s);
            var r2 = new Rect((Screen.width - sz2.x) * 0.5f, Screen.height - sz2.y - 46f, sz2.x, sz2.y);
            DrawHand(r2, s, a * 0.55f, a);
        }
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
