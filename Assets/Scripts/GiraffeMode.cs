using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★ "테이블 기능" 패턴의 첫 사례 — 평소엔 꺼져 있다가(active=false) 특정 상태에서 켜지는 기능.
/// (앞으로 이런 기능을 늘릴 땐: 테이블 루트에 컴포넌트를 붙이고 `active` 게이트로 평소 무동작 → 게임 상태가 켬.)
///
/// 지라프(기린) 모드: 켜지면 각 다리를 키로 늘리고 줄일 수 있다(다리가 길어져 키 큰 기린처럼).
///   - 최소 길이 = 초기 다리 길이(scale.y), 최대 = 그 maxMultiplier(7)배.
///   - Leg_FR: R=짧게 / Y=길게,  Leg_FL: Q=짧게 / E=길게,
///     Leg_BR: P=짧게 / ]=길게,  Leg_BL: U=짧게 / O=길게.
///   - 꺼지면(active=false) 다리는 초기 길이로 서서히 복귀.
/// 다리 메시(Leg_FR 등)는 다리 피벗 Rigidbody의 자식이라, 메시의 Y스케일/위치를 바꾸면
/// 콜라이더 길이가 변해 발이 땅을 밀어 몸이 들린다(물리로 자연스럽게).
/// TablePlayer 루트에 부착한다.
/// </summary>
public class GiraffeMode : MonoBehaviour, ICheatUnlockable
{
    [Tooltip("평소 꺼짐. 게임 상태가 이걸 켜면 다리 신축 활성. (테스트는 toggleKey 로)")]
    public bool active = false;
    [Tooltip("true면 토글키 무시(=기능 미개방). Jungle은 기린 컷신 전까지 잠김.")]
    public bool locked = false;
    [Tooltip("토글 키(기본 숫자 2). 실제 게임에선 상태로 active 를 켜도 됨.")]
    public Key toggleKey = Key.Digit2;
    [Tooltip("최대 길이 = 초기 길이 × 이 배수")]
    public float maxMultiplier = 7f;
    [Tooltip("길이 변화 속도(스케일/초)")]
    public float speed = 0.8f;

    [Header("전체 다리 동시 신축 (개별키 4개 동시누름은 키보드 고스팅으로 안 먹힐 수 있어 단일키 제공)")]
    [Tooltip("이 키 하나로 네 다리 모두 늘린다(기본 = )")]
    public Key allLonger = Key.Equals;
    [Tooltip("이 키 하나로 네 다리 모두 줄인다(기본 - )")]
    public Key allShorter = Key.Minus;

    /// <summary>기린의 힘을 한 번이라도 얻었는가(컷신 부여 또는 직접 사용). 조작법 메뉴가 기린 조작 표시 여부 판단에 사용.</summary>
    public static bool Unlocked;

    // 치트모드: 잠금 해제 → 토글키(2)가 바로 먹힘.
    public void CheatUnlock() { locked = false; Unlocked = true; }

    static readonly string[] LegNames = { "Leg_FR", "Leg_FL", "Leg_BR", "Leg_BL" };
    static readonly Key[] Shorter = { Key.R, Key.Q, Key.P, Key.U };
    static readonly Key[] Longer  = { Key.Y, Key.E, Key.RightBracket, Key.O };

    Transform[] mesh;
    float[] baseLen, len;
    // 다리 피벗 Rigidbody. 길이 변할 때마다 관성/무게중심을 '현재 길이 기준'으로 안정화(StabilizeInertia).
    Rigidbody[] legRb;
    // ★ 멀티 대비: 신축 입력도 TableInput 경유(각 다리 주인의 키셋 소유). legSets[i]=그 다리의 키셋.
    TableInput tableInput;
    LegControlSet[] legSets;

    // ★ 멀티 게스트 모드: 입력/물리(신축·관성·무게중심)는 전부 끄고, NetVisualSync가 주는
    //   시각 상태(다리 길이·스킨)만 NetApplyVisual로 반영한다. (물리는 호스트 전용)
    [HideInInspector] public bool netVisualOnly;

    /// <summary>현재 다리 길이(NetVisualSync가 호스트에서 읽어 동기화).</summary>
    public float GetLen(int i) => len != null && i >= 0 && i < len.Length ? len[i] : 0f;

