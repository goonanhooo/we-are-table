using UnityEngine;

/// <summary>
/// ★2026-09-04 지하 동굴 대기(IceCavern). 카메라가 이 박스(Transform 위치/스케일) 안에 있으면 안개·태양·앰비언트를 동굴용(어둡고
/// 먼 거리 안개)으로 부드럽게 블렌드하고, 나가면 원래 값으로 되돌린다. 빙산 야외 안개(밝은 하늘색)와 태양이 거대 동굴을 밝게
/// 씻어내지 않게 하면서 먼 벽이 어둠에 잠기는 깊이감을 준다. (동굴 파셋은 Custom/VertexColorCave가 앰비언트/태양을 자체 감쇠)
/// </summary>
[ExecuteAlways]
public class CaveFogZone : MonoBehaviour
{
    [Header("동굴 안 값")]
    public Color caveFogColor = new Color(0.02f, 0.018f, 0.02f);
    public float caveFogStart = 60f, caveFogEnd = 420f;
    public Color caveAmbientSky = new Color(0.10f, 0.09f, 0.09f), caveAmbientEquator = new Color(0.08f, 0.07f, 0.07f), caveAmbientGround = new Color(0.05f, 0.045f, 0.04f);
    [Tooltip("동굴 안 태양 세기(0=완전 차단)")] public float caveSunIntensity = 0.02f;
    public float blendSeconds = 0.8f;
    [Tooltip("태양(비면 RenderSettings.sun 또는 'Sun' 오브젝트)")] public Light sun;
    public Camera cam;

    float w;                 // 0=바깥, 1=동굴 안
    bool saved;
    Color oFog, oSky, oEq, oGround; float oStart, oEnd, oSun; bool oFogOn;

    void OnDisable() { if (saved && w > 0f) { w = 0f; Apply(); } }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        var c = cam != null ? cam : Camera.main; if (c == null) return;
        if (sun == null) { sun = RenderSettings.sun; if (sun == null) { var s = GameObject.Find("Sun"); if (s != null) sun = s.GetComponent<Light>(); } }
        Vector3 l = transform.InverseTransformPoint(c.transform.position);
        bool inside = Mathf.Abs(l.x) <= 0.5f && Mathf.Abs(l.y) <= 0.5f && Mathf.Abs(l.z) <= 0.5f;
        if (!saved)
        {
            oFogOn = RenderSettings.fog; oFog = RenderSettings.fogColor; oStart = RenderSettings.fogStartDistance; oEnd = RenderSettings.fogEndDistance;
            oSky = RenderSettings.ambientSkyColor; oEq = RenderSettings.ambientEquatorColor; oGround = RenderSettings.ambientGroundColor;
            oSun = sun != null ? sun.intensity : 1f; saved = true;
        }
        float target = inside ? 1f : 0f;
        if (Mathf.Approximately(w, target)) { if (inside) Apply(); return; }
        w = Mathf.MoveTowards(w, target, Time.deltaTime / Mathf.Max(0.01f, blendSeconds));
        Apply();
    }

    void Apply()
    {
        RenderSettings.fog = w > 0.001f ? true : oFogOn;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(oFog, caveFogColor, w);
        RenderSettings.fogStartDistance = Mathf.Lerp(oStart, caveFogStart, w);
        RenderSettings.fogEndDistance = Mathf.Lerp(oEnd, caveFogEnd, w);
        RenderSettings.ambientSkyColor = Color.Lerp(oSky, caveAmbientSky, w);
        RenderSettings.ambientEquatorColor = Color.Lerp(oEq, caveAmbientEquator, w);
        RenderSettings.ambientGroundColor = Color.Lerp(oGround, caveAmbientGround, w);
        if (sun != null) sun.intensity = Mathf.Lerp(oSun, caveSunIntensity, w);
    }
    public float Weight => w;
#if UNITY_EDITOR
    void OnDrawGizmosSelected() { Gizmos.color = new Color(0.4f, 0.3f, 0.2f, 0.6f); Gizmos.matrix = transform.localToWorldMatrix; Gizmos.DrawWireCube(Vector3.zero, Vector3.one); }
#endif
}
