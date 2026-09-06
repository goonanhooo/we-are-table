using UnityEngine;

/// <summary>
/// ★ 우주오징어 모드(2026-07-07) — 무중력에서 다리 신축 키(FR=Y/FL=E/BR=]/BL=O — 기린 'longer'와 같은 키)를
/// 누르면 그 다리 '발끝에서 분사'해 다리 축 방향(발→피벗)으로 추진된다.
/// 다리를 스윙/스핀으로 조준하고 뿜어서 오징어처럼 날아다니는 조작감.
/// 테이블 기능 패턴: Table.prefab 루트 부착, 평소 active=false(무동작).
/// 해금 방식은 미정 — 지금은 우주 씬(SpaceStage)이 입장 시 active=true 부여, 치트는 unlocked만.
/// active 동안 전 바디 useGravity=false + 표류 감쇠(driftDamping) 적용, 끄면 원복.
/// ⚠️기린 모드와 같은 키를 쓰므로 우주 씬은 GiraffeMode.locked=true(SpaceStage가 설정).
/// 멀티: 물리는 호스트 전용 — 게스트는 netVisualOnly(NetLegLink.ApplyGuestMode)로 분사 파티클만 호스트 마스크(NetVisualSync.VState.squid)대로 표시.
/// ★2026-09-06 외형: 켜져 있고(thrustLocked 아님) 힘을 받은 뒤엔 우주오징어처럼 살짝 보라(tintRenderers에 _BaseColor 프로퍼티 블록) + 눈 두 개(eyes, 프리팹 자식 SquidEyes 활성).
///   게스트는 VState.squid의 bit4(=켜짐)로 같은 외형. 컷씬의 발광 블록과 공존(_EmissionColor만 따로).
/// </summary>
public class SquidMode : MonoBehaviour, ICheatUnlockable
{
    [Tooltip("켜짐 여부(평소 꺼짐 — SpaceStage가 씬 입장 시 켬).")]
    public bool active;
    public bool unlocked;
    /// <summary>★2026-09-05 우주 도입 컷씬용: true면 무중력(active)은 유지하되 분사 입력만 막음(오징어가 힘을 주기 전).</summary>
    [HideInInspector] public bool thrustLocked;
    /// <summary>한 번이라도 켜졌는가 — PauseMenu 조작법에 '우주오징어' 페이지 노출용(GiraffeMode.Unlocked 패턴).</summary>
    public static bool Unlocked;
    /// <summary>★2026-09-06 멀티 게스트: 물리 없이 분사 파티클만(호스트가 NetVisualSync로 보낸 마스크). NetLegLink.ApplyGuestMode가 설정.</summary>
    [HideInInspector] public bool netVisualOnly;
    [HideInInspector] public int netJetMask;
    /// <summary>호스트: 지금 분사 중인 다리 비트마스크(NetVisualSync가 발행).</summary>
    public int JetMask { get; private set; }
    bool netActive;
    public void NetApplyVisual(bool on, int mask) { netActive = on; netJetMask = on ? mask : 0; }
    /// <summary>오징어 외형(보라+눈)을 보여야 하는가 — 호스트: 켜짐·힘 받음, 게스트: 호스트가 보낸 bit.</summary>
    public bool LookOn => netVisualOnly ? netActive : (active && !thrustLocked);
    [Tooltip("다리 하나가 내는 추진력(N). 4다리 동시 분사 = ×4. 테이블 총질량 ~7.6kg.")]
    public float thrustForce = 12f;
    [Tooltip("무중력 표류 감쇠(linearDamping) — 0이면 순수 관성이라 멈추기 힘들다.")]
    public float driftDamping = 0.10f;
    [Tooltip("분사 파티클 재질(비면 이펙트 생략, 물리는 동작).")]
    public Material jetMat;
    [Header("오징어 외형(켜진 동안)")]
    [Tooltip("눈 두 개(프리팹 자식, 평소 비활성)")] public GameObject eyes;
    [Tooltip("보라 틴트를 입힐 렌더러(상판+다리 4)")] public Renderer[] tintRenderers;
    public Color tintColor = new Color(0.62f, 0.40f, 0.95f);
    [Range(0f, 1f)] public float tintAmount = 0.55f;

    TableInput ti;
    ArtLegController[] legs;
    Transform[] meshes;
    ArticulationBody[] legAbs;
    ParticleSystem[] jets;
    ArticulationBody[] allBodies;
    float[] origDamping;
    bool[] origGravity;
    bool applied, lookApplied;
    MaterialPropertyBlock lookMpb;

    /// <summary>보라 틴트 + 눈. 켜진 동안 매 프레임 다시 씌움(다른 연출이 블록을 지워도 복구), 끌 땐 1회 원복.</summary>
    void ApplyLook(bool on)
    {
        if (lookMpb == null) lookMpb = new MaterialPropertyBlock();
        if (eyes != null && eyes.activeSelf != on) eyes.SetActive(on);
        if (tintRenderers != null)
            foreach (var r in tintRenderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                r.GetPropertyBlock(lookMpb);
                Color baseC = r.sharedMaterial.HasProperty("_BaseColor") ? r.sharedMaterial.GetColor("_BaseColor") : Color.white;
                lookMpb.SetColor("_BaseColor", on ? Color.Lerp(baseC, tintColor, tintAmount) : baseC);
                r.SetPropertyBlock(lookMpb);
            }
        lookApplied = on;
    }

