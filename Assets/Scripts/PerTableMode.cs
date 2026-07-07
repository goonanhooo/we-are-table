using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ★ 1인 1테이블 멀티 모드(2026-07-08) — 씬에 미리 놓인 테이블 4대를 접속 순서대로 1인당 1대씩 배정.
/// 각 플레이어는 자기 테이블의 '네 다리 전부'를 조작(자기 키보드의 WASD/TFGH/IJKL/Arrows 풀 키셋).
/// 구현: 테이블마다 이미 붙어 있는 NetLegLink를 재사용 — AssignAllServer(clientId)로 4다리 일괄 배정.
///  - 물리는 전 테이블 호스트 시뮬(기존 구조 그대로), 게스트는 자기 테이블 입력만 서버로 릴레이.
///  - 남는 테이블은 서버가 Despawn(전 클라 제거).
///  - 로컬 처리: 내 테이블에 카메라 타깃, 남의 테이블 PauseMenu/CheatMode/Bgm 끔,
///    LegColorXray는 전부 끔(GameObject.Find 이름 전역검색이라 다중 테이블에서 오작동).
///  - 슬롯 비콘: 각 테이블 위에 플레이어 색 구슬(파/빨/초/노) — RaceStage/SoccerStage가 색 덮어쓸 수 있음.
/// 씬 배치: "PerTableMode" GO + NetworkObject, tables[]에 TablePlayer 4개 연결. 싱글로 열어도 1대만 남아 정상.
/// </summary>
public class PerTableMode : NetworkBehaviour
{
    const ulong NONE = ulong.MaxValue;
    public static PerTableMode Instance { get; private set; }

    [Tooltip("배정 순서대로 테이블 루트(TablePlayer 인스턴스) 4개")]
    public GameObject[] tables = new GameObject[4];

    static readonly Color[] SlotColors =
    { new Color(0.25f, 0.5f, 1f), new Color(1f, 0.3f, 0.25f), new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.85f, 0.25f) };

    readonly NetworkVariable<NetLegLink.LegOwners> slotOwners = new(
        new NetLegLink.LegOwners { s0 = NONE, s1 = NONE, s2 = NONE, s3 = NONE },
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    Transform[] beacons;
    bool localApplied;
    bool soloCleaned;

    /// <summary>슬롯 소유 clientId(없으면 ulong.MaxValue).</summary>
    public ulong OwnerOfSlot(int i) => slotOwners.Value.Get(i);
    public bool SlotUsed(int i) => slotOwners.Value.Get(i) != NONE;
    public int UsedSlots { get { int n = 0; for (int i = 0; i < 4; i++) if (SlotUsed(i)) n++; return n; } }
    public int MySlot
    {
        get
        {
            if (NetworkManager == null) return -1;
            for (int i = 0; i < 4; i++)
                if (slotOwners.Value.Get(i) == NetworkManager.LocalClientId) return i;
            return -1;
        }
    }
    public string SlotName(int i)
    {
        ulong v = OwnerOfSlot(i);
        if (v == NONE) return "-";
        return v == NetworkManager.ServerClientId ? "호스트" : "P" + v;
    }

    /// <summary>트리거에 들어온 콜라이더가 몇 번 슬롯 테이블인지(-1=테이블 아님/미배정).</summary>
    public int SlotOfCollider(Collider c)
    {
        var root = c.transform.root;
        for (int i = 0; i < 4; i++)
            if (tables[i] != null && tables[i].transform == root) return i;
        return -1;
    }

    /// <summary>슬롯 테이블의 ArticulationBody 루트(board).</summary>
    public ArticulationBody BoardOf(int slot)
    {
        if (tables[slot] == null) return null;
        foreach (var ab in tables[slot].GetComponentsInChildren<ArticulationBody>(true))
            if (ab.isRoot) return ab;
        return null;
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (IsServer) Assign();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Assign()
    {
        // 접속 순서(호스트 먼저)로 슬롯 배정
        var ids = new System.Collections.Generic.List<ulong>(NetworkManager.ConnectedClientsIds);
        ids.Remove(NetworkManager.LocalClientId);
        ids.Sort();
        ids.Insert(0, NetworkManager.LocalClientId);

        var o = new NetLegLink.LegOwners { s0 = NONE, s1 = NONE, s2 = NONE, s3 = NONE };
        for (int i = 0; i < tables.Length; i++)
        {
            if (tables[i] == null) continue;
            if (i < ids.Count)
            {
                o.Set(i, ids[i]);
                var link = tables[i].GetComponentInChildren<NetLegLink>(true);
                if (link != null) link.AssignAllServer(ids[i]);
            }
            else
            {
                var no = tables[i].GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true);
                else Destroy(tables[i]);
                tables[i] = null;
            }
        }
        slotOwners.Value = o;
        Debug.Log("[PerTableMode] assigned slots=" + ids.Count);
    }

    void Update()
    {
        // 싱글(세션 없음)로 열었을 때: 스폰이 없으니 테이블 1대만 남기고 정리(2초 유예 — 세션 시작과 경합 방지)
        if (!IsSpawned && !soloCleaned && Time.timeSinceLevelLoad > 2f
            && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
        {
            soloCleaned = true;
            for (int i = 1; i < tables.Length; i++)
                if (tables[i] != null) { Destroy(tables[i]); tables[i] = null; }
            return;
        }
        if (localApplied || !IsSpawned || MySlot < 0) return;
        localApplied = true;
        ApplyLocal();
    }

    void ApplyLocal()
    {
        int my = MySlot;
        var cam = FindAnyObjectByType<CameraFollow>();
        var board = BoardOf(my);
        if (cam != null && board != null) { cam.target = board.transform; cam.targetName = ""; }

        beacons = new Transform[4];
        for (int i = 0; i < tables.Length; i++)
        {
            if (tables[i] == null) continue;
            bool mine = i == my;
            foreach (var pm in tables[i].GetComponentsInChildren<PauseMenu>(true)) pm.enabled = mine;
            foreach (var cm in tables[i].GetComponentsInChildren<CheatMode>(true)) cm.enabled = mine;
            foreach (var bg in tables[i].GetComponentsInChildren<Bgm>(true)) bg.enabled = mine;
            foreach (var lx in tables[i].GetComponentsInChildren<LegColorXray>(true)) lx.enabled = false;

            // 슬롯 비콘(상판 위 색 구슬)
            var b = BoardOf(i);
            if (b != null)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.name = "SlotBeacon_" + i;
                s.transform.SetParent(b.transform, false);
                s.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                s.transform.localScale = Vector3.one * 0.22f;
                SetBeaconColor(s, SlotColors[i]);
                beacons[i] = s.transform;
            }
        }
    }

    /// <summary>스테이지가 팀 색 등으로 비콘 색을 바꿀 때 사용.</summary>
    public void TintBeacon(int slot, Color c)
    {
        if (beacons == null || beacons[slot] == null) return;
        SetBeaconColor(beacons[slot].gameObject, c);
    }

    static void SetBeaconColor(GameObject s, Color c)
    {
        var r = s.GetComponent<MeshRenderer>();
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * 1.6f);
        r.material = m;   // 런타임 전용(씬 저장 안 함)
    }
}
