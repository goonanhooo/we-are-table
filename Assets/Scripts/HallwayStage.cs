using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 컷신으로 시작하지만 이후 플레이 가능한 "하얀 복도" 스테이지.
/// - 런타임에 흰 복도(바닥/벽/천장) + 끝의 의자 + 바닥 아래 구름(하늘)을 생성한다.
/// - 입장 시 카메라가 테이블을 정면에서 lookSeconds 동안 바라보다, 드라마틱하게 의자 쪽으로 향한다(발견).
/// - 컷신 동안 테이블은 kinematic으로 정지(입력 무시). 컷신이 끝나면 dynamic으로 풀려 조작 가능.
/// - 테이블이 의자 쪽으로 가다 트랩 구간에 들어오면 바닥이 아래로 열리고 테이블이 떨어진다.
/// - 떨어지면 하늘(스카이박스)+구름이 보이고, 잠시 뒤 다음 씬으로 이동.
/// </summary>
[RequireComponent(typeof(Camera))]
public class HallwayStage : MonoBehaviour
{
    [Header("재질 (의자 = 테이블과 같은 색)")]
    public Material woodMaterial;     // Table.mat 할당. 비면 런타임 색으로 대체.

    [Header("레이아웃")]
    public float startZ = -10f;       // 테이블 시작 z
    public float chairZ = 13f;        // 의자 z
    public float hallWidth = 6f;
    public float hallHeight = 5f;
    public float trapStartZ = 2f;     // 트랩도어 시작 z
    public float trapEndZ = 8f;       // 트랩도어 끝 z

    [Header("연출 / 흐름")]
    public float lookSeconds = 5f;     // 테이블 응시 시간
    public float panSeconds = 2.5f;    // 의자로 향하는 팬 시간
    public float chairHoldSeconds = 3f;// 의자 클로즈업 유지 시간
    public float pullbackSeconds = 3f; // 천천히 뒤로 빠져 플레이 화면으로
    public float fallToNextDelay = 2.5f;
    public string nextScene = "Stage1";

    Camera cam;
    Transform table;                  // 테이블 바디("Table")
    Rigidbody[] tableBodies;
    GameObject trapdoor;
    Material whiteMat;
    bool controllable, trapOpened, falling;
    float fallTimer;

    void Awake() { cam = GetComponent<Camera>(); }

    void Start()
    {
        whiteMat = MakeMat(new Color(0.95f, 0.95f, 0.96f), 0.1f);
        BuildHallway();
        BuildChair();
        BuildClouds();

        var t = GameObject.Find("Table");
        if (t != null)
        {
            table = t.transform;
            tableBodies = t.transform.root.GetComponentsInChildren<Rigidbody>(true);
        }

        FreezeTable(true);  // 컷신 동안 정지(입력 무시)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(Intro());
    }

