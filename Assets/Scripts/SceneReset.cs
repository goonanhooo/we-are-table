using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테이블(상판 + 다리)을 초기 '자세'로 되돌린다. (씬 재로드 아님)
/// - 시작 시 상판(이 오브젝트)과 모든 다리 Rigidbody의 초기 위치/회전 + 상판 기준 상대 배치를 기록.
/// - '1' 키 → ResetPoseInPlace(): 처음 위치로 가지 않고 '현재 위치/방향'에서 기울기·다리만 똑바로 복원.
/// - ResetAll(): 처음(지면 스냅된) 위치로 텔레포트. 용암 사망 리스폰 등에서 호출.
/// 상판(Table) GameObject에 부착한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SceneReset : MonoBehaviour
{
    [Tooltip("리셋 직후 다리를 수직으로 강제 고정하는 시간(초). 안착 보정용.")]
    public float verticalHoldSeconds = 0.4f;
    [Tooltip("리셋 시 지면 위로 띄우는 높이 = 테이블 높이 × 이 비율 (지형에 잠기는 것 방지).")]
    public float groundLiftFactor = 0.5f;
    [Tooltip("위쪽 벡터가 이 값보다 작아지면(=거의 옆/뒤집힘) 'Press 1' 후보. 1=똑바로, 0=옆으로 누움.")]
    public float flipDot = 0.2f;
    [Tooltip("뒤집힌 채 '가만히' 이 시간(초) 이상 지속되면 안내 표시.")]
    public float flipPromptDelay = 2f;
    [Tooltip("이 속도보다 빠르게 움직이면(구르는 중) 안내 안 띄움 — 정착된 뒤에만 표시.")]
    public float flipCalmSpeed = 1.2f;

    float tableHeight = 1f;       // Start에서 콜라이더로 측정(지형 잠김 방지 리프트용)
    float flippedSince = -1f;     // 뒤집히기 시작한 시각(unscaled). <0 = 안 뒤집힘
    GUIStyle flipBig, flipSmall;
    Texture2D flipTex;

    Rigidbody[] bodies;
    Vector3[] initPos;
    Quaternion[] initRot;
    LegController[] legs;
    // '1' 제자리 자세복원용: 각 강체의 상판(bodies[0]) 기준 상대 배치 + 상판의 지면 위 높이.
    Vector3[] relPos;
    Quaternion[] relRot;
    float boardClearance;

    // ⚠️ Awake(물리 시작 전)에서 '깨끗한 프리팹 포즈'를 캡처한다. Start로 하면 빌드에선 테이블이
    //    이미 낙하 중일 때 잡혀 '낙하 중 자세'를 기억 → 리셋 시 다리가 폭주하던 버그가 있었음.
    void Awake()
    {
        var list = new List<Rigidbody>();
        var self = GetComponent<Rigidbody>();
        if (self != null) list.Add(self);

        legs = FindObjectsByType<LegController>(FindObjectsSortMode.None);
        foreach (var l in legs)
        {
            var r = l.GetComponent<Rigidbody>();
            if (r != null && !list.Contains(r)) list.Add(r);
        }

        bodies = list.ToArray();
        initPos = new Vector3[bodies.Length];
        initRot = new Quaternion[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            initPos[i] = bodies[i].transform.position;
            initRot[i] = bodies[i].transform.rotation;
        }

        // 상판 기준 상대 배치 기록(깨끗한 프리팹 포즈). 리셋 때 이 배치를 그대로 재구성.
        relPos = new Vector3[bodies.Length];
        relRot = new Quaternion[bodies.Length];
        Quaternion invBoard = Quaternion.Inverse(initRot[0]);
        for (int i = 0; i < bodies.Length; i++)
        {
            relPos[i] = invBoard * (initPos[i] - initPos[0]);
            relRot[i] = invBoard * initRot[i];
        }
        MeasureGeometry();   // tableHeight + boardClearance (콜라이더 기하 기반, 지형 레이캐스트 불필요)
    }

    /// <summary>상판+다리 콜라이더로 테이블 높이와 '상판 원점이 최저점 위로 뜬 높이(boardClearance)'를 잰다.
    /// 깨끗한 프리팹 포즈에서 1회 측정 → 지형 생성 타이밍과 무관. 다리 피벗은 상판 자식이 아니라 bodies를 훑는다.</summary>
    void MeasureGeometry()
    {
        bool any = false; Bounds b = new Bounds(transform.position, Vector3.zero);
        if (bodies != null)
            foreach (var rb in bodies)
            {
                if (rb == null) continue;
                foreach (var c in rb.GetComponentsInChildren<Collider>())
                {
                    if (c.isTrigger) continue;
                    if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
                }
            }
        if (any)
        {
            tableHeight = Mathf.Max(0.2f, b.size.y);
            boardClearance = Mathf.Max(0f, transform.position.y - b.min.y);
        }
    }

    /// <summary>XZ 지점의 지면 높이(테이블 자신 제외). fromY 높이에서 아래로만 쏴서 그 위(예: 옆 건물 옥상)는 무시.
    /// '1' 제자리 리셋은 fromY=테이블 바로 위 → 테이블이 딛고 선 땅을 정확히 잡는다. 못 찾으면 fromY 반환.</summary>
    float GroundYAt(Vector2 xz, float fromY)
    {
        var hits = Physics.RaycastAll(new Vector3(xz.x, fromY, xz.y), Vector3.down, 2000f, ~0, QueryTriggerInteraction.Ignore);
        float gy = float.NegativeInfinity; bool found = false;
        foreach (var h in hits)
        {
            if (h.collider.transform.root == transform.root) continue;   // 테이블 자신 제외
            if (h.point.y > gy) { gy = h.point.y; found = true; }
        }
        return found ? gy : fromY;
    }


    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.digit1Key.wasPressedThisFrame)
            ResetPoseInPlace();

        // 뒤집힘 감지: 상판이 '많이 누운' 채 '거의 멈춰'(정착) 있어야 타이머 시작.
        // 구르는 중·일시적 기울임엔 시작 안 함 → 너무 자주 뜨던 문제 해결.
        bool flipped = Vector3.Dot(transform.up, Vector3.up) < flipDot;
        bool calm = bodies != null && bodies.Length > 0 && bodies[0] != null
                    && bodies[0].linearVelocity.magnitude < flipCalmSpeed
                    && bodies[0].angularVelocity.magnitude < flipCalmSpeed;
        if (flipped && calm) { if (flippedSince < 0f) flippedSince = Time.unscaledTime; }
        else if (!flipped) flippedSince = -1f;   // 똑바로 돌아오면만 리셋(살짝 흔들려도 타이머 유지)
    }

    bool ShowFlipPrompt =>
        flippedSince >= 0f && (Time.unscaledTime - flippedSince) >= flipPromptDelay;

    /// <summary>
    /// '1' 키: 처음 위치로 리스폰하지 않고 — 지금 있는 자리(XZ)·바라보는 방향(yaw)은 유지한 채
    /// 기울기/다리만 초기 상태로 똑바로 세운다.
    /// </summary>
    public void ResetPoseInPlace()
    {
        if (bodies == null || bodies.Length == 0 || bodies[0] == null) { ResetAll(); return; }
        var b0 = bodies[0].transform.position;
        // ★ 지면 스캔을 '테이블 바로 위'에서 아래로 → 옆 건물 옥상이 아니라 테이블이 딛고 선 땅을 잡는다.
        ResetPoseInternal(new Vector2(b0.x, b0.z), b0.y + 0.5f);
    }

    /// <summary>지정한 XZ 지점의 땅 위에 똑바로 선 자세로 복원. (용암 사망 → 가장 가까운 안전한 땅 리스폰용.)</summary>
    public void ResetPoseAtXZ(float x, float z)
    {
        if (bodies == null || bodies.Length == 0 || bodies[0] == null) { ResetAll(); return; }
        // 다른 XZ(고지대)로 보내는 경우라 충분히 높은 곳에서 스캔.
        ResetPoseInternal(new Vector2(x, z), 500f);
    }

    void ResetPoseInternal(Vector2 xz, float scanFromY)
    {
        var board = bodies[0];
        // 바라보는 '카메라 방향'으로 테이블 forward 를 맞춤 → 리셋 후 항상 WASD 다리(Leg_FL)가 화면 먼왼쪽.
        // (기울어진 상태의 eulerAngles.y 는 부정확해 다리-카메라 매핑이 틀어지므로 카메라 기준으로 정렬한다.)
        float yaw = board.transform.eulerAngles.y;         // 폴백(카메라 없을 때)
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
        // 항상 '완전히 똑바로'(yaw만) 세운다. 스폰/낙하 중 잡힌 board 기울기를 절대 물려받지 않음.
        // (빌드는 실행·물리 순서가 달라 Start()가 board가 기운 뒤 호출될 수 있어, 그 tilt가 리셋마다 박히던 버그 수정.)
        Quaternion newBoardRot = Quaternion.Euler(0f, yaw, 0f);
        // 지면 위로 '테이블 절반 높이'만큼 띄워 생성 → 지형에 잠기지 않고 살짝 위에서 똑바로 안착.
        float lift = boardClearance + tableHeight * groundLiftFactor;
        Vector3 newBoardPos = new Vector3(xz.x, GroundYAt(xz, scanFromY) + lift, xz.y);

        for (int i = 0; i < bodies.Length; i++)
        {
            var b = bodies[i];
            if (b == null) continue;
            Vector3 p = newBoardPos + newBoardRot * relPos[i];
            Quaternion r = newBoardRot * relRot[i];
            b.position = p;
            b.rotation = r;
            b.transform.SetPositionAndRotation(p, r);
            b.linearVelocity = Vector3.zero;
            b.angularVelocity = Vector3.zero;
        }
        if (legs != null)
            foreach (var l in legs)
                if (l != null) { l.ResetState(); l.HoldVertical(verticalHoldSeconds); }

        var giraffe = FindAnyObjectByType<GiraffeMode>();
        if (giraffe != null) giraffe.ResetLegs();
    }

    /// <summary>테이블을 초기(지면 스냅된) 위치·자세로 되돌린다. 용암 사망 리스폰 등에서도 호출.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            var b = bodies[i];
            if (b == null) continue;
            // 동적 Rigidbody 는 물리 위치가 우선 → rb.position/rotation 으로 직접 텔레포트(트랜스폼도 동기).
            b.position = initPos[i];
            b.rotation = initRot[i];
            b.transform.SetPositionAndRotation(initPos[i], initRot[i]);
            b.linearVelocity = Vector3.zero;
            b.angularVelocity = Vector3.zero;
        }
        if (legs != null)
            foreach (var l in legs)
                if (l != null)
                {
                    l.ResetState();
                    l.HoldVertical(verticalHoldSeconds); // 리셋 직후 잠깐 수직 고정 → 안착 후 해제
                }

        // 지라프 모드로 늘어난 다리 길이도 초기화
        var giraffe = FindAnyObjectByType<GiraffeMode>();
        if (giraffe != null) giraffe.ResetLegs();
    }

    // 테이블이 뒤집히면 화면 하단에 'Press "1"' 안내. (일시정지 중엔 숨김)
    void OnGUI()
    {
        if (Time.timeScale == 0f) return;
        if (!ShowFlipPrompt) return;
        EnsureFlipStyles();

        float w = 440f, h = 96f;
        float x = (Screen.width - w) * 0.5f, y = Screen.height - h - 64f;
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(new Rect(x, y, w, h), flipTex);
        GUI.color = new Color(1f, 0.78f, 0.25f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, 5f), flipTex);                 // 상단 강조선
        GUI.color = prev;

        GUI.Label(new Rect(x, y + 12f, w, 46f), "Press \"1\"", flipBig);
        GUI.Label(new Rect(x, y + 56f, w, 30f), "테이블이 뒤집혔어요 — 자세 다시 잡기", flipSmall);
    }

    void EnsureFlipStyles()
    {
        if (flipBig != null) return;
        flipTex = Texture2D.whiteTexture;
        flipBig = new GUIStyle(GUI.skin.label)
        { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        flipBig.normal.textColor = new Color(1f, 0.85f, 0.4f, 1f);
        flipSmall = new GUIStyle(GUI.skin.label)
        { fontSize = 17, alignment = TextAnchor.MiddleCenter };
        flipSmall.normal.textColor = Color.white;
    }
}
