using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// IceMountain: 굴러다니는 눈사람 '머리'가 고정된 '몸통' 근처로 오면 발동하는 컷신.
/// 머리가 몸통 위로 올라가 조립 → 대사 2줄(말풍선) → 옆 앵글 → 파란 빛이 테이블로 들어가 번쩍 →
/// IcePower 잠금 해제(3번으로 눈사람의 힘=얼음 테이블). 기린 컷신(GiraffeCutscene) 패턴을 본뜸.
/// </summary>
public class SnowmanCutscene : MonoBehaviour
{
    [Header("참조(비우면 이름으로 찾음)")]
    public Transform body;     // SnowmanBody (고정)
    public Transform head;     // SnowmanHead (굴러다님)
    [Tooltip("머리가 조립될 월드 위치(머리 중심). 비우면 body+offset로 계산.")]
    public Vector3 assembledPos;
    public float assembledHeadOffsetY = 2.96f;   // body.y 위로 머리중심이 갈 높이

    [Header("발동 — ★2026-08-20: 머리가 '제자리(몸통 위 조립 위치)'의 가상 구에 닿으면 조립")]
    [Tooltip("머리 중심이 조립 위치(body + assembledHeadOffsetY)에서 이 거리 안이면 발동(머리 반지름 1.75 감안 후한 값)")]
    public float attachRadius = 2.0f;

    [Header("타이밍(초)")]
    public float assembleSeconds = 1.5f;
    public float talkSeconds = 2.6f;
    public float panSeconds = 1.6f;
    public float lightSeconds = 2.6f;
    public float glowSeconds = 2.6f;

    [Header("대사 말풍선(기린과 동일 텍스트 — GiraffeBubble 재활용)")]
    public Texture2D bubble1;  // "날 도와줘서 고마워.."
    public Texture2D bubble2;  // "내 힘을 줄게..."
    Texture2D currentBubble;

    [Header("음성(선택)")]
    public AudioClip voice1, voice2;
    [Tooltip("머리 얹기 성공음 — 컷씬 발동 순간 재생(VARCO Sound 생성)")]
    public AudioClip successClip;
    AudioSource voiceSrc;

    Transform table, tableTop, cam;
    Behaviour cameraFollow;
    Rigidbody headRb;
    IcePower icePower;
    bool started, finished;
    bool netGuestVisual;          // ★멀티 게스트: 머리 포즈는 호스트가 NetworkTransform으로 보냄 — 로컬에선 안 움직임
    public bool netRemoteDriven;  // ★멀티 게스트: 근접 자동발동 끄고 NetStageSync가 PlayRemote()로만 발동

    /// <summary>컷씬 시작 여부(멀티 동기화용 읽기).</summary>
    public bool Started => started;

    /// <summary>[멀티 게스트] 호스트에서 컷씬이 시작됐을 때 로컬 재생(카메라/말풍선/빛 — 머리 이동 제외).</summary>
    public void PlayRemote()
    {
        netGuestVisual = true;
        Play();
    }

    void Start()
    {
        if (body == null) { var b = GameObject.Find("SnowmanBody"); if (b) body = b.transform; }
        if (head == null) { var h = GameObject.Find("SnowmanHead"); if (h) head = h.transform; }
        // ★2026-08-17: 이 퍼즐은 테니스공(붙잡기)+기린(높이)으로 머리를 옮기는 설계 — 씬 입장 시 테니스 부여
        var tn = Object.FindAnyObjectByType<TennisMode>();
        if (tn != null) { tn.unlocked = true; tn.active = true; }
        if (head != null) headRb = head.GetComponent<Rigidbody>();
        var tgo = GameObject.Find("Table"); if (tgo) table = tgo.transform;
        var ttgo = GameObject.Find("TableTop"); if (ttgo) tableTop = ttgo.transform;
        if (Camera.main) { cam = Camera.main.transform; cameraFollow = Camera.main.GetComponent("CameraFollow") as Behaviour; }
        icePower = Object.FindAnyObjectByType<IcePower>(FindObjectsInactive.Include);
        if (icePower != null) icePower.unlocked = false;  // 컷신 전까지 잠금
        if (assembledPos == Vector3.zero && body != null)
            assembledPos = body.position + Vector3.up * assembledHeadOffsetY;
    }

    void Update()
    {
        if (started || finished || netRemoteDriven || head == null || body == null) return;
        // ★머리가 조립 위치의 가상 구와 접촉하면 성공(기린+테니스공으로 들어 올리는 풀이)
        Vector3 seat = body.position + Vector3.up * assembledHeadOffsetY;
        if (Vector3.Distance(head.position, seat) < attachRadius) Play();
    }