    // ---------- 환경 생성 ----------
    Material MakeMat(Color c, float smooth)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }

    GameObject Box(string n, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!keepCollider) { var c = g.GetComponent<Collider>(); if (c) Destroy(c); }
        return g;
    }

    void BuildHallway()
    {
        float hw = hallWidth, hh = hallHeight;
        float backZ = startZ - 6f;
        float frontZ = chairZ + 4f;
        float midZ = (backZ + frontZ) * 0.5f;
        float len = frontZ - backZ;

        // 바닥: 트랩 앞/뒤 두 조각 + 트랩도어(중간)
        Box("Floor_A", new Vector3(0, -0.5f, (backZ + trapStartZ) * 0.5f), new Vector3(hw, 1f, trapStartZ - backZ), whiteMat, true);
        Box("Floor_B", new Vector3(0, -0.5f, (trapEndZ + frontZ) * 0.5f), new Vector3(hw, 1f, frontZ - trapEndZ), whiteMat, true);
        trapdoor = Box("Trapdoor", new Vector3(0, -0.5f, (trapStartZ + trapEndZ) * 0.5f), new Vector3(hw, 1f, trapEndZ - trapStartZ), whiteMat, true);

        // 양 옆 벽 / 천장 / 앞뒤 벽 → 닫힌 흰 복도
        Box("Wall_L", new Vector3(-hw * 0.5f, hh * 0.5f, midZ), new Vector3(0.4f, hh, len), whiteMat, true);
        Box("Wall_R", new Vector3(hw * 0.5f, hh * 0.5f, midZ), new Vector3(0.4f, hh, len), whiteMat, true);
        Box("Ceiling", new Vector3(0, hh, midZ), new Vector3(hw, 0.4f, len), whiteMat, false);
        Box("Wall_Back", new Vector3(0, hh * 0.5f, backZ), new Vector3(hw, hh, 0.4f), whiteMat, true);
        Box("Wall_Front", new Vector3(0, hh * 0.5f, frontZ), new Vector3(hw, hh, 0.4f), whiteMat, true);
    }

    void BuildChair()
    {
        Material m = woodMaterial != null ? woodMaterial : MakeMat(new Color(0.85f, 0.66f, 0.42f), 0.2f);
        var root = new GameObject("Chair");
        root.transform.position = new Vector3(0, 0, chairZ);
        root.transform.rotation = Quaternion.Euler(0f, -25f, 0f);   // 약간 사선

        // 테이블과 한쌍을 이루는 작은 의자
        float seatY = 0.42f;
        float w = 0.6f;       // 좌석 폭/깊이(작게)
        float legT = 0.08f;
        AddPart(root, "Seat", new Vector3(0, seatY, 0), new Vector3(w, 0.1f, w), m);
        AddPart(root, "Back", new Vector3(0, seatY + 0.42f, w * 0.5f - 0.05f), new Vector3(w, 0.85f, 0.1f), m); // 등받이 뒤쪽(+Z)
        float lo = w * 0.5f - legT * 0.5f - 0.02f;
        AddPart(root, "Leg1", new Vector3(-lo, seatY * 0.5f, -lo), new Vector3(legT, seatY, legT), m);
        AddPart(root, "Leg2", new Vector3(lo, seatY * 0.5f, -lo), new Vector3(legT, seatY, legT), m);
        AddPart(root, "Leg3", new Vector3(-lo, seatY * 0.5f, lo), new Vector3(legT, seatY, legT), m);
        AddPart(root, "Leg4", new Vector3(lo, seatY * 0.5f, lo), new Vector3(legT, seatY, legT), m);
    }

    void AddPart(GameObject parent, string n, Vector3 localPos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        var c = g.GetComponent<Collider>(); if (c) Destroy(c);
        g.transform.SetParent(parent.transform, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    void BuildClouds()
    {
        var cloudMat = MakeMat(Color.white, 0f);
        for (int i = 0; i < 16; i++)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            c.name = "Cloud" + i;
            var col = c.GetComponent<Collider>(); if (col) Destroy(col);
            float x = Random.Range(-16f, 16f);
            float y = Random.Range(-35f, -7f);
            float z = Random.Range(startZ - 4f, chairZ + 8f);
            c.transform.position = new Vector3(x, y, z);
            c.transform.localScale = new Vector3(Random.Range(4f, 9f), Random.Range(1.4f, 2.8f), Random.Range(4f, 9f));
            c.GetComponent<MeshRenderer>().sharedMaterial = cloudMat;
        }
    }

    // ---------- 테이블 정지/해제 ----------
    void FreezeTable(bool freeze)
    {
        if (tableBodies == null) return;
        foreach (var rb in tableBodies)
            if (rb != null) rb.isKinematic = freeze;
    }

    // ---------- 컷신 ----------
    IEnumerator Intro()
    {
        if (table == null) { controllable = true; FreezeTable(false); yield break; }
        Vector3 tp = table.position;
        Vector3 chairPos = new Vector3(0f, 0f, chairZ);

        // A: 테이블 — 살짝 올려다보는 자연스러운 각, 화면 오른쪽으로 치우치게
        Vector3 aPos = tp + new Vector3(-1.3f, 0.15f, 3.6f);
        Vector3 aLook = tp + new Vector3(0.5f, 0.55f, 0f);
        // C: 의자 클로즈업 (테이블 쪽에서 사선으로)
        Vector3 cPos = chairPos + new Vector3(1.0f, 0.9f, -2.4f);
        Vector3 cLook = chairPos + new Vector3(0f, 0.55f, 0f);
        // P: 플레이 추적 시작점(테이블 뒤) — LateUpdate 추적과 동일하게 맞춰 자연스럽게 이어짐
        Vector3 pPos = tp + new Vector3(0f, 2.0f, -4.6f);
        Vector3 pLook = tp + new Vector3(0f, 0.3f, 3f);

        SetCam(aPos, aLook);
        yield return new WaitForSeconds(lookSeconds);                      // 1) 테이블 응시
        yield return CamLerp(aPos, aLook, cPos, cLook, panSeconds);        // 2) 의자로 팬(발견)
        yield return new WaitForSeconds(chairHoldSeconds);                 // 3) 의자 클로즈업 유지
        yield return CamLerp(cPos, cLook, pPos, pLook, pullbackSeconds);   // 4) 천천히 뒤로 → 플레이 화면

        FreezeTable(false);   // 이제 물리/조작 활성
        controllable = true;
    }

    IEnumerator CamLerp(Vector3 p0, Vector3 l0, Vector3 p1, Vector3 l1, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, dur)));
            SetCam(Vector3.Lerp(p0, p1, u), Vector3.Lerp(l0, l1, u));
            yield return null;
        }
        SetCam(p1, l1);
    }

    void SetCam(Vector3 pos, Vector3 look)
    {
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
    }

    // ---------- 조작 중 카메라 추적 ----------
    void LateUpdate()
    {
        if (!controllable || table == null) return;
        Vector3 tp = table.position;
        Vector3 desired = tp + new Vector3(0f, 2.0f, -4.6f);
        cam.transform.position = Vector3.Lerp(cam.transform.position, desired, Time.deltaTime * 4f);
        Vector3 look = tp + new Vector3(0f, 0.3f, 3f);
        Quaternion want = Quaternion.LookRotation(look - cam.transform.position, Vector3.up);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, Time.deltaTime * 4f);
    }

    // ---------- 트랩도어 / 낙하 / 씬 이동 ----------
    void Update()
    {
        if (table == null) return;

        if (!trapOpened && controllable && table.position.z >= trapStartZ)
        {
            trapOpened = true;
            StartCoroutine(OpenTrap());
        }

        if (!falling && table.position.y < -2f) falling = true;
        if (falling)
        {
            fallTimer += Time.deltaTime;
            if (fallTimer >= fallToNextDelay)
            {
                falling = false;
                if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
            }
        }
    }

    IEnumerator OpenTrap()
    {
        if (trapdoor == null) yield break;
        var col = trapdoor.GetComponent<Collider>(); if (col) col.enabled = false;
        // 앞쪽 모서리를 축으로 아래로 회전하며 열리는 해치
        Vector3 pivot = trapdoor.transform.position + new Vector3(0f, 0.5f, (trapEndZ - trapStartZ) * 0.5f);
        float ang = 0f;
        while (ang < 95f)
        {
            float step = Time.deltaTime * 140f;
            trapdoor.transform.RotateAround(pivot, Vector3.right, step);
            ang += step;
            yield return null;
        }
        Destroy(trapdoor);
    }
}
