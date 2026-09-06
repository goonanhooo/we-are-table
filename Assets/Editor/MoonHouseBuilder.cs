using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ★2026-09-06 달 맵 결말용 소박한 집(House) 생성기. Tools/Moon/Build Home — Moon 씬이 열린 상태에서 실행.
/// 기존 "House"·"MoonHomeEnding"·골 깃발(Goal/GoalFlag)을 지우고 다시 만든다(전부 씬 오브젝트 — 인스펙터에서 편집 가능).
/// 위치: HousePos(골 깃발 자리), 문이 스폰 쪽(원점)을 향하도록 회전. 바닥 높이는 네 모서리 지형 레이 중 최고점.
/// 구성: 바닥/벽(문 개구부·상인방)/박공 지붕+박공 삼각형/굴뚝(연기)/열린 문/창(프레임+따뜻한 발광 유리, 커튼)/현관 계단/가로등
///       부엌: 싱크 카운터·수도, 화구 2개 스토브+주전자(김), 선반+병, 찬장, 화분, 액자, 러그, 식탁 자리 위 매달린 램프(포인트 라이트)+보조 조명
///       식탁 자리(TablePlace)·의자 대기/착석 마커(ChairWait/ChairSeat)·최종 카메라(CamFinal)·기다리는 의자(홀웨이 Chair 복사본)
/// 재질: Assets/Guide/Moon/House*.mat(URP Lit, 없으면 생성).
/// </summary>
public static class MoonHouseBuilder
{
    public static Vector3 HousePos = new Vector3(90f, 0f, 110f);
    const string MatDir = "Assets/Guide/Moon";

    [MenuItem("Tools/Moon/Build Home")]
    public static void Menu() { Build(); }

