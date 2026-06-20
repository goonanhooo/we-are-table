using System.Collections;
using UnityEngine;

/// <summary>
/// 기린 철창 앞 버튼. 테이블이 아주 살짝만 닿아도(=pressRadius 안에 테이블 강체가 들어오면) 눌린 것으로 판정.
/// 눌리면: 버튼이 살짝 내려갔다 → 철창(cage)이 하늘로 천천히 올라가 시야 밖으로 → 실제로 비활성(사라짐) →
/// 기린 컷신(GiraffeCutscene.Play) 실행. 한 번만 동작.
/// 버튼 GameObject(누르는 윗면) 에 부착. cage/cutscene 는 비우면 이름으로 찾는다.
/// </summary>
public class CageButton : MonoBehaviour
{
    [Header("참조(비우면 자동 검색)")]
    public Transform cage;              // 올라가 사라질 철창 루트
    public GiraffeCutscene cutscene;
    public Transform buttonCap;         // 눌릴 때 내려가는 윗부분(비우면 this)

    [Header("감도")]
    [Tooltip("테이블 강체가 이 거리 안에 들어오면 눌림(아주 민감하게 크게).")]
    public float pressRadius = 1.3f;

    [Header("연출")]
    public float capDip = 0.12f;        // 버튼 눌림 깊이
    public float riseHeight = 60f;      // 철창이 올라갈 높이
    public float riseSeconds = 3.2f;    // 올라가는 시간

    Rigidbody[] tableBodies;
    Vector3 capBasePos;
    bool pressed;

    void Start()
    {
        if (cage == null) { var c = GameObject.Find("GiraffeCage"); if (c) cage = c.transform; }
        if (cutscene == null) cutscene = Object.FindAnyObjectByType<GiraffeCutscene>();
        if (buttonCap == null) buttonCap = transform;
        capBasePos = buttonCap.localPosition;

        var tgo = GameObject.Find("Table");
        if (tgo != null) tableBodies = tgo.transform.root.GetComponentsInChildren<Rigidbody>(true);
    }

    void Update()
    {
        if (pressed || tableBodies == null) return;
        Vector3 p = transform.position;
        foreach (var rb in tableBodies)
        {
            if (rb == null) continue;
            if (Vector3.Distance(rb.position, p) < pressRadius) { pressed = true; StartCoroutine(Sequence()); break; }
        }
    }

    IEnumerator Sequence()
    {
        // 버튼 눌림(살짝 내려감)
        for (float t = 0; t < 0.15f; t += Time.deltaTime)
        {
            float u = t / 0.15f;
            if (buttonCap != null) buttonCap.localPosition = capBasePos - Vector3.up * (capDip * u);
            yield return null;
        }

        // 철창이 하늘로 천천히 상승 → 시야 밖
        if (cage != null)
        {
            Vector3 from = cage.position;
            Vector3 to = from + Vector3.up * riseHeight;
            for (float t = 0; t < riseSeconds; t += Time.deltaTime)
            {
                float u = t / riseSeconds;
                u = u * u * (3f - 2f * u);              // ease in-out
                cage.position = Vector3.Lerp(from, to, u);
                yield return null;
            }
            cage.position = to;
            cage.gameObject.SetActive(false);            // 실제로 사라짐
        }

        // 살짝 텀 후 컷신 시작
        yield return new WaitForSeconds(0.3f);
        if (cutscene != null) cutscene.Play();
    }
}
