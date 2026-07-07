using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ArticulationBody 테이블용 리셋/리스폰 — 본게임 SceneReset의 상호작용을 충실히 재현. 테이블 루트에 부착.
/// - '1' = 지금 자리(XZ)에서 자세만 복원. ★yaw는 '카메라가 보는 방향'으로 정렬(원본과 동일 —
///   기울어진 상태의 eulerAngles.y 는 부정확해 다리-카메라 매핑이 틀어지므로 카메라 기준. 리셋 후 항상 WASD 다리가 화면 먼왼쪽).
/// - ResetPoseAtXZ = 용암 리스폰 등: 지정 XZ의 땅 위에 복원(500 높이에서 스캔 — 다른 고지대로 보내는 경우 대비).
/// - lift = 콜라이더 기하 1회 측정(boardClearance + tableHeight × groundLiftFactor) — 원본 공식.
/// - 다리 ResetState + HoldVertical(0.4s), GiraffeMode.ResetLegs(늘어난 길이 초기화).
/// - 뒤집힘 2초(+정착) 시 'Press 1' 배너.
/// </summary>
public class ArtSceneReset : MonoBehaviour
{
    [Tooltip("리셋 직후 다리 수직 고정 시간(초) — 원본 SceneReset.verticalHoldSeconds")]
    public float verticalHoldSeconds = 0.4f;
    [Tooltip("지면 위로 띄우는 비율(테이블 높이 기준) — 원본 groundLiftFactor")]
    public float groundLiftFactor = 0.5f;
    [Tooltip("뒤집힘 판정: up·worldUp 이 이 값 미만이면 누움")]
    public float flipDot = 0.2f;
    public float flipPromptDelay = 2f;
    [Tooltip("뒤집힘 타이머 조건: 이 속도 미만으로 정착했을 때만(구르는 중 제외)")]
    public float flipCalmSpeed = 1.2f;
    [Tooltip("워치독: 원점에서 이 거리 초과 = 폭발로 간주해 복구. 우주 등 넓은 씬은 인스턴스에서 키울 것.")]
    public float watchdogMaxDist = 600f;
    [Tooltip("워치독: 이 y 아래 = 추락/폭발로 간주해 복구. 무중력 씬은 크게 음수로.")]
    public float watchdogMinY = -150f;
    [Tooltip("리셋 시 아래에 지면이 없으면 현재 높이 유지(무중력 씬용). false=기존 동작(스캔 높이/폴백).")]
    public bool keepYIfNoGround = false;
    [Tooltip("★'1' 수동 리셋 차단(축구 등 규칙 씬 — 골이 나야만 재배치). 워치독/뒤집힘 복구는 유지.")]
    public bool disableManualReset = false;

    ArticulationBody root;
    ArticulationBody[] bodies;
    ArtLegController[] legs;
    float tableHeight = 0.8f, boardClearance = 0.75f;   // MeasureGeometry로 갱신
    float flippedTime;
    Vector2 lastGoodXZ; bool haveGoodXZ;                 // NaN/폭발 워치독용(원본 SceneReset 이식)
    GUIStyle bannerStyle, bannerSub;
    Texture2D white;

    void Awake()
    {
        bodies = GetComponentsInChildren<ArticulationBody>(true);
        foreach (var ab in bodies)
            if (ab.isRoot) { root = ab; break; }
        legs = GetComponentsInChildren<ArtLegController>(true);
        MeasureGeometry();
    }

