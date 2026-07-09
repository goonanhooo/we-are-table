using System.Collections;
using UnityEngine;

/// <summary>
/// ★ 맵 선택 3D 프리뷰 전환 연출(2026-07-10) — 갤럭시식 '전시 회전'이 아니라,
/// 선택(호버)이 바뀔 때마다 미니어처 섬이 방향성 있게 튀어나오고/밀려나는 전환 애니메이션.
/// - 사용처: LevelSelect(1인/로컬 맵 선택)와 NetDebugPanel 로비(멀티 맵 선택) 공용.
/// - 섬은 MapMiniatures의 미니어처를 복제(원본 불변, 복제본은 자동 회전 없음).
/// - 배치: 시작 카메라 기준 로컬 오프셋(화면 오른쪽 빈 공간) + 맵별 오프셋/회전/스케일 오버라이드.
/// - Show(i,dir): 0.12s 디바운스 + 전환 중 최신 요청만 큐잉(중복 입력 안전).
/// - 등장(0.3s): 방향 반대쪽+아래에서 슬라이드, 스케일 0.7→1, 살짝 기울었다 정착. 이후 미세 봅만.
/// - 퇴장(0.26s): 누른 방향으로 밀리며 축소. Confirm: 펄스 확대→아래로 꺼짐→콜백(기존 로드 로직).
/// </summary>
public class MapSelectPreview : MonoBehaviour
{
    [Header("참조")]
    public MapMiniatures miniatures;

    [Header("배치(카메라 로컬) — 화면 오른쪽 빈 공간")]
    [SerializeField] Vector3 cameraLocalOffset = new Vector3(1.45f, 0.52f, 3.4f);   // 시작 디오라마 테이블 위에 떠 있게(상판 관통 방지)
    [SerializeField] float baseScale = 0.6f;
    [Tooltip("맵별 추가 오프셋/회전/스케일(비면 기본값). 인덱스=맵 인덱스")]
    [SerializeField] Vector3[] previewOffset = new Vector3[10];
    [SerializeField] Vector3[] previewEuler = new Vector3[10];
    [SerializeField] float[] previewScale = new float[10];

    [Header("전환 타이밍/연출")]
    [SerializeField] float enterTime = 0.3f;
    [SerializeField] float exitTime = 0.26f;
    [SerializeField] float slideDistance = 1.1f;
    [SerializeField] float dropDistance = 0.55f;
    [SerializeField] float enterStartScale = 0.7f;
    [SerializeField] float tiltAngle = 10f;
    [SerializeField] float hoverDebounce = 0.12f;
    [SerializeField] float idleBobAmp = 0.012f;
    [SerializeField] float idleBobHz = 1.1f;
    [Tooltip("정착 후 전시용 Y 회전 속도(도/초) — 천천히. 0이면 회전 없음")]
    [SerializeField] float idleSpinSpeed = 14f;
    [SerializeField] float confirmPulse = 1.12f;
    [SerializeField] float confirmTime = 0.34f;

    Camera cam;
    Transform holder;      // 카메라 로컬 배치 기준(매 프레임 카메라 추종)
    Transform island;      // 현재 표시 중인 복제 섬
    int currentIndex = -1;
    int pendingIndex = -1;
    int pendingDir = 1;
    float pendingSince;
    bool transitioning;
    bool confirming;
    float idleT;
    Vector3 targetLocalPos;
    Quaternion targetLocalRot;
    float targetScale;

    /// <summary>전환/확정 중(호출측 중복 입력 차단용).</summary>
    public bool Busy => confirming;
    public int CurrentIndex => currentIndex;

    void Awake()
    {
        var hgo = new GameObject("PreviewHolder");
        holder = hgo.transform;
        holder.SetParent(transform, false);
    }

