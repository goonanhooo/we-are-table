using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ArticulationBody 테이블용 '1' 리셋(ArticulationTest 씬). 루트에 부착.
/// '1' = 현재 XZ 위치에서 자세만 똑바로 복원(TeleportRoot + 전 관절 0) — 본게임 SceneReset의 핵심 재현.
/// 지라프 다리 길이도 초기화(GiraffeMode.ResetLegs).
/// </summary>
public class ArtSceneReset : MonoBehaviour
{
    ArticulationBody root;
    ArtLegController[] legs;
    float groundLift = 0.9f;   // 리셋 시 지면 위로 띄우는 높이

    void Awake()
    {
        foreach (var ab in GetComponentsInChildren<ArticulationBody>(true))
            if (ab.isRoot) { root = ab; break; }
        legs = GetComponentsInChildren<ArtLegController>(true);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || root == null) return;
        if (kb.digit1Key.wasPressedThisFrame) ResetPoseInPlace();
    }

    public void ResetPoseInPlace()
    {
        Vector3 p = root.transform.position;
        // 지면 탐색(아래로 레이)
        float groundY = 0f;
        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y;
        root.TeleportRoot(new Vector3(p.x, groundY + groundLift, p.z), Quaternion.identity);
        foreach (var ab in GetComponentsInChildren<ArticulationBody>(true))
        {
            ab.linearVelocity = Vector3.zero;
            ab.angularVelocity = Vector3.zero;
        }
        foreach (var l in legs) { l.ResetState(); l.HoldVertical(0.4f); }
        var gm = GetComponentInChildren<GiraffeMode>(true);
        if (gm != null) gm.ResetLegs();
    }
}
