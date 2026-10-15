using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ★ 멀티(호스트 권한) 2단계: 다리 배정 + 게스트 입력 RPC. Table.prefab 루트(NetworkObject 옆)에 부착.
///
/// - 물리는 호스트에서만 돈다(게스트 바디는 NetworkRigidbody가 kinematic으로 만들고 NetworkTransform이 포즈 수신).
/// - 게스트는 자기에게 배정된 다리(키셋)의 로컬 키보드만 읽어 서버로 보낸다(SendInputRpc).
/// - 호스트는 받은 입력을 TableInput.SetExternal로 주입 → LegController는 로컬/원격을 구분하지 않는다.
/// - 배정: 게스트 접속 순서대로 TFGH(뒤오른발) → IJKL(앞왼발) → Arrows(앞오른발). 호스트=WASD+남는 다리 전부.
///   (로비 다리 '선택' UI는 다음 단계 — 지금은 자동 배정. 중간 난입 차단도 로비 단계에서.)
/// - 게스트 퇴장 → 그 다리는 다시 호스트 로컬로. 호스트 퇴장 → 게스트 쪽 NetDebugPanel이 세션 종료 처리.
/// </summary>
public partial class NetLegLink : NetworkBehaviour
{
    const ulong NONE = ulong.MaxValue;
    readonly NetworkVariable<uint> checkpointMask=new(1,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
    public uint CheckpointMask=>IsServer?CheckpointProgress.Mask:checkpointMask.Value;


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
    readonly NetworkVariable<byte> phoneMask = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int NativeMask
    {
        get { int mask=0;for(int i=0;i<4;i++)if(owners.Value.Get(i)!=NONE)mask|=1<<i;return mask; }
    }
    public int PhoneMask => phoneMask.Value;

    // 게임 시작 여부(호스트가 로비에서 시작 누름) — 시작 후엔 다리 변경 금지, 세션도 잠김(난입 차단)
    readonly NetworkVariable<bool> started = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 로비에서 호스트가 고른 맵(NetDebugPanel.Maps 인덱스, 255=미선택) — 게스트 로비에도 표시
    readonly NetworkVariable<byte> mapIndex = new(255,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ★2026-09-05 로비 난이도(0=보통, 1=겁쟁이들을 위한) — 호스트 선택, 전원 Difficulty.NetOverride로 적용
    readonly NetworkVariable<byte> difficulty = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ★세션 상태 캐시(서버 static): NetworkVariable은 NetLegLink '인스턴스'에 살아서
    //   씬 전환(StartGame→NetScene.Load, 정글→빙산 등)마다 새 인스턴스에서 리셋된다 —
    //   로비가 시작 화면으로 오면서(2026-07-07) 시작 직후 배정이 날아가는 치명타가 됨.
    //   서버가 바뀔 때마다 여기 적어두고 새 인스턴스 OnNetworkSpawn에서 복원한다.
    //   새 세션 시작 때 ResetSessionCache()로 비움(LevelSelect 방 만들기/NetDebugPanel 나가기).
    static bool sHasCache;
    static LegOwners sOwners;
    static bool sStarted;
    static byte sMap = 255;
    static byte sDiff;
    public static void ResetSessionCache() { savedNames.Clear(); sHasCache = false; sStarted = false; sMap = 255; sDiff = 0; Difficulty.NetOverride = -1; }
    /// <summary>캐시 켜기 — ⚠️처음 켤 때 sOwners를 현재 값(전부 NONE)으로 채움: 기본 struct(전부 0=클라 0 소유)가
    /// 씬 전환 후 복원되면 호스트 다리가 External 소스로 잠기는 함정(맵/난이도만 고르고 다리 안 고른 경우).</summary>
    void Cache()
    {
        if (!sHasCache) sOwners = owners.Value;
        sHasCache = true;
    }

    TableInput tableInput;
    int myMask;                       // 내가 소유한 다리 비트마스크(게스트 입력 전송 + 표시)
    readonly byte[] lastSent = { 255, 255, 255, 255 };   // 다리별 마지막 전송(변화 시만 전송)
    readonly float[] lastHeartbeat = new float[4];

    public bool Started => started.Value;
    /// <summary>로비에서 호스트가 고른 맵 인덱스(-1=미선택).</summary>
    public int MapIndex => mapIndex.Value == 255 ? -1 : mapIndex.Value;
    /// <summary>로비 난이도(Difficulty.Mode 값).</summary>
    public int DifficultyIndex => difficulty.Value;

    /// <summary>호스트: 로비에서 난이도 선택 — 전원 동기화 + 호스트 로컬 기본값에도 저장.</summary>
    public void SetDifficulty(int d)
    {
        if (!IsServer || started.Value) return;
        d = Mathf.Clamp(d, 0, 1);
        difficulty.Value = (byte)d;
        sDiff = (byte)d; Cache();
        Difficulty.Local = (Difficulty.Mode)d;
    }

    void OnDifficultyChanged(byte _, byte v) => Difficulty.NetOverride = v;
    /// <summary>다리 주인 표시용: -2=빈 자리, -1=호스트, 그 외=clientId.</summary>
    public long OwnerOf(int leg)
    {
        ulong v = owners.Value.Get(leg);
        if (v == NONE) return (phoneMask.Value & (1 << leg)) != 0 ? -3 : -2;
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

    /// <summary>로비에서 다리 선택 — ★토글(모든 클라 호출 가능, 서버 검증):
    /// 빈 자리 클릭=획득(기존 자리 유지 → 한 명이 여러 다리 가능), 내 자리 클릭=해제, 남의 자리=무시.</summary>
    [Rpc(SendTo.Server)]
    public void ClaimLegRpc(byte leg, RpcParams rpc = default)
    {
        if (!IsServer || leg > 3 || started.Value || ChoosingMap || !NetworkManager.ConnectedClients.ContainsKey(rpc.Receive.SenderClientId)) return;                       // 시작 후 변경 금지
        if ((phoneMask.Value & (1 << leg)) != 0 || (PhoneControllerBridge.Instance != null && PhoneControllerBridge.Instance.HasPlayer(leg))) return;
        ulong who = rpc.Receive.SenderClientId;
        var o = owners.Value;
        ulong cur = o.Get(leg);
        if (cur == who) o.Set(leg, NONE);                           // 내 자리 → 해제(토글)
        else if (cur == NONE) o.Set(leg, who);                      // 빈 자리 → 획득(여러 개 OK)
        else return;                                                // 남의 자리 → 무시
        SetOwners(o);
    }

    /// <summary>★서버(1인 1테이블 모드): 이 테이블의 네 다리를 전부 한 클라이언트에 배정.
    /// 호스트 자신 테이블이면 NONE(=로컬 폴링). ⚠️로비 세션 캐시(sOwners)는 건드리지 않음 —
    /// 협동 맵으로 돌아가면 로비 배정이 복원되도록.</summary>
    public void AssignAllServer(ulong clientId)
    {
        if (!IsServer) return;
        ulong v = clientId == NetworkManager.LocalClientId ? NONE : clientId;
        owners.Value = new LegOwners { s0 = v, s1 = v, s2 = v, s3 = v };
    }

    /// <summary>호스트: 로비에서 맵 선택(NetDebugPanel.Maps 인덱스) — 게스트 로비에도 표시.</summary>
    public void SetMap(int i)
    {
        if (!IsServer || started.Value || !CheckpointProgress.IsUnlocked(i)) return;
        mapIndex.Value = (byte)i;
        sMap = (byte)i; Cache();
    }

    /// <summary>호스트: 게임 시작 — 다리 고정 + 세션 잠금(중간 난입 차단). 씬 로드는 호출 쪽(NetDebugPanel).</summary>
    public void StartGame()
    {
        if (!IsServer || started.Value || !ChoosingMap || !CanChooseMap || !CheckpointProgress.IsUnlocked(MapIndex)) return;
        started.Value = true;
        sStarted = true; Cache();
        _ = NetSession.LockAsync();
    }

    void SetOwners(LegOwners o)
    {
        owners.Value = o;
        sOwners = o; sHasCache = true;
    }

    public override void OnNetworkSpawn()
    {
        NetScene.IntentionalLeave=false;
        tableInput = TableInput.Of(this);
        owners.OnValueChanged += OnOwnersChanged;

        if (IsServer)
        {
            if (PhoneControllerBridge.Instance != null) PhoneControllerBridge.WebPlayActive = true;
            checkpointMask.Value=CheckpointProgress.Mask;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            // ★씬 전환으로 새로 스폰된 인스턴스: 이전 씬의 배정/시작/맵/난이도 상태 복원
            if (sHasCache)
            {
                owners.Value = sOwners;
                started.Value = sStarted;
                mapIndex.Value = sMap;
                difficulty.Value = sDiff;
            }
            else
            {
                // 새 세션의 기본 난이도 = 호스트가 시작 화면에서 마지막으로 고른 값
                difficulty.Value = (byte)Difficulty.Local;
                sDiff = difficulty.Value;
            }
        }
        // ★난이도는 전원(호스트 포함)이 세션 값으로 덮어씀 — 게스트는 스폰 시 이미 동기화된 값
        difficulty.OnValueChanged += OnDifficultyChanged;
        Difficulty.NetOverride = difficulty.Value;
        if (IsClient && !IsServer) ApplyGuestMode();
        OnOwnersChanged(default, owners.Value);
        SpawnLobby();
    }

    public override void OnNetworkDespawn()
    {
        DespawnLobby();
        owners.OnValueChanged -= OnOwnersChanged;
        difficulty.OnValueChanged -= OnDifficultyChanged;
        // 세션이 끝나며 사라질 때만 난이도 덮어쓰기 해제(씬 전환 중 디스폰은 새 인스턴스가 다시 적용)
        if (NetworkManager == null || !NetworkManager.IsListening) Difficulty.NetOverride = -1;
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        // 세션 종료 → 전 다리 로컬 복귀(호스트가 싱글로 이어서 조작 가능)
        if (tableInput != null)
            for (int i = 0; i < 4; i++) tableInput.SetSource((LegControlSet)i, TableInput.LegSource.Local);
    }

    // ── 서버: 퇴장 시 그 사람 다리 전부 해제(호스트 로컬 복귀) ──
    void OnClientDisconnected(ulong clientId)
    {
        RemoveLobbyPlayer(clientId);
        var o = owners.Value; bool ch = false;
        for (int i = 0; i < 4; i++)
            if (o.Get(i) == clientId) { o.Set(i, NONE); ch = true; }
        if (ch) SetOwners(o);
    }

    // ── 배정 변경 반영: 호스트=입력 소스 전환, 각자=내 다리 마스크 갱신 ──
    void OnOwnersChanged(LegOwners _, LegOwners o)
    {
        if (IsServer && tableInput != null)
            for (int i = 0; i < 4; i++)
                tableInput.SetSource((LegControlSet)i,
                    (o.Get(i) == NONE && (phoneMask.Value & (1 << i)) == 0) || o.Get(i) == NetworkManager.LocalClientId
                        ? TableInput.LegSource.Local : TableInput.LegSource.External);

        // 내 다리 마스크(호스트 포함 — 표시용. 게스트는 입력 전송에도 사용)
        myMask = 0;
        for (int i = 0; i < 4; i++)
            if (o.Get(i) == NetworkManager.LocalClientId) myMask |= 1 << i;
    }

    // ── 게스트: 내가 소유한 '모든' 다리의 입력을 서버로 (다리별 변화 시 즉시 + 0.2s 하트비트) ──
    void Update()
    {
        if (!IsSpawned) return;
        if (IsServer)
        {
            byte mask=0;var bridge=PhoneControllerBridge.Instance;
            if(bridge!=null&&PhoneControllerBridge.WebPlayActive)
                for(int i=0;i<4;i++)if(bridge.HasPlayer(i) && (NativeMask & (1<<i))==0)mask|=(byte)(1<<i);
            if(phoneMask.Value!=mask)phoneMask.Value=mask;
            return;
        }
        if (myMask == 0) return;
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        var giraffe=GetComponent<GiraffeMode>();
        byte group=0;
        if(started.Value && giraffe!=null && giraffe.active && !giraffe.locked && !PauseMenu.InputBlocked && !LetterOverlay.IsOpen)
            group=(byte)((giraffe.AllLongerHeld?1:0)|(giraffe.AllShorterHeld?2:0));
        if(group!=lastGroupStretch || (group!=0&&Time.unscaledTime-nextGroupStretch>.1f))
        {lastGroupStretch=group;nextGroupStretch=Time.unscaledTime;GroupStretchRpc(group);}
        if(started.Value&&keyboard!=null&&!PauseMenu.InputBlocked&&!LetterOverlay.IsOpen)
            for(int key=1;key<=5;key++)if(keyboard[(UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.Digit1+key-1)].wasPressedThisFrame)RequestPowerRpc((byte)key);
        for (int i = 0; i < 4; i++)
        {
            if ((myMask & (1 << i)) == 0) continue;
            byte packed = Pack(TableInput.PollKeyset((LegControlSet)i));
            if (packed != lastSent[i] || Time.unscaledTime - lastHeartbeat[i] > 0.2f)
            {
                lastSent[i] = packed; lastHeartbeat[i] = Time.unscaledTime;
                SendInputRpc((byte)i, packed);
            }
        }
    }

    byte lastGroupStretch;float nextGroupStretch;
    [Rpc(SendTo.Server)]
    void GroupStretchRpc(byte flags,RpcParams rpc=default)
    {
        if(!IsServer||!started.Value||flags>3)return;
        bool owns=false;for(int i=0;i<4;i++)if(owners.Value.Get(i)==rpc.Receive.SenderClientId)owns=true;
        if(!owns)return;
        var g=GetComponent<GiraffeMode>();if(g==null||g.locked)return;
        if(flags==0){g.SetGroupStretch(rpc.Receive.SenderClientId,false,false);return;}
        if(!g.active||!AbilityHud.CanReadInput(g))return;
        g.SetGroupStretch(rpc.Receive.SenderClientId,(flags&1)!=0,(flags&2)!=0);
    }

    [Rpc(SendTo.Server)]
    public void RequestPowerRpc(byte key,RpcParams rpc=default)
    {
        if(!IsServer||!started.Value||key<1||key>5||Time.timeScale<=0)return;
        bool owns=false;for(int i=0;i<4;i++)if(owners.Value.Get(i)==rpc.Receive.SenderClientId)owns=true;
        if(!owns)return;
        var reset=GetComponent<ArtSceneReset>();
        var table=GetComponentsInChildren<ArticulationBody>();foreach(var b in table)if(b.isRoot&&b.immovable)return;
        var cam=Camera.main;var follow=cam!=null?cam.GetComponent<CameraFollow>():null;if(follow!=null&&!follow.enabled)return;
        var g=GetComponent<GiraffeMode>();var t=GetComponent<TennisMode>();var ice=GetComponent<IcePower>();var squid=GetComponent<SquidMode>();
        if(key==1){if(reset!=null)reset.TryPoseReset();}
        else if(key==2&&g!=null&&!g.locked){g.active=!g.active;if(g.active)AbilitySelection.Select(g);}
        else if(key==3&&t!=null&&t.unlocked)t.SetActive(!t.active);
        else if(key==4&&ice!=null&&ice.unlocked)ice.SetActive(!ice.active);
        else if(key==5&&squid!=null&&squid.unlocked&&!squid.thrustLocked){squid.active=!squid.active;if(squid.active)AbilitySelection.Select(squid);}
    }

    [Rpc(SendTo.Server)]
    void SendInputRpc(byte leg, byte packed, RpcParams rpc = default)
    {
        if (leg > 3 || tableInput == null) return;
        // 검증: 보낸 클라이언트가 실제 그 다리 주인인가
        if (owners.Value.Get(leg) != rpc.Receive.SenderClientId) return;
        tableInput.SetExternal((LegControlSet)leg, Unpack(packed));
    }

    static byte Pack(LegInput i) => (byte)((i.up ? 1 : 0) | (i.down ? 2 : 0) | (i.left ? 4 : 0) | (i.right ? 8 : 0) | (i.longer ? 16 : 0) | (i.shorter ? 32 : 0) | (i.grab ? 64 : 0) | (i.release ? 128 : 0));
    static LegInput Unpack(byte b) => new LegInput
    { up = (b & 1) != 0, down = (b & 2) != 0, left = (b & 4) != 0, right = (b & 8) != 0, longer = (b & 16) != 0, shorter = (b & 32) != 0, grab = (b & 64) != 0, release = (b & 128) != 0 };

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
        Toggle<ArtSceneReset>(root, false);   // ★ 게스트: '1' 리셋/워치독도 호스트 전용(TeleportRoot 충돌 방지)
        Toggle<NaNBlackBox>(root, false);
        foreach (var s in root.GetComponentsInChildren<SquidMode>(true)) s.netVisualOnly = true;   // ★우주오징어: 물리는 호스트, 분사 파티클만 호스트 마스크로 표시
        foreach (var g in root.GetComponentsInChildren<GiraffeMode>(true)) g.netVisualOnly = true;
        foreach (var p in root.GetComponentsInChildren<IcePower>(true)) p.netVisualOnly = true;
        foreach (var t in root.GetComponentsInChildren<TennisMode>(true)) t.netVisualOnly = true;   // ★테니스공: 시각만(붙기 물리는 호스트)
        // 씬 레벨 스크립트: 추락 리스폰도 호스트 물리 기준(게스트에서 이중 리셋 방지)
        var fr = Object.FindAnyObjectByType<FallRespawn>();
        if (fr != null) fr.enabled = false;
    }

    static void Toggle<T>(Transform root, bool on) where T : MonoBehaviour
    { foreach (var c in root.GetComponentsInChildren<T>(true)) c.enabled = on; }
}