    /// <summary>게스트 시각 적용(물리 없음): 다리 길이 + 기린 스킨. 매 프레임 호출해도 무해(멱등).</summary>
    public void NetApplyVisual(bool on, float l0, float l1, float l2, float l3)
    {
        if (mesh == null || len == null) return;   // Start 전 — 다음 프레임 재시도됨
        float[] ls = { l0, l1, l2, l3 };
        for (int i = 0; i < mesh.Length && i < 4; i++)
        {
            if (mesh[i] == null || ls[i] <= 0f) continue;
            if (Mathf.Abs(len[i] - ls[i]) < 1e-4f) continue;
            len[i] = ls[i];
            var s = mesh[i].localScale; s.y = ls[i]; mesh[i].localScale = s;
            var p = mesh[i].localPosition; p.y = -ls[i] * 0.5f; mesh[i].localPosition = p;
        }
        if (on && !skinned) ApplyGiraffeSkin();
        else if (!on && skinned) RemoveGiraffeSkin();
        if (skinned) UpdateLegTiling();
        active = on;   // 좌상단 기린 아이콘 표시 일치
    }

    [Header("기린 무늬 스킨(지라프 모드 켜질 때 몸통+다리)")]
    [Tooltip("몸통(상판) 무늬 스케일(단위당 타일 수, 월드). 클수록 점 작아짐.")]
    public float patternScale = 0.6f;
    [Tooltip("다리 점의 월드 크기(m). 다리가 얇아 몸통과 같은 크기면 면이 단색이 됨 → 다리는 약간 작게(또렷). 정사각 점, 신장해도 일정.")]
    public float legSpotWorld = 0.08f;
    Renderer bodyRenderer;
    Renderer[] legRenderers;
    MeshFilter bodyMF;
    MeshFilter[] legMF;
    Mesh origBodyMesh;
    Mesh[] origLegMesh;
    Material origBody;
    Material[] origLegs;
    Material giraffeBody;
    Material[] giraffeLegs;
    Texture2D giraffeTex;
    Mesh bodyNet, legBox;
    bool skinned;

    [Header("지라프 모드 안정화/힘 (구조 변경 없이 물리값만)")]
    [Tooltip("지라프 모드일 때 다리 모터 힘 배수(긴 다리로 몸을 밀 힘).")]
    public float giraffeForceMul = 2.6f;
    [Tooltip("늘어난 다리 평균 길이 1당 상판 무게중심을 아래로 내리는 양(다리 끝쪽으로, 바닥 무겁게=안정).")]
    public float comDropPerStretch = 0.6f;
    [Tooltip("무게중심 최대 하강. ⚠️너무 깊으면(예 4) 힌지 앵커-무게중심 지렛대가 길어져 관절 솔버 폭발(NaN) 위험 — 2.2 안전.")]
    public float maxComDrop = 2.2f;
    [Tooltip("무게중심 이동 속도(유닛/초). 다리가 늘어난 '뒤에' 천천히 내려가고, 줄면 천천히 올라온다(급변 금지 — 움직임 위화감 방지).")]
    public float comDropSpeed = 1.0f;
    [Tooltip("다리가 늘어났을 때 상판 회전 감쇠 추가치(흔들림/휘청임 억제 — 움직임을 일반 모드처럼 차분하게. 무게중심 하강을 얕게 줄인 대신 이걸로 안정 보상).")]
    public float stretchAngularDamping = 1.6f;
    Rigidbody boardRb;        // 상판(샤시) 강체
    ArticulationBody artBoard;          // ★ ArticulationBody 테이블(테스트 씬)의 상판 — boardRb 없을 때 사용
    ArtLegController[] artCtrls;        // ★ art 다리 컨트롤러(forceMultiplier 전달용)
    Vector3 boardComBase;     // 원래 무게중심
    float boardAngDampBase;   // 원래 회전 감쇠
    float comDrop;            // 현재 적용 중인 하강량(스무딩 상태)
    bool comStored;
    LegController[] legCtrls;

