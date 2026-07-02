using UnityEngine;

/// <summary>
/// 근접 버튼(재사용). 테이블 강체가 pressRadius 안에 들어오면 눌린 것으로 판정 → 버튼 캡이 살짝 내려가고 door.Open().
/// 바닥 버튼은 걸어가서, 천장 버튼은 지라프 모드로 키를 키워야 닿는 높이에 배치하면 '지라프 전용'이 된다.
/// (무게 물리 대신 근접 판정이라 아래에서 위로 누르는 천장 버튼도 동작.)
/// </summary>
public class LevelButton : MonoBehaviour
{
    public SlidingDoor door;
    [Tooltip("눌릴 때 살짝 내려가는 캡(비우면 this).")]
    public Transform cap;
    [Tooltip("테이블 강체가 이 거리 안이면 눌림.")]
    public float pressRadius = 1.6f;
    public float capDip = 0.1f;
    [Tooltip("눌리면 색을 이 색으로(빨강→초록 같은 피드백). 비활성 시 무시.")]
    public bool tintOnPress = true;

    Rigidbody[] tableBodies;
    Vector3 capBase;
    bool pressed;
    Renderer capRend;

    void Start()
    {
        if (cap == null) cap = transform;
        capBase = cap.localPosition;
        capRend = cap.GetComponent<Renderer>();
        var tgo = GameObject.Find("Table");
        if (tgo == null) tgo = GameObject.Find("TablePlayer");
        if (tgo != null) tableBodies = tgo.transform.root.GetComponentsInChildren<Rigidbody>(true);
    }

    void Update()
    {
        if (pressed || tableBodies == null) return;
        Vector3 p = transform.position;
        foreach (var rb in tableBodies)
        {
            if (rb == null) continue;
            if (Vector3.Distance(rb.position, p) < pressRadius)
            {
                pressed = true;
                cap.localPosition = capBase - Vector3.up * capDip;
                if (tintOnPress && capRend != null) capRend.material.color = new Color(0.3f, 0.85f, 0.4f);
                if (door != null) door.Open();
                break;
            }
        }
    }
}
