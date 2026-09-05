using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-09-05 우주 맵 도입 컷씬. 씬 GO `SpaceIntro`에 부착, 참조는 전부 인스펙터(씬 오브젝트).
///  ① 추격: 테이블이 탄 로켓(tableShip)이 의자가 탄 우주선(chairShip)을 뒤쫓아 pathStart→pathBreak로 날아감(카메라 = tableShip 자식 camChase에서 의자선을 봄)
///  ② 파괴: pathBreak에서 섬광+파편(debris/debrisSparks) — 로켓 모델 꺼지고 테이블이 그 자리에 나타나 천천히 구르며 표류(무중력, 추진 잠금)
///     의자선은 chairEscape로 빠르게 멀어져 사라짐
///  ③ 우주오징어(squid, SquidDummy 더미)가 squidFrom→squidStop으로 헤엄쳐 와 말풍선 2줄("내 힘을 줄게!" / "얼른 쫓아가!")
///  ④ 힘 구슬(orb)이 오징어→테이블로 날아가 테이블이 보랏빛으로 빛남 → SquidMode.thrustLocked 해제(우주 발사 능력) + 손글씨 조작 힌트
///  ⑤ 오징어가 squidExit로 사라지고 카메라 복귀(CameraFollow.BlendFrom)
/// 로켓 모델은 노즈가 로컬 +Z(Red White Rocket.fbx는 회전 (270,0,0)일 때 위를 향함 = identity면 +Z). 멀티: 호스트 → NetStageSync가 게스트 PlayRemote()(테이블 조작 생략).
/// </summary>
public class SpaceIntroCutscene : MonoBehaviour
{
    [Header("배우")]
    public Transform tableShip;            // 테이블이 탄 로켓(비주얼)
    public Transform chairShip;            // 의자가 탄 우주선
    public Transform squid;                // SquidDummy 루트(비활성 시작)
    public Transform squidHead;            // 말풍선 앵커
    public Transform orb;                  // 힘 구슬(비활성 시작)
    public ParticleSystem debris, debrisSparks;
    public Light flashLight;

    [Header("경로/마커")]
    public Transform pathStart, pathBreak, chairEscape;
    public Transform camChase;             // tableShip 자식(추격 카메라 위치)
    public Transform camSquid, camSquidLook;
    public Transform squidFrom, squidStop, squidExit;

    [Header("타이밍")]
    public float chaseSeconds = 5.5f;
    public float chairLead = 16f, chairLeadEnd = 34f;
    public float driftSeconds = 2.2f;
    public float chairEscapeSeconds = 5f;
    public float squidArriveSeconds = 2.6f;
    public float lineSeconds = 2.2f;
    public float orbSeconds = 1.4f;
    public float glowSeconds = 1.8f;
    public float squidLeaveSeconds = 2.6f;
    public float hintSeconds = 6f;
    public Color glowColor = new Color(0.7f, 0.4f, 1.5f);

    [HideInInspector] public bool netRemoteDriven;
    public bool Started => started;

    bool started, remote;
    ArticulationBody tableRoot; Renderer[] tableRenderers;
    Transform cam; CameraFollow camFollow;
    string bubbleText = ""; string hintText = ""; float flashAlpha;
    GUIStyle bubbleStyle, hintStyle;
    Vector3 shipDir = Vector3.forward;

    void Start()
    {
        if (squid != null) squid.gameObject.SetActive(false);
        if (orb != null) orb.gameObject.SetActive(false);
        if (debris != null) debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (debrisSparks != null) debrisSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (flashLight != null) flashLight.enabled = false;
        if (pathStart != null && pathBreak != null) shipDir = (pathBreak.position - pathStart.position).normalized;
        if (tableShip != null && pathStart != null) { tableShip.position = pathStart.position; tableShip.rotation = Quaternion.LookRotation(shipDir); }
        if (chairShip != null && pathStart != null) { chairShip.position = pathStart.position + shipDir * chairLead; chairShip.rotation = Quaternion.LookRotation(shipDir); }
        if (!netRemoteDriven) Play();
    }