    void Start()
    {
        int n = LegNames.Length;
        mesh = new Transform[n];
        baseLen = new float[n];
        len = new float[n];
        legRb = new Rigidbody[n];
        for (int i = 0; i < n; i++)
        {
            mesh[i] = FindDeep(transform.root, LegNames[i]);
            if (mesh[i] == null) continue;
            baseLen[i] = mesh[i].localScale.y; len[i] = baseLen[i];
            legRb[i] = mesh[i].GetComponentInParent<Rigidbody>();
            if (legRb[i] != null) LockInertia(i);
        }

        // ★ 입력 허브 + 다리별 키셋 파악(신축 입력을 그 다리 주인의 LegInput에서 읽기 위함)
        tableInput = TableInput.Of(this);
        legSets = new LegControlSet[n];
        for (int i = 0; i < n; i++)
        {
            legSets[i] = (LegControlSet)i;   // 폴백(구 매핑)
            var lc = legRb[i] != null ? legRb[i].GetComponent<LegController>() : null;
            if (lc != null) legSets[i] = lc.controlSet;
            // ★ ArticulationBody 테이블: 피벗에 ArtLegController
            if (lc == null && mesh[i] != null)
            {
                var alc = mesh[i].GetComponentInParent<ArtLegController>();
                if (alc != null) legSets[i] = alc.controlSet;
            }
        }
        // ★ art 보드/컨트롤러(테스트 씬) — Rigidbody 경로가 비면 이쪽을 사용
        foreach (var ab in transform.root.GetComponentsInChildren<ArticulationBody>(true))
            if (ab.isRoot) { artBoard = ab; break; }
        artCtrls = transform.root.GetComponentsInChildren<ArtLegController>(true);

        // 상판(샤시) 강체 = 다리 힌지가 연결된 몸체. 다리 컨트롤러들도 수집.
        for (int i = 0; i < n && boardRb == null; i++)
            if (legRb[i] != null) { var hj = legRb[i].GetComponent<HingeJoint>(); if (hj != null) boardRb = hj.connectedBody; }
        if (boardRb == null) boardRb = GetComponentInParent<Rigidbody>();
        legCtrls = transform.root.GetComponentsInChildren<LegController>(true);

        // ★ NaN(position=∞) 폭발 방지: 상판 Rigidbody의 maxLinearVelocity 기본값이 1e16(무제한)이라
        //   긴 다리+모터 힘으로 솔버가 발산하면 무한 속도로 튕겨 "Invalid AABB"/NaN 크래시가 남.
        //   기린 모드가 있는 모든 씬에서 전 바디에 유한 상한 적용(다리는 LegController가 더 낮게 캡 — Min이라 안전).
        foreach (var rb in transform.root.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null) continue;
            rb.maxLinearVelocity = Mathf.Min(rb.maxLinearVelocity, 40f);
            rb.maxAngularVelocity = Mathf.Min(rb.maxAngularVelocity, 20f);
            rb.maxDepenetrationVelocity = Mathf.Min(rb.maxDepenetrationVelocity, 3f);   // ⚠️1.2는 잔 튐 증가(반복 보정) — 3 유지
            // 관절 안정성은 '연결된 두 바디' 모두의 솔버 반복에 의존 — 다리는 LegController가 40인데
            // 상판은 기본 6이라 상판 쪽에서 관절이 발산(NaN)할 수 있음 → 전 바디 40 보장.
            rb.solverIterations = Mathf.Max(rb.solverIterations, 40);
            rb.solverVelocityIterations = Mathf.Max(rb.solverVelocityIterations, 40);
        }

