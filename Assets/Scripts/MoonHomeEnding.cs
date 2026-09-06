using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-09-06 달 맵 결말 — 이 게임의 마지막(사용자 요청). 씬 GO "MoonHomeEnding"(참조는 전부 씬 오브젝트 — House는 Tools/Moon/Build Home으로 생성).
/// 달 끝의 소박한 집, 안은 고즈넉한 부엌. 식탁 자리(tablePlace) 옆에 의자(chair)가 기다린다.
/// 테이블이 식탁 자리 반경(triggerRadius)에 들어와 정착(속도<settleSpeed가 settleTime)하면 1회:
///  ① 테이블 동결 → 식탁 자리로 스르르(moveSeconds, TeleportRoot 보간 — 관절은 드라이브가 스스로 곧게)
///  ② 기다리던 의자가 chairWait→chairSeat(테이블 옆, 상판 아래로 살짝)로 밀려 들어감(chairSeconds) — 의자와 테이블이 한 쌍
///  ③ 카메라: 추적 시점에서 camFinal(부엌 와이드 구도)로 blendSeconds 동안 이동, FOV finalFov
///  ④ 램프 밝아짐(lampFrom→lampTo)·주전자 김(steam 방출 ↑) ⑤ captionDelay 뒤 손글씨 자막 "끝" ⑥ clearDelay 뒤 StageTimer.Clear()(기록)+Progress 클리어.
///  테이블은 식탁 자리에 그대로(동결 유지), 카메라도 부엌 구도 유지 — ESC 메뉴로 나감.
/// 멀티: 호스트 판정 → NetStageSync.moonEndingStarted → 게스트 PlayRemote(동결/이동 생략 — 테이블 위치는 NetworkTransform으로 옴; 의자·카메라·램프·자막은 로컬 재생, CLEAR는 stageCleared로).
/// </summary>
public class MoonHomeEnding : MonoBehaviour
{
    [Header("배우/마커(씬 오브젝트)")]
    [Tooltip("식탁 자리(바닥 높이, 회전=테이블 정면)")] public Transform tablePlace;
    [Tooltip("기다리는 의자(홀웨이 Chair 복사본)")] public Transform chair;
    public Transform chairWait, chairSeat;
    [Tooltip("최종 부엌 구도 카메라 마커(위치+회전)")] public Transform camFinal;
    public Light lamp; public float lampFrom = 1.2f, lampTo = 2.4f;
    public ParticleSystem steam; public float steamRateFinal = 16f;

    [Header("발동")]
    public float triggerRadius = 2.4f;
    [Tooltip("이 속도 아래로 settleTime 머물러야(자세 안정 후)")] public float settleSpeed = 0.7f;
    public float settleTime = 0.6f;

    [Header("연출")]
    public float moveSeconds = 1.6f;
    public float chairSeconds = 1.4f;
    public float blendSeconds = 3.0f;
    public float finalFov = 40f;
    [Tooltip("발동 후 자막까지")] public float captionDelay = 4.5f;
    public float captionFade = 1.5f;
    [Tooltip("발동 후 CLEAR(기록)까지")] public float clearDelay = 9f;

    [HideInInspector] public bool netRemoteDriven;
    public bool Started => started;

    bool started, remote; float settle;
    ArticulationBody tableRoot; Vector3 lastPos;
    Transform cam; Camera camC; CameraFollow camFollow;
    string caption = ""; float captionAlpha; GUIStyle capStyle, subStyle;

    void Start()
    {
        if (chair != null && chairWait != null) chair.SetPositionAndRotation(chairWait.position, chairWait.rotation);
        if (lamp != null) lamp.intensity = lampFrom;
    }

    void Update()
    {
        if (started || netRemoteDriven || tablePlace == null) return;
        if (tableRoot == null)
        {
            var asr = Object.FindAnyObjectByType<ArtSceneReset>();
            if (asr == null) return;
            foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { tableRoot = ab; break; }
            if (tableRoot == null) return;
            lastPos = tableRoot.transform.position;
        }
        var p = tableRoot.transform.position;
        float speed = (p - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f); lastPos = p;
        Vector3 d = p - tablePlace.position; float dy = d.y; d.y = 0f;
        bool inZone = d.sqrMagnitude <= triggerRadius * triggerRadius && Mathf.Abs(dy) < 1.5f;
        settle = inZone && speed < settleSpeed ? settle + Time.deltaTime : 0f;
        if (settle >= settleTime) Play();
    }

