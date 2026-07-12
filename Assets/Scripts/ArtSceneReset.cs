using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ArticulationBody 테이블용 리셋/리스폰. 테이블 루트에 부착.
/// ★2026-07-12 개편(사용자 요청): '1' 제자리 자세 복원 폐지 → **F1 = 다시 시작(처음 스폰 위치 리스폰)**.
/// - F1 또는 뒤집힘 배너의 [다시 시작] 버튼 = Restart(): 초기 스폰 XZ에 리스폰(스캔은 초기 높이 바로 위에서 —
///   빙산 동굴처럼 지붕 있는 곳에서 지붕 위로 안 올라가게). 정글은 JungleStage.RestartRespawn이 가로채
///   강을 건넌 뒤면 '지금 있던 쪽' 안전 지점(체크포인트 규칙)에 리스폰.
/// - ResetPoseAtXZ = 용암/추락 리스폰 등: 지정 XZ의 땅 위에 복원. yaw는 카메라 방향(조작-화면 매핑 일관).
/// - lift = 콜라이더 기하 1회 측정(boardClearance + tableHeight × groundLiftFactor).
/// - 다리 ResetState + HoldVertical(0.4s), GiraffeMode.ResetLegs(늘어난 길이 초기화).
/// - 뒤집힘 2초(+정착) 시 [다시 시작 (F1)] 버튼 배너(클릭은 Mouse.current — PauseMenu 패턴).
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
    [Tooltip("★F1 다시 시작 차단(축구 등 규칙 씬 — 골이 나야만 재배치). 워치독/뒤집힘 복구는 유지.")]
    public bool disableManualReset = false;

    ArticulationBody root;
    ArticulationBody[] bodies;
    ArtLegController[] legs;
    float tableHeight = 0.8f, boardClearance = 0.75f;   // MeasureGeometry로 갱신
    float flippedTime;
    Vector2 lastGoodXZ; bool haveGoodXZ;                 // NaN/폭발 워치독용(원본 SceneReset 이식)
    Vector3 initPos;                                     // F1 다시 시작용 초기 스폰(루트 위치, Awake 캡처)
    GUIStyle bannerStyle, bannerSub, btnLabel;
    Texture2D white;

    void Awake()
    {
        bodies = GetComponentsInChildren<ArticulationBody>(true);
        foreach (var ab in bodies)
            if (ab.isRoot) { root = ab; break; }
        legs = GetComponentsInChildren<ArtLegController>(true);
        if (root != null) initPos = root.transform.position;
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
        if (!disableManualReset && kb.f1Key.wasPressedThisFrame) Restart();

        // 뒤집힘 감지(원본: 많이 누움 + 거의 정지일 때만 카운트 → 구르는 중 오탐 방지)
        bool flipped = Vector3.Dot(root.transform.up, Vector3.up) < flipDot
                       && root.linearVelocity.magnitude < flipCalmSpeed
                       && root.angularVelocity.magnitude < flipCalmSpeed;
        flippedTime = flipped ? flippedTime + Time.deltaTime : 0f;

        // 뒤집힘 배너의 [다시 시작] 버튼 클릭 — GUI.Button은 새 입력시스템에서 안 먹어 Mouse.current로 직접(PauseMenu 패턴)
        var mouse = Mouse.current;
        if (!disableManualReset && flippedTime >= flipPromptDelay && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 p = mouse.position.ReadValue();
            Vector2 gm = new Vector2(p.x, Screen.height - p.y);   // 좌하 원점 → GUI 좌상 원점
            if (RestartBtnRect().Contains(gm)) Restart();
        }
    }

    Rect BannerRect() => new Rect((Screen.width - 372f) * 0.5f, Screen.height * 0.18f, 372f, 112f);
    Rect RestartBtnRect()
    {
        var r = BannerRect();
        return new Rect(r.x + r.width * 0.5f - 110f, r.y + 56f, 220f, 42f);
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (disableManualReset) return;
        if (flippedTime < flipPromptDelay) return;
        if (bannerStyle == null)
        {
            white = Texture2D.whiteTexture;
            Color ink = new Color(0.08f, 0.08f, 0.08f, 1f);
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = ink;
            bannerSub = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            bannerSub.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            btnLabel = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            btnLabel.normal.textColor = ink;
        }
        var r = BannerRect();
        UiKit.Panel(r, 0.95f);   // 흰 배경 + 얇은 검은 테두리(통일 스타일)
        GUI.Label(new Rect(r.x, r.y + 8f, r.width, 34f), "뒤집혔어요!", bannerStyle);

        var btn = RestartBtnRect();
        bool hover = btn.Contains(Event.current.mousePosition);
        UiKit.Panel(btn);
        if (hover) UiKit.Fill(btn, new Color(0f, 0f, 0f, 0.10f));
        GUI.Label(btn, "다시 시작 (F1)", btnLabel);
    }

    /// <summary>F1/[다시 시작] 버튼: 처음 스폰 위치에서 재스폰. 정글은 강을 건넌 뒤면
    /// JungleStage가 '지금 있던 쪽' 안전 지점(체크포인트 규칙)으로 가로챈다.</summary>
    public void Restart()
    {
        if (root == null || disableManualReset) return;
        var js = Object.FindAnyObjectByType<JungleStage>();
        if (js != null && js.RestartRespawn(this)) return;
        // 스캔을 '초기 높이 바로 위'에서 → 빙산 동굴처럼 지붕 있는 스폰에서 지붕 위로 안 올라감.
        ResetPoseAtXZ(initPos.x, initPos.z, initPos.y + 2f);
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
