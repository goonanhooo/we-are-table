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
///
/// ★2026-09-02 에디터 편집형(사용자 요청 "런타임 말고 편집할 수 있게"): 바닥 공·쥐구멍·등장/퇴장 경로·
/// 카메라 샷·컷씬 애벌레·퇴장 애벌레·빛 구슬이 전부 **씬 오브젝트**이고 아래 필드로 참조한다.
/// 씬에서 위치/재질/파라미터를 직접 옮기면 그대로 반영. 참조가 비어 있을 때만 예전 런타임 생성으로 폴백.
/// </summary>
public class FrameHallClimb : MonoBehaviour
{
    [Header("씬 오브젝트(에디터에서 직접 편집)")]
    [Tooltip("바닥에 흩어진 공 4개(시각 전용, 콜라이더 없음). 위치를 옮기면 그대로 반영.")]
    public Transform[] looseBalls;
    [Tooltip("쥐구멍 위치(애벌레 등장 기준점). 비어 있으면 holePos 숫자 사용.")]
    public Transform holeAnchor;
    [Tooltip("애벌레 등장 경로(순서대로, 첫 점=벽 속, 마지막 점=머리 정지점).")]
    public Transform[] emergePath;
    [Tooltip("말풍선 앵커(비어 있으면 머리 위 0.45).")]
    public Transform bubblePoint;
    [Tooltip("클로즈업 카메라 위치 / 바라볼 점")]
    public Transform camCloseup, camCloseupLook;
    [Header("힘 전달/퇴장 샷 — 테이블 기준 상대 오프셋(테이블 위치가 매번 달라 마커 대신 숫자)")]
    [Tooltip("힘 전달 클로즈업: 테이블 뒤(애벌레 반대쪽) 거리 / 옆 / 높이 / 바라볼 점 높이 — 테이블이 화면에 꽉 차게")]
    public float glowCamBack = 3.2f, glowCamSide = 1.6f, glowCamUp = 1.9f, glowLookUp = 0.3f;
    [Tooltip("퇴장 관전(어깨 너머): 테이블 뒤 / 옆 / 높이. 위치 고정, 시선만 애벌레를 따라감")]
    public float shoulderBack = 2.4f, shoulderSide = 1.1f, shoulderUp = 1.7f;
    [Tooltip("카메라 이동 시간(초): 컷씬 진입 / 클로즈업 전환 / 어깨 샷 전환 — 컷 없이 부드럽게")]
    public float camIntroSeconds = 1.6f, camGlowSeconds = 1.4f, camShoulderSeconds = 1.2f;
    [Tooltip("시선 추적 부드러움(클수록 빠르게 따라감)")]
    public float lookFollowSpeed = 4f;
    [Tooltip("컷씬 애벌레(자식 구 8개 = 몸통, 평소 비활성). 크기/재질은 자식에서 편집.")]
    public Transform cutsceneChain;
    [Tooltip("퇴장 경로(순서대로 — 마지막은 벽 위 구멍 너머).")]
    public Transform[] exitPath;
    [Tooltip("퇴장 애벌레(씬 배치, 평소 비활성). 속도/걸음새/진폭은 이 컴포넌트에서 편집.")]
    public TennisCaterpillar goodbyeCat;
    [Tooltip("힘 전달 빛 구슬(씬 배치, 평소 비활성). 재질/라이트는 오브젝트에서 편집.")]
    public GameObject orb;

    [Header("배치(숫자 폴백 — 위 씬 오브젝트가 비어 있을 때만)")]
    public Vector3 holePos = new Vector3(14.99f, -12f, -31f);   // 쥐구멍(오른쪽 벽 바닥)
    public float grabRadius = 0.7f;                              // 공 줍기 판정(후하게)
    public float hallZ = -21f;
    public float shaftZ = -22.5f;
    public Texture2D guideTex;                                   // ControlsTennis.png(씬에서 할당)
    public Texture2D bubble1;                                    // CatBubble1.png "잘했어..."(기린 말풍선 형식)
    public Texture2D bubble2;                                    // CatBubble2.png "나의 힘을 줄게..."

