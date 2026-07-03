using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트용 치트모드] 옵션(PauseMenu)에서 ON/OFF. TablePlayer 루트에 부착.
/// 켜면:
///  - 모든 능력 해금(GiraffeMode.Unlocked, IcePower.unlocked).
///  - 비행: 중력 끄고 ↑↓←→(카메라 기준 수평) + Space(상)/LeftCtrl(하), Shift=가속.
///  - 순간이동: T = 카메라가 보는 지점(레이캐스트)으로 테이블 통째 이동.
/// 끄면 중력/물리 원복(능력은 유지). 정적 Active 라 씬 넘어가도 유지(새 플레이 시 리셋).
/// </summary>
public class CheatMode : MonoBehaviour
{
    public static bool Active = false;

    public float flySpeed = 16f;
    public float fastMul = 3f;

    Rigidbody[] bodies;
    bool[] origGravity;
    IcePower ice;
    bool gravityOff;
    bool[] origKinematic;
    bool cheatPhysics;
    ICheatUnlockable[] unlockables;

    void Start()
    {
        var list = new List<Rigidbody>();
        var t = GameObject.Find("Table");
        if (t != null) { var rb = t.GetComponent<Rigidbody>(); if (rb != null) list.Add(rb); }
        foreach (var lc in FindObjectsByType<LegController>(FindObjectsSortMode.None))
        {
            var rb = lc.GetComponent<Rigidbody>();
            if (rb != null && !list.Contains(rb)) list.Add(rb);
        }
        bodies = list.ToArray();
        origGravity = new bool[bodies.Length];
        origKinematic = new bool[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) { origGravity[i] = bodies[i].useGravity; origKinematic[i] = bodies[i].isKinematic; }
        ice = GetComponentInChildren<IcePower>();

        // 모든 능력(ICheatUnlockable) 수집 → 치트 ON일 때 매 프레임 해금.
        // 앞으로 새 능력을 추가할 때 ICheatUnlockable만 구현하면 별도 코드 없이 치트로 즉시 사용 가능.
        var us = new List<ICheatUnlockable>();
        foreach (var mb in transform.root.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb is ICheatUnlockable u) us.Add(u);
        unlockables = us.ToArray();
    }

    void Update()
    {
        if (Active)
        {
            // 모든 능력 해금(잠금 풀림 → 능력 키가 바로 먹힘. 예: 2 누르면 즉시 지라프).
            if (unlockables != null)
                foreach (var u in unlockables) u?.CheatUnlock();
            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame) TeleportToLook();
        }
        // 비행: 물리 충돌(벽/바닥 디페네트레이션)로 폭발하지 않게 kinematic(노클립)으로 전환. 끄면 원복.
        if (Active && !cheatPhysics)
        {
            for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null) { bodies[i].linearVelocity = Vector3.zero; bodies[i].angularVelocity = Vector3.zero; bodies[i].isKinematic = true; }
            foreach (var ab in FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None)) if (ab.isRoot) { ab.immovable = true; ab.linearVelocity = Vector3.zero; ab.angularVelocity = Vector3.zero; }   // ★ art 테이블
            cheatPhysics = true;
        }
        else if (!Active && cheatPhysics)
        {
            for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null) bodies[i].isKinematic = origKinematic[i];
            foreach (var ab in FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None)) if (ab.isRoot) ab.immovable = false;   // ★ art 테이블
            cheatPhysics = false;
        }
    }

    void FixedUpdate()
    {
        if (!Active || bodies == null) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        var cam = Camera.main;
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right; right.y = 0f; right.Normalize();
        float f = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
        float s = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
        // 상승 = Space 또는 '>'(마침표키),  하강 = LeftCtrl 또는 '<'(쉼표키)
        float u = ((kb.spaceKey.isPressed || kb.periodKey.isPressed) ? 1f : 0f)
                - ((kb.leftCtrlKey.isPressed || kb.commaKey.isPressed) ? 1f : 0f);
        Vector3 dir = fwd * f + right * s + Vector3.up * u;
        float spd = flySpeed * ((kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) ? fastMul : 1f);
        Vector3 delta = dir * spd * Time.fixedDeltaTime;
        foreach (var rb in bodies)
        {
            if (rb == null) continue;
            Vector3 p = rb.position;
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) continue;  // 안전 가드(혹시 모를 비유한)
            rb.position = p + delta;   // kinematic 텔레포트(노클립) — 입력 없으면 제자리, 충돌 폭발 없음
        }
    }

    void TeleportToLook()
    {
        var cam = Camera.main;
        if (cam == null || bodies == null) return;
        var t = GameObject.Find("Table");
        if (t == null) return;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 3000f))
        {
            Vector3 target = hit.point + Vector3.up * 2.5f;
            Vector3 off = target - t.transform.position;
            foreach (var rb in bodies)
            {
                if (rb == null) continue;
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.position += off;
            }
        }
    }

    GUIStyle hintStyle;
    void OnGUI()
    {
        if (!Active) return;
        if (hintStyle == null)
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        hintStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        var r = new Rect(12, 12, 560, 70);
        GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 8, r.y + 6, r.width - 16, r.height - 12),
            "🛠 CHEAT ON  —  화살표=비행(카메라 기준), Space/'>'=상, LeftCtrl/'<'=하, Shift=가속\nT=보는 곳으로 순간이동,  모든 능력 해금(기린=,-  얼음3)", hintStyle);
    }
}
