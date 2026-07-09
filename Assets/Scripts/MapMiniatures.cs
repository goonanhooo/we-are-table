using UnityEngine;

/// <summary>
/// ★ 맵 선택 미니어처(2026-07-10) — 마리오 갤럭시 스테이지 선택처럼, 맵마다 '빙빙 도는 3D 미니어처 섬'.
/// 스크린샷 대신 절차 생성한 심플 아이콘 섬(기린/눈사람/화산 등 상징물을 미니 스타일로 재창작)을
/// 맵별 라이브 카메라 → RenderTexture 로 그려 LevelSelect 썸네일 자리에 표시한다.
/// - 전부 런타임 생성(씬 직렬화 없음). 섬들은 월드 (0,-500,0) 근처 격리 구역에 배치.
/// - 카메라는 선택 화면이 보일 때만 켬(MarkVisible 하트비트) — 평소 렌더 비용 0.
/// - 섬 순서 = LevelSelect.levels 순서(Hallway/Jungle/Ice/Space/Track/Moon/Adventure/Climb).
/// </summary>
public class MapMiniatures : MonoBehaviour
{
    public float spinSpeed = 28f;      // 도/초
    public int rtWidth = 384, rtHeight = 240;
    public bool debugLog = false;      // 진단용

    Transform[] spins;
    Camera[] cams;
    RenderTexture[] rts;
    float lastMark = -99f;
    bool built;

    static readonly Vector3 Base = new Vector3(0f, -500f, 0f);

    // 맵별 하늘색(카메라 클리어) — 8=경주(대전), 9=축구(대전)는 멀티 로비 전용
    static readonly Color[] Sky = {
        new Color(0.93f, 0.90f, 0.84f),   // 시작(홀웨이 크림)
        new Color(0.55f, 0.78f, 0.95f),   // 정글
        new Color(0.72f, 0.84f, 0.95f),   // 빙산
        new Color(0.03f, 0.04f, 0.10f),   // 우주
        new Color(0.60f, 0.82f, 0.97f),   // 트랙
        new Color(0.02f, 0.02f, 0.04f),   // 달
        new Color(0.85f, 0.72f, 0.55f),   // 모험(협곡 노을)
        new Color(0.62f, 0.83f, 0.96f),   // 등반
        new Color(0.60f, 0.82f, 0.97f),   // 경주(대전)
        new Color(0.58f, 0.80f, 0.95f),   // 축구(대전)
    };

    public int Count => rts != null ? rts.Length : 0;
    public Texture RT(int i) => rts != null && i >= 0 && i < rts.Length ? rts[i] : null;

    int lastRenderFrame = -1;

    /// <summary>프리뷰 연출용 — i번 미니어처 섬(Spin 서브트리)을 복제해 parent 아래로.
    /// 복제본은 spins 배열 밖이라 자동 회전하지 않는다(전시 회전 금지 스펙).</summary>
    public Transform CloneIsland(int i, Transform parent)
    {
        if (!built) Build();
        if (spins == null || i < 0 || i >= spins.Length || spins[i] == null) return null;
        var clone = Instantiate(spins[i].gameObject, parent, false).transform;
        clone.name = "IslandClone_" + i;
        clone.localPosition = Vector3.zero;
        clone.localRotation = Quaternion.identity;
        clone.localScale = Vector3.one;
        return clone;
    }