    [Header("컷씬 타이밍(초)")]
    public float emergeSeconds = 2.0f;
    public float talkSeconds = 2.6f;
    public float lightSeconds = 2.0f;
    public float glowSeconds = 2.2f;
    public float exitWatchSeconds = 6.5f;
    [Tooltip("빛 구슬 크기(출발→도착)")]
    public float orbStartScale = 0.5f, orbEndScale = 0.12f;

    // 폴백: 흩뿌린 공 4개(샤프트 바닥, 반지름 0.17 → y = -12 + 0.17)
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
    /// <summary>★멀티 게스트: 판정/부여 없이 시각만(NetStageSync가 구동).</summary>
    [HideInInspector] public bool netGuest;
    public bool CutsceneStarted => cutsceneStarted;
    /// <summary>다리별 공 부착 비트마스크(legs 순서) — 호스트가 발행.</summary>
    public int CarriedMask
    {
        get { int m = 0; if (carried != null) for (int i = 0; i < carried.Length; i++) if (carried[i] != null) m |= 1 << i; return m; }
    }
    /// <summary>게스트: 호스트가 발행한 부착 마스크대로 바닥 공을 다리에 붙여 보이게(멱등).</summary>
    public void NetApplyCarried(int mask)
    {
        if (carried == null || loose == null) return;
        for (int i = 0; i < carried.Length; i++)
        {
            if ((mask & (1 << i)) == 0 || carried[i] != null) continue;
            for (int k = 0; k < loose.Length; k++)
                if (loose[k] != null) { carried[i] = loose[k]; loose[k] = null; break; }
        }
    }
    /// <summary>게스트: 호스트 컷씬 시작에 맞춰 로컬 재생(카메라/애벌레/말풍선 — 힘 부여 물리는 없음).</summary>
    public void PlayRemote()
    {
        if (cutsceneStarted) return;
        cutsceneStarted = true;
        StartCoroutine(GrantCutscene());
    }
    bool showGuide; float guideShownAt;
    float hint2Until;
    Texture2D currentBubble; Vector3 bubbleAnchor;
    Transform[] chain;
    bool chainIsScene;
    GUIStyle txt;

    const int ChainN = 8;
    const float Spacing = 0.30f;

    Vector3 HolePos => holeAnchor != null ? holeAnchor.position : holePos;