    /// <summary>워치독(원본 SceneReset 이식): 어느 바디든 위치가 NaN/거대값이면 마지막 정상 XZ로 전체 복구.
    /// 폭발은 '거대하지만 유한'일 수 있어 Finite 검사만으론 못 잡는다 — 거리/깊이 한계 포함.</summary>
    void FixedUpdate()
    {
        if (root == null) return;
        bool anyBad = false;
        foreach (var b in bodies)
        {
            if (b == null) continue;
            var p = b.transform.position;
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)
                || p.sqrMagnitude > watchdogMaxDist * watchdogMaxDist || p.y < watchdogMinY) { anyBad = true; break; }
        }
        if (!anyBad)
        {
            var rp = root.transform.position;
            lastGoodXZ = new Vector2(rp.x, rp.z); haveGoodXZ = true;
        }
        else if (haveGoodXZ)
            ResetPoseAtXZ(lastGoodXZ.x, lastGoodXZ.y);   // TeleportRoot가 사슬 전체를 유한 좌표로 복구
    }

    /// <summary>콜라이더 기하로 테이블 전체 높이/상판 원점의 최저점 위 높이를 1회 측정(원본 MeasureGeometry).</summary>
    void MeasureGeometry()
    {
        if (root == null) return;
        bool any = false; Bounds b = new Bounds(root.transform.position, Vector3.zero);
        foreach (var c in transform.root.GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger) continue;
            if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
        }
        if (any)
        {
            tableHeight = Mathf.Max(0.2f, b.size.y);
            boardClearance = Mathf.Max(0f, root.transform.position.y - b.min.y);
        }
    }

    /// <summary>XZ 지점의 지면 높이 — fromY에서 아래로 RaycastAll, 테이블 자신 제외, 가장 높은 지점. 못 찾으면 fromY.</summary>
    float GroundYAt(Vector2 xz, float fromY, out bool found)
    {
        var hits = Physics.RaycastAll(new Vector3(xz.x, fromY, xz.y), Vector3.down, 2000f, ~0, QueryTriggerInteraction.Ignore);
        float gy = float.NegativeInfinity; found = false;
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
        if (kb == null || root == null) return;
        if (!disableManualReset && kb.digit1Key.wasPressedThisFrame) ResetPoseInPlace();

        // 뒤집힘 감지(원본: 많이 누움 + 거의 정지일 때만 카운트 → 구르는 중 오탐 방지)
        bool flipped = Vector3.Dot(root.transform.up, Vector3.up) < flipDot
                       && root.linearVelocity.magnitude < flipCalmSpeed
                       && root.angularVelocity.magnitude < flipCalmSpeed;
        flippedTime = flipped ? flippedTime + Time.deltaTime : 0f;
    }

    void OnGUI()
    {
        if (flippedTime < flipPromptDelay) return;
        if (bannerStyle == null)
        {
            white = Texture2D.whiteTexture;
            Color ink = new Color(0.08f, 0.08f, 0.08f, 1f);
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 29, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = ink;
            bannerSub = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            bannerSub.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        }
        float w = 372f, h = 80f;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.18f, w, h);
        UiKit.Panel(r, 0.95f);   // 흰 배경 + 얇은 검은 테두리(통일 스타일)
        GUI.Label(new Rect(r.x, r.y + 8f, r.width, 36f), "Press 1", bannerStyle);
        GUI.Label(new Rect(r.x, r.y + 46f, r.width, 24f), "뒤집혔어요 — 1 키로 자세 복구", bannerSub);
    }

    /// <summary>'1': 지금 자리(XZ)·카메라 방향(yaw) 유지, 기울기/다리만 똑바로.
    /// 지면 스캔을 '테이블 바로 위'에서 → 옆 건물 옥상이 아니라 테이블이 딛고 선 땅을 잡는다.</summary>
    public void ResetPoseInPlace()
    {
        if (root == null) return;
        var p = root.transform.position;
        ResetPoseInternal(new Vector2(p.x, p.z), p.y + 0.5f);
    }

    /// <summary>지정 XZ의 땅 위에 복원(용암 사망 → 안전한 땅 리스폰용). 고지대로 보낼 수 있어 높은 곳에서 스캔.</summary>
    public void ResetPoseAtXZ(float x, float z)
    {
        if (root == null) return;
        ResetPoseInternal(new Vector2(x, z), 500f);
    }

    /// <summary>지정 XZ + 지정 스캔 높이에서 복원 — 리스폰 지점이 천장/지붕 아래(실내)일 때 천장 위에 안 얹히게.</summary>
    public void ResetPoseAtXZ(float x, float z, float scanFromY)
    {
        if (root == null) return;
        ResetPoseInternal(new Vector2(x, z), scanFromY);
    }

    void ResetPoseInternal(Vector2 xz, float scanFromY)
    {
        // ★ yaw = 카메라가 보는 방향(원본과 동일) — 리셋 후 조작-화면 매핑 일관. 폴백=현재 yaw, NaN→0.
        float yaw = root.transform.eulerAngles.y;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
        if (!float.IsFinite(yaw)) yaw = 0f;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        if (!float.IsFinite(scanFromY)) scanFromY = 500f;
        float gy = GroundYAt(xz, scanFromY, out bool grounded);
        float lift = boardClearance + tableHeight * groundLiftFactor;
        if (!grounded && keepYIfNoGround) gy = root.transform.position.y - lift;   // 무중력: 지금 높이에서 자세만 복원
        if (!float.IsFinite(gy)) gy = 30f;

        root.TeleportRoot(new Vector3(xz.x, gy + lift, xz.y), rot);
        root.linearVelocity = Vector3.zero;
        root.angularVelocity = Vector3.zero;
        foreach (var l in legs) if (l != null) { l.ResetState(); l.HoldVertical(verticalHoldSeconds); }

        var gm = GetComponentInChildren<GiraffeMode>(true);
        if (gm == null) gm = FindAnyObjectByType<GiraffeMode>();
        if (gm != null) gm.ResetLegs();
    }
}