    /// <summary>선택 화면이 이 프레임에 보임(LevelSelect.OnGUI에서 호출) —
    /// ⚠️이 컨텍스트에서 즉시 회전+수동 Render()해야 RT에 그려짐(실측:
    /// Update()에서 Render()하면 검게 나옴, OnGUI/코루틴 컨텍스트는 정상).</summary>
    public void MarkVisible()
    {
        if (!built) Build();
        lastMark = Time.unscaledTime;
        if (Time.frameCount == lastRenderFrame) return;   // OnGUI 다중 호출 가드(프레임당 1회)
        lastRenderFrame = Time.frameCount;
        if (debugLog) Debug.Log("MM: render frame=" + Time.frameCount);
        for (int i = 0; i < spins.Length; i++)
            if (spins[i] != null) spins[i].Rotate(0f, spinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        for (int i = 0; i < cams.Length; i++)
            if (cams[i] != null) cams[i].Render();
    }

    void Start() { Build(); }

    // ── 빌드 ──────────────────────────────────────────────────────────────
    void Build()
    {
        if (built) return;
        built = true;
        int n = 10;
        spins = new Transform[n];
        cams = new Camera[n];
        rts = new RenderTexture[n];
        for (int i = 0; i < n; i++)
        {
            var slot = new GameObject("Mini_" + i).transform;
            slot.SetParent(transform, false);
            slot.position = Base + new Vector3(i * 12f, 0f, 0f);
            var spin = new GameObject("Spin").transform;
            spin.SetParent(slot, false);
            spins[i] = spin;

            switch (i)
            {
                case 0: BuildHallway(spin); break;
                case 1: BuildJungle(spin); break;
                case 2: BuildIce(spin); break;
                case 3: BuildSpace(spin); break;
                case 4: BuildTrack(spin); break;
                case 5: BuildMoon(spin); break;
                case 6: BuildAdventure(spin); break;
                case 7: BuildClimb(spin); break;
                case 8: BuildRace(spin); break;
                case 9: BuildSoccer(spin); break;
            }

            // 카메라(살짝 내려다보는 갤럭시 앵글) + RT
            rts[i] = new RenderTexture(rtWidth, rtHeight, 16);
            var cgo = new GameObject("MiniCam");
            cgo.transform.SetParent(slot, false);
            cgo.transform.localPosition = new Vector3(0f, 1.15f, -2.3f);
            cgo.transform.LookAt(slot.position + Vector3.up * 0.22f);
            var c = cgo.AddComponent<Camera>();
            c.targetTexture = rts[i];
            c.fieldOfView = 30f;
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 20f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Sky[i];
            c.enabled = false;
            cams[i] = c;
        }
    }

    // ── 공용 프리미티브 헬퍼 ──
    static Material Mat(Color c, float smooth = 0.1f, Color? emis = null)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        if (emis.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emis.Value);
        }
        return m;
    }

    static GameObject Prim(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    // 떠 있는 섬 받침: 잔디 원반 + 아래로 흙 반구
    static void IslandBase(Transform p, Color top, Color dirt)
    {
        Prim(PrimitiveType.Cylinder, p, new Vector3(0f, -0.05f, 0f), new Vector3(1.5f, 0.06f, 1.5f), Mat(top));
        Prim(PrimitiveType.Sphere, p, new Vector3(0f, -0.32f, 0f), new Vector3(1.35f, 0.62f, 1.35f), Mat(dirt));
    }

    // ── 맵별 미니어처(상징물 심플 재창작) ────────────────────────────────
    void BuildHallway(Transform p)   // 시작: 크림 방 + 의자 + 빨간 문패
    {
        IslandBase(p, new Color(0.92f, 0.88f, 0.80f), new Color(0.72f, 0.64f, 0.52f));
        var wall = Mat(new Color(0.95f, 0.92f, 0.86f));
        Prim(PrimitiveType.Cube, p, new Vector3(0f, 0.24f, 0.28f), new Vector3(0.9f, 0.5f, 0.06f), wall);
        Prim(PrimitiveType.Cube, p, new Vector3(-0.45f, 0.24f, 0f), new Vector3(0.06f, 0.5f, 0.6f), wall);
        Prim(PrimitiveType.Cube, p, new Vector3(0.45f, 0.24f, 0f), new Vector3(0.06f, 0.5f, 0.6f), wall);
        // 미니 의자(주인공!)
        var wood = Mat(new Color(0.55f, 0.36f, 0.2f));
        Prim(PrimitiveType.Cube, p, new Vector3(0f, 0.16f, 0f), new Vector3(0.16f, 0.03f, 0.16f), wood);
        Prim(PrimitiveType.Cube, p, new Vector3(0f, 0.26f, 0.065f), new Vector3(0.16f, 0.18f, 0.03f), wood);
        for (int dx = -1; dx <= 1; dx += 2)
            for (int dz = -1; dz <= 1; dz += 2)
                Prim(PrimitiveType.Cube, p, new Vector3(dx * 0.06f, 0.08f, dz * 0.06f), new Vector3(0.025f, 0.16f, 0.025f), wood);
        Prim(PrimitiveType.Sphere, p, new Vector3(0.28f, 0.06f, -0.25f), Vector3.one * 0.08f, Mat(new Color(0.9f, 0.2f, 0.2f), 0.4f));
    }

    void BuildJungle(Transform p)   // 정글: 화산+용암 + 야자수 + 미니 기린
    {
        IslandBase(p, new Color(0.42f, 0.66f, 0.3f), new Color(0.5f, 0.38f, 0.26f));
        var rock = Mat(new Color(0.55f, 0.42f, 0.32f));
        Prim(PrimitiveType.Cylinder, p, new Vector3(-0.28f, 0.28f, 0.22f), new Vector3(0.5f, 0.28f, 0.5f), rock);
        Prim(PrimitiveType.Cylinder, p, new Vector3(-0.28f, 0.5f, 0.22f), new Vector3(0.26f, 0.1f, 0.26f), rock);
        Prim(PrimitiveType.Sphere, p, new Vector3(-0.28f, 0.58f, 0.22f), Vector3.one * 0.2f, Mat(new Color(1f, 0.45f, 0.05f), 0.3f, new Color(1f, 0.35f, 0f) * 1.6f));
        // 야자수
        var trunk = Mat(new Color(0.6f, 0.45f, 0.3f));
        var leaf = Mat(new Color(0.3f, 0.62f, 0.28f));
        Prim(PrimitiveType.Cylinder, p, new Vector3(0.42f, 0.2f, -0.2f), new Vector3(0.05f, 0.2f, 0.05f), trunk);
        Prim(PrimitiveType.Sphere, p, new Vector3(0.42f, 0.42f, -0.2f), new Vector3(0.3f, 0.12f, 0.3f), leaf);
        // 미니 기린(노란 몸+목+갈색 점 느낌은 생략 — 실루엣만)
        var gir = Mat(new Color(0.95f, 0.78f, 0.25f));
        Prim(PrimitiveType.Cube, p, new Vector3(0.1f, 0.14f, 0.05f), new Vector3(0.2f, 0.12f, 0.1f), gir);
        Prim(PrimitiveType.Cube, p, new Vector3(0.19f, 0.3f, 0.05f), new Vector3(0.05f, 0.24f, 0.05f), gir, new Vector3(0f, 0f, -12f));
        Prim(PrimitiveType.Cube, p, new Vector3(0.23f, 0.42f, 0.05f), new Vector3(0.09f, 0.05f, 0.06f), gir);
        for (int k = 0; k < 4; k++)
            Prim(PrimitiveType.Cube, p, new Vector3(0.03f + (k % 2) * 0.14f, 0.05f, 0.02f + (k / 2) * 0.07f), new Vector3(0.03f, 0.1f, 0.03f), gir);
    }

    void BuildIce(Transform p)   // 빙산: 흰 봉우리 + 눈사람 + 슬라이드
    {
        IslandBase(p, new Color(0.92f, 0.95f, 0.98f), new Color(0.6f, 0.72f, 0.84f));
        var snow = Mat(new Color(0.97f, 0.98f, 1f), 0.25f);
        Prim(PrimitiveType.Cube, p, new Vector3(-0.25f, 0.34f, 0.2f), new Vector3(0.42f, 0.62f, 0.42f), snow, new Vector3(0f, 45f, 0f));
        Prim(PrimitiveType.Cube, p, new Vector3(-0.25f, 0.62f, 0.2f), new Vector3(0.18f, 0.3f, 0.18f), snow, new Vector3(0f, 25f, 0f));
        // 슬라이드(하얀 리본)
        Prim(PrimitiveType.Cube, p, new Vector3(0.12f, 0.3f, 0.1f), new Vector3(0.14f, 0.02f, 0.55f), Mat(Color.white, 0.5f), new Vector3(28f, -30f, 0f));
        // 눈사람
        Prim(PrimitiveType.Sphere, p, new Vector3(0.38f, 0.1f, -0.25f), Vector3.one * 0.18f, snow);
        Prim(PrimitiveType.Sphere, p, new Vector3(0.38f, 0.24f, -0.25f), Vector3.one * 0.13f, snow);
        Prim(PrimitiveType.Sphere, p, new Vector3(0.38f, 0.27f, -0.31f), Vector3.one * 0.035f, Mat(new Color(0.95f, 0.5f, 0.1f)));
    }

    void BuildSpace(Transform p)   // 우주: 어두운 소행성 + 은하 소용돌이 + 금색 별
    {
        Prim(PrimitiveType.Sphere, p, new Vector3(0f, -0.12f, 0f), new Vector3(1.1f, 0.8f, 1.1f), Mat(new Color(0.2f, 0.2f, 0.26f)));
        // 은하 소용돌이(납작 타원 2장 교차 + 밝은 코어)
        var glowB = Mat(new Color(0.5f, 0.65f, 1f), 0.3f, new Color(0.35f, 0.5f, 1f) * 1.4f);
        var glowW = Mat(Color.white, 0.3f, new Color(1f, 0.95f, 0.8f) * 1.8f);
        Prim(PrimitiveType.Sphere, p, new Vector3(0f, 0.42f, 0f), new Vector3(0.85f, 0.05f, 0.35f), glowB, new Vector3(8f, 0f, 0f));
        Prim(PrimitiveType.Sphere, p, new Vector3(0f, 0.42f, 0f), new Vector3(0.35f, 0.05f, 0.85f), glowB, new Vector3(0f, 0f, 8f));
        Prim(PrimitiveType.Sphere, p, new Vector3(0f, 0.44f, 0f), Vector3.one * 0.16f, glowW);
        // 소행성/별
        var rock = Mat(new Color(0.45f, 0.44f, 0.5f));
        var star = Mat(new Color(1f, 0.9f, 0.5f), 0.5f, new Color(1f, 0.85f, 0.35f) * 2f);
        Prim(PrimitiveType.Sphere, p, new Vector3(0.62f, 0.12f, 0.3f), Vector3.one * 0.12f, rock);
        Prim(PrimitiveType.Sphere, p, new Vector3(-0.6f, 0.05f, -0.28f), Vector3.one * 0.09f, rock);
        Prim(PrimitiveType.Sphere, p, new Vector3(0.5f, 0.55f, -0.35f), Vector3.one * 0.05f, star);
        Prim(PrimitiveType.Sphere, p, new Vector3(-0.45f, 0.6f, 0.3f), Vector3.one * 0.04f, star);
        Prim(PrimitiveType.Sphere, p, new Vector3(0.1f, 0.75f, 0.15f), Vector3.one * 0.045f, star);
    }

    void BuildTrack(Transform p)   // 트랙: 타원 트랙 + 결승 아치
    {
        IslandBase(p, new Color(0.45f, 0.68f, 0.32f), new Color(0.52f, 0.4f, 0.28f));
        var lane = Mat(new Color(0.85f, 0.45f, 0.35f));
        var white = Mat(Color.white, 0.3f);
        for (int k = 0; k < 14; k++)
        {
            float a = k / 14f * Mathf.PI * 2f;
            var pos = new Vector3(Mathf.Cos(a) * 0.52f, 0.0f, Mathf.Sin(a) * 0.34f);
            Prim(PrimitiveType.Cube, p, pos, new Vector3(0.16f, 0.025f, 0.1f), lane, new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
        }
        // 결승 아치
        Prim(PrimitiveType.Cube, p, new Vector3(0.52f, 0.12f, -0.12f), new Vector3(0.03f, 0.22f, 0.03f), white);
        Prim(PrimitiveType.Cube, p, new Vector3(0.52f, 0.12f, 0.12f), new Vector3(0.03f, 0.22f, 0.03f), white);
        Prim(PrimitiveType.Cube, p, new Vector3(0.52f, 0.24f, 0f), new Vector3(0.035f, 0.045f, 0.3f), Mat(new Color(0.9f, 0.25f, 0.2f)));
    }

    void BuildMoon(Transform p)   // 달: 크레이터 회색 구 + 깃발 + 파란 지구
    {
        var gray = Mat(new Color(0.62f, 0.62f, 0.66f));
        Prim(PrimitiveType.Sphere, p, new Vector3(0f, -0.1f, 0f), new Vector3(1.25f, 0.95f, 1.25f), gray);
        var dark = Mat(new Color(0.45f, 0.45f, 0.5f));
        Prim(PrimitiveType.Cylinder, p, new Vector3(0.25f, 0.33f, 0.15f), new Vector3(0.26f, 0.012f, 0.26f), dark, new Vector3(6f, 0f, -8f));
        Prim(PrimitiveType.Cylinder, p, new Vector3(-0.3f, 0.28f, -0.15f), new Vector3(0.18f, 0.012f, 0.18f), dark, new Vector3(-7f, 0f, 6f));
        Prim(PrimitiveType.Cylinder, p, new Vector3(0.05f, 0.25f, -0.38f), new Vector3(0.13f, 0.012f, 0.13f), dark, new Vector3(-14f, 0f, 0f));
        // 깃발
        Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.55f, 0f), new Vector3(0.02f, 0.16f, 0.02f), Mat(new Color(0.8f, 0.8f, 0.85f)));
        Prim(PrimitiveType.Cube, p, new Vector3(0.08f, 0.64f, 0f), new Vector3(0.14f, 0.08f, 0.01f), Mat(new Color(0.9f, 0.25f, 0.2f)));
        // 지구(멀리 떠 있는 파란 구슬)
        Prim(PrimitiveType.Sphere, p, new Vector3(-0.55f, 0.7f, 0.35f), Vector3.one * 0.14f, Mat(new Color(0.3f, 0.55f, 0.9f), 0.4f, new Color(0.15f, 0.3f, 0.6f)));
    }

    void BuildAdventure(Transform p)   // 모험: 협곡 벽 + 능력 토템 구슬 3색
    {
        IslandBase(p, new Color(0.8f, 0.68f, 0.48f), new Color(0.6f, 0.44f, 0.3f));
        var rock = Mat(new Color(0.78f, 0.58f, 0.4f));
        var rockD = Mat(new Color(0.6f, 0.43f, 0.3f));
        Prim(PrimitiveType.Cube, p, new Vector3(-0.42f, 0.28f, 0.05f), new Vector3(0.25f, 0.5f, 0.75f), rock, new Vector3(0f, 6f, 0f));
        Prim(PrimitiveType.Cube, p, new Vector3(0.42f, 0.24f, -0.05f), new Vector3(0.25f, 0.42f, 0.75f), rockD, new Vector3(0f, -5f, 0f));
        // 가운데 얼음 조각(빙판 구간)
        Prim(PrimitiveType.Cube, p, new Vector3(0f, 0.015f, -0.15f), new Vector3(0.3f, 0.02f, 0.35f), Mat(new Color(0.55f, 0.78f, 0.92f), 0.9f));
        // 토템 구슬 3색(기린 노랑/얼음 하늘/오징어 보라)
        var post = Mat(new Color(0.45f, 0.43f, 0.41f));
        Color[] orbs = { new Color(1f, 0.8f, 0.3f), new Color(0.4f, 0.8f, 1f), new Color(0.75f, 0.5f, 1f) };
        for (int k = 0; k < 3; k++)
        {
            float x = -0.16f + k * 0.16f;
            Prim(PrimitiveType.Cube, p, new Vector3(x, 0.08f, 0.32f), new Vector3(0.035f, 0.16f, 0.035f), post);
            Prim(PrimitiveType.Sphere, p, new Vector3(x, 0.2f, 0.32f), Vector3.one * 0.07f, Mat(orbs[k], 0.5f, orbs[k] * 1.6f));
        }
    }

    void BuildRace(Transform p)   // 경주(대전): 트랙 + 나란히 선 미니 테이블 2대
    {
        IslandBase(p, new Color(0.45f, 0.68f, 0.32f), new Color(0.52f, 0.4f, 0.28f));
        var lane = Mat(new Color(0.85f, 0.45f, 0.35f));
        for (int k = 0; k < 14; k++)
        {
            float a = k / 14f * Mathf.PI * 2f;
            var pos = new Vector3(Mathf.Cos(a) * 0.52f, 0.0f, Mathf.Sin(a) * 0.34f);
            Prim(PrimitiveType.Cube, p, pos, new Vector3(0.16f, 0.025f, 0.1f), lane, new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
        }
        // 미니 테이블 2대(파랑/빨강 비콘) — 대전 느낌
        var wood = Mat(new Color(0.55f, 0.36f, 0.2f));
        Color[] team = { new Color(0.25f, 0.5f, 1f), new Color(1f, 0.3f, 0.25f) };
        for (int t = 0; t < 2; t++)
        {
            float z = t == 0 ? -0.09f : 0.09f;
            Prim(PrimitiveType.Cube, p, new Vector3(0.52f, 0.1f, z), new Vector3(0.14f, 0.02f, 0.1f), wood);
            for (int dx = -1; dx <= 1; dx += 2)
                for (int dz = -1; dz <= 1; dz += 2)
                    Prim(PrimitiveType.Cube, p, new Vector3(0.52f + dx * 0.055f, 0.05f, z + dz * 0.035f), new Vector3(0.02f, 0.09f, 0.02f), wood);
            Prim(PrimitiveType.Sphere, p, new Vector3(0.52f, 0.16f, z), Vector3.one * 0.045f, Mat(team[t], 0.5f, team[t] * 1.6f));
        }
    }

    void BuildSoccer(Transform p)   // 축구(대전): 피치 + 골대 2개 + 공
    {
        IslandBase(p, new Color(0.35f, 0.62f, 0.3f), new Color(0.5f, 0.38f, 0.26f));
        var line = Mat(new Color(0.95f, 0.95f, 0.95f), 0.2f);
        Prim(PrimitiveType.Cube, p, new Vector3(0f, 0.005f, 0f), new Vector3(0.02f, 0.015f, 0.7f), line);      // 센터 라인
        Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.006f, 0f), new Vector3(0.1f, 0.006f, 0.1f), line);   // 센터 점
        var post = Mat(new Color(0.92f, 0.92f, 0.95f), 0.5f);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.52f;
            Prim(PrimitiveType.Cube, p, new Vector3(x, 0.08f, -0.14f), new Vector3(0.02f, 0.16f, 0.02f), post);
            Prim(PrimitiveType.Cube, p, new Vector3(x, 0.08f, 0.14f), new Vector3(0.02f, 0.16f, 0.02f), post);
            Prim(PrimitiveType.Cube, p, new Vector3(x, 0.165f, 0f), new Vector3(0.02f, 0.02f, 0.3f), post);
        }
        Prim(PrimitiveType.Sphere, p, new Vector3(0.08f, 0.06f, 0.05f), Vector3.one * 0.11f, Mat(Color.white, 0.35f));
    }

    void BuildClimb(Transform p)   // 등반: 암벽(색 홀드) + 정상 깃발 + 테니스공
    {
        IslandBase(p, new Color(0.46f, 0.63f, 0.34f), new Color(0.52f, 0.4f, 0.28f));
        var wall = Mat(new Color(0.55f, 0.57f, 0.62f));
        Prim(PrimitiveType.Cube, p, new Vector3(0f, 0.4f, 0.22f), new Vector3(0.75f, 0.75f, 0.14f), wall);
        Color[] holds = { new Color(0.88f, 0.25f, 0.22f), new Color(0.24f, 0.48f, 0.9f), new Color(0.95f, 0.82f, 0.2f), new Color(0.3f, 0.75f, 0.38f) };
        Random.InitState(7);
        for (int k = 0; k < 8; k++)
            Prim(PrimitiveType.Cube, p,
                new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.14f, 0.68f), 0.14f),
                Vector3.one * 0.05f, Mat(holds[k % 4], 0.35f), new Vector3(0f, 0f, Random.Range(0f, 360f)));
        // 정상 깃발 + 테니스공
        Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.84f, 0.22f), new Vector3(0.015f, 0.07f, 0.015f), Mat(new Color(0.8f, 0.8f, 0.85f)));
        Prim(PrimitiveType.Cube, p, new Vector3(0.05f, 0.88f, 0.22f), new Vector3(0.09f, 0.05f, 0.01f), Mat(new Color(0.9f, 0.25f, 0.2f)));
        Prim(PrimitiveType.Sphere, p, new Vector3(0.35f, 0.06f, -0.25f), Vector3.one * 0.12f, Mat(new Color(0.72f, 0.93f, 0.18f), 0.4f, new Color(0.25f, 0.35f, 0.03f)));
    }
}