    void Start()
    {
        var b = GameObject.Find("Table");
        if (b != null) table = b.transform;
        var tt = GameObject.Find("TableTop");
        if (tt != null) tableTop = tt.transform;
        if (Camera.main != null) { cam = Camera.main.transform; cameraFollow = Camera.main.GetComponent("CameraFollow") as Behaviour; }
        tennis = Object.FindAnyObjectByType<TennisMode>();
        giraffe = Object.FindAnyObjectByType<GiraffeMode>();

        if (table != null)
        {
            legs = table.root.GetComponentsInChildren<ArtLegController>(true);
            legMesh = new Transform[legs.Length];
            carried = new Transform[legs.Length];
            for (int i = 0; i < legs.Length; i++)
                legMesh[i] = legs[i].transform.childCount > 0 ? legs[i].transform.GetChild(0) : legs[i].transform;
        }

        // 바닥 공: 씬에 배치된 것을 사용(에디터 편집) — 비어 있으면 폴백으로 생성
        int nScene = 0;
        if (looseBalls != null) foreach (var t in looseBalls) if (t != null) nScene++;
        if (nScene > 0)
        {
            loose = new Transform[nScene];
            int k = 0;
            foreach (var t in looseBalls) if (t != null) { t.gameObject.SetActive(true); loose[k++] = t; }
        }
        else
        {
            Debug.LogWarning("[FrameHallClimb] looseBalls 미할당 — 런타임 생성 폴백(씬에 배치하면 편집 가능)");
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

        // 컷씬 소품은 평소 숨김(씬에서 켜 둔 채 저장해도 안전)
        if (cutsceneChain != null) cutsceneChain.gameObject.SetActive(false);
        if (goodbyeCat != null) goodbyeCat.gameObject.SetActive(false);
        if (orb != null) orb.SetActive(false);
    }

    Vector3 Tip(int i) => legMesh[i].TransformPoint(0f, -0.5f, 0f);
    Material BallMat() => tennis != null ? tennis.ballMat : null;

    void Update()
    {
        if (table == null || legs == null) return;

        // ── 공 줍기: 빈 다리 발끝이 바닥 공에 닿으면 부착 (게스트는 호스트 마스크만 반영) ──
        if (!cutsceneStarted && !netGuest)
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

        if (!cutsceneDone || netGuest) return;   // 게스트: 테니스 on/off는 NetVisualSync가 반영

        // ── 홀 진입 = 테니스 회수+기린 해금 / 샤프트로 떨어지면 재부여 ──
        bool nowHall = table.position.z > hallZ && table.position.y > -1.5f;
        bool nowShaft = table.position.z < shaftZ;
        if (nowHall && !inHall)
        {
            inHall = true;
            if (tennis != null) tennis.active = false;
            hint2Until = Time.time + 7f;
        }
        else if (nowShaft && inHall)
        {
            inHall = false;
            if (tennis != null) tennis.active = true;
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

    // ───────────────────────── 씬 경로/샷 읽기(폴백 포함) ─────────────────────────

    static Vector3[] Pts(Transform[] arr, Vector3[] fallback)
    {
        int n = 0;
        if (arr != null) foreach (var t in arr) if (t != null) n++;
        if (n < 2) return fallback;
        var r = new Vector3[n];
        int k = 0;
        foreach (var t in arr) if (t != null) r[k++] = t.position;
        return r;
    }

    Vector3[] EmergePathPts()
    {
        Vector3 hp = HolePos;
        return Pts(emergePath, new Vector3[]
        {
            hp + new Vector3(2.5f, 0.17f, 0f),       // 구멍 깊숙이(벽 속 — 안 보임)
            hp + new Vector3(0.3f, 0.17f, 0f),
            hp + new Vector3(-0.35f, 0.17f, 0.35f),  // S 곡선
            hp + new Vector3(-0.75f, 0.17f, -0.25f),
            hp + new Vector3(-1.05f, 0.17f, 0.1f),   // 머리 정지점
        });
    }

    Vector3[] ExitPathPts(Vector3 head)
    {
        return Pts(exitPath, new Vector3[]
        {
            head,
            new Vector3(6f, -11.7f, -26.5f),
            new Vector3(1.5f, -11.6f, WallZ),
            new Vector3(1.0f, -6f, WallZ),
            new Vector3(0.6f, 0.5f, WallZ),
            new Vector3(0.5f, 0.35f, -20f),
            new Vector3(0.5f, 0.35f, -16f),
        });
    }

    // ───────────────────────── 컷씬: 쥐구멍 애벌레의 힘 전달 ─────────────────────────

    IEnumerator GrantCutscene()
    {
        FreezeTable(true);
        if (cameraFollow != null) cameraFollow.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;

        // 카메라: 쥐구멍을 비스듬히 봄(씬의 카메라 샷 오브젝트 우선)
        Vector3 hp = HolePos;
        Vector3 camP = camCloseup != null ? camCloseup.position : hp + new Vector3(-5.5f, 2.6f, 1.5f);
        Vector3 lookP = camCloseupLook != null ? camCloseupLook.position : hp + Vector3.up * 0.4f;
        yield return CamMoveTo(camP, lookP, camIntroSeconds);   // ★컷 대신 현재 시점에서 부드럽게 이동
        yield return Hold(0.3f, camP, lookP);

        // 1) 애벌레가 쥐구멍에서 S자로 반쯤만 나옴(몸 절반은 구멍 속 — 겹침 없음)
        var path = EmergePathPts();
        Vector3 head = path[path.Length - 1];
        chain = AcquireChain(path[0]);
        yield return MoveChain(path, emergeSeconds, camP, lookP);

        // 2) 대사 2줄 — 기린 컷씬과 동일 형식의 이미지 말풍선(말하는 동안 몸은 정지)
        bubbleAnchor = bubblePoint != null ? bubblePoint.position : head + Vector3.up * 0.45f;
        currentBubble = bubble1;
        yield return Hold(talkSeconds, camP, lookP);
        currentBubble = bubble2;
        yield return Hold(talkSeconds, camP, lookP);
        currentBubble = null;

        // 3) ★테이블 클로즈업(꽉 차게): 테이블 뒤·옆·위에서 테이블을 보고, 애벌레는 배경 → 빛이 화면 안으로 날아온다
        Vector3 tp = tableTop != null ? tableTop.position : table.position;
        Vector3 hDir = head - tp; hDir.y = 0f; hDir = hDir.sqrMagnitude > 1e-4f ? hDir.normalized : Vector3.forward;
        Vector3 sideDir = Vector3.Cross(Vector3.up, hDir);
        Vector3 camW = tp - hDir * glowCamBack + sideDir * glowCamSide + Vector3.up * glowCamUp;
        Vector3 lookW = tp + Vector3.up * glowLookUp;
        yield return CamMoveTo(camW, lookW, camGlowSeconds);

        bool orbIsScene = orb != null;
        var orbGo = orbIsScene ? orb : MakeOrb();
        orbGo.SetActive(true);
        Vector3 from = head + Vector3.up * 0.35f;
        Vector3 to = (tableTop != null ? tableTop.position : table.position) + Vector3.up * 0.2f;
        for (float t = 0; t < lightSeconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / lightSeconds);
            orbGo.transform.position = Vector3.Lerp(from, to, u);
            orbGo.transform.localScale = Vector3.one * Mathf.Lerp(orbStartScale, orbEndScale, u);
            SetCam(camW, lookW); yield return null;
        }
        if (orbIsScene) orbGo.SetActive(false); else Object.Destroy(orbGo);
        if (tennis != null && !netGuest) tennis.unlocked = true;
        yield return FlashTable(camW, lookW);

        // 4) ★어깨 너머 시점: 테이블 뒤에 가만히 서서 시선만 애벌레를 따라감(벽을 타고 구멍으로 퇴장)
        ReleaseChain();
        var exitPts = ExitPathPts(head);
        TennisCaterpillar cat;
        if (goodbyeCat != null)
        {
            // 씬 배치 애벌레: 속도/걸음새/진폭은 컴포넌트 값 그대로(에디터 편집)
            goodbyeCat.destroyOnFinish = false;
            goodbyeCat.gameObject.SetActive(true);
            goodbyeCat.transform.position = exitPts[0];
            goodbyeCat.Go(exitPts);
            cat = goodbyeCat;
        }
        else
        {
            var go = new GameObject("GoodbyeCaterpillar");
            go.transform.position = exitPts[0];
            cat = go.AddComponent<TennisCaterpillar>();
            cat.bunchAmp = 0f;   // 수축 진동 제거
            cat.liftAmp = 0f;    // 위아래 들썩임 제거 — 가로 S웨이브만 남아 뱀처럼 미끄러짐
            cat.BuildAndGo(exitPts, BallMat(), 4.5f, TennisCaterpillar.Gait.Wave);
        }
        Vector3 camS = tp - hDir * shoulderBack + sideDir * shoulderSide + Vector3.up * shoulderUp;
        yield return WatchFromShoulder(cat, camS, exitWatchSeconds);

        // 5) 복귀(스냅 대신 블렌드) + 조작 설명 이미지
        if (cameraFollow != null) { (cameraFollow as CameraFollow)?.BlendFrom(1.2f); cameraFollow.enabled = true; }
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        FreezeTable(false);
        cutsceneDone = true;
        if (guideTex != null) { showGuide = true; guideShownAt = Time.time; }
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
                if (!netGuest) tennis.active = true;   // 게스트는 NetVisualSync가 켬
                for (int i = 0; i < carried.Length; i++)
                    if (carried[i] != null) { carried[i].gameObject.SetActive(false); carried[i] = null; }   // 씬 공은 숨김(재생 시 다시 켬)
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
            SetCam(cp, cl); yield return null;
        }
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", Color.black); r.SetPropertyBlock(mpb);
        }
    }