    public void Play() { if (started) return; started = true; remote = false; StartCoroutine(Run()); }
    public void PlayRemote() { if (started) return; started = true; remote = true; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        if (tableRoot == null)
        {
            var asr = Object.FindAnyObjectByType<ArtSceneReset>(FindObjectsInactive.Include);
            if (asr != null) foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { tableRoot = ab; break; }
        }
        if (!remote && tableRoot != null) { tableRoot.immovable = true; tableRoot.linearVelocity = Vector3.zero; tableRoot.angularVelocity = Vector3.zero; }
        var mainCam = Camera.main;
        if (mainCam != null) { cam = mainCam.transform; camC = mainCam; camFollow = mainCam.GetComponent<CameraFollow>(); if (camFollow != null) camFollow.enabled = false; }
        StageTimer.Suppress = true;
        float t0 = Time.time;

        // ① 테이블 식탁 자리로 + ③ 카메라 부엌 구도로(동시)
        Vector3 p0 = tableRoot != null ? tableRoot.transform.position : Vector3.zero; Quaternion r0 = tableRoot != null ? tableRoot.transform.rotation : Quaternion.identity;
        Vector3 p1 = tablePlace.position; Quaternion r1 = tablePlace.rotation;
        Vector3 camA = cam != null ? cam.position : p1; Quaternion camRA = cam != null ? cam.rotation : Quaternion.identity; float fovA = camC != null ? camC.fieldOfView : 60f;
        float total = Mathf.Max(moveSeconds, blendSeconds);
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            if (!remote && tableRoot != null && t <= moveSeconds + Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / moveSeconds));
                tableRoot.TeleportRoot(Vector3.Lerp(p0, p1, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.12f, Quaternion.Slerp(r0, r1, u));
            }
            CamTo(camA, camRA, fovA, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / blendSeconds)));
            yield return null;
        }
        if (!remote && tableRoot != null) tableRoot.TeleportRoot(p1, r1);
        CamTo(camA, camRA, fovA, 1f);

        // ② 의자가 식탁으로 스르르
        if (chair != null && chairSeat != null)
        {
            Vector3 c0 = chair.position; Quaternion cr0 = chair.rotation;
            for (float t = 0f; t < chairSeconds; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / chairSeconds);
                chair.SetPositionAndRotation(Vector3.Lerp(c0, chairSeat.position, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.05f, Quaternion.Slerp(cr0, chairSeat.rotation, u));
                yield return null;
            }
            chair.SetPositionAndRotation(chairSeat.position, chairSeat.rotation);
        }

        // ④ 램프 밝아짐 + 김
        if (steam != null) { var em = steam.emission; em.rateOverTime = steamRateFinal; if (!steam.isPlaying) steam.Play(); }
        for (float t = 0f; t < 1.6f; t += Time.deltaTime) { if (lamp != null) lamp.intensity = Mathf.Lerp(lampFrom, lampTo, Mathf.SmoothStep(0f, 1f, t / 1.6f)); yield return null; }
        if (lamp != null) lamp.intensity = lampTo;

        // ⑤ 자막
        while (Time.time - t0 < captionDelay) yield return null;
        caption = L.T("끝", "the end");
        for (float t = 0f; t < captionFade; t += Time.deltaTime) { captionAlpha = t / captionFade; yield return null; }
        captionAlpha = 1f;

        // ⑥ 클리어(기록)
        while (Time.time - t0 < clearDelay) yield return null;
        StageTimer.Suppress = false; StageTimer.FooterOverride = L.T("ESC = 메뉴", "ESC = menu");
        if (!remote)
        {
            if (StageTimer.Instance != null) StageTimer.Instance.Clear();
            Progress.MarkCleared("Moon");
        }
    }

    void CamTo(Vector3 camA, Quaternion camRA, float fovA, float u)
    {
        if (cam == null || camFinal == null) return;
        cam.position = Vector3.Lerp(camA, camFinal.position, u);
        cam.rotation = Quaternion.Slerp(camRA, camFinal.rotation, u);
        if (camC != null) camC.fieldOfView = Mathf.Lerp(fovA, finalFov, u);
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(caption) || captionAlpha <= 0f) return;
        if (capStyle == null) { capStyle = HandUI.Style(64, HandUI.Ink); subStyle = HandUI.Style(22, HandUI.InkSoft); }
        float w = 360f, h = 150f;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.70f, w, h);
        var prev = GUI.color; GUI.color = new Color(1f, 1f, 1f, captionAlpha);
        HandUI.Paper(r, HandUI.BoxWide, 0.94f);
        GUI.Label(new Rect(r.x, r.y + 18f, w, 80f), caption, capStyle);
        GUI.Label(new Rect(r.x, r.y + 100f, w, 30f), "we are table", subStyle);
        GUI.color = prev;
    }

    void OnDrawGizmosSelected()
    {
        if (tablePlace != null) { Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f); Gizmos.DrawWireSphere(tablePlace.position, triggerRadius); }
        if (camFinal != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(camFinal.position, 0.3f); Gizmos.DrawRay(camFinal.position, camFinal.forward * 3f); }
    }
}