    void LateUpdate()
    {
        // 카메라 추종(시작 화면 카메라가 배경 연출로 움직여도 프리뷰는 화면 우측에 고정)
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam != null)
        {
            holder.position = cam.transform.TransformPoint(cameraLocalOffset);
            holder.rotation = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(holder.position - cam.transform.position, Vector3.up).normalized, Vector3.up);
        }

        // 디바운스된 보류 요청 처리
        if (!confirming && pendingIndex >= 0 && Time.unscaledTime - pendingSince >= hoverDebounce && !transitioning)
        {
            int idx = pendingIndex, dir = pendingDir;
            pendingIndex = -1;
            if (idx != currentIndex) StartCoroutine(Transition(idx, dir));
        }

        // idle: 미세 봅 + 천천히 Y 전시 회전(연출 중이 아닐 때만)
        if (island != null && !transitioning && !confirming)
        {
            idleT += Time.unscaledDeltaTime;
            var p = targetLocalPos;
            p.y += Mathf.Sin(idleT * idleBobHz * Mathf.PI * 2f) * idleBobAmp;
            island.localPosition = p;
            if (idleSpinSpeed != 0f)
            {
                targetLocalRot = targetLocalRot * Quaternion.Euler(0f, idleSpinSpeed * Time.unscaledDeltaTime, 0f);
                island.localRotation = targetLocalRot;
            }
        }
    }

    // ── 공개 API ─────────────────────────────────────────────────────────
    /// <summary>호버/선택 변경: dir=+1(오른쪽으로 이동=Next), -1(왼쪽=Previous).</summary>
    public void Show(int index, int dir)
    {
        if (confirming) return;
        if (index == currentIndex) { pendingIndex = -1; return; }   // 현 섬으로 복귀 = 보류 요청 취소(리뷰 지적)
        if (pendingIndex != index) pendingSince = Time.unscaledTime;
        pendingIndex = index;
        pendingDir = dir == 0 ? 1 : (int)Mathf.Sign(dir);
    }

    /// <summary>선택 화면 이탈 — 현재 섬 퇴장.</summary>
    public void HideAll()
    {
        pendingIndex = -1;
        if (confirming) return;
        if (island != null && !transitioning) StartCoroutine(ExitOnly(1));
    }

    /// <summary>로비 '맵 클릭'용 — 선택 확정 펄스(섬 유지).</summary>
    public void Pulse()
    {
        if (island != null && !transitioning && !confirming) StartCoroutine(PulseCo());
    }

    /// <summary>클릭 확정: 펄스 확대 후 아래로 꺼짐 → onDone(기존 로드 로직). 섬 없으면 즉시 onDone.</summary>
    public void Confirm(System.Action onDone)
    {
        if (confirming) return;
        if (island == null) { onDone?.Invoke(); return; }
        StartCoroutine(ConfirmCo(onDone));
    }

    // ── 연출 코루틴 ──────────────────────────────────────────────────────
    IEnumerator Transition(int index, int dir)
    {
        transitioning = true;
        if (island != null) yield return ExitCo(island, dir);
        island = null;
        currentIndex = index;

        if (miniatures != null)
        {
            var clone = miniatures.CloneIsland(index, holder);
            if (clone != null)
            {
                island = clone;
                targetLocalPos = index < previewOffset.Length ? previewOffset[index] : Vector3.zero;
                targetLocalRot = Quaternion.Euler(index < previewEuler.Length ? previewEuler[index] : Vector3.zero);
                float ps = (index < previewScale.Length && previewScale[index] > 0.01f) ? previewScale[index] : 1f;
                targetScale = baseScale * ps;
                yield return EnterCo(island, dir);
                idleT = 0f;
            }
        }
        transitioning = false;
    }

    IEnumerator EnterCo(Transform t, int dir)
    {
        // Next(+1)면 오른쪽+아래에서, Previous(-1)면 왼쪽+아래에서 등장
        Vector3 from = targetLocalPos + new Vector3(dir * slideDistance, -dropDistance, 0f);
        Quaternion fromRot = targetLocalRot * Quaternion.Euler(0f, 0f, -dir * tiltAngle);
        for (float x = 0f; x < 1f; x += Time.unscaledDeltaTime / Mathf.Max(enterTime, 0.01f))
        {
            if (t == null) yield break;
            float u = 1f - (1f - x) * (1f - x) * (1f - x);   // ease-out cubic
            t.localPosition = Vector3.LerpUnclamped(from, targetLocalPos, u);
            t.localRotation = Quaternion.SlerpUnclamped(fromRot, targetLocalRot, u);
            t.localScale = Vector3.one * Mathf.LerpUnclamped(targetScale * enterStartScale, targetScale, u);
            yield return null;
        }
        t.localPosition = targetLocalPos;
        t.localRotation = targetLocalRot;
        t.localScale = Vector3.one * targetScale;
    }

    IEnumerator ExitCo(Transform t, int dir)
    {
        // 현재 모델은 누른 방향의 반대쪽으로 밀려나며 축소(새 모델이 dir쪽에서 오니까)
        Vector3 from = t.localPosition;
        Vector3 to = from + new Vector3(-dir * slideDistance, -dropDistance * 0.7f, 0f);
        Quaternion r0 = t.localRotation;
        Quaternion r1 = r0 * Quaternion.Euler(0f, 0f, dir * tiltAngle);
        float s0 = t.localScale.x;
        for (float x = 0f; x < 1f; x += Time.unscaledDeltaTime / Mathf.Max(exitTime, 0.01f))
        {
            if (t == null) yield break;
            float u = x * x;   // ease-in
            t.localPosition = Vector3.LerpUnclamped(from, to, u);
            t.localRotation = Quaternion.SlerpUnclamped(r0, r1, u);
            t.localScale = Vector3.one * Mathf.LerpUnclamped(s0, s0 * 0.55f, u);
            yield return null;
        }
        Destroy(t.gameObject);
    }

    IEnumerator ExitOnly(int dir)
    {
        transitioning = true;
        var t = island;
        island = null;
        currentIndex = -1;
        if (t != null) yield return ExitCo(t, dir);
        transitioning = false;
    }

    IEnumerator PulseCo()
    {
        var t = island;
        float s = targetScale;
        for (float x = 0f; x < 1f; x += Time.unscaledDeltaTime / 0.16f)
        {
            if (t == null) yield break;
            float u = Mathf.Sin(x * Mathf.PI);
            t.localScale = Vector3.one * (s * (1f + 0.09f * u));
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one * s;
    }

    IEnumerator ConfirmCo(System.Action onDone)
    {
        confirming = true;
        var t = island;
        island = null;
        currentIndex = -1;
        float s = t.localScale.x;
        Vector3 p0 = t.localPosition;
        float half = confirmTime * 0.4f;
        for (float x = 0f; x < 1f; x += Time.unscaledDeltaTime / Mathf.Max(half, 0.01f))
        {
            if (t == null) break;
            t.localScale = Vector3.one * Mathf.Lerp(s, s * confirmPulse, 1f - (1f - x) * (1f - x));
            yield return null;
        }
        for (float x = 0f; x < 1f; x += Time.unscaledDeltaTime / Mathf.Max(confirmTime - half, 0.01f))
        {
            if (t == null) break;
            float u = x * x;
            t.localPosition = p0 + Vector3.down * (dropDistance * 1.2f * u);
            t.localScale = Vector3.one * Mathf.Lerp(s * confirmPulse, s * 0.4f, u);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
        confirming = false;
        onDone?.Invoke();
    }
}