    // ── 컷씬용 수동 애벌레(씬 배치 자식 8개 우선, 없으면 생성) ──

    Transform[] AcquireChain(Vector3 at)
    {
        if (cutsceneChain != null && cutsceneChain.childCount > 0)
        {
            chainIsScene = true;
            cutsceneChain.gameObject.SetActive(true);
            int n = cutsceneChain.childCount;
            var arr = new Transform[n];
            for (int i = 0; i < n; i++) { arr[i] = cutsceneChain.GetChild(i); arr[i].position = at; }
            return arr;
        }
        chainIsScene = false;
        var made = new Transform[ChainN];
        for (int i = 0; i < ChainN; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(s.GetComponent<Collider>());
            s.name = "CutsceneCat_" + i;
            s.transform.localScale = Vector3.one * (BallR * 2f);
            if (BallMat() != null) s.GetComponent<MeshRenderer>().sharedMaterial = BallMat();
            s.transform.position = at;
            made[i] = s.transform;
        }
        return made;
    }

    void ReleaseChain()
    {
        if (chain == null) return;
        if (chainIsScene) { if (cutsceneChain != null) cutsceneChain.gameObject.SetActive(false); }
        else foreach (var c in chain) if (c != null) Object.Destroy(c.gameObject);
        chain = null;
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
        float endS = total;   // 머리만 경로 끝에 — 몸은 뒤로 늘어서서 겹치지 않음
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float headS = Mathf.SmoothStep(0f, 1f, t / dur) * endS;
            PoseChain(path, headS);
            SetCam(cp, cl); yield return null;
        }
        PoseChain(path, endS);
    }

    void PoseChain(Vector3[] path, float headS)
    {
        float t = Time.time;
        for (int i = 0; i < chain.Length; i++)
        {
            float phase = t * 2.2f * Mathf.PI * 2f - i * 0.95f;
            Vector3 p = Sample(path, headS - i * Spacing + Mathf.Sin(phase) * 0.07f);
            p.y += Mathf.Max(0f, Mathf.Sin(phase)) * 0.06f;
            if (chain[i] != null) chain[i].position = p;
        }
    }

    IEnumerator Hold(float dur, Vector3 cp, Vector3 cl)
    { for (float t = 0; t < dur; t += Time.deltaTime) { SetCam(cp, cl); yield return null; } }

    /// <summary>★현재 카메라 시점에서 목표 위치/시선으로 부드럽게 이동(컷 없음).</summary>
    IEnumerator CamMoveTo(Vector3 p1, Vector3 l1, float dur)
    {
        if (cam == null) yield break;
        Vector3 p0 = cam.position; Quaternion r0 = cam.rotation;
        Quaternion r1 = Quaternion.LookRotation((l1 - p1).normalized, Vector3.up);
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / dur);
            cam.position = Vector3.Lerp(p0, p1, u); cam.rotation = Quaternion.Slerp(r0, r1, u);
            yield return null;
        }
        cam.position = p1; cam.rotation = r1;
    }

    /// <summary>★어깨 너머 고정 위치로 이동한 뒤 가만히 서서, 시선만 애벌레 머리를 부드럽게 추적.</summary>
    IEnumerator WatchFromShoulder(TennisCaterpillar cat, Vector3 camS, float dur)
    {
        if (cam == null) { yield return new WaitForSeconds(dur); yield break; }
        Vector3 p0 = cam.position;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, camShoulderSeconds)));
            Vector3 p = Vector3.Lerp(p0, camS, u);
            Vector3 target = cat != null ? cat.HeadPosition : p + Vector3.forward;
            Vector3 dir = target - p;
            if (dir.sqrMagnitude > 1e-4f)
            {
                Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
                cam.rotation = Quaternion.Slerp(cam.rotation, want, 1f - Mathf.Exp(-lookFollowSpeed * Time.deltaTime));
            }
            cam.position = p;
            yield return null;
        }
    }

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

    /// <summary>폴백(씬 orb 미할당 시): 발광 구슬 + 포인트라이트 생성.</summary>
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

    // ───────────────────────── 에디터 기즈모(선택 시): 경로/카메라 샷 시각화 ─────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        void Poly(Transform[] arr, Color c)
        {
            if (arr == null) return;
            Gizmos.color = c;
            Transform prev = null;
            foreach (var t in arr)
            {
                if (t == null) continue;
                Gizmos.DrawSphere(t.position, 0.12f);
                if (prev != null) Gizmos.DrawLine(prev.position, t.position);
                prev = t;
            }
        }
        Poly(emergePath, new Color(1f, 0.9f, 0.2f));      // 등장 경로: 노랑
        Poly(exitPath, new Color(0.3f, 0.9f, 1f));        // 퇴장 경로: 하늘
        if (holeAnchor != null) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(holeAnchor.position, 0.3f); }
        if (bubblePoint != null) { Gizmos.color = Color.white; Gizmos.DrawWireCube(bubblePoint.position, Vector3.one * 0.2f); }
        void Shot(Transform p, Transform look, Color c)
        {
            if (p == null) return;
            Gizmos.color = c;
            Gizmos.DrawWireCube(p.position, Vector3.one * 0.35f);
            if (look != null) { Gizmos.DrawLine(p.position, look.position); Gizmos.DrawWireSphere(look.position, 0.2f); }
        }
        Shot(camCloseup, camCloseupLook, Color.magenta);
        if (looseBalls != null) { Gizmos.color = Color.green; foreach (var t in looseBalls) if (t != null) Gizmos.DrawWireSphere(t.position, grabRadius); }
    }
