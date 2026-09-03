using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★2026-09-03 야외 테니스 경기장(TennisCourt) — 정글과 빙산 사이(액자 홀 대체, 사용자 요청 "밝고 심플하게").
/// 퍼즐은 FrameHallClimb 그대로(공 4개 수집 → 애벌레 컷씬 → 테니스공의 힘 → 60° 관중석 경사면 등반).
/// 이 스크립트는 결말만: 경사면 꼭대기 플랫폼의 둥근 구멍(정글 DarkTube 복사본)으로 떨어지면
/// 어두워지며 빙산으로 전환(GiraffeGate/FrameHallStage와 같은 규칙). 치트 ON + ` = 즉시 구멍 위로 텔레포트(테스트).
/// 씬 참조: exitHole(구멍 중심 마커).
/// </summary>
public class TennisCourtStage : MonoBehaviour
{
    [Header("출구 구멍(씬 오브젝트)")]
    [Tooltip("플랫폼 구멍 중심(빈 오브젝트). XZ 반경 안에서 아래로 떨어지면 전환")] public Transform exitHole;
    public float holeRadius = 2.4f;
    [Tooltip("보드가 이 y 아래(구멍 XZ 안)면 페이드 아웃 시작")] public float fadeStartY = 10f;
    [Tooltip("보드가 이 y 아래면 씬 전환")] public float fallTriggerY = 4f;
    public string nextScene = "IceMountain";
    public float fadeOutSeconds = 1.2f;
    public float fadeInDelay = 0.5f;
    public float fadeInSeconds = 1.8f;

    Transform board;
    bool transitioned, fadeStarted;
    public bool Transitioned => transitioned;

    void Update()
    {
        if (board == null) { var b = GameObject.Find("Table"); if (b != null) board = b.transform; }
        if (board == null || exitHole == null || transitioned) return;

        // [테스트] 치트 ON + ` = 구멍 위로 텔레포트
        if (CheatMode.Active)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame)
            {
                var ab = board.GetComponent<ArticulationBody>();
                if (ab != null) { ab.TeleportRoot(exitHole.position + Vector3.up * 2.5f, Quaternion.identity); ab.linearVelocity = Vector3.zero; }
            }
        }

        Vector2 d = new Vector2(board.position.x - exitHole.position.x, board.position.z - exitHole.position.z);
        bool inHole = d.sqrMagnitude < holeRadius * holeRadius;
        if (!fadeStarted && inHole && board.position.y < fadeStartY) { fadeStarted = true; ScreenFader.FadeOut(fadeOutSeconds); }
        if (inHole && board.position.y < fallTriggerY) TryFall();
    }

    public void TryFall()
    {
        if (transitioned) return;
        transitioned = true;
        Time.timeScale = 1f;
        ScreenFader.FadeOut(0.25f);
        ScreenFader.FadeInAfterNextLoad(fadeInDelay, fadeInSeconds);
        NetScene.Load(nextScene);   // 멀티: 호스트만 실제 전환
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (exitHole == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0.2f);
        Gizmos.DrawWireSphere(exitHole.position, holeRadius);
        Gizmos.DrawLine(exitHole.position, new Vector3(exitHole.position.x, fallTriggerY, exitHole.position.z));
    }
#endif
}
