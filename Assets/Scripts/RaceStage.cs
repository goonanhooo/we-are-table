using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ★ 멀티 경주 스테이지(Race.unity) — 1인 1테이블(PerTableMode) 위에서 트랙 한 바퀴 순위전.
/// - 출발 4초 카운트다운(서버 시간 기준 — 전 클라 동일) 동안 출발선 배리어 콜라이더 유지, GO에 개방.
/// - 반환 게이트(midGate, 트랙 반대편) 통과자만 결승 인정(역주행/제자리 방지 — Track 골 규칙과 동일).
/// - 완주 순서/기록을 NetworkVariable로 발행 → 전원 순위표 표시. 전원 완주 12초 후 자동 재경주.
/// 트리거는 RaceGateRelay(gateId 1=반환, 2=결승)가 이 스크립트로 전달(서버만 판정).
/// </summary>
public class RaceStage : NetworkBehaviour
{
    public PerTableMode mode;
    [Tooltip("슬롯별 출발 지점(재경주 배치)")]
    public Transform[] startPoints = new Transform[4];
    [Tooltip("출발선 배리어(카운트다운 동안 켜짐)")]
    public GameObject startBarrier;
    public float countdown = 4f;
    public float restartDelay = 12f;

    struct RaceNet : INetworkSerializable, System.IEquatable<RaceNet>
    {
        public double start;            // 출발 시각(서버 시간)
        public uint order;              // 완주 순서: 4비트씩 slot+1
        public float t0, t1, t2, t3;    // 슬롯별 기록(0=미완주)
        public byte phase;              // 0=주행(카운트다운 포함) 1=전원 완주
        public float T(int i) => i == 0 ? t0 : i == 1 ? t1 : i == 2 ? t2 : t3;
        public void SetT(int i, float v) { if (i == 0) t0 = v; else if (i == 1) t1 = v; else if (i == 2) t2 = v; else t3 = v; }
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref start); s.SerializeValue(ref order);
            s.SerializeValue(ref t0); s.SerializeValue(ref t1); s.SerializeValue(ref t2); s.SerializeValue(ref t3);
            s.SerializeValue(ref phase);
        }
        public bool Equals(RaceNet o) => start == o.start && order == o.order && phase == o.phase
            && t0 == o.t0 && t1 == o.t1 && t2 == o.t2 && t3 == o.t3;
    }

    readonly NetworkVariable<RaceNet> net = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    readonly bool[] passedMid = new bool[4];
    double restartAt = -1;
    float myBannerUntil;
    int myRank;

    double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    float Clock => (float)(Now - net.Value.start);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            var n = net.Value;
            n.start = Now + countdown;
            net.Value = n;
        }
    }

    /// <summary>RaceGateRelay가 전달(서버만 유효). gateId: 1=반환, 2=결승.</summary>
    public void OnGate(int gateId, Collider other)
    {
        if (!IsServer || !IsSpawned || mode == null) return;
        var n = net.Value;
        if (n.phase != 0 || Clock < 0f) return;
        int slot = mode.SlotOfCollider(other);
        if (slot < 0) return;

        if (gateId == 1) { passedMid[slot] = true; return; }
        if (!passedMid[slot] || n.T(slot) > 0f) return;

        n.SetT(slot, Clock);
        for (int p = 0; p < 4; p++)                       // order의 빈 자리에 기록
            if (((n.order >> (p * 4)) & 0xF) == 0) { n.order |= (uint)(slot + 1) << (p * 4); break; }

        bool all = true;
        for (int i = 0; i < 4; i++)
            if (mode.SlotUsed(i) && n.T(i) <= 0f) all = false;
        if (all) { n.phase = 1; restartAt = Now + restartDelay; }
        net.Value = n;
    }

    void Update()
    {
        if (!IsSpawned) return;
        if (startBarrier != null && startBarrier.activeSelf != (Clock < 0f))
            startBarrier.SetActive(Clock < 0f);

        // 내 완주 순간 배너
        int mine = mode != null ? mode.MySlot : -1;
        if (mine >= 0 && myRank == 0)
        {
            int r = RankOf(mine);
            if (r > 0) { myRank = r; myBannerUntil = Time.unscaledTime + 4f; }
        }

        if (!IsServer) return;
        if (net.Value.phase == 1 && restartAt > 0 && Now >= restartAt) Restart();
    }

    int RankOf(int slot)
    {
        for (int p = 0; p < 4; p++)
            if (((net.Value.order >> (p * 4)) & 0xF) == (uint)(slot + 1)) return p + 1;
        return 0;
    }

    void Restart()
    {
        restartAt = -1;
        for (int i = 0; i < 4; i++)
        {
            passedMid[i] = false;
            if (!mode.SlotUsed(i) || startPoints[i] == null) continue;
            var asr = mode.tables[i] != null ? mode.tables[i].GetComponentInChildren<ArtSceneReset>(true) : null;
            if (asr != null) asr.ResetPoseAtXZ(startPoints[i].position.x, startPoints[i].position.z, 30f);
        }
        net.Value = new RaceNet { start = Now + countdown };
        myRank = 0;
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (!IsSpawned || mode == null) return;
        var n = net.Value;
        float c = Clock;

        // 카운트다운 / GO
        if (n.phase == 0 && c < 1f)
        {
            string msg = c < 0f ? Mathf.CeilToInt(-c).ToString() : "GO!";
            var r = UiKit.Chip(Screen.width * 0.5f, Screen.height * 0.32f, 180f, 84f);
            GUI.Label(r, msg, UiKit.Title);
        }
        // 경과 시계
        if (n.phase == 0 && c >= 0f)
        {
            var r = UiKit.Chip(Screen.width * 0.5f, 12f, 150f, 34f);
            GUI.Label(r, StageTimer.Fmt(c), UiKit.Btn);
        }

        // 순위표(좌상단)
        float y = 12f;
        UiKit.Fill(new Rect(10f, y, 210f, 26f * (CountRows() + 1) + 10f), new Color(1f, 1f, 1f, 0.85f));
        GUI.Label(new Rect(20f, y + 4f, 190f, 24f), "— 순위 —", UiKit.Btn);
        y += 30f;
        for (int p = 0; p < 4; p++)
        {
            uint v = (n.order >> (p * 4)) & 0xF;
            if (v == 0) continue;
            int slot = (int)v - 1;
            GUI.Label(new Rect(20f, y, 190f, 24f), (p + 1) + "위  " + mode.SlotName(slot) + "  " + StageTimer.Fmt(n.T(slot)), UiKit.Label);
            y += 26f;
        }
        for (int i = 0; i < 4; i++)
        {
            if (!mode.SlotUsed(i) || n.T(i) > 0f) continue;
            GUI.Label(new Rect(20f, y, 190f, 24f), "—    " + mode.SlotName(i), UiKit.Label);
            y += 26f;
        }

        if (myRank > 0 && Time.unscaledTime < myBannerUntil)
        {
            var r = UiKit.Chip(Screen.width * 0.5f, Screen.height * 0.24f, 300f, 70f);
            GUI.Label(r, myRank + "위!", UiKit.Title);
        }
        if (n.phase == 1)
        {
            var r = UiKit.Chip(Screen.width * 0.5f, Screen.height * 0.14f, 360f, 46f);
            GUI.Label(r, "경주 종료 — 곧 재시작합니다", UiKit.Btn);
        }
    }

    int CountRows()
    {
        int rows = 0;
        for (int i = 0; i < 4; i++) if (mode.SlotUsed(i)) rows++;
        return Mathf.Max(rows, 1);
    }
}
