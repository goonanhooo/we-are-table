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

    const float ChairYaw = -25f;      // 의자 사선 각도(배치 + 클로즈업 정면 방향 공유)

    Camera cam;
    Transform table;                  // 테이블 바디("Table")
    Rigidbody[] tableBodies;
    GameObject leftFlap, rightFlap;   // 가운데서 갈라져 아래로 열리는 두 짝
    Material whiteMat;
    bool controllable, trapOpened, falling;
    float fallTimer;

    void Awake() { cam = GetComponent<Camera>(); }

    void Start()
    {
        whiteMat = MakeMat(new Color(0.95f, 0.95f, 0.96f), 0.1f);
        BuildHallway();
        BuildChair();

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
        // 트랩: 의자 앞 정사각형 구역. 가운데서 갈라져 양쪽이 복도 가장자리 경첩 기준 아래로 열림.
        float sqZ = (trapStartZ + trapEndZ) * 0.5f;
        float sqD = trapEndZ - trapStartZ;
        leftFlap = Box("TrapL", new Vector3(-hw * 0.25f, -0.5f, sqZ), new Vector3(hw * 0.5f, 1f, sqD), whiteMat, true);
        rightFlap = Box("TrapR", new Vector3(hw * 0.25f, -0.5f, sqZ), new Vector3(hw * 0.5f, 1f, sqD), whiteMat, true);

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
        root.transform.rotation = Quaternion.Euler(0f, ChairYaw, 0f);   // 약간 사선

        // 테이블과 한쌍을 이루는 작은 의자
        float seatY = 0.42f;
        float w = 0.6f;       // 좌석 폭/깊이(작게)
        float legT = 0.08f;
        AddPart(root, "Seat", new Vector3(0, seatY, 0), new Vector3(w, 0.1f, w), m);
        AddPart(root, "Back", new Vector3(0, seatY + 0.3f, w * 0.5f - 0.05f), new Vector3(w, w, 0.1f), m); // 등받이(정사각형 w×w, 뒤쪽 +Z)
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
        // C: 의자 클로즈업 — 의자가 향한 방향(정면)에서 바라봄 (사선 배치를 반영)
        Vector3 chairFront = Quaternion.Euler(0f, ChairYaw, 0f) * Vector3.back; // 의자 정면 방향
        Vector3 cLook = chairPos + new Vector3(0f, 0.5f, 0f);
        Vector3 cPos = cLook + chairFront * 2.6f + Vector3.up * 0.35f;
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
        // 위치는 smoothstep, 회전은 Slerp → 각속도가 고르게 부드럽게 전환(확 꺾이지 않음).
        Quaternion r0 = Quaternion.LookRotation(l0 - p0, Vector3.up);
        Quaternion r1 = Quaternion.LookRotation(l1 - p1, Vector3.up);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, dur)));
            cam.transform.position = Vector3.Lerp(p0, p1, u);
            cam.transform.rotation = Quaternion.Slerp(r0, r1, u);
            yield return null;
        }
        cam.transform.position = p1;
        cam.transform.rotation = Quaternion.LookRotation(l1 - p1, Vector3.up);
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

        if (falling)
        {
            // 낙하 중엔 테이블을 빠르게(지연 없이) 따라가 하늘/구름 속 추락을 프레임에 담음.
            Vector3 d = tp + new Vector3(0f, 1.5f, -5f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, d, Time.deltaTime * 9f);
            Quaternion w = Quaternion.LookRotation((tp + Vector3.up * 0.2f) - cam.transform.position, Vector3.up);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, w, Time.deltaTime * 9f);
            return;
        }

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

        // 정사각형 트랩 구역의 '중간 쯤'에 들어서면 열림
        float trapMidZ = (trapStartZ + trapEndZ) * 0.5f;
        if (!trapOpened && controllable && table.position.z >= trapMidZ)
        {
            trapOpened = true;
            StartCoroutine(OpenTrap());
        }

        // 바닥 밑으로 내려가면 낙하 시작(물리 그대로 — 다리는 브레이크 모터라 떨리지 않음).
        if (!falling && table.position.y < -2f)
        {
            falling = true;
            // 약 5초간 둥실 떨어지도록 약한 공기저항만 부여(연출 스크립트 없음)
            if (tableBodies != null)
                foreach (var rb in tableBodies)
                    if (rb != null && !rb.isKinematic) rb.linearDamping = 0.4f;
        }
        if (falling)
        {
            fallTimer += Time.deltaTime;
            if (fallTimer >= fallToNextDelay)
            {
                falling = false;
                // 낙하 속도를 다음 씬으로 전달 → 정글 공중에서 같은 속도로 이어서 떨어짐
                var brb = table != null ? table.GetComponent<Rigidbody>() : null;
                FallCarry.active = true;
                FallCarry.ySpeed = brb != null ? brb.linearVelocity.y : -14f;
                if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
            }
        }
    }

    // 가운데서 갈라져 양쪽이 복도 가장자리(경첩) 기준 아래로 열리는 두 짝짜리 트랩.
    IEnumerator OpenTrap()
    {
        float hw = hallWidth;
        float sqZ = (trapStartZ + trapEndZ) * 0.5f;
        Vector3 lPivot = new Vector3(-hw * 0.5f, 0f, sqZ);   // 왼쪽 가장자리 경첩(윗면)
        Vector3 rPivot = new Vector3(hw * 0.5f, 0f, sqZ);    // 오른쪽 가장자리 경첩(윗면)
        if (leftFlap) { var c = leftFlap.GetComponent<Collider>(); if (c) c.enabled = false; }
        if (rightFlap) { var c = rightFlap.GetComponent<Collider>(); if (c) c.enabled = false; }

        float ang = 0f;
        while (ang < 100f)
        {
            float step = Time.deltaTime * 130f;
            if (leftFlap) leftFlap.transform.RotateAround(lPivot, Vector3.forward, -step);  // 안쪽(중앙) 모서리가 아래로
            if (rightFlap) rightFlap.transform.RotateAround(rPivot, Vector3.forward, step);  // 반대로 → 가운데서 갈라짐
            ang += step;
            yield return null;
        }
        // 열린 채로 둠(아래로 갈라진 모양 유지)
    }
}
