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
        public byte stuck;   // ★2026-08-20 테니스공 붙은 다리 비트마스크
        public float l0, l1, l2, l3;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref giraffe); s.SerializeValue(ref ice); s.SerializeValue(ref tennis); s.SerializeValue(ref stuck);
            s.SerializeValue(ref l0); s.SerializeValue(ref l1); s.SerializeValue(ref l2); s.SerializeValue(ref l3);
        }
        public bool Equals(VState o) =>
            giraffe == o.giraffe && ice == o.ice && tennis == o.tennis && stuck == o.stuck
            && Mathf.Abs(l0 - o.l0) < 1e-4f && Mathf.Abs(l1 - o.l1) < 1e-4f
            && Mathf.Abs(l2 - o.l2) < 1e-4f && Mathf.Abs(l3 - o.l3) < 1e-4f;
    }

    readonly NetworkVariable<VState> state = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    GiraffeMode gm;
    IcePower ip;
    TennisMode tm;

    public override void OnNetworkSpawn()
    {
        gm = transform.root.GetComponentInChildren<GiraffeMode>(true);
        ip = transform.root.GetComponentInChildren<IcePower>(true);
        tm = transform.root.GetComponentInChildren<TennisMode>(true);
    }

    void Update()
    {
        if (!IsSpawned) return;
        if (IsServer)
        {
            var s = new VState
            {
                giraffe = gm != null && gm.active,
                ice = ip != null && ip.active,
                tennis = tm != null && tm.active,
                stuck = (byte)(tm != null ? tm.StuckMask : 0),
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
            if (gm != null) gm.NetApplyVisual(s.giraffe, s.l0, s.l1, s.l2, s.l3);
            if (ip != null) ip.NetSetIceVisual(s.ice);
            if (tm != null) tm.NetApplyVisual(s.tennis, s.stuck);
        }
    }
}
