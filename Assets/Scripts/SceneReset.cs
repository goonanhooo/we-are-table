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
// [RequireComponent(typeof(Rigidbody))]  ★art 전환: Rigidbody 없는 프리팹 허용(Awake에서 자가 비활성)
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
        // ★ ArticulationBody 테이블(프리팹 전환 후): Rigidbody 없음 → 이 컴포넌트 비활성
        //   ('1' 리셋/뒤집힘 안내는 ArtSceneReset이 담당. fixedDeltaTime 설정은 유지 위해 아래 줄 이후에 disable)
        if (bodies.Length == 0) { Time.fixedDeltaTime = 0.005f; enabled = false; return; }
        // ★속도 상한(전 씬 공통): 기본값 maxLinearVelocity=1e16(무제한)이라 공중에서 다리가 휘둘리다
        //   모서리에 부딪히면 속도가 폭발→좌표 Inf→렌더 AABB 깨짐('Invalid AABB'). GiraffeMode가 없는
        //   Hallway 등에서도 안전하도록 여기서 캡을 건다. (GiraffeMode 씬은 매 프레임 더 강한 캡으로 덮어씀.)
        // 작은 타임스텝 = 깊은 침투↓ = 접촉 폭발↓ (속도/움직임 자체엔 영향 없음 — 물리가 더 정밀해질 뿐).
        Time.fixedDeltaTime = 0.005f;
        foreach (var b in bodies)
        {
            if (b == null) continue;
            b.maxLinearVelocity = 40f;
            b.maxAngularVelocity = 25f;
            b.maxDepenetrationVelocity = 2f;   // 깊이 박혀도 부드럽게 밀어냄(폭발성 튕김 차단) — 움직임 무관
            if (b.solverIterations < 16) b.solverIterations = 16;
        }
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

    Vector2 lastGoodXZ; bool haveGoodXZ;
    static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    static bool FiniteQ(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);
    // 정상 = 유한 + 원점에서 합리적 거리(폭발로 멀리 날아가면 '거대하지만 유한'이라 Finite는 통과하므로 거리도 검사).
    static bool BodyOk(Rigidbody b) => Finite(b.position) && FiniteQ(b.rotation) && Finite(b.linearVelocity) && Finite(b.angularVelocity)
        && b.position.sqrMagnitude < 600f * 600f && b.position.y > -150f;

    // NaN 안전망: 긴 다리+강한 힘으로 솔버가 가끔 NaN(위치/회전/속도)을 내면 렌더 전(매 물리스텝)에 잡는다.
    // → 카메라 'IsFinite(distanceAlongView)' 어설션, 'Invalid AABB'(렌더 바운드 깨짐) 방지.
    // 복구는 부드럽게: NaN 난 '다리'만 상판 기준 제자리로 되돌림(상판 정상일 때). 상판 자체가 NaN이면 전체 자세 리셋.
    void FixedUpdate() { Watchdog(); }

    // ★ LateUpdate = 물리 step '이후' · 렌더 '직전'. FixedUpdate 워치독은 물리 step 이전에 돌아서
    //   폭발(NaN/거대화)을 항상 한 프레임 늦게 잡고, 그 사이 깨진 렌더 AABB가 그대로 그려져
    //   'Invalid AABB'/'Invalid worldAABB' 어설션이 GUIUtility:ProcessEvent에서 폭주 → 에디터 프리즈/크래시.
    //   여기서 한 번 더 정상화하면 '렌더 전에' 좌표가 유한해져 어설션·크래시가 사라진다(감각 변화 0).
    void LateUpdate() { Watchdog(); }

    void Watchdog()
    {
        if (bodies == null || bodies.Length == 0 || bodies[0] == null) return;

        bool boardOk = BodyOk(bodies[0]);
        bool anyBad = false;
        for (int i = 0; i < bodies.Length; i++)
        {
            var b = bodies[i];
            if (b == null || BodyOk(b)) continue;
            anyBad = true;
            b.linearVelocity = Vector3.zero;
            b.angularVelocity = Vector3.zero;
            if (i > 0 && boardOk && relPos != null && relPos.Length > i)
            {
                // 다리: 상판 기준 깨끗한 상대배치로 즉시 복원(길이는 유지, 전체 리셋 아님)
                Vector3 bp = bodies[0].position; Quaternion br = bodies[0].rotation;
                Vector3 p = bp + br * relPos[i];
                Quaternion r = br * relRot[i];
                if (Finite(p) && FiniteQ(r)) { b.position = p; b.rotation = r; b.transform.SetPositionAndRotation(p, r); }
            }
        }

        if (anyBad)
        {
            // 조인트 소독: 폭발 프레임에 LegController가 NaN 각도로 한계/모터를 써넣었을 수 있음 —
            // 바디만 복구하면 조인트에 래치된 NaN이 다음 스텝 재폭발을 일으킨다.
            for (int i = 0; i < bodies.Length; i++)
            {
                var lc = bodies[i] != null ? bodies[i].GetComponent<LegController>() : null;
                if (lc != null) lc.SanitizeJoint();
            }
        }

        if (anyBad && !boardOk)
        {
            Vector2 xz = haveGoodXZ ? lastGoodXZ : new Vector2(initPos[0].x, initPos[0].z);
            ResetPoseAtXZ(xz.x, xz.y);   // 상판 NaN → 마지막 정상 위치로 전체 자세 복구
        }
        else if (boardOk)
        {
            var p = bodies[0].position;
            lastGoodXZ = new Vector2(p.x, p.z); haveGoodXZ = true;
        }
    }

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
        // ★방탄: board/카메라가 NaN이면 yaw가 NaN이 되어 복구해도 다시 NaN(무한 루프). NaN이면 0으로.
        if (!float.IsFinite(yaw)) yaw = 0f;
        // 항상 '완전히 똑바로'(yaw만) 세운다. 스폰/낙하 중 잡힌 board 기울기를 절대 물려받지 않음.
        // (빌드는 실행·물리 순서가 달라 Start()가 board가 기운 뒤 호출될 수 있어, 그 tilt가 리셋마다 박히던 버그 수정.)
        Quaternion newBoardRot = Quaternion.Euler(0f, yaw, 0f);
        // 지면 위로 '테이블 절반 높이'만큼 띄워 생성 → 지형에 잠기지 않고 살짝 위에서 똑바로 안착.
        float lift = boardClearance + tableHeight * groundLiftFactor;
        // ★방탄: 스캔 시작점/결과가 NaN이면 폴백 높이로(스폰 근처 안전고도).
        if (!float.IsFinite(scanFromY)) scanFromY = 500f;
        float gy = GroundYAt(xz, scanFromY);
        if (!float.IsFinite(gy)) gy = 30f;
        Vector3 newBoardPos = new Vector3(xz.x, gy + lift, xz.y);

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
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (Time.timeScale == 0f) return;
        if (!ShowFlipPrompt) return;
        EnsureFlipStyles();

        float w = 372f, h = 80f;
        float x = (Screen.width - w) * 0.5f, y = Screen.height - h - 56f;
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, w, h), flipTex);
        GUI.color = new Color(1f, 0.78f, 0.25f, 1f);
        GUI.DrawTexture(new Rect(x, y, w, 4f), flipTex);                 // 상단 강조선
        GUI.color = prev;

        GUI.Label(new Rect(x, y + 10f, w, 40f), "Press \"1\"", flipBig);
        GUI.Label(new Rect(x, y + 48f, w, 24f), "테이블이 뒤집혔어요 — 자세 다시 잡기", flipSmall);
    }

    void EnsureFlipStyles()
    {
        if (flipBig != null) return;
        flipTex = Texture2D.whiteTexture;
        flipBig = new GUIStyle(GUI.skin.label)
        { fontSize = 29, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        flipBig.normal.textColor = new Color(1f, 0.85f, 0.4f, 1f);
        flipSmall = new GUIStyle(GUI.skin.label)
        { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        flipSmall.normal.textColor = Color.white;
    }
}
