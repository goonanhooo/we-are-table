using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Jungle: 테이블이 기린 근처에 오면 발생하는 컷신.
/// 기린이 일어나 테이블을 향함 → 대사 2줄 → 옆 앵글 전환 → 기린 품속 빛이 테이블로 들어감 →
/// 테이블 번쩍 → 시스템 메시지 → 이후 GiraffeMode 잠금 해제(2번으로 지라프 모드 개방 가능).
/// 컷신 동안 테이블은 kinematic 정지, 카메라(CameraFollow)는 잠시 꺼지고 직접 제어.
/// </summary>
public class GiraffeCutscene : MonoBehaviour
{
    [Header("기린(비우면 'Giraffe' 이름으로 찾음)")]
    public Transform giraffe;
    [Tooltip("기린 모델 forward 보정(머리 방향이 반대면 180)")]
    public float faceOffsetY = 0f;

    [Header("발동 / 포즈")]
    [Tooltip("true면 테이블이 triggerRange 안에 오면 자동 발동. false면 철창 버튼(CageButton)이 Play()로 발동.")]
    public bool autoTrigger = false;
    public float triggerRange = 7f;     // (autoTrigger 시) 테이블이 이 거리 안이면 컷신
    public float lyingTilt = 88f;       // 눕힌 각(서있는 회전에서 옆으로)
    public float lyingYOffset = 0.5f;   // 누울 때 살짝 띄움

    [Header("타이밍(초)")]
    public float standSeconds = 1.6f;
    public float talkSeconds = 2.6f;
    public float panSeconds = 1.8f;
    public float lightSeconds = 3.0f;
    public float messageSeconds = 5f;
    [Tooltip("테이블이 빛나며 기린으로 변신하는 시간(길게)")]
    public float glowSeconds = 2.8f;

    [Header("기린 음성(대사 줄마다 한 번씩)")]
    public AudioClip voice1;   // 첫 대사
    public AudioClip voice2;   // 둘째 대사
    AudioSource voiceSrc;

    [Header("기린 대사 말풍선 이미지(기린 왼쪽에 표시 — 음성과 동시)")]
    public Texture2D bubble1;  // "날 도와줘서 고마워.."
    public Texture2D bubble2;  // "내 힘을 줄게..."
    Texture2D currentBubble;

    Transform table, tableTop, cam;
    Behaviour cameraFollow;
    GiraffeMode giraffeMode;
    float groundY;
    Quaternion standRot;
    bool started, finished;

    void Start()
    {
        if (giraffe == null) { var g = GameObject.Find("Giraffe"); if (g) giraffe = g.transform; }
        var tgo = GameObject.Find("Table"); if (tgo) table = tgo.transform;
        var ttgo = GameObject.Find("TableTop"); if (ttgo) tableTop = ttgo.transform;
        if (Camera.main) { cam = Camera.main.transform; cameraFollow = Camera.main.GetComponent("CameraFollow") as Behaviour; }
        giraffeMode = Object.FindAnyObjectByType<GiraffeMode>();
        if (giraffeMode != null) giraffeMode.locked = true; // 컷신 전까지 잠금

        if (giraffe != null)
        {
            groundY = giraffe.position.y;
            standRot = giraffe.rotation;
            giraffe.rotation = standRot * Quaternion.Euler(0, 0, lyingTilt);   // 눕힘
            giraffe.position = new Vector3(giraffe.position.x, groundY + lyingYOffset, giraffe.position.z);
        }
    }

    void Update()
    {
        if (!autoTrigger || started || finished || table == null || giraffe == null) return;
        var a = new Vector2(table.position.x, table.position.z);
        var b = new Vector2(giraffe.position.x, giraffe.position.z);
        if (Vector2.Distance(a, b) < triggerRange) Play();
    }

