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

    // 게임 시작 여부(호스트가 로비에서 시작 누름) — 시작 후엔 다리 변경 금지, 세션도 잠김(난입 차단)
    readonly NetworkVariable<bool> started = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    TableInput tableInput;
    int myLeg = -1;              // 게스트: 내가 배정받은 다리(키셋 인덱스). -1=없음
    byte lastSent = 255;         // 마지막으로 보낸 입력(변화 있을 때만 전송)
    float lastHeartbeat;

    public LegControlSet? MyLeg => myLeg >= 0 ? (LegControlSet)myLeg : (LegControlSet?)null;
    public bool Started => started.Value;
    /// <summary>다리 주인 표시용: -2=빈 자리, -1=호스트, 그 외=clientId.</summary>
    public long OwnerOf(int leg)
    {
        ulong v = owners.Value.Get(leg);
        if (v == NONE) return -2;
        if (NetworkManager != null && v == NetworkManager.LocalClientId && IsServer) return -1;
        return (long)v;
    }
    public bool IsMine(int leg) => NetworkManager != null && owners.Value.Get(leg) == NetworkManager.LocalClientId;
    public string OwnersDebug()
    {
        var o = owners.Value; var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 4; i++)
            sb.Append((LegControlSet)i).Append('=').Append(o.Get(i) == NONE ? "빈자리" : "P" + o.Get(i)).Append(i < 3 ? "  " : "");
        return sb.ToString();
    }

    /// <summary>로비에서 다리 선택(모든 클라 호출 가능 — 서버가 검증). 이미 주인 있으면 무시, 내 기존 다리는 해제.</summary>
    [Rpc(SendTo.Server)]
    public void ClaimLegRpc(byte leg, RpcParams rpc = default)
    {
        if (leg > 3 || started.Value) return;                       // 시작 후 변경 금지
        ulong who = rpc.Receive.SenderClientId;
        var o = owners.Value;
        if (o.Get(leg) != NONE && o.Get(leg) != who) return;        // 남의 자리
        for (int i = 0; i < 4; i++) if (o.Get(i) == who) o.Set(i, NONE);   // 내 기존 자리 해제
        o.Set(leg, who);
        owners.Value = o;
    }

    /// <summary>호스트: 게임 시작 — 다리 고정 + 세션 잠금(중간 난입 차단).</summary>
    public void StartGame()
    {
        if (!IsServer || started.Value) return;
        started.Value = true;
        _ = NetSession.LockAsync();
    }

    public override void OnNetworkSpawn()
    {
        tableInput = TableInput.Of(this);
        owners.OnValueChanged += OnOwnersChanged;

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        if (IsClient && !IsServer) ApplyGuestMode();
        OnOwnersChanged(default, owners.Value);
    }

    public override void OnNetworkDespawn()
    {
        owners.OnValueChanged -= OnOwnersChanged;
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        // 세션 종료 → 전 다리 로컬 복귀(호스트가 싱글로 이어서 조작 가능)
        if (tableInput != null)
            for (int i = 0; i < 4; i++) tableInput.SetSource((LegControlSet)i, TableInput.LegSource.Local);
    }

    // ── 서버: 퇴장 시 그 사람 다리 해제(호스트 로컬 복귀) ──
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

        // 내 다리 파악(호스트 포함 — 표시용. 게스트는 입력 전송에도 사용)
        myLeg = -1;
        for (int i = 0; i < 4; i++)
            if (o.Get(i) == NetworkManager.LocalClientId) myLeg = i;
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

    // ── 게스트 모드: 물리/치트 스크립트는 호스트 전용 — 게스트에선 끔(포즈는 NetworkTransform 수신).
    //    GiraffeMode/IcePower는 끄지 않고 netVisualOnly — NetVisualSync가 시각(길이·스킨·재질)만 반영. ──
    void ApplyGuestMode()
    {
        var root = transform.root;
        foreach (var lc in root.GetComponentsInChildren<LegController>(true)) lc.enabled = false;
        // ★ ArticulationBody 테이블: 게스트는 시뮬 끄고 NetworkTransform 포즈 수신만(자식→루트 역순 비활성)
        foreach (var alc in root.GetComponentsInChildren<ArtLegController>(true)) alc.enabled = false;
        var abs2 = root.GetComponentsInChildren<ArticulationBody>(true);
        for (int i = abs2.Length - 1; i >= 0; i--) abs2[i].enabled = false;
        Toggle<SceneReset>(root, false);
        Toggle<PlaytestCheat>(root, false);
        Toggle<NaNBlackBox>(root, false);
        foreach (var g in root.GetComponentsInChildren<GiraffeMode>(true)) g.netVisualOnly = true;
        foreach (var p in root.GetComponentsInChildren<IcePower>(true)) p.netVisualOnly = true;
    }

    static void Toggle<T>(Transform root, bool on) where T : MonoBehaviour
    { foreach (var c in root.GetComponentsInChildren<T>(true)) c.enabled = on; }
}