    static Material Mat(string name, Color c, float metallic = 0f, float smooth = 0.35f, Color? emission = null)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) { AssetDatabase.CreateFolder("Assets/Guide", "Moon"); }
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 lp, Vector3 ls, Material m, bool collider = true, Vector3? euler = null)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name;
        g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = ls; if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
        var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    static GameObject Box(string n, Transform p, Vector3 lp, Vector3 ls, Material m, bool col = true, Vector3? e = null) => Prim(PrimitiveType.Cube, n, p, lp, ls, m, col, e);
    static GameObject Cyl(string n, Transform p, Vector3 lp, Vector3 ls, Material m, bool col = false, Vector3? e = null) => Prim(PrimitiveType.Cylinder, n, p, lp, ls, m, col, e);
    static GameObject Ball(string n, Transform p, Vector3 lp, float s, Material m, bool col = false) => Prim(PrimitiveType.Sphere, n, p, lp, Vector3.one * s, m, col);
    static Transform Marker(string n, Transform p, Vector3 lp, Vector3 euler) { var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(euler); return g.transform; }

    /// <summary>박공 삼각형(양면) 메시.</summary>
    static GameObject Gable(string n, Transform p, float z, float halfW, float y0, float y1, Material m)
    {
        var g = new GameObject(n); g.transform.SetParent(p, false);
        var mesh = new Mesh { name = n };
        var v = new[] { new Vector3(-halfW, y0, z), new Vector3(halfW, y0, z), new Vector3(0f, y1, z) };
        mesh.vertices = new[] { v[0], v[1], v[2], v[0], v[2], v[1] };
        mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = MatDir + "/" + n + "Mesh.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (old != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var mf = g.AddComponent<MeshFilter>(); mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
        return g;
    }

    static ParticleSystem Puffs(string n, Transform p, Vector3 lp, Color c, float rate, float size, float speed, float life)
    {
        var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = g.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = life; main.startSpeed = speed; main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size); main.startColor = c; main.maxParticles = 200; main.loop = true; main.playOnAwake = true;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.05f;
        var col = ps.colorOverLifetime; col.enabled = true; var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
        var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Guide/SquidJet.mat"); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    public static void Build()
    {
        var scene = SceneManager_Active();
        foreach (var n in new[] { "House", "MoonHomeEnding", "Goal", "GoalFlag" }) { var o = GameObject.Find(n); if (o != null) Object.DestroyImmediate(o); }

        // 재질
        var wall = Mat("HouseWall", new Color(0.93f, 0.88f, 0.78f), 0f, 0.25f);
        var roof = Mat("HouseRoof", new Color(0.50f, 0.24f, 0.20f), 0f, 0.3f);
        var wood = Mat("HouseWood", new Color(0.52f, 0.36f, 0.22f), 0f, 0.4f);
        var floor = Mat("HouseFloor", new Color(0.66f, 0.48f, 0.30f), 0f, 0.45f);
        var counter = Mat("HouseCounter", new Color(0.88f, 0.87f, 0.83f), 0f, 0.5f);
        var metal = Mat("HouseMetal", new Color(0.55f, 0.57f, 0.60f), 0.7f, 0.7f);
        var dark = Mat("HouseDark", new Color(0.18f, 0.18f, 0.2f), 0.3f, 0.5f);
        var glass = Mat("HouseGlass", new Color(1f, 0.9f, 0.7f), 0f, 0.9f, new Color(1.6f, 1.25f, 0.75f));
        var curtain = Mat("HouseCurtain", new Color(0.95f, 0.78f, 0.45f), 0f, 0.2f);
        var rug = Mat("HouseRug", new Color(0.70f, 0.30f, 0.28f), 0f, 0.1f);
        var plant = Mat("HousePlant", new Color(0.32f, 0.62f, 0.32f), 0f, 0.3f);
        var pot = Mat("HousePot", new Color(0.72f, 0.46f, 0.30f), 0f, 0.3f);
        var kettle = Mat("HouseKettle", new Color(0.86f, 0.86f, 0.9f), 0.8f, 0.8f);
        var jarA = Mat("HouseJarA", new Color(0.82f, 0.62f, 0.32f), 0f, 0.6f);
        var jarB = Mat("HouseJarB", new Color(0.52f, 0.70f, 0.52f), 0f, 0.6f);
        var jarC = Mat("HouseJarC", new Color(0.80f, 0.50f, 0.50f), 0f, 0.6f);
        var canvas = Mat("HouseCanvas", new Color(0.80f, 0.86f, 0.92f), 0f, 0.2f);
        var bulb = Mat("HouseBulb", new Color(1f, 0.95f, 0.8f), 0f, 0.5f, new Color(2.2f, 1.8f, 1.1f));

        // 위치/회전(문이 원점=스폰 쪽) + 바닥 높이(네 모서리 지형 최고점)
        Vector3 pos = HousePos; Vector3 toSpawn = new Vector3(-pos.x, 0f, -pos.z).normalized;
        Quaternion rot = Quaternion.LookRotation(toSpawn, Vector3.up);
        float ground = -999f, gmin = 999f;
        foreach (var c in new[] { new Vector3(-4.5f, 0, -4f), new Vector3(4.5f, 0, -4f), new Vector3(-4.5f, 0, 4.5f), new Vector3(4.5f, 0, 4.5f), Vector3.zero })
        {
            Vector3 w = pos + rot * c; if (Physics.Raycast(w + Vector3.up * 60f, Vector3.down, out var hit, 200f)) { ground = Mathf.Max(ground, hit.point.y); gmin = Mathf.Min(gmin, hit.point.y); }
        }
        if (ground < -900f) ground = 0f; if (gmin > 900f) gmin = ground;
        pos.y = ground + 0.02f;
        var house = new GameObject("House"); house.transform.SetPositionAndRotation(pos, rot);
        var H = house.transform;

        // 바닥(윗면 y=0.12) + 기초(지형 틈 가림)
        Box("Floor", H, new Vector3(0f, 0.06f, 0f), new Vector3(8.6f, 0.12f, 7.6f), floor);
        float found = Mathf.Max(0.3f, ground - gmin + 0.3f);
        Box("Foundation", H, new Vector3(0f, -found * 0.5f, 0f), new Vector3(8.7f, found, 7.7f), dark);
        // 벽(높이 3.0, 두께 0.25) — 앞벽은 문 개구부(폭 2.4, 높이 2.6)
        float wy = 0.12f + 1.5f;
        Box("WallBack", H, new Vector3(0f, wy, -3.5f), new Vector3(8.5f, 3f, 0.25f), wall);
        Box("WallLeft", H, new Vector3(-4.125f, wy, 0f), new Vector3(0.25f, 3f, 7.25f), wall);
        Box("WallRight", H, new Vector3(4.125f, wy, 0f), new Vector3(0.25f, 3f, 7.25f), wall);
        Box("WallFrontL", H, new Vector3(-2.6625f, wy, 3.5f), new Vector3(2.925f, 3f, 0.25f), wall);
        Box("WallFrontR", H, new Vector3(2.6625f, wy, 3.5f), new Vector3(2.925f, 3f, 0.25f), wall);
        Box("Lintel", H, new Vector3(0f, 0.12f + 2.8f, 3.5f), new Vector3(2.4f, 0.4f, 0.25f), wall);
        // 지붕(박공): 용마루 y 4.92, 처마 y 3.0(x ±4.6)
        float ridgeY = 0.12f + 4.8f, eaveY = 0.12f + 2.95f; float half = 4.6f; float ang = Mathf.Atan2(ridgeY - eaveY, half) * Mathf.Rad2Deg; float slope = Mathf.Sqrt(half * half + (ridgeY - eaveY) * (ridgeY - eaveY));
        Box("RoofL", H, new Vector3(-half * 0.5f, (ridgeY + eaveY) * 0.5f, 0f), new Vector3(slope + 0.2f, 0.16f, 8.2f), roof, true, new Vector3(0f, 0f, -ang));
        Box("RoofR", H, new Vector3(half * 0.5f, (ridgeY + eaveY) * 0.5f, 0f), new Vector3(slope + 0.2f, 0.16f, 8.2f), roof, true, new Vector3(0f, 0f, ang));
        Box("Ridge", H, new Vector3(0f, ridgeY + 0.02f, 0f), new Vector3(0.35f, 0.2f, 8.25f), roof, false);
        Gable("GableFront", H, 3.5f, 4.25f, 0.12f + 3f, ridgeY - 0.05f, wall);
        Gable("GableBack", H, -3.5f, 4.25f, 0.12f + 3f, ridgeY - 0.05f, wall);
        // 굴뚝 + 연기
        Box("Chimney", H, new Vector3(2.4f, ridgeY - 0.2f, -1.4f), new Vector3(0.6f, 1.6f, 0.6f), roof);
        Puffs("ChimneySmoke", H, new Vector3(2.4f, ridgeY + 0.65f, -1.4f), new Color(0.85f, 0.85f, 0.9f, 0.5f), 4f, 0.7f, 0.5f, 5f);
        // 열린 문(왼쪽 경첩, 바깥으로 110°)
        var hinge = Marker("DoorHinge", H, new Vector3(-1.2f, 0.12f, 3.5f), new Vector3(0f, -110f, 0f));
        Box("Door", hinge, new Vector3(0.575f, 1.25f, 0f), new Vector3(1.15f, 2.5f, 0.08f), wood);
        Ball("DoorKnob", hinge, new Vector3(1.02f, 1.2f, -0.07f), 0.08f, metal);
        // 창: 앞벽 오른쪽 + 오른쪽 벽(프레임 + 발광 유리 양면 + 커튼 안쪽)
        Box("WinFrontFrame", H, new Vector3(2.6f, 0.12f + 1.9f, 3.5f), new Vector3(1.3f, 1.1f, 0.34f), wood, false);
        Box("WinFrontGlass", H, new Vector3(2.6f, 0.12f + 1.9f, 3.5f), new Vector3(1.1f, 0.9f, 0.38f), glass, false);
        Box("WinRightFrame", H, new Vector3(4.125f, 0.12f + 1.9f, 0.4f), new Vector3(0.34f, 1.1f, 1.3f), wood, false);
        Box("WinRightGlass", H, new Vector3(4.125f, 0.12f + 1.9f, 0.4f), new Vector3(0.38f, 0.9f, 1.1f), glass, false);
        Box("CurtainA", H, new Vector3(3.93f, 0.12f + 1.95f, 0.4f - 0.62f), new Vector3(0.06f, 1.25f, 0.34f), curtain, false);
        Box("CurtainB", H, new Vector3(3.93f, 0.12f + 1.95f, 0.4f + 0.62f), new Vector3(0.06f, 1.25f, 0.34f), curtain, false);
        Box("CurtainRod", H, new Vector3(3.9f, 0.12f + 2.6f, 0.4f), new Vector3(0.04f, 0.04f, 1.7f), metal, false);
        Box("CurtainFA", H, new Vector3(2.6f - 0.75f, 0.12f + 1.95f, 3.37f), new Vector3(0.34f, 1.25f, 0.06f), curtain, false);
        Box("CurtainFB", H, new Vector3(2.6f + 0.75f, 0.12f + 1.95f, 3.37f), new Vector3(0.34f, 1.25f, 0.06f), curtain, false);
        // 현관 계단 + 가로등
        Box("Step", H, new Vector3(0f, 0.0f, 4.15f), new Vector3(2.9f, 0.14f, 1.1f), floor);
        Cyl("LampPost", H, new Vector3(-3.3f, 0.8f, 5.6f), new Vector3(0.09f, 0.8f, 0.09f), metal, true);
        Ball("LampPostHead", H, new Vector3(-3.3f, 1.75f, 5.6f), 0.32f, bulb);
        var postLight = new GameObject("LampPostLight").AddComponent<Light>(); postLight.transform.SetParent(H, false); postLight.transform.localPosition = new Vector3(-3.3f, 1.75f, 5.6f);
        postLight.type = LightType.Point; postLight.color = new Color(1f, 0.82f, 0.58f); postLight.intensity = 1.2f; postLight.range = 7f;

        // ── 부엌 ──
        var K = new GameObject("Kitchen").transform; K.SetParent(H, false);
        // 싱크 카운터
        Box("Counter", K, new Vector3(-2.2f, 0.12f + 0.45f, -3.1f), new Vector3(2.6f, 0.9f, 0.6f), counter);
        Box("CounterTop", K, new Vector3(-2.2f, 0.12f + 0.93f, -3.1f), new Vector3(2.7f, 0.06f, 0.66f), wood, false);
        Box("Sink", K, new Vector3(-2.7f, 0.12f + 0.965f, -3.1f), new Vector3(0.7f, 0.05f, 0.45f), metal, false);
        Cyl("Tap", K, new Vector3(-2.7f, 0.12f + 1.12f, -3.33f), new Vector3(0.05f, 0.16f, 0.05f), metal);
        Cyl("TapSpout", K, new Vector3(-2.7f, 0.12f + 1.27f, -3.24f), new Vector3(0.04f, 0.1f, 0.04f), metal, false, new Vector3(90f, 0f, 0f));
        // 스토브 + 주전자 + 김
        Box("Stove", K, new Vector3(-0.5f, 0.12f + 0.45f, -3.1f), new Vector3(0.8f, 0.9f, 0.6f), dark);
        Box("StoveTop", K, new Vector3(-0.5f, 0.12f + 0.93f, -3.1f), new Vector3(0.84f, 0.06f, 0.64f), metal, false);
        Cyl("BurnerA", K, new Vector3(-0.68f, 0.12f + 0.975f, -3.1f), new Vector3(0.26f, 0.015f, 0.26f), dark);
        Cyl("BurnerB", K, new Vector3(-0.32f, 0.12f + 0.975f, -3.1f), new Vector3(0.26f, 0.015f, 0.26f), dark);
        Ball("Kettle", K, new Vector3(-0.68f, 0.12f + 1.15f, -3.1f), 0.34f, kettle);
        Cyl("KettleSpout", K, new Vector3(-0.50f, 0.12f + 1.24f, -3.02f), new Vector3(0.05f, 0.12f, 0.05f), kettle, false, new Vector3(0f, 0f, -55f));
        Ball("KettleLid", K, new Vector3(-0.68f, 0.12f + 1.32f, -3.1f), 0.08f, dark);
        var steam = Puffs("Steam", K, new Vector3(-0.44f, 0.12f + 1.36f, -3.0f), new Color(0.95f, 0.95f, 1f, 0.5f), 5f, 0.22f, 0.35f, 2.2f);
        // 선반 + 병
        Box("Shelf", K, new Vector3(-2.0f, 0.12f + 1.85f, -3.22f), new Vector3(2.8f, 0.05f, 0.3f), wood, false);
        Cyl("JarA", K, new Vector3(-3.0f, 0.12f + 2.0f, -3.22f), new Vector3(0.2f, 0.13f, 0.2f), jarA);
        Cyl("JarB", K, new Vector3(-2.55f, 0.12f + 2.0f, -3.22f), new Vector3(0.18f, 0.12f, 0.18f), jarB);
        Cyl("JarC", K, new Vector3(-2.1f, 0.12f + 2.02f, -3.22f), new Vector3(0.22f, 0.15f, 0.22f), jarC);
        Cyl("JarD", K, new Vector3(-1.6f, 0.12f + 1.98f, -3.22f), new Vector3(0.17f, 0.1f, 0.17f), jarA);
        Ball("JarLidA", K, new Vector3(-3.0f, 0.12f + 2.15f, -3.22f), 0.14f, wood);
        // 찬장(오른쪽 뒤) + 손잡이
        Box("Cabinet", K, new Vector3(3.3f, 0.12f + 0.9f, -3.1f), new Vector3(1.2f, 1.8f, 0.5f), wood);
        Ball("CabinetKnobA", K, new Vector3(3.15f, 0.12f + 1.0f, -2.83f), 0.06f, metal);
        Ball("CabinetKnobB", K, new Vector3(3.45f, 0.12f + 1.0f, -2.83f), 0.06f, metal);
        // 화분(카운터 왼쪽 끝) + 액자(왼쪽 벽)
        Cyl("Pot", K, new Vector3(-3.35f, 0.12f + 1.05f, -3.05f), new Vector3(0.24f, 0.13f, 0.24f), pot);
        Ball("Plant", K, new Vector3(-3.35f, 0.12f + 1.33f, -3.05f), 0.38f, plant);
        Box("Frame", K, new Vector3(-3.97f, 0.12f + 1.9f, 0.2f), new Vector3(0.06f, 0.62f, 0.82f), wood, false);
        Box("Canvas", K, new Vector3(-3.95f, 0.12f + 1.9f, 0.2f), new Vector3(0.04f, 0.5f, 0.7f), canvas, false);
        // 러그 + 식탁 자리 + 램프
        Box("Rug", K, new Vector3(0.9f, 0.12f + 0.012f, 0.7f), new Vector3(3.0f, 0.024f, 2.5f), rug, false);
        var tablePlace = Marker("TablePlace", K, new Vector3(0.9f, 0.12f, 0.6f), Vector3.zero);
        var chairSeat = Marker("ChairSeat", K, new Vector3(0.9f, 0.12f, 0.6f + 0.85f), Vector3.zero);          // 의자 등받이가 +Z → 테이블(−Z)을 봄, 좌석 앞이 상판 아래로 15cm
        var chairWait = Marker("ChairWait", K, new Vector3(2.55f, 0.12f, 1.5f), new Vector3(0f, -55f, 0f));
        Cyl("LampCord", K, new Vector3(0.9f, 0.12f + 2.72f, 0.6f), new Vector3(0.02f, 0.2f, 0.02f), dark);
        Cyl("LampShade", K, new Vector3(0.9f, 0.12f + 2.42f, 0.6f), new Vector3(0.6f, 0.12f, 0.6f), counter);
        Ball("LampBulb", K, new Vector3(0.9f, 0.12f + 2.3f, 0.6f), 0.13f, bulb);
        var lamp = new GameObject("LampLight").AddComponent<Light>(); lamp.transform.SetParent(K, false); lamp.transform.localPosition = new Vector3(0.9f, 0.12f + 2.2f, 0.6f);
        lamp.type = LightType.Point; lamp.color = new Color(1f, 0.86f, 0.62f); lamp.intensity = 1.2f; lamp.range = 7.5f; lamp.shadows = LightShadows.Soft;
        var fill = new GameObject("FillLight").AddComponent<Light>(); fill.transform.SetParent(K, false); fill.transform.localPosition = new Vector3(-1.6f, 0.12f + 2.3f, -2.0f);
        fill.type = LightType.Point; fill.color = new Color(1f, 0.8f, 0.6f); fill.intensity = 0.55f; fill.range = 6.5f;
        // 최종 카메라(앞쪽 왼쪽 구석에서 식탁·창·찬장 쪽으로)
        var camFinal = Marker("CamFinal", H, new Vector3(-2.7f, 0.12f + 2.0f, 2.9f), Vector3.zero);
        camFinal.rotation = Quaternion.LookRotation((H.TransformPoint(new Vector3(1.1f, 0.85f, 0.2f)) - camFinal.position).normalized, Vector3.up);

        // 기다리는 의자(홀웨이 Chair 복사본)
        var chair = BorrowChair(H, wood);
        if (chair != null) { chair.SetPositionAndRotation(chairWait.position, chairWait.rotation); }

        // 엔딩 스크립트
        var endGo = new GameObject("MoonHomeEnding"); var end = endGo.AddComponent<MoonHomeEnding>();
        end.tablePlace = tablePlace; end.chair = chair; end.chairWait = chairWait; end.chairSeat = chairSeat; end.camFinal = camFinal; end.lamp = lamp; end.steam = steam;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("[MoonHouseBuilder] built at " + pos + " ground=" + ground.ToString("F2") + "/" + gmin.ToString("F2") + " chair=" + (chair != null));
    }

    static UnityEngine.SceneManagement.Scene SceneManager_Active() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    /// <summary>홀웨이 씬을 additive로 열어 "Chair"를 복사해 온다(홀웨이는 저장 없이 닫음).</summary>
    static Transform BorrowChair(Transform parent, Material fallback)
    {
        var target = SceneManager_Active();
        var hall = EditorSceneManager.OpenScene("Assets/Scenes/Hallway.unity", OpenSceneMode.Additive);
        GameObject src = null;
        foreach (var root in hall.GetRootGameObjects()) if (root.name == "Chair") { src = root; break; }
        Transform result = null;
        if (src != null)
        {
            var copy = Object.Instantiate(src); copy.name = "Chair";
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, target);
            foreach (var c in copy.GetComponentsInChildren<Component>(true)) { if (c is Rigidbody || c is Joint || c is MonoBehaviour) Object.DestroyImmediate(c); }
            copy.transform.SetParent(parent, true);
            result = copy.transform;
        }
        EditorSceneManager.CloseScene(hall, true);
        return result;
    }
}
