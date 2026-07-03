using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// [진단용] 테이블 '튐(pop)' 조사기 v2 — 시드 기반 무작위 퍼저 + 가설 버킷 분류.
/// 플레이 중 코드로 AddComponent 해서 사용(씬/프리팹에 저장 안 함).
///
/// 구조 분석으로 도출한 튐 가설(불연속 지점)별로 이벤트를 자동 분류한다:
///   [limit]  한계각(±90°) 근접 중 발생 — 리밋 슬램
///   [freeze] 직전 ±6스텝 내 다리 동결/해제 전환 — 밴드 스냅
///   [spin]   좌우(스핀) 입력 활성 중 — 힌지 축 재설정
///   [contact] 같은 스텝 충돌 임펄스 존재 — 지형/모서리
///   [cascade] 몸통(Table) 튐인데 접촉 없음 + 동결 다리 3개 이상 — 동결 연쇄
/// fuzz=true: 시드 RNG로 다리별 독립 무작위 입력(유지 0.15~1.2s, 무입력/스윙/스핀/조합) 주입.
/// </summary>
public class PopProbe : MonoBehaviour
{
    public float dvThreshold = 0.7f;    // 한 스텝 선속도 변화 임계(m/s)
    public float wThreshold = 6f;       // 몸통 각속도 변화 임계(rad/s) — 회전 저크도 체감 튐
    public float warmup = 5f;
    public bool fuzz;                   // 무작위 입력 퍼징
    public int seed = 12345;            // 재현용 시드

    public struct PopEvent
    {
        public float t; public string body; public float dv, dw;
        public string contact; public float impulse; public float minSep;
        public string hinges;           // "FR:88*↑ FL:12 ..." (각도, *=한계근접, ↑↓=스윙입력, F=동결)
        public string flags;            // 버킷 태그
    }
    public readonly List<PopEvent> events = new();

    Rigidbody[] bodies; Vector3[] prevV; Vector3 prevW; int tableIdx = -1;
    ContactTap[] taps;
    HingeJoint[] hinges;
    LegController[] legCtrls;           // 바디별(상판 null)
    TableInput ti;
    System.Random rng;
    float t0; int stepCount;
    // ★ 뒤집힘 처리: 2초 지속 시 '1' 리셋으로 재기립(뒤집힌 채 파닥이는 무효 데이터 방지)
    Transform tableTf; SceneReset sceneReset;
    float flippedSince = -1f, suppressUntil;
    public int resetCount;
    float nextDump = 30f;   // 30초마다 리포트 파일 자동 저장(플레이 종료로 인한 데이터 소실 방지)

    // 다리별 상태 추적(전환 감지)
    bool[] wasInput = new bool[4];          // 키셋 인덱스 기준 직전 입력 여부
    int[] lastToggleStep = new int[4];      // 마지막 동결↔해제 전환 스텝
    // 퍼저 상태
    float[] nextSwitch = new float[4];
    LegInput[] cur = new LegInput[4];

    public class ContactTap : MonoBehaviour
    {
        public string lastOther = ""; public float lastImpulse; public float minSep = 999f; public int frame = -1;
        void OnCollisionEnter(Collision c) => Take(c);
        void OnCollisionStay(Collision c) => Take(c);
        void Take(Collision c)
        {
            if (Time.frameCount != frame) { frame = Time.frameCount; lastImpulse = 0f; minSep = 999f; lastOther = ""; }
            float imp = c.impulse.magnitude;
            if (imp > lastImpulse) { lastImpulse = imp; lastOther = c.collider != null ? c.collider.gameObject.name : "?"; }
            for (int i = 0; i < c.contactCount; i++)
            { float s = c.GetContact(i).separation; if (s < minSep) minSep = s; }
        }
    }

    void Start()
    {
        var root = transform.root;
        bodies = root.GetComponentsInChildren<Rigidbody>(true);
        prevV = new Vector3[bodies.Length];
        taps = new ContactTap[bodies.Length];
        hinges = new HingeJoint[bodies.Length];
        legCtrls = new LegController[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            prevV[i] = bodies[i].linearVelocity;
            taps[i] = bodies[i].gameObject.GetComponent<ContactTap>() ?? bodies[i].gameObject.AddComponent<ContactTap>();
            hinges[i] = bodies[i].GetComponent<HingeJoint>();
            legCtrls[i] = bodies[i].GetComponent<LegController>();
            if (bodies[i].gameObject.name == "Table") { tableIdx = i; prevW = bodies[i].angularVelocity; }
        }
        ti = TableInput.Of(this);
        if (tableIdx >= 0) tableTf = bodies[tableIdx].transform;
        sceneReset = FindAnyObjectByType<SceneReset>();
        rng = new System.Random(seed);
        t0 = Time.time;
        for (int s = 0; s < 4; s++) lastToggleStep[s] = -999;
    }

