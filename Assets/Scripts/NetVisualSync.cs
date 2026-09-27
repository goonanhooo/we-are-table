using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ★ 멀티: 능력 '시각' 동기화(Table.prefab 루트). 물리는 호스트 전용이므로 게스트에는 겉모습만 맞춘다.
/// - 호스트: 기린 모드(active)+다리 길이 4개+얼음 모드(active)를 NetworkVariable로 발행(변화 시만).
/// - 게스트: 매 프레임 GiraffeMode.NetApplyVisual / IcePower.NetSetIceVisual 로 반영(멱등 —
///   씬 전환 직후 Start 순서와 무관하게 수렴).
/// 다리 '포즈'는 NetworkTransform(피벗), 다리 '길이/스킨/재질'은 여기가 담당.
/// </summary>
public class NetVisualSync : NetworkBehaviour
{
    public struct VState : INetworkSerializable, System.IEquatable<VState>
    {
        public bool giraffe, ice, tennis;
        public byte powers;
        public byte stuck;   // ★2026-08-20 테니스공 붙은 다리 비트마스크
        public byte squid;   // ★2026-09-06 우주오징어: bit0~3 분사 중인 다리, bit4 켜짐(보라 틴트+눈)
        public float l0, l1, l2, l3;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref powers); s.SerializeValue(ref giraffe); s.SerializeValue(ref ice); s.SerializeValue(ref tennis); s.SerializeValue(ref stuck); s.SerializeValue(ref squid);
            s.SerializeValue(ref l0); s.SerializeValue(ref l1); s.SerializeValue(ref l2); s.SerializeValue(ref l3);
        }
        public bool Equals(VState o) =>
            powers == o.powers && giraffe == o.giraffe && ice == o.ice && tennis == o.tennis && stuck == o.stuck && squid == o.squid
            && Mathf.Abs(l0 - o.l0) < 1e-4f && Mathf.Abs(l1 - o.l1) < 1e-4f
            && Mathf.Abs(l2 - o.l2) < 1e-4f && Mathf.Abs(l3 - o.l3) < 1e-4f;
    }

    readonly NetworkVariable<VState> state = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    GiraffeMode gm;
    IcePower ip;
    TennisMode tm;
    SquidMode sm;
    int knownPowers=-1;

    public override void OnNetworkSpawn()
    {
        AbilityComponents.Ensure(gameObject);
        gm = transform.root.GetComponentInChildren<GiraffeMode>(true);
        ip = transform.root.GetComponentInChildren<IcePower>(true);
        tm = transform.root.GetComponentInChildren<TennisMode>(true);
        sm = transform.root.GetComponentInChildren<SquidMode>(true);
    }

    void Update()
    {
        if (!IsSpawned) return;
        if (IsServer)
        {
            var s = new VState
            {
                powers=(byte)((gm!=null&&!gm.locked?1:0)|(tm!=null&&tm.unlocked?2:0)|(ip!=null&&ip.unlocked?4:0)|(sm!=null&&sm.unlocked&&!sm.thrustLocked?8:0)),
                giraffe = gm != null && gm.active,
                ice = ip != null && ip.active,
                tennis = tm != null && tm.active,
                stuck = (byte)(tm != null ? tm.StuckMask : 0),
                squid = (byte)(sm != null && sm.active ? (sm.JetMask | (sm.thrustLocked ? 0 : 16)) : 0),   // bit0~3 분사 다리, bit4 = 켜짐(보라+눈 외형)
                l0 = gm != null ? gm.GetLen(0) : 0f,
                l1 = gm != null ? gm.GetLen(1) : 0f,
                l2 = gm != null ? gm.GetLen(2) : 0f,
                l3 = gm != null ? gm.GetLen(3) : 0f,
            };
            if (!s.Equals(state.Value)) state.Value = s;
        }
        else
        {
            var s = state.Value;
            if(gm!=null)gm.locked=(s.powers&1)==0;
            if(tm!=null)tm.unlocked=(s.powers&2)!=0;
            if(ip!=null)ip.unlocked=(s.powers&4)!=0;
            if(sm!=null){sm.unlocked=(s.powers&8)!=0;sm.thrustLocked=(s.powers&8)==0;}
            GiraffeMode.Unlocked=(s.powers&1)!=0;TennisMode.Unlocked=(s.powers&2)!=0;SquidMode.Unlocked=(s.powers&8)!=0;
            var hud=GetComponent<AbilityHud>();
            if(knownPowers<0){if(hud!=null)hud.RestoreEarnedPowers(s.powers);}
            else
            {
                int fresh=s.powers&~knownPowers;
                if((fresh&1)!=0&&gm!=null)AbilityHud.Acquire(gm,"giraffe");
                if((fresh&2)!=0&&tm!=null)AbilityHud.Acquire(tm,"tennis");
                if((fresh&4)!=0&&ip!=null)AbilityHud.Acquire(ip,"ice");
                if((fresh&8)!=0&&sm!=null)AbilityHud.Acquire(sm,"squid");
            }
            knownPowers=s.powers;
            if (gm != null) gm.NetApplyVisual(s.giraffe, s.l0, s.l1, s.l2, s.l3);
            if (ip != null) ip.NetSetIceVisual(s.ice);
            if (tm != null) tm.NetApplyVisual(s.tennis, s.stuck);
            if (sm != null) sm.NetApplyVisual((s.squid & 16) != 0, s.squid & 15);
        }
    }
}
