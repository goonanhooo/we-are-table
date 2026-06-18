using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>씬 전환 시 낙하 속도를 넘겨 "공중에서 같은 속도로 이어서 떨어지게" 하기 위한 정적 전달자.</summary>
public static class FallCarry
{
    public static bool active;
    public static float ySpeed;
}

/// <summary>
/// 정글 스테이지. 물로 둘러싸인 섬 + 가운데 거대한 깊은 구멍(용암) + 구멍 중앙의 빛나는 블랙홀 + 누워있는 더미 기린.
/// 이전 씬(Hallway)에서 떨어지던 속도 그대로 공중에서 이어서 낙하 → 섬 위 착지점에 자연스럽게 살짝 튕기며 착지.
/// 용암에 상판이 닿으면 사망(재시작). 블랙홀 근처에 가면 빨려 들어감.
/// 지형/오브젝트는 런타임 생성(씬 YAML은 카메라/라이트/볼륨/테이블 프리팹만).
/// </summary>
public class JungleStage : MonoBehaviour
{
    [Header("낙하 / 착지")]
    public Vector3 spawnPoint = new Vector3(13f, 26f, -3f);  // 공중 낙하 시작점(섬 위)
    public float defaultFallSpeed = -14f;

    [Header("지형")]
    public float islandHalf = 22f;   // 섬 바깥 반폭(정사각)
    public float holeHalf = 7f;      // 가운데 구멍 반폭
    public float holeDepth = 70f;    // 구멍 깊이
    public float waterY = -1.2f;

    [Header("블랙홀")]
    public Vector3 blackHolePos = new Vector3(0f, -26f, 0f);
    public float pullRadius = 15f;
    public float pullForce = 40f;
    public float captureRadius = 2.5f;

    Transform table, tableTop;
    Rigidbody[] tableBodies;
    Transform blackHole;
    float lavaTopY;
    bool ended;