#endif

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

        // 컷씬 말풍선 — 기린 컷씬과 동일 형식(이미지, 애벌레 왼쪽에 표시·꼬리가 애벌레를 가리킴)
        if (currentBubble != null && Camera.main != null)
        {
            Vector3 sp = Camera.main.WorldToScreenPoint(bubbleAnchor);
            if (sp.z > 0f)
            {
                float bw = Mathf.Clamp(Screen.width * 0.30f, 240f, 420f);
                float bh = bw * (currentBubble.height / (float)currentBubble.width);
                var r = new Rect(sp.x - bw - 55f, Screen.height - sp.y - bh * 0.5f, bw, bh);
                r.x = Mathf.Max(8f, r.x);
                r.y = Mathf.Clamp(r.y, 8f, Screen.height - bh - 8f);
                GUI.DrawTexture(r, currentBubble, ScaleMode.ScaleToFit, true);
            }
        }

        // 홀 진입 힌트
        if (Time.time < hint2Until)
        {
            string s = L.T("테니스공의 힘은 여기까지 — 화분 속 버튼은 기린의 힘(2)으로 몸을 들어 밟아! 초록 링은 밟지 말 것",
                           "the tennis power rests here — lift yourself with the giraffe power (2) to press the button in the pot! don't touch the green ring");
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
