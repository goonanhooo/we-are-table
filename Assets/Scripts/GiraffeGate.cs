using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 지라프 터치버튼 4개가 '동시에' 밟혀(Pressed) 있는 순간 → 문(둥근 뚜껑)이 경첩처럼 열리는 애니메이션 +
/// 건물 구석에서 전체를 비추며 문 열리는 모습을 보여주는 컷씬. 그 뒤 테이블이 구멍으로 떨어지면 빙산 씬으로 전환.
/// 버튼은 모멘터리(떼면 빨강)라, 네 발로 네 버튼을 동시에 밟고 있어야만 발동한다.
/// </summary>
public class GiraffeGate : MonoBehaviour
{
    public GiraffeTouchButton[] buttons;

    [Header("문(뚜껑)")]
    public Transform lid;
    [Tooltip("경첩 회전 각도(도).")]
    public float doorOpenAngle = 105f;
    public float doorSeconds = 1.3f;

    [Header("컷씬")]
    [Tooltip("문 열림 보여준 뒤 멈춰서 보여주는 시간(초).")]
    public float cutsceneHold = 1.0f;
    [Tooltip("구석 카메라 위치(Warehouse 로컬). 높은 구석에서 전체+문을 봄.")]
    public Vector3 cornerLocal = new Vector3(-22f, 24f, 30f);

    [Header("낙하 → 다음 씬")]
    public string nextScene = "IceCavern";   // ★2026-09-06 정글 다음 = 광차 동굴(IceCavern) → 눈사람 동굴 도착 시 빙산(IceMountain). 씬 인스턴스도 갱신됨
    public float fallTriggerY = 5f;
    public float holeRadius = 2.5f;

    [Header("페이드(기둥 속에서 어두워졌다 빙산에서 밝아짐)")]
    [Tooltip("보드가 이 y 아래로 내려가면(구멍 XZ 안) 페이드 아웃 시작.")]
    public float fadeStartY = -2f;
    public float fadeOutSeconds = 1.2f;
    public float fadeInDelay = 0.5f;     // 빙산 로드 후 검정 유지 시간
    public float fadeInSeconds = 1.8f;

    Transform board;
    Vector2 holeXZ;
    bool opened, transitioned, fadeStarted;

    void Start()
    {
        if (lid != null) holeXZ = new Vector2(lid.position.x, lid.position.z);
        var b = GameObject.Find("Table");
        if (b != null) board = b.transform;
    }

    void Update()
    {
        // [테스트] 치트 ON(옵션 메뉴) 상태에서 `(물결/백쿼트 키) = 버튼 없이 문 열기 — 구멍 낙하만 바로 확인용
        if (!opened && CheatMode.Active)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame) ForceOpen();
        }

        if (!opened && buttons != null && buttons.Length > 0)
        {
            bool all = true;
            foreach (var b in buttons) if (b == null || !b.Pressed) { all = false; break; }   // ★동시에 전부 Pressed
            if (all) { opened = true; StartCoroutine(OpenCutscene()); }
        }

        if (opened && !transitioned && board != null)
        {
            bool inHole = new Vector2(board.position.x - holeXZ.x, board.position.z - holeXZ.y).sqrMagnitude < holeRadius * holeRadius;
            // 기둥 속으로 떨어지기 시작하면 서서히 어두워짐(씬 전환 전에 완전 검정 도달)
            if (!fadeStarted && inHole && board.position.y < fadeStartY)
            { fadeStarted = true; ScreenFader.FadeOut(fadeOutSeconds); }
            if (inHole && board.position.y < fallTriggerY) TryFall();
        }
    }

    public bool Opened => opened;

    /// <summary>구멍 트리거(GateFallTrigger)가 테이블 진입 시 호출 — 문 열린 뒤에만 전환.
    /// 트리거는 샤프트 깊숙이(y≈-8) 있어 테이블이 어둠 속을 한참 떨어진 뒤 빙산씬으로 넘어간다.</summary>
    public void TryFall()
    {
        if (!opened || transitioned) return;
        transitioned = true;
        Time.timeScale = 1f;
        ScreenFader.FadeOut(0.25f);                              // 안전망: 아직 안 어두우면 즉시 마저 어둡게
        ScreenFader.FadeInAfterNextLoad(fadeInDelay, fadeInSeconds);  // 빙산: 검정 유지 후 서서히 밝아짐
        NetScene.Load(nextScene);   // 멀티: 호스트가 전원 씬 전환(게스트 트리거는 무시)
    }

    /// <summary>[테스트용] 버튼 4개 없이 문 열림 컷씬 발동(에디터 검증 편의).</summary>
    public void ForceOpen()
    {
        if (opened) return;
        opened = true;
        StartCoroutine(OpenCutscene());
    }

    IEnumerator OpenCutscene()
    {
        var cam = Camera.main;
        var w = GameObject.Find("Warehouse");
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;

        // 1) 건물 구석(높은 곳)으로 컷 — 전체 + 문 보여줌
        if (cam != null && w != null)
        {
            if (follow != null) follow.enabled = false;
            Vector3 camPos = w.transform.TransformPoint(cornerLocal);
            Vector3 lookAt = lid != null ? lid.position : w.transform.position;
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up));
        }
        yield return new WaitForSeconds(0.5f);

        // 2) ★문(뚜껑)이 옆으로 '스르륵' 미끄러져 열림 → 실제 구멍(수직 샤프트)이 드러난다.
        //    방향 = 건물 중심에서 바깥쪽(버튼들과 안 겹치게), 거리 = 뚜껑 지름보다 살짝 크게(완전 개방).
        if (lid != null)
        {
            Vector3 dir = Vector3.right;
            if (w != null)
            {
                dir = lid.position - w.transform.position; dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : w.transform.right;
            }
            Vector3 from = lid.position;
            Vector3 to = from + dir * (lid.lossyScale.x * 1.15f);
            for (float t = 0; t < doorSeconds; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / doorSeconds);
                lid.position = Vector3.Lerp(from, to, u);
                yield return null;
            }
            lid.position = to;
        }

        // 3) 잠깐 멈춰서 보여준 뒤 카메라 복귀
        yield return new WaitForSeconds(cutsceneHold);
        if (follow != null) follow.enabled = true;
    }
    public void RestoreCompleted()
    {
        StopAllCoroutines();opened=true;
        if(lid!=null)lid.gameObject.SetActive(false);
    }

}