    public void Play()
    {
        if (started || finished || head == null) return;
        started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // 테니스공으로 머리를 붙잡은 채 발동됐을 수 있음 — 컷씬 애니메이션과 싸우지 않게 조인트 해제
        var tn = Object.FindAnyObjectByType<TennisMode>();
        if (tn != null) tn.active = false;

        PlaySuccessSfx();   // 머리 얹기 성공음(발동 순간)
        FreezeTable(true);
        if (headRb != null) { headRb.linearVelocity = Vector3.zero; headRb.angularVelocity = Vector3.zero; headRb.isKinematic = true; }
        if (cameraFollow != null) cameraFollow.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;

        // 눈사람 '얼굴 방향' 기준 정면에서 조금 멀리 — 몸통을 어느 방향으로 돌려놔도 항상 얼굴 쪽에서 찍힘.
        Vector3 camFront = body.position + body.right * 1.6f + Vector3.up * 3.2f + body.forward * 9.5f;
        Vector3 camLook = body.position + Vector3.up * 1.1f;

        // 0→1) 머리가 몸통 위로 올라가 조립 — 카메라는 플레이어 시점에서 정면 샷으로 '부드럽게' 넘어감(스냅 X)
        Vector3 cp0 = cam != null ? cam.position : camFront;
        Quaternion cr0 = cam != null ? cam.rotation : Quaternion.identity;
        Quaternion crF = Quaternion.LookRotation((camLook - camFront).normalized, Vector3.up);
        Vector3 hp0 = head.position; Quaternion hr0 = head.rotation;
        for (float t = 0; t < assembleSeconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0, 1, t / assembleSeconds);
            // 살짝 호를 그리며(위로 떴다 안착) — 게스트는 호스트 동기화가 머리를 움직임
            if (!netGuestVisual)
            {
                Vector3 p = Vector3.Lerp(hp0, assembledPos, u); p.y += Mathf.Sin(u * Mathf.PI) * 1.2f;
                head.position = p;
                head.rotation = Quaternion.Slerp(hr0, body.rotation, u);   // 얼굴을 몸통이 보는 방향으로(회전된 몸통 대응)
            }
            float cu = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / Mathf.Min(0.9f, assembleSeconds)));
            if (cam != null) { cam.position = Vector3.Lerp(cp0, camFront, cu); cam.rotation = Quaternion.Slerp(cr0, crF, cu); }
            yield return null;
        }
        if (!netGuestVisual) { head.position = assembledPos; head.rotation = body.rotation; }

        // 1.5) 안착 후 살짝 '통통' 정착 바운스(귀엽게)
        for (float t = 0; t < 0.42f; t += Time.deltaTime)
        {
            float u = t / 0.42f;
            if (!netGuestVisual)
                head.position = assembledPos + Vector3.up * (Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * 0.14f * (1f - u));
            SetCam(camFront, camLook); yield return null;
        }
        if (!netGuestVisual) head.position = assembledPos;

        // 2~3) 대사 = 눈사람 머리 옆 말풍선 + 음성. 카메라는 아주 천천히 다가감(정적인 롱테이크 방지)
        Vector3 camNear = Vector3.Lerp(camFront, camLook, 0.14f);
        float talkTotal = talkSeconds * 2f;
        currentBubble = bubble1; PlayVoice(voice1);
        bool second = false;
        for (float t = 0; t < talkTotal; t += Time.deltaTime)
        {
            if (!second && t >= talkSeconds) { second = true; currentBubble = bubble2; PlayVoice(voice2); }
            float u = Mathf.SmoothStep(0, 1, t / talkTotal);
            SetCam(Vector3.Lerp(camFront, camNear, u), camLook);
            // 첫 마디에 맞춰 머리가 아주 살짝 두 번 까딱(생동감)
            if (!netGuestVisual)
            {
                if (t < 0.9f)
                    head.position = assembledPos + Vector3.up * (Mathf.Abs(Mathf.Sin(t / 0.9f * Mathf.PI * 2f)) * 0.05f);
                else head.position = assembledPos;
            }
            yield return null;
        }
        currentBubble = null;
        Vector3 camTalkEnd = camNear;

        // 4) ★힘 전달 앵글: 눈사람↔테이블 라인의 옆(방 앞쪽)에서 둘 다 프레임에 담는 뷰로 이동
        Vector3 tablePos = (tableTop != null ? tableTop.position : table.position);
        Vector3 focusA = assembledPos;                       // 눈사람 머리
        Vector3 focusB = tablePos + Vector3.up * 0.4f;       // 테이블
        Vector3 mid = (focusA + focusB) * 0.5f;
        Vector3 line = focusB - focusA; line.y = 0f;
        float sep = Mathf.Max(line.magnitude, 2.5f);
        Vector3 side = line.sqrMagnitude > 1e-4f ? Vector3.Cross(line.normalized, Vector3.up) : body.forward;
        if (Vector3.Dot(side, body.forward) < 0f) side = -side;   // ★눈사람 '얼굴 쪽'에서 촬영(뒤통수 앵글 방지)
        Vector3 camPow = mid + side * (sep * 1.15f + 5.5f) + Vector3.up * 2.6f;
        // 동굴 방 안으로 클램프(확장된 내부 x±16 z[-22,24], 천장 아래 — 여유 두고)
        camPow.x = Mathf.Clamp(camPow.x, -14f, 14f);
        camPow.z = Mathf.Clamp(camPow.z, -20f, 22f);
        camPow.y = Mathf.Min(camPow.y, body.position.y + 8.8f);   // 방 높이가 바뀌어도 유효(절대 34 하드코딩 X)
        Vector3 camPowLook = mid + Vector3.up * 0.2f;
        yield return CamLerp(camTalkEnd, camLook, camPow, camPowLook, panSeconds * 0.7f);

        // 5) 파란 빛이 눈사람 → 테이블로, 살짝 호를 그리며 (양쪽이 다 보이는 앵글 유지)
        var light = MakeLight();
        Vector3 from = assembledPos;
        Vector3 tgt = tablePos + Vector3.up * 0.2f;
        for (float t = 0; t < lightSeconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0, 1, t / lightSeconds);
            Vector3 lp = Vector3.Lerp(from, tgt, u); lp.y += Mathf.Sin(u * Mathf.PI) * 1.1f;   // 포물선 궤적
            light.transform.position = lp;
            light.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 0.12f, u);
            SetCam(camPow, camPowLook); yield return null;
        }
        Destroy(light);
        // 테이블 몸이 빛나다 절정에서 얼음(눈사람의 힘)으로 변신 → 빛 잦아들면 얼음 형태 유지(FlashTable 내부).
        yield return FlashTable(camPow, camPowLook);
        yield return Hold(1.3f, camPow, camPowLook);

        // 7) 눈사람 힘 조작법 이미지 (비활성 오브젝트라 GameObject.Find 안 됨 → FindObjectsInactive)
        var cg = Object.FindAnyObjectByType<ControlsGuide>(FindObjectsInactive.Include);
        if (cg != null) cg.Show();

        // 종료
        if (cameraFollow != null) cameraFollow.enabled = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        FreezeTable(false);
        finished = true;
    }

    IEnumerator Hold(float dur, Vector3 cp, Vector3 cl)
    { for (float t = 0; t < dur; t += Time.deltaTime) { SetCam(cp, cl); yield return null; } }

    void PlaySuccessSfx()
    {
        if (successClip == null) return;
        if (voiceSrc == null) { voiceSrc = GetComponent<AudioSource>(); if (voiceSrc == null) voiceSrc = gameObject.AddComponent<AudioSource>(); voiceSrc.playOnAwake = false; voiceSrc.spatialBlend = 0f; }
        voiceSrc.PlayOneShot(successClip, Sfx.Volume);
    }

    void PlayVoice(AudioClip c)
    {
        if (c == null) return;
        if (voiceSrc == null) { voiceSrc = GetComponent<AudioSource>(); if (voiceSrc == null) voiceSrc = gameObject.AddComponent<AudioSource>(); voiceSrc.playOnAwake = false; voiceSrc.spatialBlend = 0f; }
        voiceSrc.PlayOneShot(c);
    }

    IEnumerator CamLerp(Vector3 p0, Vector3 l0, Vector3 p1, Vector3 l1, float dur)
    {
        Quaternion r0 = Quaternion.LookRotation(l0 - p0, Vector3.up), r1 = Quaternion.LookRotation(l1 - p1, Vector3.up);
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0, 1, t / dur);
            cam.position = Vector3.Lerp(p0, p1, u); cam.rotation = Quaternion.Slerp(r0, r1, u); yield return null;
        }
    }

    // 테이블 '몸'이 파랗게 빛나고, 빛이 절정일 때 얼음(눈사람의 힘)으로 변신 →
    // 빛이 잦아들면 얼음 형태로 남는다. (화면 전체 번쩍임 X — 몸만 은은히 빛나는 세기로 낮춤.)
    IEnumerator FlashTable(Vector3 cp, Vector3 cl)
    {
        var rends = table != null ? table.root.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
        var mpb = new MaterialPropertyBlock();
        float dur = glowSeconds;
        bool transformed = false;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float u = t / dur;
            float k = Mathf.Pow(Mathf.Clamp01(u / 0.18f) * Mathf.Clamp01((1f - u) / 0.28f), 0.6f);
            // 빛 절정(u≥0.45)에서 얼음으로 변신(1회) — 빛나는 도중 형태가 바뀐다.
            if (!transformed && u >= 0.45f)
            {
                transformed = true;
                if (icePower != null) { icePower.unlocked = true; icePower.active = true; icePower.Apply(); }
                if (table != null) rends = table.root.GetComponentsInChildren<MeshRenderer>();  // 얼음재질로 스왑된 렌더러 재취득
            }
            Color emi = new Color(0.4f, 0.7f, 1.5f) * (k * 1.1f);   // 은은한 '몸빛'(과하지 않게)
            foreach (var r in rends)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;
                if (m != null && !m.IsKeywordEnabled("_EMISSION")) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", emi); r.SetPropertyBlock(mpb);
            }
            SetCam(cp, cl); yield return null;
        }
        // 빛 종료: MPB 비워 얼음재질 자체의 은은한 발광만 남긴다(검정 override 안 함).
        foreach (var r in rends) { if (r == null) continue; r.SetPropertyBlock(null); }
        // 혹시 절정 전에 끝났으면 확실히 변신 보장.
        if (icePower != null && !icePower.active) { icePower.unlocked = true; icePower.active = true; icePower.Apply(); }
    }

    GameObject MakeLight()
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "SnowLight"; var c = s.GetComponent<Collider>(); if (c) Destroy(c);
        s.transform.localScale = Vector3.one * 0.6f;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.color = new Color(0.6f, 0.8f, 1f); m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.4f, 0.7f, 1.6f) * 1.6f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        s.GetComponent<Renderer>().sharedMaterial = m;
        // 화면 전체가 파랗게 물드는 것 방지 — 이동하는 작은 구슬빛만(주변만 살짝).
        var pl = s.AddComponent<Light>(); pl.type = LightType.Point; pl.color = new Color(0.5f, 0.75f, 1f); pl.range = 4.5f; pl.intensity = 1.4f;
        return s;
    }

    void SetCam(Vector3 pos, Vector3 look)
    { if (cam != null) { cam.position = pos; cam.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up); } }

    void FreezeTable(bool freeze)
    {
        if (table == null) return;
        foreach (var rb in table.root.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) rb.isKinematic = freeze;
        // ★ ArticulationBody 테이블: 루트 고정(immovable)으로 동결
        foreach (var ab in table.root.GetComponentsInChildren<ArticulationBody>(true))
            if (ab != null && ab.isRoot) { ab.immovable = freeze; if (freeze) { ab.linearVelocity = Vector3.zero; ab.angularVelocity = Vector3.zero; } }
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (currentBubble == null || body == null) return;
        var c = Camera.main; if (c == null) return;
        // ★꼬리(버블 텍스처 오른쪽 가장자리, 위에서 ~43% 높이)가 '머리 공 왼쪽 가장자리'에 닿게 배치.
        Vector3 headC = assembledPos;                        // 조립된 머리 중심
        Vector3 sp = c.WorldToScreenPoint(headC);
        if (sp.z <= 0f) return;
        // 머리 공의 화면 반지름(px): 중심에서 카메라-오른쪽으로 반경만큼 이동한 점을 투영해 계산
        Vector3 spEdge = c.WorldToScreenPoint(headC - c.transform.right * 0.72f);
        float rPix = Mathf.Abs(sp.x - spEdge.x);
        float bw = Mathf.Clamp(Screen.width * 0.30f, 240f, 420f);
        float bh = bw * (currentBubble.height / (float)currentBubble.width);
        float headGuiY = Screen.height - sp.y;               // GUI 좌표계 머리 중심
        var r = new Rect(sp.x - rPix - bw + 6f,              // 오른쪽 끝(꼬리 끝)이 공 왼쪽에 살짝 겹침
                         headGuiY - bh * 0.43f,              // 꼬리 높이(43%)를 머리 중심에 맞춤
                         bw, bh);
        r.x = Mathf.Max(8f, r.x); r.y = Mathf.Clamp(r.y, 8f, Screen.height - bh - 8f);
        GUI.DrawTexture(r, currentBubble, ScaleMode.ScaleToFit, true);
    }
}
