using UnityEngine;

/// <summary>
/// 구멍 입구에 두는 트리거. 뚜껑이 열린 뒤 테이블이 구멍으로 떨어져 이 트리거에 들어오면
/// GiraffeGate.TryFall()을 호출 → 빙산 씬으로 전환(지형이 바로 아래 있어도 진입 순간 바로 발동).
/// </summary>
[RequireComponent(typeof(Collider))]
public class GateFallTrigger : MonoBehaviour
{
    public GiraffeGate gate;
    Transform tableRoot;

    void Start()
    {
        var t = GameObject.Find("Table");
        if (t == null) t = GameObject.Find("TablePlayer");
        if (t != null) tableRoot = t.transform.root;
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (gate == null || tableRoot == null) return;
        if (other.transform.root == tableRoot) gate.TryFall();
    }
}
