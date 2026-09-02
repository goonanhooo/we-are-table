using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★2026-07-15 액자 맵(FrameHall) — 정글과 빙산 사이의 갤러리 홀.
/// 정면 벽에 '기린 테이블이 화분에 다리 하나로 선' 액자, 중앙에 화분.
///
/// ★2026-09-03 퍼즐을 버튼식으로 교체(사용자 요청 — 자세 판정은 너무 어려움):
///   - 화분 = 텅 빈 원통, 바닥에 '밟아야 하는' 버튼(GiraffeTouchButton: 평소 빨강 → 눌리면 초록)
///   - 화분 둘레 = 화분 지름 ~4배의 넓은 도넛 링 버튼(RingPlate: 평소 초록 → 무언가 닿으면 빨강)
///   - 둘 다 초록(안쪽 눌림 && 링 안 눌림)을 holdNeed 초 유지 → 해결.
///   (다리 하나를 화분에 넣고 기린 힘으로 몸을 들어 나머지 다리를 링에서 띄우는 것이 정답)
/// 결말(정글 GiraffeGate 방식): 바닥 뚜껑(HoleLid)이 옆으로 스르륵 밀려나 구멍이 드러나고, 높은 구석 카메라로
/// 보여준 뒤 테이블이 구멍(어두운 원통)으로 떨어지면 어두워지며 빙산(IceMountain)으로 전환.
/// 씬 오브젝트 참조(에디터 편집형): innerButton / outerRing / hatchLid / lidOpenPoint / hatchCam.
/// 치트 ON + ` 키 = 즉시 해결(테스트용, GiraffeGate 패턴).
/// </summary>
public class FrameHallStage : MonoBehaviour
{
    [Header("버튼 퍼즐 — 화분 안 버튼 밟기 + 바깥 링 안 밟기(둘 다 초록 유지)")]
    [Tooltip("화분 바닥의 '밟아야 하는' 버튼(빨강→초록)")] public GiraffeTouchButton innerButton;
    [Tooltip("화분 둘레의 '밟으면 안 되는' 링(초록→빨강)")] public RingPlate outerRing;
    [Tooltip("둘 다 초록 유지 시간(초)")] public float holdNeed = 1.0f;

    [Header("바닥 뚜껑(정글 HoleLid 방식 — 씬 오브젝트)")]
    [Tooltip("구멍을 덮은 뚜껑(콜라이더 포함). 해결 시 lidOpenPoint로 미끄러진다.")] public Transform hatchLid;
    [Tooltip("뚜껑이 밀려나 멈출 위치(빈 오브젝트 — 씬에서 옮겨 편집)")] public Transform lidOpenPoint;
    public float lidSeconds = 1.3f;
    [Tooltip("컷씬 카메라 위치(높은 구석 — 빈 오브젝트). 뚜껑을 바라본다.")] public Transform hatchCam;
    [Tooltip("뚜껑 열린 뒤 멈춰서 보여주는 시간(초)")] public float cutsceneHold = 1.0f;

    [Header("낙하 → 다음 씬")]
    public string nextScene = "IceMountain";
    [Tooltip("구멍 XZ 반경(뚜껑 원래 위치 기준) — 이 안에서 아래로 떨어져야 전환")] public float holeRadius = 4.5f;
    [Tooltip("보드가 이 y 아래(구멍 XZ 안)면 페이드 아웃 시작")] public float fadeStartY = -2f;
    [Tooltip("보드가 이 y 아래면 씬 전환")] public float fallTriggerY = -12f;
    public float fadeOutSeconds = 1.2f;
    public float fadeInDelay = 0.5f;
    public float fadeInSeconds = 1.8f;

    Transform board;
    float holdTimer;
    bool solved, transitioned, fadeStarted;
    Vector2 holeXZ;

    // ── 검증/디버그용 읽기 전용 상태(RunCommand 폴링) ──
    public float HoldTimerNow => holdTimer;
    public bool Solved => solved;
    public bool Transitioned => transitioned;
    public float BoardY => board != null ? board.position.y : -999f;
    public bool InnerPressed => innerButton != null && innerButton.Pressed;
    public bool OuterPressed => outerRing != null && outerRing.Pressed;
    public string PuzzleDebug() =>
        "inner=" + (innerButton ? innerButton.Pressed.ToString() : "null") + " outer=" + (outerRing ? outerRing.Pressed.ToString() : "null")
        + (outerRing ? "(" + outerRing.LastToucher + ")" : "") + " hold=" + holdTimer.ToString("F2") + " solved=" + solved;