    public void CheatUnlock() { unlocked = true; }

    void Start()
    {
        ti = TableInput.Of(this);
        legs = transform.root.GetComponentsInChildren<ArtLegController>(true);
        meshes = new Transform[legs.Length];
        legAbs = new ArticulationBody[legs.Length];
        jets = new ParticleSystem[legs.Length];
        for (int i = 0; i < legs.Length; i++)
        {
            legAbs[i] = legs[i].GetComponent<ArticulationBody>();
            meshes[i] = legs[i].transform.childCount > 0 ? legs[i].transform.GetChild(0) : legs[i].transform;
            jets[i] = jetMat != null ? MakeJet(i) : null;
        }
        allBodies = transform.root.GetComponentsInChildren<ArticulationBody>(true);
    }

    // 발끝 분사 파티클(런타임 생성 — 씬/프리팹 직렬화 불필요). 월드 시뮬레이션이라 비행 궤적에 김이 남는다.
    ParticleSystem MakeJet(int i)
    {
        var go = new GameObject("SquidJet_" + i);
        go.transform.SetParent(legs[i].transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.45f;
        main.startSpeed = 7f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.9f, 1f, 0.9f), new Color(0.85f, 0.6f, 1f, 0.9f));
        main.maxParticles = 300;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 11f;
        shape.radius = 0.03f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.4f, 0.7f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = jetMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    void Update()
    {
        bool look = LookOn;
        if (look || lookApplied) ApplyLook(look);
        if (netVisualOnly)
        {
            // 게스트: 다리 포즈는 NetworkTransform, 분사 여부는 호스트 마스크 — 물리/무중력 설정은 건드리지 않음
            if (jets == null) return;
            for (int i = 0; i < legs.Length; i++) { if (jets[i] == null) continue; PlaceJet(i); SetRate(jets[i], (netJetMask & (1 << i)) != 0 ? 110f : 0f); }
            return;
        }
        if (!active)
        {
            if (applied) Restore();
            if (jets != null) foreach (var j in jets) if (j != null) SetRate(j, 0f);
            JetMask = 0;
            return;
        }
        if (!applied) ApplyZeroG();
        Unlocked = true;   // 켜진 순간부터 조작법 메뉴에 오징어 페이지 노출
        if (ti == null) ti = TableInput.Of(this);

        int mask = 0;
        for (int i = 0; i < legs.Length; i++)
        {
            bool on = !thrustLocked && ti != null && ti.Get(legs[i].controlSet).longer;
            if (on) mask |= 1 << i;
            if (jets[i] == null) continue;
            PlaceJet(i);
            SetRate(jets[i], on ? 110f : 0f);
        }
        JetMask = mask;
    }

    // 분사구 = 발끝, 분사 방향 = 다리 바깥(-meshUp). 매 프레임 추적(다리 스윙/스핀 따라 회전).
    void PlaceJet(int i)
    {
        Vector3 up = meshes[i].up;
        if (up.sqrMagnitude > 1e-6f)
            jets[i].transform.SetPositionAndRotation(meshes[i].TransformPoint(0f, -0.5f, 0f), Quaternion.LookRotation(-up));
    }

    static void SetRate(ParticleSystem ps, float rate)
    {
        var em = ps.emission;
        em.rateOverTime = rate;
    }

    void FixedUpdate()
    {
        if (netVisualOnly || !active || !applied || ti == null || thrustLocked) return;
        for (int i = 0; i < legs.Length; i++)
        {
            if (legAbs[i] == null || !legAbs[i].enabled) continue;
            if (!ti.Get(legs[i].controlSet).longer) continue;
            Vector3 dir = meshes[i].up;                                   // 발→피벗 = 로켓 추진 방향
            Vector3 tip = meshes[i].TransformPoint(0f, -0.5f, 0f);
            legAbs[i].AddForceAtPosition(dir * thrustForce, tip);
        }
    }

    void ApplyZeroG()
    {
        origGravity = new bool[allBodies.Length];
        origDamping = new float[allBodies.Length];
        for (int i = 0; i < allBodies.Length; i++)
        {
            if (allBodies[i] == null) continue;
            origGravity[i] = allBodies[i].useGravity;
            origDamping[i] = allBodies[i].linearDamping;
            allBodies[i].useGravity = false;
            allBodies[i].linearDamping = driftDamping;
        }
        applied = true;
    }

    void Restore()
    {
        for (int i = 0; i < allBodies.Length; i++)
        {
            if (allBodies[i] == null) continue;
            allBodies[i].useGravity = origGravity[i];
            allBodies[i].linearDamping = origDamping[i];
        }
        applied = false;
    }
}
