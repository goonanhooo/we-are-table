using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [플레이테스트 치트 — 빌드 시 제거 예정]
/// Shift + 방향키로 테이블을 수평(XZ)으로 순간 슬라이드 이동시킨다.
/// 테이블 관련 Rigidbody(본체 "Table" + LegController가 붙은 다리 피벗들)만 같은 양만큼 옮기고
/// 속도를 0으로 만들어, 관절을 유지한 채 통째로 미끄러지듯 움직인다. (버튼 등 다른 물체는 영향 없음)
/// 제거 방법: 이 컴포넌트를 테이블에서 떼거나 스크립트를 삭제.
/// </summary>
public class PlaytestCheat : MonoBehaviour
{
    [Tooltip("수평 이동 속도(m/s)")]
    public float speed = 5f;

    Rigidbody[] bodies;

    void Start()
    {
        var list = new List<Rigidbody>();
        var tableGo = GameObject.Find("Table");
        if (tableGo != null)
        {
            var rb = tableGo.GetComponent<Rigidbody>();
            if (rb != null) list.Add(rb);
        }
        foreach (var lc in FindObjectsByType<LegController>(FindObjectsSortMode.None))
        {
            var rb = lc.GetComponent<Rigidbody>();
            if (rb != null && !list.Contains(rb)) list.Add(rb);
        }
        bodies = list.ToArray();
    }

    void FixedUpdate()
    {
        var kb = Keyboard.current;
        if (kb == null || bodies == null) return;
        if (!(kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) return;

        float x = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
        float z = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
        if (x == 0f && z == 0f) return;

        Vector3 delta = new Vector3(x, 0f, z).normalized * speed * Time.fixedDeltaTime;
        // ★ ArticulationBody 테이블: 루트 TeleportRoot로 전체 이동
        if (bodies.Length == 0)
        {
            foreach (var ab in FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None))
                if (ab.isRoot && float.IsFinite(ab.transform.position.x))
                    ab.TeleportRoot(ab.transform.position + delta, ab.transform.rotation);
            return;
        }
        foreach (var rb in bodies)
        {
            if (rb == null || rb.isKinematic) continue;
            // NaN 가드: 솔버 폭발로 위치가 NaN이면 건드리지 않는다(에러 스팸 방지).
            // 복구는 SceneReset 워치독이 담당 — 여기서 NaN+delta를 쓰면 매 스텝 오류가 도배된다.
            var p = rb.position;
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) continue;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = p + delta;
        }
    }
}
