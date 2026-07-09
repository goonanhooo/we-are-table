using System.Collections;
using UnityEngine;

/// <summary>
/// ★ 애벌레 버튼(2026-07-10, Climb 씬) — 정글 건물 버튼과 같은 룩/판정 규칙:
/// 수평 = 캡 풋프린트(lossyScale·yaw OverlapBox), 수직 = 아래 belowWindow~위 heightWindow(후하게).
/// 누르면 버튼 근처 구덩이(pitFrom)에서 테니스공 애벌레 여러 마리가 나와
/// 반대 구덩이(pitTo)로 기어 들어간다. 웨이브가 끝나면 다시 누를 수 있음.
/// </summary>
public class CaterpillarButton : MonoBehaviour
{
    [Header("참조")]
    public Transform buttonCap;
    public Transform pitFrom;
    public Transform pitTo;
    public Material ballMat;

    [Header("판정(GButton 규칙)")]
    public float heightWindow = 5f;
    public float belowWindow = 1.5f;

    [Header("웨이브")]
    public int caterpillars = 4;
    public float spawnInterval = 1.1f;
    public float cooldown = 4f;

    Transform tableRoot;
    Vector3 capRestWorld;
    Vector3 capBaseLocal;
    Material capMat;
    Color capColor0;
    bool busy;
    readonly Collider[] overlap = new Collider[24];

    void Start()
    {
        if (buttonCap == null) buttonCap = transform;
        capRestWorld = buttonCap.position;
        capBaseLocal = buttonCap.localPosition;
        var r = buttonCap.GetComponent<Renderer>();
        if (r != null) { capMat = r.material; capColor0 = capMat.color; }
        var t = GameObject.Find("Table");
        if (t != null) tableRoot = t.transform.root;
    }

    void Update()
    {
        if (busy || tableRoot == null || buttonCap == null) return;
        Vector3 half = new Vector3(
            buttonCap.lossyScale.x * 0.5f,
            (heightWindow + belowWindow) * 0.5f,
            buttonCap.lossyScale.z * 0.5f);
        Vector3 center = capRestWorld + Vector3.up * ((heightWindow - belowWindow) * 0.5f);
        Quaternion orient = Quaternion.Euler(0f, buttonCap.eulerAngles.y, 0f);
        int n = Physics.OverlapBoxNonAlloc(center, half, overlap, orient, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = overlap[i];
            if (c == null || c.transform.root != tableRoot) continue;
            StartCoroutine(Wave());
            break;
        }
    }

    IEnumerator Wave()
    {
        busy = true;
        // 눌림 연출: 캡 내려감 + 초록
        if (capMat != null) capMat.color = new Color(0.25f, 0.85f, 0.3f);
        for (float u = 0f; u < 1f; u += Time.deltaTime / 0.12f)
        {
            buttonCap.localPosition = capBaseLocal - Vector3.up * (0.07f * u);
            yield return null;
        }

        Vector3 a = pitFrom.position;
        Vector3 b = pitTo.position;
        for (int k = 0; k < caterpillars; k++)
        {
            SpawnOne(a, b, k);
            yield return new WaitForSeconds(spawnInterval);
        }

        yield return new WaitForSeconds(cooldown);
        // 복구(재사용 가능)
        for (float u = 1f; u > 0f; u -= Time.deltaTime / 0.25f)
        {
            buttonCap.localPosition = capBaseLocal - Vector3.up * (0.07f * u);
            yield return null;
        }
        buttonCap.localPosition = capBaseLocal;
        if (capMat != null) capMat.color = capColor0;
        busy = false;
    }

    void SpawnOne(Vector3 a, Vector3 b, int k)
    {
        // 마리별 경유지: 구덩이 속 → 테두리 → 살짝 굽은 지상 경로(마리마다 좌우 오프셋) → 반대 구덩이 속
        Vector3 dir = b - a; dir.y = 0f;
        float len = dir.magnitude;
        dir /= Mathf.Max(len, 0.01f);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float lat = ((k % 2 == 0) ? 1f : -1f) * (0.5f + 0.45f * (k / 2));
        float ballR = 0.17f;

        var path = new Vector3[7];
        path[0] = a + Vector3.up * -0.35f;                                        // 공 무더기 속
        path[1] = a + Vector3.up * ballR;                                          // 구덩이 위로
        path[2] = a + dir * (len * 0.25f) + side * lat + Vector3.up * ballR;
        path[3] = a + dir * (len * 0.5f) + side * (lat * 1.4f) + Vector3.up * ballR;
        path[4] = a + dir * (len * 0.75f) + side * lat + Vector3.up * ballR;
        path[5] = b + Vector3.up * ballR;                                          // 반대 구덩이 테두리
        path[6] = b + Vector3.up * -0.35f;                                         // 무더기 속으로
        var go = new GameObject("Caterpillar_" + k);
        go.transform.position = path[0];
        var cat = go.AddComponent<TennisCaterpillar>();
        cat.BuildAndGo(path, ballMat, Random.Range(1.0f, 1.5f));
    }
}