    void Start()
    {
        var b = GameObject.Find("Table");
        if (b != null) board = b.transform;
        if (hatchLid != null) holeXZ = new Vector2(hatchLid.position.x, hatchLid.position.z);
    }

    void Update()
    {
        // board 레이지 재탐색(스폰 타이밍에 따라 Start에서 못 잡는 경우 대비)
        if (board == null)
        {
            var b = GameObject.Find("Table");
            if (b != null) board = b.transform;
        }

        // [테스트] 치트 ON + ` = 즉시 해결
        if (!solved && CheatMode.Active)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame) Solve();
        }

        if (solved)
        {
            // ── 정글과 동일: 구멍 XZ 안에서 아래로 떨어지면 어두워지고 → 더 내려가면 빙산으로 ──
            if (!transitioned && board != null)
            {
                bool inHole = new Vector2(board.position.x - holeXZ.x, board.position.z - holeXZ.y).sqrMagnitude < holeRadius * holeRadius;
                if (!fadeStarted && inHole && board.position.y < fadeStartY)
                { fadeStarted = true; ScreenFader.FadeOut(fadeOutSeconds); }
                if (inHole && board.position.y < fallTriggerY) TryFall();
            }
            return;
        }

        // 버튼 판정: 안쪽 눌림 && 링 안 눌림 → 유지 시간 누적
        bool ok = innerButton != null && innerButton.Pressed && (outerRing == null || !outerRing.Pressed);
        holdTimer = ok ? holdTimer + Time.deltaTime : 0f;
        if (holdTimer >= holdNeed) Solve();
    }

    public void Solve()
    {
        if (solved) return;
        solved = true;
        StartCoroutine(SolveCutscene());
    }

    /// <summary>구멍으로 떨어짐 → 빙산. (멀티: 호스트만 실제 전환 — NetScene이 게스트 호출 무시)</summary>
    public void TryFall()
    {
        if (!solved || transitioned) return;
        transitioned = true;
        Time.timeScale = 1f;
        ScreenFader.FadeOut(0.25f);                                   // 안전망: 아직 안 어두우면 즉시 마저 어둡게
        ScreenFader.FadeInAfterNextLoad(fadeInDelay, fadeInSeconds);  // 빙산: 검정 유지 후 서서히 밝아짐
        NetScene.Load(nextScene);
    }

    IEnumerator SolveCutscene()
    {
        var cam = Camera.main;
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;

        // 1) 높은 구석 카메라 — 홀 전체 + 뚜껑
        if (cam != null && hatchCam != null)
        {
            if (follow != null) follow.enabled = false;
            Vector3 lookAt = hatchLid != null ? hatchLid.position : Vector3.zero;
            cam.transform.SetPositionAndRotation(hatchCam.position, Quaternion.LookRotation((lookAt - hatchCam.position).normalized, Vector3.up));
        }
        yield return new WaitForSeconds(0.5f);

        // 2) 뚜껑이 옆으로 '스르륵' 밀려나 구멍이 드러남(정글 HoleLid와 동일)
        if (hatchLid != null)
        {
            Vector3 from = hatchLid.position;
            Vector3 to = lidOpenPoint != null ? lidOpenPoint.position : from + Vector3.right * (hatchLid.lossyScale.x * 1.15f);
            for (float t = 0f; t < lidSeconds; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / lidSeconds);
                hatchLid.position = Vector3.Lerp(from, to, u);
                yield return null;
            }
            hatchLid.position = to;
        }

        // 3) 잠깐 보여준 뒤 카메라 복귀
        yield return new WaitForSeconds(cutsceneHold);
        if (follow != null) follow.enabled = true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (hatchLid != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f);
            Gizmos.DrawWireSphere(new Vector3(hatchLid.position.x, 0f, hatchLid.position.z), holeRadius);
            if (lidOpenPoint != null) { Gizmos.DrawLine(hatchLid.position, lidOpenPoint.position); Gizmos.DrawWireCube(lidOpenPoint.position, hatchLid.lossyScale); }
        }
        if (hatchCam != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(hatchCam.position, Vector3.one * 0.5f);
            if (hatchLid != null) Gizmos.DrawLine(hatchCam.position, hatchLid.position);
        }
    }
#endif
}