    void Start()
    {
        lavaTopY = -holeDepth + 4f;
        BuildWorld();

        var tgo = GameObject.Find("Table");
        if (tgo != null) { table = tgo.transform; tableBodies = tgo.transform.root.GetComponentsInChildren<Rigidbody>(true); }
        var ttgo = GameObject.Find("TableTop");
        if (ttgo != null) tableTop = ttgo.transform;

        if (table != null)
        {
            table.root.position = spawnPoint;
            float vy = FallCarry.active ? FallCarry.ySpeed : defaultFallSpeed;
            FallCarry.active = false;
            if (vy > -3f) vy = defaultFallSpeed;
            if (tableBodies != null)
                foreach (var rb in tableBodies)
                    if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, vy, 0f);
        }
    }

    // ---------- 월드 생성 ----------
    Material Mat(Color c, float smooth)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }

    Material Emissive(Color baseCol, Color emis)
    {
        var m = Mat(baseCol, 0.3f);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emis);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return m;
    }

    GameObject Box(string n, Vector3 pos, Vector3 scale, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!collider) { var c = g.GetComponent<Collider>(); if (c) Destroy(c); }
        return g;
    }

    void BuildWorld()
    {
        var water = Mat(new Color(0.20f, 0.45f, 0.75f), 0.7f);
        var grass = Mat(new Color(0.32f, 0.55f, 0.25f), 0.1f);
        var rock = Mat(new Color(0.22f, 0.20f, 0.20f), 0.1f);
        var lava = Emissive(new Color(0.9f, 0.25f, 0.05f), new Color(2.2f, 0.5f, 0.05f));

        // 물(섬을 둘러쌈)
        Box("Water", new Vector3(0f, waterY - 0.5f, 0f), new Vector3(500f, 1f, 500f), water, true);

        // 섬 = 가운데 정사각 구멍이 뚫린 프레임(4조각). 윗면 y=0.
        float ring = (holeHalf + islandHalf) * 0.5f;
        float ringW = islandHalf - holeHalf;
        Box("Island_pZ", new Vector3(0f, -0.5f, ring), new Vector3(islandHalf * 2f, 1f, ringW), grass, true);
        Box("Island_nZ", new Vector3(0f, -0.5f, -ring), new Vector3(islandHalf * 2f, 1f, ringW), grass, true);
        Box("Island_pX", new Vector3(ring, -0.5f, 0f), new Vector3(ringW, 1f, holeHalf * 2f), grass, true);
        Box("Island_nX", new Vector3(-ring, -0.5f, 0f), new Vector3(ringW, 1f, holeHalf * 2f), grass, true);

        // 구멍 벽(깊게 내려감)
        Box("Hole_pX", new Vector3(holeHalf, -holeDepth * 0.5f, 0f), new Vector3(0.5f, holeDepth, holeHalf * 2f), rock, true);
        Box("Hole_nX", new Vector3(-holeHalf, -holeDepth * 0.5f, 0f), new Vector3(0.5f, holeDepth, holeHalf * 2f), rock, true);
        Box("Hole_pZ", new Vector3(0f, -holeDepth * 0.5f, holeHalf), new Vector3(holeHalf * 2f, holeDepth, 0.5f), rock, true);
        Box("Hole_nZ", new Vector3(0f, -holeDepth * 0.5f, -holeHalf), new Vector3(holeHalf * 2f, holeDepth, 0.5f), rock, true);

        // 용암(구멍 바닥)
        Box("Lava", new Vector3(0f, lavaTopY - 2f, 0f), new Vector3(holeHalf * 2f - 0.4f, 4f, holeHalf * 2f - 0.4f), lava, false);

        // 블랙홀(구멍 중앙에서 빛남)
        blackHole = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        blackHole.name = "BlackHole";
        var bhc = blackHole.GetComponent<Collider>(); if (bhc) Destroy(bhc);
        blackHole.position = blackHolePos;
        blackHole.localScale = Vector3.one * 3.2f;
        blackHole.GetComponent<MeshRenderer>().sharedMaterial = Emissive(new Color(0.15f, 0.05f, 0.25f), new Color(0.6f, 0.2f, 1.6f));

        BuildGiraffe(new Vector3(-13f, 0f, 6f), grass);
    }

    void BuildGiraffe(Vector3 at, Material fallbackUnused)
    {
        var skin = Mat(new Color(0.92f, 0.78f, 0.35f), 0.1f); // 기린색(노랑)
        var root = new GameObject("Giraffe_Dummy");
        root.transform.position = at;
        root.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
        void Part(string n, Vector3 lp, Vector3 s) {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n; var c = g.GetComponent<Collider>(); if (c) Destroy(c);
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = lp; g.transform.localScale = s;
            g.GetComponent<MeshRenderer>().sharedMaterial = skin;
        }
        // 누워있는 기린(더미): 몸통 + 길게 뻗은 목/머리 + 옆으로 누운 다리
        Part("Body", new Vector3(0f, 0.5f, 0f), new Vector3(1.1f, 0.9f, 2.6f));
        Part("Neck", new Vector3(0f, 0.5f, 2.3f), new Vector3(0.5f, 0.5f, 1.8f));   // 바닥에 누운 목
        Part("Head", new Vector3(0f, 0.5f, 3.4f), new Vector3(0.6f, 0.55f, 0.9f));
        Part("Leg1", new Vector3(0.9f, 0.3f, 0.8f), new Vector3(1.4f, 0.3f, 0.3f));  // 옆으로 뻗은 다리
        Part("Leg2", new Vector3(0.9f, 0.3f, -0.8f), new Vector3(1.4f, 0.3f, 0.3f));
        Part("Leg3", new Vector3(-0.9f, 0.3f, 0.8f), new Vector3(1.4f, 0.3f, 0.3f));
        Part("Leg4", new Vector3(-0.9f, 0.3f, -0.8f), new Vector3(1.4f, 0.3f, 0.3f));
    }

    // ---------- 메커니즘 ----------
    void FixedUpdate()
    {
        if (ended || table == null) return;

        // 블랙홀 흡입: 반경 내면 끌어당기고, 아주 가까우면 빨려 들어감
        float d = Vector3.Distance(table.position, blackHolePos);
        if (d < pullRadius && tableBodies != null)
        {
            foreach (var rb in tableBodies)
            {
                if (rb == null || rb.isKinematic) continue;
                Vector3 dir = (blackHolePos - rb.position).normalized;
                rb.AddForce(dir * pullForce, ForceMode.Acceleration);
            }
        }
        if (d < captureRadius) { End("BlackHole"); return; }

        // 용암 사망: 상판이 구멍 안에서 용암 높이까지 내려오면
        if (tableTop != null)
        {
            Vector3 p = tableTop.position;
            bool inHole = Mathf.Abs(p.x) < holeHalf && Mathf.Abs(p.z) < holeHalf;
            if (inHole && p.y <= lavaTopY + 0.2f) { End("Lava"); return; }
        }
    }

    void Update()
    {
        if (blackHole != null) blackHole.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
    }

    void End(string cause)
    {
        if (ended) return;
        ended = true;
        // 사망/흡입 → 일단 정글 재시작(추후 게임오버/다음 연출로 교체 가능)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }
}