    /// <summary>철창 버튼이 눌려 철창이 사라진 뒤 외부에서 호출 → 컷신 시작.</summary>
    public void Play()
    {
        if (started || finished || giraffe == null) return;
        started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        FreezeTable(true);
        if (cameraFollow != null) cameraFollow.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;

        // 테이블 향하는 서있는 회전
        Vector3 toTable = table.position - giraffe.position; toTable.y = 0;
        if (toTable.sqrMagnitude > 0.01f)
            standRot = Quaternion.LookRotation(toTable.normalized, Vector3.up) * Quaternion.Euler(0, faceOffsetY, 0);
        Quaternion lyingRot = standRot * Quaternion.Euler(0, 0, lyingTilt);
        Vector3 standPos = new Vector3(giraffe.position.x, groundY, giraffe.position.z);
        Vector3 lyingPos = new Vector3(giraffe.position.x, groundY + lyingYOffset, giraffe.position.z);

        // 기린 정면(테이블 쪽)에서 보는 카메라
        Vector3 gLook = standPos + Vector3.up * 1.8f;
        Vector3 camFront = standPos + standRot * Vector3.forward * 5.5f + Vector3.up * 2.2f;

        // 1) 기립
        for (float t = 0; t < standSeconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0, 1, t / standSeconds);
            giraffe.rotation = Quaternion.Slerp(lyingRot, standRot, u);
            giraffe.position = Vector3.Lerp(lyingPos, standPos, u);
            SetCam(camFront, gLook); yield return null;
        }
        giraffe.rotation = standRot; giraffe.position = standPos;

        // 2~3) 대사 = 기린 왼쪽 말풍선 이미지 + 각 줄에 음성 한 번씩(동시)
        currentBubble = bubble1;
        PlayVoice(voice1);
        yield return Hold(talkSeconds, camFront, gLook);
        currentBubble = bubble2;
        PlayVoice(voice2);
        yield return Hold(talkSeconds, camFront, gLook);
        currentBubble = null;

        // 4) 옆 앵글(기린-테이블 마주보는 옆)
        Vector3 mid = (standPos + table.position) * 0.5f + Vector3.up * 1.3f;
        Vector3 side = Vector3.Cross((table.position - standPos).normalized, Vector3.up);
        Vector3 camSide = mid + side * 6f + Vector3.up * 1.8f;
        yield return CamLerp(camFront, gLook, camSide, mid, panSeconds);

