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

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (unlocked && kb.digit3Key.wasPressedThisFrame)
        {
            active = !active;
            Apply();
        }
    }

    public void Apply()
    {
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) cols[i].sharedMaterial = active ? slick : origColMats[i];
        if (iceMaterial != null)
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null) rends[i].sharedMaterial = active ? iceMaterial : origRendMats[i];
    }
}
