using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 눈사람에게서 얻는 '얼음 능력'. TablePlayer 루트에 부착.
/// - unlocked(눈사람 접촉으로 true) 상태에서 ★3번 키로 토글.
/// - 켜지면: 테이블 전체가 '얼음색(약간 투명)' 으로 바뀌고, 모든 콜라이더 마찰이 거의 0(slick, Minimum 결합)
///   → 힘/경사를 받으면 얼음처럼 미끄러진다. 끄면 원래 재질/마찰로 복구.
/// - 마찰 결합이 Minimum 이라, 일반 마찰 바닥/미끄럼틀 위에서도 '능력을 켰을 때만' 미끄러진다(능력이 게이트).
/// </summary>
public class IcePower : MonoBehaviour, ICheatUnlockable
{
    [Tooltip("눈사람에게서 능력을 얻었는가(획득 전엔 3번 키 무효).")]
    public bool unlocked = false;

    // 치트모드: 해금 → 3번 키 바로 먹힘.
    public void CheatUnlock() { unlocked = true; }
    public bool active = false;
    [Tooltip("능력 켤 때 테이블에 입힐 얼음(반투명) 재질.")]
    public Material iceMaterial;

    Collider[] cols;
    PhysicsMaterial[] origColMats;
    MeshRenderer[] rends;
    Material[] origRendMats;
    PhysicsMaterial slick;

    void Awake()
    {
        cols = GetComponentsInChildren<Collider>(true);
        origColMats = new PhysicsMaterial[cols.Length];
        for (int i = 0; i < cols.Length; i++) origColMats[i] = cols[i] != null ? cols[i].sharedMaterial : null;

        rends = GetComponentsInChildren<MeshRenderer>(true);
        origRendMats = new Material[rends.Length];
        for (int i = 0; i < rends.Length; i++) origRendMats[i] = rends[i] != null ? rends[i].sharedMaterial : null;

        slick = new PhysicsMaterial("IcePowerSlick")
        { dynamicFriction = 0.02f, staticFriction = 0.02f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f };

        // ★ NaN(position=∞) 방지: 얼음(마찰≈0) 모드에서 상판이 지형/슬라이드에 깊게 파묻혀
        //   무한 속도로 튕겨나가 "position vector's components is infinite" 오류가 나던 것 차단.
        //   상판 Rigidbody 의 maxLinearVelocity 가 1e16(무제한)이라 폭발 → 전 바디에 유한 상한 적용.
        //   (다리는 LegController 가 더 낮게 캡하므로 Min 으로 안전. 순서 무관.)
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null) continue;
            rb.maxLinearVelocity = Mathf.Min(rb.maxLinearVelocity, 40f);
            rb.maxAngularVelocity = Mathf.Min(rb.maxAngularVelocity, 20f);
            rb.maxDepenetrationVelocity = Mathf.Min(rb.maxDepenetrationVelocity, 3f);   // ⚠️1.2는 잔 튐 증가(반복 보정) — 3 유지
        }
    }

    // ★ 멀티 게스트 모드: 키 입력 무시 — NetVisualSync가 NetSetIceVisual로 얼음 재질만 반영.
    [HideInInspector] public bool netVisualOnly;

    /// <summary>게스트 시각 적용: 호스트의 얼음 모드 상태를 재질로 반영(마찰 변경은 kinematic이라 무해).</summary>
    public void NetSetIceVisual(bool on)
    {
        if (active == on) return;
        active = on;
        Apply();
    }

    // ★2026-09-08 능력 상호 배타: 얼음을 켜면 기린이 꺼지고, 우주오징어 중엔 얼음 금지(토글 무시·강제 OFF).
    GiraffeMode giraffeRef; SquidMode squidRef; bool refsReady;
    void Refs()
    {
        if (refsReady) return;
        refsReady = true;
        giraffeRef = transform.root.GetComponentInChildren<GiraffeMode>(true);
        squidRef = transform.root.GetComponentInChildren<SquidMode>(true);
    }

    /// <summary>얼음 모드 켜기/끄기(키·컷씬·치트 공용 진입점 — 배타 규칙 포함).</summary>
    public void SetActive(bool on)
    {
        active = on;
        Apply();
    }

    /// <summary>이 렌더러의 '얼음 이전' 원본 재질(GiraffeMode가 무늬를 입힐 때 원본을 얼음으로 오인하지 않게).</summary>
    public Material OriginalMaterialOf(Renderer r)
    {
        if (rends != null) for (int i = 0; i < rends.Length; i++) if (rends[i] == r) return origRendMats[i];
        return r != null ? r.sharedMaterial : null;
    }

    void Update()
    {
        if (netVisualOnly) return;   // ★ 멀티 게스트: 3키 토글 금지(호스트 상태를 따라감)
        Refs();
        bool squidOn = squidRef != null && squidRef.active;
        if (squidOn && active) SetActive(false);   // 우주(무중력·오징어)에선 얼음 의미 없음 + 보라 틴트와 겹침 방지
        var kb = Keyboard.current;
        if (kb == null) return;
        if (unlocked && !squidOn && kb.digit3Key.wasPressedThisFrame) SetActive(!active);
    }

    public void Apply()
    {
        Refs();
        // 켜질 때 기린은 OFF(다리는 서서히 원래 길이로 — 무늬는 다리가 다 줄면 GiraffeMode가 걷고 얼음을 다시 얹음)
        if (active && giraffeRef != null && giraffeRef.active && !giraffeRef.netVisualOnly) giraffeRef.active = false;
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) cols[i].sharedMaterial = active ? slick : origColMats[i];
        if (iceMaterial != null)
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null) rends[i].sharedMaterial = active ? iceMaterial : origRendMats[i];
        // 꺼질 때 기린 무늬가 아직 입혀져 있으면(줄어드는 중) 원본 대신 무늬로 복귀
        if (!active && giraffeRef != null && giraffeRef.Skinned) giraffeRef.ReapplySkin();
    }
}