    void OnDestroy()
    {
        // 주입 원복
        if (ti != null)
            for (int i = 0; i < 4; i++) { ti.SetExternal((LegControlSet)i, default); ti.SetSource((LegControlSet)i, TableInput.LegSource.Local); }
    }

    LegInput RandomInput()
    {
        // 분포: 무입력 30% / 단일스윙 30% / 스윙+스핀 20% / 단일스핀 10% / 반대조합 10%
        int r = rng.Next(100);
        var li = new LegInput();
        if (r < 30) return li;
        bool up = rng.Next(2) == 0;
        if (r < 60) { li.up = up; li.down = !up; return li; }
        bool left = rng.Next(2) == 0;
        if (r < 80) { li.up = up; li.down = !up; li.left = left; li.right = !left; return li; }
        if (r < 90) { li.left = left; li.right = !left; return li; }
        li.up = true; li.left = left; return li;
    }

    void FixedUpdate()
    {
        stepCount++;
        float now = Time.time - t0;

        if (fuzz && ti != null)
        {
            for (int s = 0; s < 4; s++)
            {
                ti.SetSource((LegControlSet)s, TableInput.LegSource.External);
                if (now >= nextSwitch[s])
                {
                    cur[s] = RandomInput();
                    nextSwitch[s] = now + 0.15f + (float)rng.NextDouble() * 1.05f;   // 0.15~1.2s 유지
                }
                ti.SetExternal((LegControlSet)s, cur[s]);
            }
        }

        // 동결↔해제 전환 추적(입력 유무 변화 기준 — LegController frozen과 동일 조건)
        for (int s = 0; s < 4; s++)
        {
            bool has = ti != null && ti.Get((LegControlSet)s).Any;
            if (has != wasInput[s]) { lastToggleStep[s] = stepCount; wasInput[s] = has; }
        }

        // 뒤집힘 감지 → 2초 지속 시 '1' 리셋(SceneReset.ResetPoseInPlace)으로 재기립
        bool flipped = tableTf != null && Vector3.Dot(tableTf.up, Vector3.up) < 0.2f;
        if (flipped) { if (flippedSince < 0f) flippedSince = now; }
        else flippedSince = -1f;
        if (flipped && flippedSince >= 0f && now - flippedSince > 2f && sceneReset != null)
        {
            sceneReset.ResetPoseInPlace();
            resetCount++;
            flippedSince = -1f;
            suppressUntil = now + 1.5f;   // 리셋 순간이동/정착 스파이크는 기록 제외
        }

        // 30초마다 리포트를 파일로 저장(진행 중 데이터 보존)
        if (now >= nextDump)
        {
            nextDump = now + 30f;
            try { System.IO.File.WriteAllText(Application.dataPath + "/../PopFuzzReport.txt", Report(25)); } catch { }
        }

        // 기록 게이트: 워밍업/뒤집힘/리셋 직후 구간은 무효 데이터 — 기록 안 함
        if (now < warmup || flipped || now < suppressUntil) { Snapshot(); return; }

        for (int i = 0; i < bodies.Length; i++)
        {
            var rb = bodies[i]; if (rb == null) continue;
            Vector3 v = rb.linearVelocity;
            float dv = (v - prevV[i]).magnitude;
            float dw = 0f;
            if (i == tableIdx) { dw = (rb.angularVelocity - prevW).magnitude; }
            bool pop = (dv > dvThreshold || dw > wThreshold) && float.IsFinite(dv) && float.IsFinite(dw);
            if (!pop) continue;

            var tap = taps[i];
            bool sameFrame = tap != null && tap.frame == Time.frameCount;
            string hingeStr = HingeSummary();
            string flags = Classify(i, hingeStr, sameFrame);
            events.Add(new PopEvent
            {
                t = now, body = rb.gameObject.name, dv = dv, dw = dw,
                contact = sameFrame ? tap.lastOther : "",
                impulse = sameFrame ? tap.lastImpulse : 0f,
                minSep = sameFrame ? tap.minSep : 999f,
                hinges = hingeStr, flags = flags,
            });
        }
        Snapshot();
    }

