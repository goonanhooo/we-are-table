using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-09-05 우주 맵 결말 — 달 착륙(흡수). ★2026-09-06 v2: '실제 중력'처럼.
/// 씬의 큰 달(`Moon`) 중심에서 captureRadius 안에 테이블이 들어오면:
/// 입력 잠금(SquidMode.thrustLocked)·CameraFollow 해제 → 테이블이 역제곱 중력(a = gravityMu / r²)으로 달을 향해 점점 빨라지며 낙하(TeleportRoot 적분, 천천히 회전)
/// → 카메라는 제자리에 거의 남아(camFollow 비율만 따라감) 테이블이 달 앞으로 점점 작아지는 것을 지켜봄
/// → 표면(moonRadius×surfaceFactor)에 닿거나 maxPullSeconds가 지나면 페이드 아웃 → nextScene(기본 Moon) 로드.
/// 멀티: 호스트 판정 → NetStageSync가 게스트 PlayRemote()(시각만 — 테이블 위치는 NetworkTransform으로 옴). 게스트도 페이드 인 예약.
/// </summary>
public class MoonCapture : MonoBehaviour
{
    [Tooltip("달 트랜스폼(중심)")] public Transform moon;
    [Tooltip("달 반지름(월드)")] public float moonRadius = 130f;
    [Tooltip("이 거리(중심 기준) 안에 들어오면 흡수 시작")] public float captureRadius = 320f;
    [Header("중력 낙하")]
    [Tooltip("중력 상수: 가속도 = gravityMu / r² (r=달 중심 거리). 540000이면 320m에서 5.3m/s², 표면 근처 27m/s²")] public float gravityMu = 540000f;
    [Tooltip("진입 시 진행 방향과 다른(접선) 속도 성분을 이 비율/초로 감쇠 — 달을 스치지 않고 곧장 빨려 들어가게")] public float tangentialDamping = 0.6f;
    [Tooltip("낙하 중 천천히 회전(도/초)")] public Vector3 tumbleDegPerSec = new Vector3(12f, 25f, 8f);
    [Tooltip("이 반경(달 반지름 ×)에 닿으면 페이드 시작")] public float surfaceFactor = 1.12f;
    [Tooltip("안전 상한 — 이 시간이 지나면 강제 페이드")] public float maxPullSeconds = 10f;
    [Header("카메라")]
    [Tooltip("카메라가 테이블 이동을 따라가는 비율(0=제자리, 1=완전 추적) — 작을수록 테이블이 멀어지며 작아짐")] public float camFollowRatio = 0.15f;
    [Tooltip("시선 목표: 테이블 위치를 달 방향으로 이만큼 앞선 점(0=테이블 정확히)")] public float lookAhead = 6f;
    public float fadeSeconds = 1.0f;
    public string nextScene = "Moon";
    [HideInInspector] public bool netRemoteDriven;
    public bool Started => started;

    bool started, remote;
    ArticulationBody tableRoot; Transform cam; CameraFollow camFollow;

    void Update()
    {
        if (started || netRemoteDriven || moon == null) return;
        if (tableRoot == null)
        {
            var asr = Object.FindAnyObjectByType<ArtSceneReset>();
            if (asr != null) foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { tableRoot = ab; break; }
            if (tableRoot == null) return;
        }
        if ((tableRoot.transform.position - moon.position).sqrMagnitude < captureRadius * captureRadius) Play();
    }

    public void Play() { if (started) return; CheckpointRuntime.Milestone(12); started = true; remote = false; StartCoroutine(Run()); }
    public void PlayRemote() { if (started) return; started = true; remote = true; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        var asr = Object.FindAnyObjectByType<ArtSceneReset>(FindObjectsInactive.Include);
        if (asr != null && tableRoot == null) foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { tableRoot = ab; break; }
        var squid = Object.FindAnyObjectByType<SquidMode>();
        if (squid != null) squid.thrustLocked = true;
        var mainCam = Camera.main;
        if (mainCam != null) { cam = mainCam.transform; camFollow = mainCam.GetComponent<CameraFollow>(); if (camFollow != null) camFollow.enabled = false; }

        Vector3 p = tableRoot != null ? tableRoot.transform.position : (cam != null ? cam.position : transform.position);
        Vector3 p0 = p;
        Quaternion rot = tableRoot != null ? tableRoot.transform.rotation : Quaternion.identity;
        Vector3 v = (!remote && tableRoot != null) ? tableRoot.linearVelocity : Vector3.zero;
        if (!remote && tableRoot != null) tableRoot.immovable = true;
        Vector3 camStart = cam != null ? cam.position : p0;
        bool fading = false; float fadeAt = -1f;
        for (float t = 0f; ; t += Time.deltaTime)
        {
            float dt = Time.deltaTime;
            if (!remote)
            {
                // 역제곱 중력 적분: 달로 갈수록 점점 빨라짐. 접선 성분은 감쇠해 스치지 않고 빨려 들어감.
                Vector3 toMoon = moon.position - p; float r = Mathf.Max(toMoon.magnitude, 1f); Vector3 dir = toMoon / r;
                Vector3 a = dir * (gravityMu / (r * r));
                v += a * dt;
                Vector3 vRadial = Vector3.Project(v, dir); Vector3 vTan = v - vRadial;
                v = vRadial + vTan * Mathf.Max(0f, 1f - tangentialDamping * dt);
                p += v * dt;
                rot = rot * Quaternion.Euler(tumbleDegPerSec * dt);
                if (tableRoot != null) tableRoot.TeleportRoot(p, rot);
            }
            else if (tableRoot != null) p = tableRoot.transform.position;   // 게스트: 호스트 위치(NetworkTransform)

            if (cam != null)
            {
                // 카메라는 제자리에 거의 남아 테이블이 달 앞으로 점점 작아지는 것을 본다
                Vector3 camP = camStart + (p - p0) * camFollowRatio;
                cam.position = camP;
                Vector3 look = p + (moon.position - p).normalized * lookAhead;
                cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation((look - camP).normalized, Vector3.up), dt * 5f);
            }

            float dist = (moon.position - p).magnitude;
            if (!fading && (dist < moonRadius * surfaceFactor || t > maxPullSeconds))
            {
                fading = true; fadeAt = t; ScreenFader.FadeOut(fadeSeconds);
                if (StageTimer.Instance != null) StageTimer.Instance.Clear();
            }
            if (fading && t - fadeAt >= fadeSeconds) break;
            yield return null;
        }
        // ★게스트도 페이드 인 예약(호스트가 씬을 옮기면 게스트 화면도 다시 밝아져야 함) — 로드는 호스트만
        if (!string.IsNullOrEmpty(nextScene)) ScreenFader.FadeInAfterNextLoad(0.4f, 1.5f);
        if (remote) yield break;
        if (!string.IsNullOrEmpty(nextScene)) NetScene.Load(nextScene);
    }

    void OnDrawGizmosSelected()
    {
        if (moon == null) return;
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.6f); Gizmos.DrawWireSphere(moon.position, captureRadius);
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.6f); Gizmos.DrawWireSphere(moon.position, moonRadius * surfaceFactor);
    }
}