        // 5) 빛 전달 + 테이블 번쩍
        var light = MakeLight();
        Vector3 chest = standPos + standRot * Vector3.forward * 0.5f + Vector3.up * 2.0f;
        Vector3 tgt = (tableTop != null ? tableTop.position : table.position) + Vector3.up * 0.2f;
        for (float t = 0; t < lightSeconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0, 1, t / lightSeconds);
            light.transform.position = Vector3.Lerp(chest, tgt, u);
            light.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 0.12f, u);
            SetCam(camSide, mid); yield return null;
        }
        Destroy(light);
        yield return FlashTable(camSide, mid);   // 길게 빛나며 도중에 기린 스킨으로 변신(active=true)

        // 6) 권능 부여: 발광 중 이미 기린 모드 ON됨 → 잠금만 해제. (안내 텍스트는 제거 — 조작법 이미지로 설명)
        if (giraffeMode != null) giraffeMode.locked = false;
        GiraffeMode.Unlocked = true;   // 조작법 메뉴에 기린 조작 노출
        yield return Hold(1.6f, camSide, mid);   // 변신한 모습 잠깐 보여줌

        // 기린의 힘 조작법 이미지 표시(키 2 + 다리 신축키 강조)
        var guide = Object.FindAnyObjectByType<ControlsGuide>(FindObjectsInactive.Include);
        if (guide != null) guide.Show();

        // 종료
        if (cameraFollow != null) cameraFollow.enabled = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        FreezeTable(false);
        finished = true;
    }

    IEnumerator Hold(float dur, Vector3 cp, Vector3 cl)
    { for (float t = 0; t < dur; t += Time.deltaTime) { SetCam(cp, cl); yield return null; } }

    void PlayVoice(AudioClip c)
    {
        if (c == null) return;
        if (voiceSrc == null)
        {
            voiceSrc = GetComponent<AudioSource>();
            if (voiceSrc == null) voiceSrc = gameObject.AddComponent<AudioSource>();
            voiceSrc.playOnAwake = false; voiceSrc.spatialBlend = 0f;
        }
        voiceSrc.PlayOneShot(c);
    }

    IEnumerator CamLerp(Vector3 p0, Vector3 l0, Vector3 p1, Vector3 l1, float dur)
    {
        Quaternion r0 = Quaternion.LookRotation(l0 - p0, Vector3.up), r1 = Quaternion.LookRotation(l1 - p1, Vector3.up);
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0, 1, t / dur);
            cam.position = Vector3.Lerp(p0, p1, u); cam.rotation = Quaternion.Slerp(r0, r1, u);
            yield return null;
        }
    }

    // 테이블 **몸 전체**(상판 + 4다리)가 길게 빛나고, 발광 도중 **기린 스킨으로 변신**한다.
    IEnumerator FlashTable(Vector3 cp, Vector3 cl)
    {
        var rends = table != null ? table.root.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
        var mpb = new MaterialPropertyBlock();
        float dur = glowSeconds;
        bool swapped = false;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float u = t / dur;
            // 빠르게 차오르고(18%) 길게 유지하다 끝에서 사그라듦(마지막 28%)
            float k = Mathf.Pow(Mathf.Clamp01(u / 0.18f) * Mathf.Clamp01((1f - u) / 0.28f), 0.6f);
            // 발광 도중(40%) 기린 텍스처로 변신
            if (!swapped && giraffeMode != null && u >= 0.4f) { giraffeMode.active = true; swapped = true; }
            Color emi = new Color(1.5f, 1.3f, 0.85f) * (k * 3.5f);
            foreach (var r in rends)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;   // 스킨 스왑 후 바뀐 기린 머티리얼도 빛나게 매 프레임 _EMISSION 보장
                if (m != null && !m.IsKeywordEnabled("_EMISSION"))
                { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", emi); r.SetPropertyBlock(mpb);
            }
            SetCam(cp, cl); yield return null;
        }
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", Color.black); r.SetPropertyBlock(mpb);
        }
    }

    GameObject MakeLight()
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "GiraffeLight"; var c = s.GetComponent<Collider>(); if (c) Destroy(c);
        s.transform.localScale = Vector3.one * 0.55f;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh != null ? sh : Shader.Find("Standard"));
        m.color = new Color(1f, 0.95f, 0.7f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(1.6f, 1.4f, 0.85f) * 3f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        s.GetComponent<Renderer>().sharedMaterial = m;
        var pl = s.AddComponent<Light>(); pl.type = LightType.Point; pl.color = new Color(1f, 0.9f, 0.6f); pl.range = 7f; pl.intensity = 3.5f;
        return s;
    }

    void SetCam(Vector3 pos, Vector3 look)
    { if (cam != null) { cam.position = pos; cam.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up); } }

    void FreezeTable(bool freeze)
    {
        if (table == null) return;
        foreach (var rb in table.root.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) rb.isKinematic = freeze;
    }

    // 기린 대사 = 기린 머리 왼쪽에 말풍선 이미지(텍스트는 빌드에 폰트가 없어 안 나오므로 이미지로 구워 표시).
    void OnGUI()
    {
        if (currentBubble == null || giraffe == null) return;
        var c = Camera.main;
        if (c == null) return;
        Vector3 head = giraffe.position + Vector3.up * 2.4f;     // 기린 머리 부근
        Vector3 sp = c.WorldToScreenPoint(head);
        if (sp.z <= 0f) return;                                  // 카메라 뒤면 표시 안 함

        float bw = Mathf.Clamp(Screen.width * 0.30f, 240f, 420f);
        float bh = bw * (currentBubble.height / (float)currentBubble.width);
        float gx = sp.x;
        float gy = Screen.height - sp.y;                         // GUI 좌표(상단 원점)
        // 기린 왼쪽: 말풍선 꼬리(오른쪽)가 기린 머리에 닿도록 머리 왼쪽에 배치
        var r = new Rect(gx - bw + 8f, gy - bh * 0.5f, bw, bh);
        r.x = Mathf.Max(8f, r.x);
        r.y = Mathf.Clamp(r.y, 8f, Screen.height - bh - 8f);
        GUI.DrawTexture(r, currentBubble, ScaleMode.ScaleToFit, true);
    }
}