    string HingeSummary()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < bodies.Length; i++)
        {
            var h = hinges[i]; if (h == null) continue;
            float a = h.angle;
            var lc = legCtrls[i];
            int set = lc != null ? (int)lc.controlSet : -1;
            bool input = set >= 0 && ti != null && ti.Get((LegControlSet)set).Any;
            sb.Append(bodies[i].gameObject.name.Replace("Leg_", "").Replace("_Pivot", ""))
              .Append(':').Append(a.ToString("F0"))
              .Append(Mathf.Abs(a) > 85f ? "*" : "")
              .Append(input ? "" : "F")     // F=무입력(동결 상태)
              .Append(' ');
        }
        return sb.ToString();
    }

    string Classify(int bodyIdx, string hingeStr, bool hasContact)
    {
        var f = new StringBuilder();
        if (hingeStr.Contains("*")) f.Append("[limit]");
        // 직전 6스텝 내 어떤 다리든 동결/해제 전환?
        for (int s = 0; s < 4; s++)
            if (stepCount - lastToggleStep[s] <= 6) { f.Append("[freeze:").Append((LegControlSet)s).Append(']'); break; }
        // 스핀 입력 활성?
        for (int s = 0; s < 4; s++)
        { var li = ti != null ? ti.Get((LegControlSet)s) : default; if (li.left || li.right) { f.Append("[spin]"); break; } }
        if (hasContact) f.Append("[contact]");
        // 몸통 튐 + 접촉 없음 + 동결 다리 3개 이상 = 동결 연쇄 의심
        if (bodyIdx == tableIdx && !hasContact)
        {
            int frozenCount = 0;
            for (int c = 0; c < hingeStr.Length; c++) if (hingeStr[c] == 'F') frozenCount++;
            if (frozenCount >= 3) f.Append("[cascade]");
        }
        if (f.Length == 0) f.Append("[other]");
        return f.ToString();
    }

    void Snapshot()
    {
        for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null) prevV[i] = bodies[i].linearVelocity;
        if (tableIdx >= 0 && bodies[tableIdx] != null) prevW = bodies[tableIdx].angularVelocity;
    }

    public string Report(int top = 15)
    {
        var sb = new StringBuilder();
        sb.Append("이벤트 ").Append(events.Count).Append("건 / ").Append(stepCount).Append("스텝 (seed=").Append(seed).Append(", 뒤집힘리셋 ").Append(resetCount).Append("회)\n");
        // 버킷 집계(중복 태그 각각 카운트) — 몸통(Table) 이벤트만 따로도
        var buckets = new Dictionary<string, int>(); var tableBuckets = new Dictionary<string, int>();
        int tableEvents = 0;
        foreach (var e in events)
        {
            bool isTable = e.body == "Table";
            if (isTable) tableEvents++;
            foreach (var tag in e.flags.Split(']'))
            {
                if (tag.Length == 0) continue;
                string k = tag.TrimStart('[').Split(':')[0];
                buckets[k] = buckets.TryGetValue(k, out var n) ? n + 1 : 1;
                if (isTable) tableBuckets[k] = tableBuckets.TryGetValue(k, out var m) ? m + 1 : 1;
            }
        }
        sb.Append("전체 버킷: ");
        foreach (var kv in buckets) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("  ");
        sb.Append("\n몸통(Table=").Append(tableEvents).Append("건) 버킷: ");
        foreach (var kv in tableBuckets) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("  ");
        sb.Append("\n— 상위(몸통 우선, Δv/Δw 큰 순) —\n");
        events.Sort((a, b) => (b.body == "Table" ? 1 : 0) * 100 + b.dv + b.dw > (a.body == "Table" ? 1 : 0) * 100 + a.dv + a.dw ? 1 : -1);
        for (int i = 0; i < Mathf.Min(top, events.Count); i++)
        {
            var e = events[i];
            sb.Append("t=").Append(e.t.ToString("F1")).Append(' ').Append(e.body)
              .Append(" Δv=").Append(e.dv.ToString("F1")).Append(" Δw=").Append(e.dw.ToString("F1"))
              .Append(' ').Append(e.flags)
              .Append(" 접촉=").Append(e.contact == "" ? "-" : e.contact)
              .Append(" imp=").Append(e.impulse.ToString("F1"))
              .Append(" [").Append(e.hinges).Append("]\n");
        }
        return sb.ToString();
    }
}