        // 무늬 스킨용 렌더러/메시필터 수집(몸통=TableTop, 다리=각 leg 메시).
        var ttgo = GameObject.Find("TableTop");
        if (ttgo != null) { bodyRenderer = ttgo.GetComponent<Renderer>(); bodyMF = ttgo.GetComponent<MeshFilter>(); }
        legRenderers = new Renderer[n];
        legMF = new MeshFilter[n];
        for (int i = 0; i < n; i++)
            if (mesh[i] != null) { legRenderers[i] = mesh[i].GetComponent<Renderer>(); legMF[i] = mesh[i].GetComponent<MeshFilter>(); }
    }

    // ===== 기린 무늬 스킨 (UV 방식) =====
    // 상판: 십자전개(cross-net) UV 박스로 교체 → 윗면 무늬가 모서리 넘어 옆면으로 연속.
    // 다리: 균일 UV 박스(모든 옆면 V=길이) + 월드 스케일 타일 → 몸통과 같은 점 크기, 신장 시 보정.
    void EnsureSkinAssets()
    {
        if (giraffeTex == null) giraffeTex = MakeGiraffeTexture();
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (bodyNet == null && bodyRenderer != null) bodyNet = BuildCrossNetBox(bodyRenderer.transform.lossyScale, patternScale);
        if (legBox == null) legBox = UniformLegBox();
        if (giraffeBody == null)
        {
            giraffeBody = new Material(sh) { name = "GiraffeBody(dyn)" };
            giraffeBody.SetTexture("_BaseMap", giraffeTex);
            giraffeBody.SetFloat("_Smoothness", 0.12f);
        }
        if (giraffeLegs == null)
        {
            giraffeLegs = new Material[mesh.Length];
            for (int i = 0; i < mesh.Length; i++)
            {
                var m = new Material(sh) { name = "GiraffeLeg(dyn)" };
                m.SetTexture("_BaseMap", giraffeTex);
                m.SetFloat("_Smoothness", 0.12f);
                giraffeLegs[i] = m;
            }
        }
    }

    void ApplyGiraffeSkin()
    {
        EnsureSkinAssets();
        if (bodyRenderer != null) { origBody = bodyRenderer.sharedMaterial; bodyRenderer.sharedMaterial = giraffeBody; }
        if (bodyMF != null && bodyNet != null) { origBodyMesh = bodyMF.sharedMesh; bodyMF.sharedMesh = bodyNet; }
        if (legRenderers != null)
        {
            origLegs = new Material[legRenderers.Length];
            origLegMesh = new Mesh[legRenderers.Length];
            for (int i = 0; i < legRenderers.Length; i++)
            {
                if (legRenderers[i] != null && giraffeLegs != null && i < giraffeLegs.Length)
                { origLegs[i] = legRenderers[i].sharedMaterial; legRenderers[i].sharedMaterial = giraffeLegs[i]; }
                if (legMF != null && i < legMF.Length && legMF[i] != null)
                { origLegMesh[i] = legMF[i].sharedMesh; legMF[i].sharedMesh = legBox; }
            }
        }
        skinned = true;
        UpdateLegTiling();
    }

    void RemoveGiraffeSkin()
    {
        if (bodyRenderer != null && origBody != null) bodyRenderer.sharedMaterial = origBody;
        if (bodyMF != null && origBodyMesh != null) bodyMF.sharedMesh = origBodyMesh;
        if (legRenderers != null && origLegs != null)
            for (int i = 0; i < legRenderers.Length; i++)
            {
                if (legRenderers[i] != null && origLegs[i] != null) legRenderers[i].sharedMaterial = origLegs[i];
                if (legMF[i] != null && origLegMesh[i] != null) legMF[i].sharedMesh = origLegMesh[i];
            }
        skinned = false;
    }

    // 다리 무늬: legSpotWorld 크기의 '정사각 점'이 폭/길이에 걸쳐 일정하게(신장해도 점 크기 유지) → 자연스러운 기린 다리.
    void UpdateLegTiling()
    {
        if (giraffeLegs == null) return;
        const float G = 3f;                          // 텍스처 한 타일당 셀 수
        float denom = Mathf.Max(1e-4f, legSpotWorld * G);
        for (int i = 0; i < giraffeLegs.Length; i++)
        {
            if (giraffeLegs[i] == null || mesh[i] == null) continue;
            float w = mesh[i].lossyScale.x;          // 다리 단면 폭(월드)
            giraffeLegs[i].SetTextureScale("_BaseMap", new Vector2(w / denom, len[i] / denom));
        }
    }

    // 모든 옆면 V축이 길이(Y)를 따라가는 균일 UV 박스(±0.5). (기본 큐브는 면마다 UV 방향이 달라 타일 보정 불균일)
    static Mesh UniformLegBox()
    {
        float h = 0.5f;
        Vector3[] c = {
            new Vector3(-h,-h, h), new Vector3( h,-h, h), new Vector3( h, h, h), new Vector3(-h, h, h), // +Z
            new Vector3( h,-h,-h), new Vector3(-h,-h,-h), new Vector3(-h, h,-h), new Vector3( h, h,-h), // -Z
            new Vector3( h,-h, h), new Vector3( h,-h,-h), new Vector3( h, h,-h), new Vector3( h, h, h), // +X
            new Vector3(-h,-h,-h), new Vector3(-h,-h, h), new Vector3(-h, h, h), new Vector3(-h, h,-h), // -X
            new Vector3(-h, h, h), new Vector3( h, h, h), new Vector3( h, h,-h), new Vector3(-h, h,-h), // +Y
            new Vector3(-h,-h,-h), new Vector3( h,-h,-h), new Vector3( h,-h, h), new Vector3(-h,-h, h), // -Y
        };
        var uv = new Vector2[24];
        var tri = new int[36];
        for (int f = 0; f < 6; f++)
        {
            int b = f * 4;
            uv[b] = new Vector2(0, 0); uv[b + 1] = new Vector2(1, 0); uv[b + 2] = new Vector2(1, 1); uv[b + 3] = new Vector2(0, 1);
            int t = f * 6;
            tri[t] = b; tri[t + 1] = b + 1; tri[t + 2] = b + 2;
            tri[t + 3] = b; tri[t + 4] = b + 2; tri[t + 5] = b + 3;
        }
        var m = new Mesh { name = "GiraffeLegBox" };
        m.vertices = c; m.uv = uv; m.triangles = tri; m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 십자전개(cross-net) 박스(±0.5 로컬). UV를 '월드 위치'로 매핑하되 옆면을 윗면 모서리에서 펼쳐
    // 윗면 무늬가 옆면으로 자연스럽게 이어지게(접힘). L=렌더러 lossyScale, s=patternScale(타일/단위).
    static Mesh BuildCrossNetBox(Vector3 L, float s)
    {
        float h = 0.5f;
        // 면 순서: +Z,-Z,+X,-X,+Y,-Y (UniformLegBox 와 동일 배치)
        Vector3[] c = {
            new Vector3(-h,-h, h), new Vector3( h,-h, h), new Vector3( h, h, h), new Vector3(-h, h, h), // +Z
            new Vector3( h,-h,-h), new Vector3(-h,-h,-h), new Vector3(-h, h,-h), new Vector3( h, h,-h), // -Z
            new Vector3( h,-h, h), new Vector3( h,-h,-h), new Vector3( h, h,-h), new Vector3( h, h, h), // +X
            new Vector3(-h,-h,-h), new Vector3(-h,-h, h), new Vector3(-h, h, h), new Vector3(-h, h,-h), // -X
            new Vector3(-h, h, h), new Vector3( h, h, h), new Vector3( h, h,-h), new Vector3(-h, h,-h), // +Y(top)
            new Vector3(-h,-h,-h), new Vector3( h,-h,-h), new Vector3( h,-h, h), new Vector3(-h,-h, h), // -Y(bottom)
        };
        float Wx = L.x * 0.5f, Wy = L.y * 0.5f, Wz = L.z * 0.5f;   // 월드 반-크기
        var uv = new Vector2[24];
        var tri = new int[36];
        for (int f = 0; f < 6; f++)
        {
            int b = f * 4;
            for (int k = 0; k < 4; k++)
            {
                Vector3 W = new Vector3(c[b + k].x * L.x, c[b + k].y * L.y, c[b + k].z * L.z);
                Vector2 t;
                switch (f)
                {
                    case 4: t = new Vector2(W.x, W.z); break;                              // +Y top
                    case 5: t = new Vector2(W.x, W.z); break;                              // -Y bottom
                    case 2: t = new Vector2(Wx + (Wy - W.y), W.z); break;                  // +X (윗 모서리에서 아래로 펼침)
                    case 3: t = new Vector2(-Wx - (Wy - W.y), W.z); break;                 // -X
                    case 0: t = new Vector2(W.x, Wz + (Wy - W.y)); break;                  // +Z
                    default: t = new Vector2(W.x, -Wz - (Wy - W.y)); break;                // -Z
                }
                uv[b + k] = t * s;
            }
            int ti = f * 6;
            tri[ti] = b; tri[ti + 1] = b + 1; tri[ti + 2] = b + 2;
            tri[ti + 3] = b; tri[ti + 4] = b + 2; tri[ti + 5] = b + 3;
        }
        var m = new Mesh { name = "GiraffeBodyNet" };
        m.vertices = c; m.uv = uv; m.triangles = tri; m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 그물무늬(reticulated) 기린 텍스처를 절차적으로 생성(타일 가능). 크림 선 사이 갈색 패치.
    static float TexHash(int n) { float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
    // GLSL식 smoothstep(edge0,edge1,x) → 0..1. (Unity Mathf.SmoothStep 은 from→to 보간이라 의미가 다름 — 쓰면 안 됨)
    static float SStep(float e0, float e1, float x)
    {
        if (Mathf.Abs(e1 - e0) < 1e-6f) return x < e0 ? 0f : 1f;
        float u = Mathf.Clamp01((x - e0) / (e1 - e0));
        return u * u * (3f - 2f * u);
    }
    static Texture2D MakeGiraffeTexture()
    {
        int S = 256, G = 3;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var seeds = new Vector2[G * G];
        var tint = new float[G * G];
        for (int y = 0; y < G; y++)
            for (int x = 0; x < G; x++)
            {
                int idx = y * G + x;
                seeds[idx] = new Vector2((x + 0.15f + 0.7f * TexHash(idx * 2 + 1)) / G, (y + 0.15f + 0.7f * TexHash(idx * 2 + 7)) / G);
                tint[idx] = TexHash(idx * 3 + 5);
            }
        Color cream = new Color(0.93f, 0.86f, 0.66f);
        var px = new Color[S * S];
        for (int j = 0; j < S; j++)
            for (int i = 0; i < S; i++)
            {
                Vector2 p = new Vector2((i + 0.5f) / S, (j + 0.5f) / S);
                float d1 = 9f, d2 = 9f; int n1 = 0;
                for (int s = 0; s < seeds.Length; s++)
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            Vector2 q = seeds[s] + new Vector2(ox, oy);
                            float d = (p - q).sqrMagnitude;
                            if (d < d1) { d2 = d1; d1 = d; n1 = s; }
                            else if (d < d2) d2 = d;
                        }
                float edge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
                float t = SStep(0.012f, 0.06f, edge);             // 0=경계(크림선) → 1=패치내부(갈색)
                Color brown = Color.Lerp(new Color(0.40f, 0.23f, 0.09f), new Color(0.55f, 0.34f, 0.14f), tint[n1]);
                px[j * S + i] = Color.Lerp(cream, brown, t);
            }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    // 다리 길이가 변할 때마다 '현재 길이 기준' 관성/무게중심으로 안정화.
    // ⚠️ 예전엔 초기(짧은 다리) 관성(0.04, 0.00, 0.04)을 긴 다리에 강제했는데, Y(긴축) 성분이 0에 가까워
    //    솔버가 1/I 로 폭주→NaN(IsFinite), 또 너무 작은 관성이라 긴 다리가 무게를 못 버텨 축 처짐.
    //    → 콜라이더 기준 자연 관성/무게중심을 재계산(긴 다리는 큰 관성=안정·처짐방지)하고 0근처 성분만 클램프.
    //    모터는 '목표 각속도' 구동이라 관성이 커도 스윙 속도(체감)는 동일 — 가속만 부드러워져 들썩임도 완화.
    void LockInertia(int i)
    {
        var rb = legRb[i];
        if (rb == null) return;
        rb.ResetInertiaTensor();      // 현재 콜라이더(현재 길이) 기준 자연 관성 = 긴 다리는 큰 관성(무거움)
        rb.ResetCenterOfMass();       // 무게중심도 실제 다리 중심으로
        Vector3 it = rb.inertiaTensor;
        // 0 근처 성분만 minI 로 올려 솔버 폭주(1/I→NaN) 방지. 최대는 제한하지 않는다 —
        // 관성을 깎으면(예전 cap) 긴 지렛대가 미세 입력·접촉에 과민반응해 뒤집힌 채 흔들면 폭주함.
        // 무거운 자연 관성이 외력에 안정적이고, 오버슈트는 LegController의 '각속도 길이반비례'가 막는다.
        const float minI = 0.05f;
        it.x = Mathf.Max(it.x, minI); it.y = Mathf.Max(it.y, minI); it.z = Mathf.Max(it.z, minI);
        rb.inertiaTensor = it;
        if (rb.maxAngularVelocity > 20f) rb.maxAngularVelocity = 20f;   // 폭주 상한(안전망)
    }

    /// <summary>다리 길이를 초기로 즉시 되돌린다('1' 리셋에서 호출).</summary>
    public void ResetLegs()
    {
        if (mesh == null) return;
        for (int i = 0; i < mesh.Length; i++)
        {
            if (mesh[i] == null) continue;
            len[i] = baseLen[i];
            var s = mesh[i].localScale; s.y = baseLen[i]; mesh[i].localScale = s;
            var p = mesh[i].localPosition; p.y = -baseLen[i] * 0.5f; mesh[i].localPosition = p;
        }
        Physics.SyncTransforms();   // AutoSyncTransforms=0 — 관성 재계산 전에 스케일을 물리에 반영
        for (int i = 0; i < mesh.Length; i++)
            if (mesh[i] != null && legRb[i] != null) LockInertia(i);
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // 켜져 있을 때 좌상단에 작은 기린 아이콘 표시.
    // ⚠️ IMGUI 기본 폰트가 컬러 이모지(🦒)를 못 그려서, 작은 기린을 텍스처로 직접 그린다.
    Texture2D giraffeIcon;
    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (!active) return;
        if (giraffeIcon == null) giraffeIcon = MakeGiraffeIcon();
        float s = 3.0f; // 픽셀 확대
        GUI.DrawTexture(new Rect(12f, 10f, giraffeIcon.width * s, giraffeIcon.height * s), giraffeIcon, ScaleMode.StretchToFill, true);
    }

    static Texture2D MakeGiraffeIcon()
    {
        int W = 20, H = 28;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
        t.SetPixels(px);
        Color tan = new Color(0.95f, 0.78f, 0.42f, 1f);
        Color spot = new Color(0.5f, 0.31f, 0.12f, 1f);
        void R(int x0, int y0, int x1, int y1, Color c)
        { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (x >= 0 && x < W && y >= 0 && y < H) t.SetPixel(x, y, c); }
        // 다리(4) → 몸통 → 목 → 머리/뿔 → 꼬리 (텍스처 y는 아래→위, 머리가 위)
        R(6, 0, 7, 7, tan); R(9, 0, 10, 7, tan); R(12, 0, 13, 7, tan); R(14, 0, 15, 7, tan);
        R(5, 7, 16, 13, tan);            // 몸통
        R(4, 9, 5, 12, tan);             // 꼬리
        R(11, 12, 13, 23, tan);          // 목
        R(12, 22, 17, 25, tan);          // 머리
        R(11, 25, 11, 27, tan); R(13, 25, 13, 27, tan); // 뿔 2개
        // 점무늬
        R(7, 9, 8, 10, spot); R(11, 9, 12, 10, spot); R(14, 10, 15, 11, spot);
        R(11, 15, 12, 16, spot); R(11, 19, 12, 20, spot);
        t.Apply();
        return t;
    }

    void Update()
    {
        if (netVisualOnly) return;   // ★ 멀티 게스트: 입력/물리 없음 — NetVisualSync가 시각만 반영
        if (mesh == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (!locked && kb[toggleKey].wasPressedThisFrame) active = !active;
        if (active) Unlocked = true;   // 한 번 켜지면 조작법 메뉴에 기린 조작 노출

        // 기린 무늬 스킨 on/off (켜질 때 몸통+다리에 무늬, 꺼지면 원래대로)
        if (active && !skinned) ApplyGiraffeSkin();
        else if (!active && skinned && AllLegsBase()) RemoveGiraffeSkin();

        float dt = Time.deltaTime;
        // 전체 동시 신축(단일키) — 개별키 4개 동시누름의 키보드 고스팅 회피
        bool allUp   = active && allLonger  != Key.None && kb[allLonger].isPressed;
        bool allDown = active && allShorter != Key.None && kb[allShorter].isPressed;
        int changedMask = 0;
        for (int i = 0; i < mesh.Length; i++)
        {
            if (mesh[i] == null) continue;
            float target = len[i];
            if (active)
            {
                // 신축 입력 = 그 다리 주인의 LegInput(TableInput 경유 — 로컬 키보드/네트워크 동일 경로)
                LegInput li = tableInput != null && legSets != null ? tableInput.Get(legSets[i])
                              : new LegInput { longer = kb[Longer[i]].isPressed, shorter = kb[Shorter[i]].isPressed };
                if (allUp   || li.longer)  target += speed * dt;
                if (allDown || li.shorter) target -= speed * dt;
            }
            else
            {
                // 꺼지면 초기 길이로 서서히 복귀
                target = Mathf.MoveTowards(target, baseLen[i], speed * dt);
            }
            target = Mathf.Clamp(target, baseLen[i], baseLen[i] * maxMultiplier);

            if (Mathf.Abs(target - len[i]) > 1e-5f)
            {
                len[i] = target;
                var s = mesh[i].localScale; s.y = target; mesh[i].localScale = s;
                var p = mesh[i].localPosition; p.y = -target * 0.5f; mesh[i].localPosition = p;
                changedMask |= 1 << i;
            }
        }
        if (changedMask != 0)
        {
            // ⚠️ 프로젝트가 AutoSyncTransforms=0 — 스케일 변경을 물리에 먼저 동기화하지 않으면
            //    ResetInertiaTensor가 '낡은 콜라이더' 기준으로 관성을 계산한다(스케일-관성 불일치 → 불안정).
            Physics.SyncTransforms();
            for (int i = 0; i < mesh.Length; i++)
                if ((changedMask & (1 << i)) != 0 && legRb[i] != null) LockInertia(i); // 길이 기준 관성/무게중심 안정화
        }

        if (skinned) UpdateLegTiling();   // 다리 길이에 맞춰 무늬 타일 갱신(월드 스케일 유지)

        UpdateGiraffeStability();
    }

    // 지라프 모드 안정화 (요구사항: 평소=일반 무게중심 그대로 → 다리가 '늘어나면' 그때 천천히
    // 다리 끝쪽(아래)으로 무게중심 이동 → 넘어지지 않게. 움직임은 일반 모드처럼 차분하게.)
    //   ① 목표 하강량 = 늘어난 평균 길이에 비례(안 늘었으면 0 = 일반 모드와 동일).
    //   ② 실제 하강은 comDropSpeed 로 '천천히' 따라감(즉시 점프 금지 — 급변이 움직임 위화감의 원인).
    //   ③ 늘어난 만큼 상판 회전 감쇠를 더해 긴 다리 특유의 휘청임을 눌러 일반 모드 감각 유지.
    // 구조·질량 변경 없이 CoM·damping·force만 — 크래시 안전.
    void UpdateGiraffeStability()
    {
        if (boardRb == null && artBoard == null) return;
        if (boardRb == null) { UpdateArtStability(); return; }   // ★ ArticulationBody 경로
        if (!comStored) { boardComBase = boardRb.centerOfMass; boardAngDampBase = boardRb.angularDamping; comStored = true; }

        float avgStretch = 0f;
        if (active)
        {
            for (int i = 0; i < len.Length; i++) avgStretch += Mathf.Max(0f, len[i] - baseLen[i]);
            if (len.Length > 0) avgStretch /= len.Length;
        }
        float targetDrop = Mathf.Min(maxComDrop, avgStretch * comDropPerStretch);   // 안 늘었으면 0
        comDrop = Mathf.MoveTowards(comDrop, targetDrop, comDropSpeed * Time.deltaTime);
        boardRb.centerOfMass = boardComBase + new Vector3(0f, -comDrop, 0f);        // 상판 로컬 아래(다리 끝 방향)

        // 늘어난 정도(0~1)에 비례해 회전 감쇠 추가 → 흔들림 억제(일반 모드 감각)
        float stretchNorm = maxComDrop > 1e-4f ? Mathf.Clamp01(comDrop / maxComDrop) : 0f;
        boardRb.angularDamping = boardAngDampBase + stretchAngularDamping * stretchNorm;

        float mul = active ? giraffeForceMul : 1f;
        if (legCtrls != null) foreach (var lc in legCtrls) if (lc != null) lc.forceMultiplier = mul;
        if (artCtrls != null) foreach (var ac in artCtrls) if (ac != null) ac.forceMultiplier = mul;
    }

    // ★ ArticulationBody 테이블(테스트 씬)의 안정화 — Rigidbody 판과 동일한 정책(CoM 하강 스무딩+감쇠)
    void UpdateArtStability()
    {
        if (!comStored)
        {
            artBoard.automaticCenterOfMass = false;   // 수동 CoM 제어
            boardComBase = artBoard.centerOfMass;
            boardAngDampBase = artBoard.angularDamping;
            comStored = true;
        }
        float avgStretch = 0f;
        if (active && len != null)
        {
            for (int i = 0; i < len.Length; i++) avgStretch += Mathf.Max(0f, len[i] - baseLen[i]);
            if (len.Length > 0) avgStretch /= len.Length;
        }
        float targetDrop = Mathf.Min(maxComDrop, avgStretch * comDropPerStretch);
        comDrop = Mathf.MoveTowards(comDrop, targetDrop, comDropSpeed * Time.deltaTime);
        artBoard.centerOfMass = boardComBase + new Vector3(0f, -comDrop, 0f);
        float stretchNorm = maxComDrop > 1e-4f ? Mathf.Clamp01(comDrop / maxComDrop) : 0f;
        artBoard.angularDamping = boardAngDampBase + stretchAngularDamping * stretchNorm;
        float mul = active ? giraffeForceMul : 1f;
        if (artCtrls != null) foreach (var ac in artCtrls) if (ac != null) ac.forceMultiplier = mul;
    }

    bool AllLegsBase()
    {
        for (int i = 0; i < len.Length; i++)
            if (len[i] > baseLen[i] + 0.02f) return false;
        return true;
    }
}
