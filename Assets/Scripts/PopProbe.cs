using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// [진단용] 테이블 '튕김(pop)' 기록기. 플레이 중 코드로 AddComponent 해서 사용(씬/프리팹에 저장 안 함).
/// - 매 물리스텝 5개 바디의 속도 변화(Δv)를 감시 — 임계 초과 시 '팝 이벤트'로 기록.
/// - 각 바디에 ContactTap을 붙여 같은 스텝의 충돌 상대/임펄스/침투(separation)를 캡처.
/// - walk=true 면 TableInput External로 대각 걸음 패턴을 주입해 자동 보행 테스트.
/// Report()로 상위 이벤트 요약을 얻는다.
/// </summary>
public class PopProbe : MonoBehaviour
{
    public float dvThreshold = 0.7f;   // 한 스텝(0.01s) 속도 변화 임계(m/s)
    public float warmup = 5f;          // 시작(낙하 착지) 무시 구간
    public bool walk;                  // 자동 보행 주입

    public struct PopEvent
    {
        public float t; public string body; public float dv; public Vector3 v0, v1;
        public string contact; public float impulse; public float minSep; public Vector3 pos;
        public string hinges;   // 이벤트 순간 4다리 힌지각/한계근접 요약 (예: "FR:89.7*")
    }
    public readonly List<PopEvent> events = new();

    Rigidbody[] bodies; Vector3[] prevV;
    ContactTap[] taps;
    HingeJoint[] hinges;   // 바디별 힌지(상판은 null)
    TableInput ti;
    float t0; int stepCount;

    string HingeSummary()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < bodies.Length; i++)
        {
            var h = hinges[i]; if (h == null) continue;
            float a = h.angle;
            bool nearLimit = Mathf.Abs(a) > 87f;   // swingLimit 90 기준 한계 근접
            sb.Append(bodies[i].gameObject.name.Replace("Leg_", "").Replace("_Pivot", ""))
              .Append(':').Append(a.ToString("F0")).Append(nearLimit ? "*" : "").Append(' ');
        }
        return sb.ToString();
    }

    // 같은 스텝의 접촉 정보 캡처(바디별)
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
            {
                float s = c.GetContact(i).separation;
                if (s < minSep) minSep = s;
            }
        }
    }

    void Start()
    {
        var root = transform.root;
        bodies = root.GetComponentsInChildren<Rigidbody>(true);
        prevV = new Vector3[bodies.Length];
        taps = new ContactTap[bodies.Length];
        hinges = new HingeJoint[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            prevV[i] = bodies[i].linearVelocity;
            taps[i] = bodies[i].gameObject.GetComponent<ContactTap>();
            if (taps[i] == null) taps[i] = bodies[i].gameObject.AddComponent<ContactTap>();
            hinges[i] = bodies[i].GetComponent<HingeJoint>();
        }
        ti = TableInput.Of(this);
        t0 = Time.time;
    }

    void FixedUpdate()
    {
        stepCount++;
        // 자동 보행: 대각선 다리 교대(0.45s 주기) — 접촉/스텝 이벤트를 대량 생성
        if (walk && ti != null)
        {
            // ★ 사용자 재현 시나리오: '한 방향으로 계속 밀기' — 네 다리 모두 같은 방향으로 2.4s씩 길게 유지.
            //   다리가 땅을 밀어 몸이 나가고, 다리가 한계각(±90°)까지 가서 '더 밀 것이 없는' 상태를 만든다.
            bool phase = Mathf.FloorToInt((Time.time - t0) / 2.4f) % 2 == 0;
            ti.SetSource(LegControlSet.WASD, TableInput.LegSource.External);
            ti.SetSource(LegControlSet.TFGH, TableInput.LegSource.External);
            ti.SetSource(LegControlSet.IJKL, TableInput.LegSource.External);
            ti.SetSource(LegControlSet.Arrows, TableInput.LegSource.External);
            var all = new LegInput { up = phase, down = !phase };
            ti.SetExternal(LegControlSet.WASD, all);
            ti.SetExternal(LegControlSet.Arrows, all);
            ti.SetExternal(LegControlSet.TFGH, all);
            ti.SetExternal(LegControlSet.IJKL, all);
        }

        if (Time.time - t0 < warmup) { Snapshot(); return; }

        for (int i = 0; i < bodies.Length; i++)
        {
            var rb = bodies[i]; if (rb == null) continue;
            Vector3 v = rb.linearVelocity;
            float dv = (v - prevV[i]).magnitude;
            if (dv > dvThreshold && float.IsFinite(dv))
            {
                var tap = taps[i];
                bool sameFrame = tap != null && tap.frame == Time.frameCount;
                events.Add(new PopEvent
                {
                    t = Time.time - t0, body = rb.gameObject.name, dv = dv, v0 = prevV[i], v1 = v,
                    contact = sameFrame ? tap.lastOther : "(접촉없음=조인트/모터 유발)",
                    impulse = sameFrame ? tap.lastImpulse : 0f,
                    minSep = sameFrame ? tap.minSep : 999f,
                    pos = rb.position,
                    hinges = HingeSummary(),
                });
            }
        }
        Snapshot();
    }

    void Snapshot() { for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null) prevV[i] = bodies[i].linearVelocity; }

    public string Report(int top = 20)
    {
        var sb = new StringBuilder();
        sb.Append("팝 이벤트 ").Append(events.Count).Append("건 / ").Append(stepCount).Append("스텝 (").Append((Time.time - t0).ToString("F0")).Append("s)\n");
        // 상대별 집계
        var byContact = new Dictionary<string, int>();
        var byBody = new Dictionary<string, int>();
        foreach (var e in events)
        {
            byContact[e.contact] = byContact.TryGetValue(e.contact, out var n) ? n + 1 : 1;
            byBody[e.body] = byBody.TryGetValue(e.body, out var m) ? m + 1 : 1;
        }
        sb.Append("바디별: ");
        foreach (var kv in byBody) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("  ");
        sb.Append("\n접촉상대별: ");
        foreach (var kv in byContact) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("  ");
        sb.Append("\n— 상위 이벤트 —\n");
        events.Sort((a, b) => b.dv.CompareTo(a.dv));
        for (int i = 0; i < Mathf.Min(top, events.Count); i++)
        {
            var e = events[i];
            sb.Append("t=").Append(e.t.ToString("F1")).Append(" ").Append(e.body)
              .Append(" Δv=").Append(e.dv.ToString("F1"))
              .Append(" (").Append(e.v0.y.ToString("F1")).Append("→").Append(e.v1.y.ToString("F1")).Append("y)")
              .Append(" 상대=").Append(e.contact)
              .Append(" imp=").Append(e.impulse.ToString("F1"))
              .Append(" sep=").Append(e.minSep < 900f ? e.minSep.ToString("F3") : "-")
              .Append(" 힌지[").Append(e.hinges).Append(']')
              .Append('\n');
        }
        return sb.ToString();
    }
}
