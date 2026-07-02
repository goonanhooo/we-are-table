using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ★ 멀티(호스트 권한) 2단계: 다리 배정 + 게스트 입력 RPC. Table.prefab 루트(NetworkObject 옆)에 부착.
///
/// - 물리는 호스트에서만 돈다(게스트 바디는 NetworkRigidbody가 kinematic으로 만들고 NetworkTransform이 포즈 수신).
/// - 게스트는 자기에게 배정된 다리(키셋)의 로컬 키보드만 읽어 서버로 보낸다(SendInputRpc).
/// - 호스트는 받은 입력을 TableInput.SetExternal로 주입 → LegController는 로컬/원격을 구분하지 않는다.
/// - 배정: 게스트 접속 순서대로 TFGH(앞오른발) → IJKL(뒤왼발) → Arrows(뒤오른발). 호스트=WASD+남는 다리 전부.
///   (로비 다리 '선택' UI는 다음 단계 — 지금은 자동 배정. 중간 난입 차단도 로비 단계에서.)
/// - 게스트 퇴장 → 그 다리는 다시 호스트 로컬로. 호스트 퇴장 → 게스트 쪽 NetDebugPanel이 세션 종료 처리.
/// </summary>
public class NetLegLink : NetworkBehaviour
{
    const ulong NONE = ulong.MaxValue;

    /// <summary>다리(키셋 인덱스 0~3) → 소유 클라이언트 id. NONE=호스트 로컬.</summary>
    public struct LegOwners : INetworkSerializable, System.IEquatable<LegOwners>
    {
        public ulong s0, s1, s2, s3;   // LegControlSet 순서(WASD, TFGH, IJKL, Arrows)
        public ulong Get(int i) => i == 0 ? s0 : i == 1 ? s1 : i == 2 ? s2 : s3;
        public void Set(int i, ulong v) { if (i == 0) s0 = v; else if (i == 1) s1 = v; else if (i == 2) s2 = v; else s3 = v; }
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref s0); s.SerializeValue(ref s1); s.SerializeValue(ref s2); s.SerializeValue(ref s3); }
        public bool Equals(LegOwners o) => s0 == o.s0 && s1 == o.s1 && s2 == o.s2 && s3 == o.s3;
    }

    readonly NetworkVariable<LegOwners> owners = new(
        new LegOwners { s0 = NONE, s1 = NONE, s2 = NONE, s3 = NONE },
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 게스트 접속 순서대로 배정할 다리(호스트는 WASD 유지)
    static readonly LegControlSet[] AssignOrder = { LegControlSet.TFGH, LegControlSet.IJKL, LegControlSet.Arrows };

    TableInput tableInput;
    int myLeg = -1;              // 게스트: 내가 배정받은 다리(키셋 인덱스). -1=없음
    byte lastSent = 255;         // 마지막으로 보낸 입력(변화 있을 때만 전송)
    float lastHeartbeat;

    public LegControlSet? MyLeg => myLeg >= 0 ? (LegControlSet)myLeg : (LegControlSet?)null;
    public string OwnersDebug()
    {
        var o = owners.Value; var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 4; i++)
            sb.Append((LegControlSet)i).Append('=').Append(o.Get(i) == NONE ? "호스트" : "P" + o.Get(i)).Append(i < 3 ? "  " : "");
        return sb.ToString();
    }

    public override void OnNetworkSpawn()
    {
        tableInput = TableInput.Of(this);
        owners.OnValueChanged += OnOwnersChanged;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
        if (IsClient && !IsServer) ApplyGuestMode();
        OnOwnersChanged(default, owners.Value);
    }

    public override void OnNetworkDespawn()
    {
        owners.OnValueChanged -= OnOwnersChanged;
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        // 세션 종료 → 전 다리 로컬 복귀(호스트가 싱글로 이어서 조작 가능)
        if (tableInput != null)
            for (int i = 0; i < 4; i++) tableInput.SetSource((LegControlSet)i, TableInput.LegSource.Local);
    }

    // ── 서버: 접속/퇴장 시 다리 배정 ──
    void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.LocalClientId) return;   // 호스트 자신
        var o = owners.Value;
        foreach (var set in AssignOrder)
        {
            int i = (int)set;
            if (o.Get(i) == NONE) { o.Set(i, clientId); owners.Value = o; return; }
        }
        Debug.Log("[NetLegLink] 빈 다리 없음 — 관전만: client " + clientId);
    }

    void OnClientDisconnected(ulong clientId)
    {
        var o = owners.Value; bool ch = false;
        for (int i = 0; i < 4; i++)
            if (o.Get(i) == clientId) { o.Set(i, NONE); ch = true; }
        if (ch) owners.Value = o;
    }

    // ── 배정 변경 반영: 호스트=입력 소스 전환, 게스트=내 다리 파악 ──
    void OnOwnersChanged(LegOwners _, LegOwners o)
    {
        if (IsServer && tableInput != null)
            for (int i = 0; i < 4; i++)
                tableInput.SetSource((LegControlSet)i,
                    o.Get(i) == NONE ? TableInput.LegSource.Local : TableInput.LegSource.External);

        if (IsClient && !IsServer)
        {
            myLeg = -1;
            for (int i = 0; i < 4; i++)
                if (o.Get(i) == NetworkManager.LocalClientId) myLeg = i;
        }
    }

    // ── 게스트: 자기 다리 입력을 서버로 (변화 시 즉시 + 0.2s 하트비트) ──
    void Update()
    {
        if (!IsSpawned || IsServer || myLeg < 0) return;
        byte packed = Pack(TableInput.PollKeyset((LegControlSet)myLeg));
        if (packed != lastSent || Time.unscaledTime - lastHeartbeat > 0.2f)
        {
            lastSent = packed; lastHeartbeat = Time.unscaledTime;
            SendInputRpc((byte)myLeg, packed);
        }
    }

    [Rpc(SendTo.Server)]
    void SendInputRpc(byte leg, byte packed, RpcParams rpc = default)
    {
        if (leg > 3 || tableInput == null) return;
        // 검증: 보낸 클라이언트가 실제 그 다리 주인인가
        if (owners.Value.Get(leg) != rpc.Receive.SenderClientId) return;
        tableInput.SetExternal((LegControlSet)leg, Unpack(packed));
    }

    static byte Pack(LegInput i) => (byte)((i.up ? 1 : 0) | (i.down ? 2 : 0) | (i.left ? 4 : 0) | (i.right ? 8 : 0) | (i.longer ? 16 : 0) | (i.shorter ? 32 : 0));
    static LegInput Unpack(byte b) => new LegInput
    { up = (b & 1) != 0, down = (b & 2) != 0, left = (b & 4) != 0, right = (b & 8) != 0, longer = (b & 16) != 0, shorter = (b & 32) != 0 };

    // ── 게스트 모드: 물리/치트/능력 스크립트는 호스트 전용 — 게스트에선 끔(포즈는 NetworkTransform 수신) ──
    void ApplyGuestMode()
    {
        var root = transform.root;
        foreach (var lc in root.GetComponentsInChildren<LegController>(true)) lc.enabled = false;
        Toggle<SceneReset>(root, false);
        Toggle<PlaytestCheat>(root, false);
        Toggle<GiraffeMode>(root, false);
        Toggle<IcePower>(root, false);
        Toggle<NaNBlackBox>(root, false);
    }

    static void Toggle<T>(Transform root, bool on) where T : MonoBehaviour
    { foreach (var c in root.GetComponentsInChildren<T>(true)) c.enabled = on; }
}
