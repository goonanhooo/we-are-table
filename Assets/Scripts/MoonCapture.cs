using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-09-05 우주 맵 결말 — 달 착륙(흡수). 씬의 큰 달(`Moon`) 중심에서 captureRadius 안에 테이블이 들어오면:
/// 입력 잠금(SquidMode.thrustLocked) → 테이블이 달 쪽으로 빨려 들어가며 가속(TeleportRoot), 카메라는 뒤에서 따라가며 달이 화면을 채움
/// → 페이드 아웃 → nextScene(기본 Moon) 로드. StageTimer가 있으면 Clear.
/// 멀티: 호스트 판정 → NetStageSync가 게스트 PlayRemote()(시각만).
/// </summary>
public class MoonCapture : MonoBehaviour
{
    [Tooltip("달 트랜스폼(중심)")] public Transform moon;
    [Tooltip("달 반지름(월드)")] public float moonRadius = 130f;
    [Tooltip("이 거리(중심 기준) 안에 들어오면 흡수 시작")] public float captureRadius = 200f;
    public float pullSeconds = 3.2f;
    public float fadeSeconds = 1.2f;
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

    public void Play() { if (started) return; started = true; remote = false; StartCoroutine(Run()); }
    public void PlayRemote() { if (started) return; started = true; remote = true; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        var asr = Object.FindAnyObjectByType<ArtSceneReset>(FindObjectsInactive.Include);
        if (asr != null && tableRoot == null) foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { tableRoot = ab; break; }
        var squid = Object.FindAnyObjectByType<SquidMode>();
        if (squid != null) squid.thrustLocked = true;
        if (!remote && tableRoot != null) tableRoot.immovable = true;
        var mainCam = Camera.main;
        if (mainCam != null) { cam = mainCam.transform; camFollow = mainCam.GetComponent<CameraFollow>(); if (camFollow != null) camFollow.enabled = false; }
        Vector3 p0 = tableRoot != null ? tableRoot.transform.position : (cam != null ? cam.position : transform.position);
        Quaternion r0 = tableRoot != null ? tableRoot.transform.rotation : Quaternion.identity;
        Vector3 dir = (moon.position - p0).normalized;
        Vector3 p1 = moon.position - dir * (moonRadius * 0.55f);   // 표면 안쪽까지(화면이 달로 꽉 참)
        Vector3 camStart = cam != null ? cam.position : p0;
        bool fading = false;
        for (float t = 0f; t < pullSeconds; t += Time.deltaTime)
        {
            float u = t / pullSeconds;
            float s = u * u * (3f - 2f * u); s = s * s;                     // 점점 빨라짐
            Vector3 p = Vector3.Lerp(p0, p1, s);
            if (!remote && tableRoot != null) tableRoot.TeleportRoot(p, r0 * Quaternion.Euler(0f, 0f, u * 140f));
            if (cam != null)
            {
                // 뒤에서 따라가되 테이블보다 느리게 → 테이블이 달로 멀어지며 달이 화면을 채움
                Vector3 camP = Vector3.Lerp(camStart, p0 + dir * (Vector3.Distance(p0, p1) * 0.45f) + Vector3.up * 3f, s * 0.9f);
                cam.position = camP;
                cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation((moon.position - camP).normalized, Vector3.up), Time.deltaTime * 4f);
            }
            if (!fading && t > pullSeconds - fadeSeconds)
            {
                fading = true; ScreenFader.FadeOut(fadeSeconds);
                if (StageTimer.Instance != null) StageTimer.Instance.Clear();   // CLEAR 패널은 페이드 뒤에(연출 안 가리게)
            }
            yield return null;
        }
        if (remote) yield break;
        if (!string.IsNullOrEmpty(nextScene)) { ScreenFader.FadeInAfterNextLoad(0.4f, 1.5f); NetScene.Load(nextScene); }
    }

    void OnDrawGizmosSelected()
    {
        if (moon == null) return;
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.6f); Gizmos.DrawWireSphere(moon.position, captureRadius);
    }
}
