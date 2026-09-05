using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-09-06 광차 동굴(IceCavern) 정거장 도착 컷씬. 씬 GO "CavernChairCutscene"에 부착, 참조는 전부 씬 오브젝트.
/// 테이블이 60° 오르막을 다 올라 정거장 윗면(station 구역)에 '정착'하면(도착 즉시 X — settleSpeed 아래로 settleTime) 1회:
///  ① 테이블 시점(테이블 위 camUp) 카메라로 블렌드 → 저 멀리 선로 위 `chairCart`(광차 비주얼 + 옆에 테니스공으로 붙은 의자 + 램프)가
///     trackA(startU부터)→trackB를 따라 cartSpeed로 올라감 → watchSeconds 동안 FOV fovStart→fovEnd로 서서히 당겨 봄
///  ② CameraFollow.BlendFrom 복귀. 의자 광차는 카메라가 돌아온 뒤에도 끝까지 달려가 trackB 끝에서 비활성.
/// 멀티: 호스트 판정 → NetStageSync가 게스트 PlayRemote()(테이블 동결 생략, 시각만).
/// </summary>
public class CavernChairCutscene : MonoBehaviour
{
    [Header("배우")]
    [Tooltip("광차 비주얼 + 옆에 붙은 의자(비활성 시작)")] public Transform chairCart;
    public RailTrack trackA, trackB;
    [Tooltip("레일 윗면에서 광차 피벗까지(MineCart.floorHeight와 동일)")] public float cartFloorHeight = 0.9f;

    [Header("발동 구역(정거장 윗면)")]
    public Transform station;
    public float stationHalfX = 12f;
    public float stationMinZ = -378f, stationMaxZ = -350f;
    public float stationMinY = -128.8f, stationMaxY = -124f;
    [Tooltip("이 속도 아래로 settleTime 동안 머물러야 발동(오르막 끝 자세 안정)")] public float settleSpeed = 0.8f;
    public float settleTime = 0.8f;

    [Header("연출")]
    [Tooltip("의자 광차가 trackA의 이 u에서 출발(0.2 ≈ 정거장 40m 앞)")] public float startU = 0.2f;
    public float cartSpeed = 12f;
    public float watchSeconds = 11f;
    [Tooltip("테이블 시점 카메라 높이(테이블 루트 기준)")] public float camUp = 1.3f;
    public float fovStart = 55f, fovEnd = 12f;
    public float blendIn = 0.6f;

    [HideInInspector] public bool netRemoteDriven;
    public bool Started => started;

    bool started, remote; float settle;
    ArticulationBody tableRoot; Transform cam; Camera camC; CameraFollow camFollow;
    Vector3 lastPos; bool cartRunning;

    void Start()
    {
        if (chairCart != null) chairCart.gameObject.SetActive(false);
        if (trackA != null) trackA.Sample();
        if (trackB != null) trackB.Sample();
    }

    void Update()
    {
        if (started || netRemoteDriven) return;
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
        bool inZone = p.y >= stationMinY && p.y <= stationMaxY && p.z >= stationMinZ && p.z <= stationMaxZ && Mathf.Abs(p.x - (station != null ? station.position.x : 0f)) <= stationHalfX;
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
        if (!remote && tableRoot != null) tableRoot.immovable = true;
        var mainCam = Camera.main;
        if (mainCam != null) { cam = mainCam.transform; camC = mainCam; camFollow = mainCam.GetComponent<CameraFollow>(); if (camFollow != null) camFollow.enabled = false; }
        float baseFov = camC != null ? camC.fieldOfView : 60f;

        // 의자 광차 출발(카메라와 독립 — 끝까지 달려감)
        if (chairCart != null && trackA != null) { chairCart.gameObject.SetActive(true); StartCoroutine(RunCart()); }

        Vector3 camA = cam != null ? cam.position : Vector3.zero; Quaternion camRA = cam != null ? cam.rotation : Quaternion.identity;
        for (float t = 0f; t < watchSeconds; t += Time.deltaTime)
        {
            if (cam != null)
            {
                Vector3 tp = tableRoot != null ? tableRoot.transform.position : cam.position;
                Vector3 eye = tp + Vector3.up * camUp;
                Vector3 look = chairCart != null && chairCart.gameObject.activeSelf ? chairCart.position + Vector3.up * 0.8f : eye + Vector3.forward;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / blendIn));
                cam.position = Vector3.Lerp(camA, eye, u);
                cam.rotation = Quaternion.Slerp(camRA, Quaternion.LookRotation((look - cam.position).normalized, Vector3.up), u);
                if (camC != null) camC.fieldOfView = Mathf.Lerp(fovStart, fovEnd, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / watchSeconds)));
            }
            yield return null;
        }
        if (camC != null) camC.fieldOfView = baseFov;
        if (camFollow != null) { camFollow.BlendFrom(1.2f); camFollow.enabled = true; }
        if (!remote && tableRoot != null) tableRoot.immovable = false;
    }

    /// <summary>의자 광차: trackA(startU→1) → trackB(0→1) 등속 주행, 끝나면 비활성.</summary>
    IEnumerator RunCart()
    {
        if (cartRunning) yield break; cartRunning = true;
        float lenA = trackA.Length, lenB = trackB != null ? trackB.Length : 0f;
        float dist = startU * lenA;
        while (true)
        {
            dist += cartSpeed * Time.deltaTime;
            Vector3 pos, tan;
            if (dist < lenA) { float u = dist / lenA; pos = trackA.PosAt(u); tan = trackA.TangentAt(u); }
            else if (trackB != null && dist - lenA < lenB) { float u = (dist - lenA) / lenB; pos = trackB.PosAt(u); tan = trackB.TangentAt(u); }
            else break;
            chairCart.position = pos + Vector3.up * cartFloorHeight;
            if (tan.sqrMagnitude > 1e-6f) chairCart.rotation = Quaternion.LookRotation(tan.normalized, Vector3.up);
            yield return null;
        }
        chairCart.gameObject.SetActive(false);
        cartRunning = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
        float cx = station != null ? station.position.x : 0f;
        Gizmos.DrawWireCube(new Vector3(cx, (stationMinY + stationMaxY) * 0.5f, (stationMinZ + stationMaxZ) * 0.5f), new Vector3(stationHalfX * 2f, stationMaxY - stationMinY, stationMaxZ - stationMinZ));
        if (trackA != null) { trackA.Sample(); Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(trackA.PosAt(startU), 1.5f); }
    }
}
