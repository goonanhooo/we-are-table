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

    Transform table, tableTop, cam;
    Behaviour cameraFollow;
    GiraffeMode giraffeMode;
    float groundY;
    Quaternion standRot;
    bool started, finished;
    string speech = "", systemMsg = "";
    Font kfont;

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

        // 2~3) 대사
        speech = "날 도와줘서 고마워..";
        yield return Hold(talkSeconds, camFront, gLook);
        speech = "내 힘을 줄게...";
        yield return Hold(talkSeconds, camFront, gLook);
        speech = "";

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
        yield return FlashTable(camSide, mid);

        // 6) 권능 부여 + 시스템 메시지
        if (giraffeMode != null) giraffeMode.locked = false;
        systemMsg = "기린의 힘을 얻었다\n2번 버튼을 눌러 기린의 힘을 개방해보세요";
        yield return Hold(messageSeconds, camSide, mid);
        systemMsg = "";

        // 종료
        if (cameraFollow != null) cameraFollow.enabled = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        FreezeTable(false);
        finished = true;
    }

    IEnumerator Hold(float dur, Vector3 cp, Vector3 cl)
    { for (float t = 0; t < dur; t += Time.deltaTime) { SetCam(cp, cl); yield return null; } }

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

    IEnumerator FlashTable(Vector3 cp, Vector3 cl)
    {
        var mr = tableTop != null ? tableTop.GetComponent<Renderer>() : null;
        var mpb = new MaterialPropertyBlock();
        for (float t = 0, dur = 0.6f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / dur * Mathf.PI);
            if (mr != null) { mr.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", new Color(1.5f, 1.3f, 0.8f) * (k * 3f)); mr.SetPropertyBlock(mpb); }
            SetCam(cp, cl); yield return null;
        }
        if (mr != null) { mr.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", Color.black); mr.SetPropertyBlock(mpb); }
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

    GUIStyle Style(int size, Color col)
    {
        if (kfont == null)
            kfont = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Malgun Gothic", "Arial Unicode MS" }, size);
        var st = new GUIStyle { fontSize = size, alignment = TextAnchor.MiddleCenter };
        if (kfont != null) st.font = kfont;
        st.normal.textColor = col;
        st.wordWrap = true;
        return st;
    }

    void OnGUI()
    {
        if (!string.IsNullOrEmpty(speech))
            GUI.Label(new Rect(0, Screen.height * 0.74f, Screen.width, 70), speech, Style(30, Color.white));
        if (!string.IsNullOrEmpty(systemMsg))
        {
            float w = Screen.width * 0.6f, h = 120f;
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.4f, w, h);
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(box, systemMsg, Style(26, new Color(1f, 0.95f, 0.6f)));
        }
    }
}