    public void Play() { if (started) return; started = true; remote = false; StartCoroutine(Run()); }
    public void PlayRemote() { if (started) return; started = true; remote = true; StartCoroutine(Run()); }

    void Update()
    {
        if (flashAlpha > 0f) flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.deltaTime * 2.5f);
        if (flashLight != null && flashLight.enabled) flashLight.intensity = Mathf.MoveTowards(flashLight.intensity, 0f, Time.deltaTime * 40f);
    }

    IEnumerator Run()
    {
        var asr = Object.FindAnyObjectByType<ArtSceneReset>(FindObjectsInactive.Include);
        if (asr != null)
        {
            foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { tableRoot = ab; break; }
            tableRenderers = asr.transform.root.GetComponentsInChildren<Renderer>(true);
        }
        var squidMode = Object.FindAnyObjectByType<SquidMode>();
        if (squidMode != null) squidMode.thrustLocked = true;
        if (tableRenderers != null) foreach (var r in tableRenderers) if (r != null) r.enabled = false;
        if (!remote && tableRoot != null) { tableRoot.immovable = true; }
        var mainCam = Camera.main;
        if (mainCam != null) { cam = mainCam.transform; camFollow = mainCam.GetComponent<CameraFollow>(); if (camFollow != null) camFollow.enabled = false; }

        // ① 추격
        Vector3 a = pathStart != null ? pathStart.position : Vector3.zero, b = pathBreak != null ? pathBreak.position : a + Vector3.forward * 100f;
        for (float t = 0f; t < chaseSeconds; t += Time.deltaTime)
        {
            float u = t / chaseSeconds;
            Vector3 sway = Vector3.right * Mathf.Sin(t * 1.3f) * 1.4f + Vector3.up * Mathf.Sin(t * 0.9f + 1f) * 0.9f;
            Vector3 p = Vector3.Lerp(a, b, u) + sway;
            if (tableShip != null) { tableShip.position = p; tableShip.rotation = Quaternion.LookRotation(shipDir + Vector3.right * Mathf.Cos(t * 1.3f) * 0.04f); }
            float lead = Mathf.Lerp(chairLead, chairLeadEnd, u);
            Vector3 sway2 = Vector3.right * Mathf.Sin(t * 1.1f + 2f) * 2.2f + Vector3.up * Mathf.Cos(t * 0.8f) * 1.2f;
            if (chairShip != null) { chairShip.position = Vector3.Lerp(a, b, u) + shipDir * lead + sway2; chairShip.rotation = Quaternion.LookRotation(shipDir); }
            if (!remote && tableRoot != null) tableRoot.TeleportRoot(p, Quaternion.LookRotation(shipDir));
            if (cam != null && camChase != null)
            {
                cam.position = camChase.position;
                Vector3 look = chairShip != null ? chairShip.position : p + shipDir * 20f;
                cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(look - cam.position, Vector3.up), Time.deltaTime * 5f);
            }
            yield return null;
        }

        // ② 파괴
        Vector3 breakPos = tableShip != null ? tableShip.position : b;
        flashAlpha = 1f;
        if (flashLight != null) { flashLight.transform.position = breakPos; flashLight.enabled = true; flashLight.intensity = 60f; }
        if (debris != null) { debris.transform.position = breakPos; debris.Play(true); }
        if (debrisSparks != null) { debrisSparks.transform.position = breakPos; debrisSparks.Play(true); }
        if (tableShip != null) tableShip.gameObject.SetActive(false);
        if (tableRenderers != null) foreach (var r in tableRenderers) if (r != null) r.enabled = true;
        if (!remote && tableRoot != null)
        {
            tableRoot.TeleportRoot(breakPos, Quaternion.LookRotation(shipDir));
            tableRoot.immovable = false;
            tableRoot.linearVelocity = shipDir * 1.2f;
            tableRoot.angularVelocity = new Vector3(0.5f, 0.9f, 0.35f);
        }
        Vector3 camHold = cam != null ? cam.position : breakPos;
        StartCoroutine(ChairEscape());
        for (float t = 0f; t < driftSeconds; t += Time.deltaTime)
        {
            if (cam != null)
            {
                Vector3 target = tableRoot != null ? tableRoot.transform.position : breakPos;
                cam.position = Vector3.Lerp(cam.position, camHold, 0f);
                cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(target - cam.position, Vector3.up), Time.deltaTime * 3f);
            }
            yield return null;
        }

        // ③ 오징어 등장
        if (squid != null)
        {
            squid.gameObject.SetActive(true);
            Vector3 s0 = squidFrom != null ? squidFrom.position : breakPos + new Vector3(40f, 8f, 10f);
            Vector3 s1 = squidStop != null ? squidStop.position : breakPos + new Vector3(6f, 1.5f, 3f);
            Vector3 camA = cam != null ? cam.position : camHold; Quaternion camRA = cam != null ? cam.rotation : Quaternion.identity;
            for (float t = 0f; t < squidArriveSeconds; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / squidArriveSeconds);
                squid.position = Vector3.Lerp(s0, s1, u) + Vector3.up * Mathf.Sin(t * 2f) * 0.4f;
                Vector3 tp = tableRoot != null ? tableRoot.transform.position : breakPos;
                squid.rotation = Quaternion.Slerp(squid.rotation, Quaternion.LookRotation((tp - squid.position).normalized, Vector3.up), Time.deltaTime * 3f);
                if (cam != null && camSquid != null)
                {
                    cam.position = Vector3.Lerp(camA, camSquid.position, u);
                    Vector3 look = camSquidLook != null ? camSquidLook.position : (tp + squid.position) * 0.5f;
                    cam.rotation = Quaternion.Slerp(camRA, Quaternion.LookRotation(look - cam.position, Vector3.up), u);
                }
                yield return null;
            }
            bubbleText = L.T("내 힘을 줄게!", "I'll give you my power!");
            yield return new WaitForSeconds(lineSeconds);
            bubbleText = L.T("얼른 쫓아가!", "hurry, go after it!");
            yield return new WaitForSeconds(lineSeconds);
            bubbleText = "";
        }

        // ④ 힘 구슬 → 테이블 발광 → 능력 해제
        if (orb != null)
        {
            Vector3 o0 = squidHead != null ? squidHead.position : (squid != null ? squid.position : breakPos);
            orb.gameObject.SetActive(true);
            for (float t = 0f; t < orbSeconds; t += Time.deltaTime)
            {
                float u = t / orbSeconds;
                Vector3 o1 = tableRoot != null ? tableRoot.transform.position : breakPos;
                orb.position = Vector3.Lerp(o0, o1, Mathf.SmoothStep(0f, 1f, u)) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 1.6f;
                yield return null;
            }
            orb.gameObject.SetActive(false);
        }
        var mpb = new MaterialPropertyBlock();
        var rends = tableRenderers ?? new Renderer[0];
        for (float t = 0f; t < glowSeconds; t += Time.deltaTime)
        {
            float u = t / glowSeconds;
            float k = Mathf.Pow(Mathf.Clamp01(u / 0.2f) * Mathf.Clamp01((1f - u) / 0.3f), 0.6f);
            if (!remote && squidMode != null && u >= 0.45f && squidMode.thrustLocked) { squidMode.thrustLocked = false; squidMode.unlocked = true; squidMode.active = true; }
            foreach (var r in rends)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;
                if (m != null && !m.IsKeywordEnabled("_EMISSION")) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", glowColor * k); r.SetPropertyBlock(mpb);
            }
            yield return null;
        }
        foreach (var r in rends) if (r != null) r.SetPropertyBlock(null);
        if (squidMode != null) { squidMode.thrustLocked = false; squidMode.unlocked = true; if (!remote) squidMode.active = true; SquidMode.Unlocked = true; }
        hintText = L.T("다리 늘리기 키(E · Y · O · ])를 누르면 그 발끝에서 분사돼요 — 다리를 겨눠서 날아가요!",
                       "hold a leg's stretch key (E · Y · O · ]) to thrust from that foot — aim your legs to fly!");
        StartCoroutine(HideHint());

        // ⑤ 오징어 퇴장 + 카메라 복귀
        if (camFollow != null) { camFollow.BlendFrom(1.4f); camFollow.enabled = true; }
        if (squid != null)
        {
            Vector3 e0 = squid.position, e1 = squidExit != null ? squidExit.position : e0 + new Vector3(-60f, 25f, -40f);
            for (float t = 0f; t < squidLeaveSeconds; t += Time.deltaTime)
            {
                float u = t / squidLeaveSeconds; u = u * u;
                squid.position = Vector3.Lerp(e0, e1, u);
                squid.rotation = Quaternion.Slerp(squid.rotation, Quaternion.LookRotation((e1 - e0).normalized, Vector3.up), Time.deltaTime * 4f);
                yield return null;
            }
            squid.gameObject.SetActive(false);
        }
    }

    IEnumerator ChairEscape()
    {
        if (chairShip == null) yield break;
        Vector3 c0 = chairShip.position, c1 = chairEscape != null ? chairEscape.position : c0 + shipDir * 500f;
        for (float t = 0f; t < chairEscapeSeconds; t += Time.deltaTime)
        {
            float u = t / chairEscapeSeconds; u = u * u * u;
            chairShip.position = Vector3.Lerp(c0, c1, u) + Vector3.right * Mathf.Sin(t * 1.5f) * 2f;
            chairShip.rotation = Quaternion.LookRotation((c1 - c0).normalized);
            yield return null;
        }
        chairShip.gameObject.SetActive(false);
    }

    IEnumerator HideHint() { yield return new WaitForSeconds(hintSeconds); hintText = ""; }

    void OnGUI()
    {
        if (!started) return;
        UiKit.ApplyKoreanFont();
        if (flashAlpha > 0f) UiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 1f, 1f, flashAlpha));
        if (bubbleStyle == null)
        {
            bubbleStyle = HandUI.Style(34, HandUI.Ink, TextAnchor.MiddleCenter);
            hintStyle = HandUI.Style(24, HandUI.Ink, TextAnchor.MiddleCenter);
        }
        if (!string.IsNullOrEmpty(bubbleText) && squidHead != null && Camera.main != null)
        {
            Vector3 sp = Camera.main.WorldToScreenPoint(squidHead.position + Vector3.up * 0.8f);
            if (sp.z > 0f)
            {
                var sz = bubbleStyle.CalcSize(new GUIContent(bubbleText));
                float w = sz.x + 56f, h = sz.y + 30f;
                var r = new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h - 24f, w, h);
                r.x = Mathf.Clamp(r.x, 10f, Screen.width - w - 10f); r.y = Mathf.Clamp(r.y, 10f, Screen.height - h - 10f);
                HandUI.Paper(r, HandUI.BoxWide, 0.96f);
                GUI.Label(r, bubbleText, bubbleStyle);
            }
        }
        if (!string.IsNullOrEmpty(hintText))
        {
            var sz = hintStyle.CalcSize(new GUIContent(hintText));
            var r = new Rect((Screen.width - sz.x - 44f) * 0.5f, Screen.height - sz.y - 60f, sz.x + 44f, sz.y + 20f);
            UiKit.Fill(r, new Color(1f, 1f, 1f, 0.6f));
            GUI.Label(r, hintText, hintStyle);
            if (HandUI.Underline != null) GUI.DrawTexture(new Rect(r.center.x - 200f, r.yMax - 9f, 400f, 12f), HandUI.Underline, ScaleMode.StretchToFill);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (pathStart != null && pathBreak != null) { Gizmos.color = Color.cyan; Gizmos.DrawLine(pathStart.position, pathBreak.position); Gizmos.DrawWireSphere(pathBreak.position, 2f); }
        if (pathBreak != null && chairEscape != null) { Gizmos.color = Color.yellow; Gizmos.DrawLine(pathBreak.position, chairEscape.position); }
        if (squidFrom != null && squidStop != null) { Gizmos.color = Color.magenta; Gizmos.DrawLine(squidFrom.position, squidStop.position); if (squidExit != null) Gizmos.DrawLine(squidStop.position, squidExit.position); }
        if (camSquid != null && camSquidLook != null) { Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawLine(camSquid.position, camSquidLook.position); }
    }
}
