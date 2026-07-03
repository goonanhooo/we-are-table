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
    // 뒤집힘 안내(본게임 SceneReset UX 보존): 2초 이상 뒤집혀 있으면 'Press 1' 배너
    float flippedTime;
    GUIStyle bannerStyle, bannerSub;
    Texture2D white;

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

        // 뒤집힘 감지(안내 배너용)
        bool flipped = Vector3.Dot(root.transform.up, Vector3.up) < 0.2f
                       && root.linearVelocity.magnitude < 1.2f;
        flippedTime = flipped ? flippedTime + Time.deltaTime : 0f;
    }

    void OnGUI()
    {
        if (flippedTime < 2f) return;
        if (bannerStyle == null)
        {
            white = Texture2D.whiteTexture;
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 29, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = Color.white;
            bannerSub = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            bannerSub.normal.textColor = new Color(0.9f, 0.9f, 0.95f, 1f);
        }
        float w = 372f, h = 80f;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.18f, w, h);
        var pc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(r, white); GUI.color = pc;
        GUI.Label(new Rect(r.x, r.y + 8f, r.width, 36f), "Press 1", bannerStyle);
        GUI.Label(new Rect(r.x, r.y + 46f, r.width, 24f), "뒤집혔어요 — 1 키로 자세 복구", bannerSub);
    }

    /// <summary>지정 XZ에서 자세 복원(정글 용암 리스폰 등 — 본게임 SceneReset.ResetPoseAtXZ 대응).</summary>
    public void ResetPoseAtXZ(float x, float z)
    {
        if (root == null) return;
        var p = root.transform.position; p.x = x; p.z = z;
        root.TeleportRoot(p, root.transform.rotation);
        ResetPoseInPlace();
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
