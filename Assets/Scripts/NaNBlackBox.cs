using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// [진단용] NaN 폭발 블랙박스. TablePlayer 루트에 부착.
/// 매 물리 스텝(FixedUpdate) 테이블 5개 강체의 상태를 링버퍼에 기록하다가,
/// 어느 바디든 비유한(NaN/∞) 값이 감지되면 '폭발 직전 N스텝'을 프로젝트 루트에 파일로 덤프한다.
/// → "position is infinite" 가 났을 때 이 덤프를 보면 어떤 다리/어떤 상태(길이·힌지각·속도)에서
///   폭발이 시작됐는지 데이터로 확정할 수 있다.
/// SceneReset 워치독(복구)보다 먼저 실행되도록 실행순서 -10000 (복구가 상태를 덮기 전에 캡처).
/// 오버헤드: 사전할당 링버퍼만 사용, 평상시 할당 0. 원인 확정 후 제거해도 됨.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class NaNBlackBox : MonoBehaviour
{
    [Tooltip("기록할 물리 스텝 수(0.01s 스텝 기준 240=2.4초)")]
    public int bufferSteps = 240;
    [Tooltip("덤프 최소 간격(초) — 같은 폭발로 연속 덤프 방지")]
    public float dumpCooldown = 10f;

    Rigidbody[] bodies;
    HingeJoint[] hinges;      // 각 바디의 힌지(상판은 null)
    Transform[] legMeshes;    // 다리 메시(스케일 기록용, 상판은 null)
    Rigidbody board;

    // 링버퍼(사전할당)
    struct Snap
    {
        public float time;
        public Vector3 pos, vel, angVel;
        public float hingeAngle, motorVel, motorForce, limMin, limMax, legScaleY;
        public float comY, angDamp;   // 상판 전용
    }
    Snap[,] ring;             // [step, body]
    int head; int count;
    float lastDump = -999f;

    void Start()
    {
        var root = transform.root;
        bodies = root.GetComponentsInChildren<Rigidbody>(true);
        int n = bodies.Length;
        hinges = new HingeJoint[n];
        legMeshes = new Transform[n];
        for (int i = 0; i < n; i++)
        {
            hinges[i] = bodies[i].GetComponent<HingeJoint>();
            if (hinges[i] != null && board == null) board = hinges[i].connectedBody;
            // 다리 메시 = 피벗의 첫 번째 자식 중 Renderer 있는 것
            foreach (Transform ch in bodies[i].transform)
                if (ch.GetComponent<Renderer>() != null) { legMeshes[i] = ch; break; }
        }
        ring = new Snap[Mathf.Max(30, bufferSteps), n];
    }

    static bool Fin(float f) => float.IsFinite(f);
    static bool Fin(Vector3 v) => Fin(v.x) && Fin(v.y) && Fin(v.z);

    void FixedUpdate()
    {
        if (bodies == null || ring == null) return;
        int n = bodies.Length;
        int steps = ring.GetLength(0);

        // ① 비유한 감지(복구 전 상태) → 덤프
        bool bad = false;
        for (int i = 0; i < n && !bad; i++)
        {
            var rb = bodies[i]; if (rb == null) continue;
            if (!Fin(rb.position) || !Fin(rb.linearVelocity) || !Fin(rb.angularVelocity)
                || !Fin(rb.rotation.x) || !Fin(rb.rotation.y) || !Fin(rb.rotation.z) || !Fin(rb.rotation.w))
                bad = true;
        }
        if (bad && Time.time - lastDump > dumpCooldown)
        {
            lastDump = Time.time;
            Dump();
        }

        // ② 현재 상태 기록(비유한 스텝은 기록하지 않음 — 버퍼에 '직전 정상 기록' 보존)
        if (!bad)
        {
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i]; if (rb == null) continue;
                Snap s;
                s.time = Time.time;
                s.pos = rb.position; s.vel = rb.linearVelocity; s.angVel = rb.angularVelocity;
                var hj = hinges[i];
                if (hj != null)
                {
                    s.hingeAngle = hj.angle;
                    s.motorVel = hj.motor.targetVelocity; s.motorForce = hj.motor.force;
                    s.limMin = hj.limits.min; s.limMax = hj.limits.max;
                }
                else { s.hingeAngle = s.motorVel = s.motorForce = s.limMin = s.limMax = 0f; }
                s.legScaleY = legMeshes[i] != null ? legMeshes[i].localScale.y : 0f;
                if (rb == board) { s.comY = rb.centerOfMass.y; s.angDamp = rb.angularDamping; }
                else { s.comY = 0f; s.angDamp = 0f; }
                ring[head, i] = s;
            }
            head = (head + 1) % steps;
            if (count < steps) count++;
        }
    }

    void Dump()
    {
        int n = bodies.Length;
        int steps = ring.GetLength(0);
        var sb = new StringBuilder(1 << 16);
        sb.AppendLine("=== NaN 블랙박스 덤프 ===");
        sb.AppendLine("씬=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            + "  time=" + Time.time.ToString("F3") + "  frame=" + Time.frameCount);
        sb.AppendLine("-- 현재(폭발) 상태 --");
        for (int i = 0; i < n; i++)
        {
            var rb = bodies[i]; if (rb == null) continue;
            sb.AppendLine(rb.gameObject.name
                + " pos=" + rb.position.ToString("F3") + " vel=" + rb.linearVelocity.ToString("F2")
                + " angVel=" + rb.angularVelocity.ToString("F2") + " rot=" + rb.rotation.ToString("F3")
                + " inertia=" + rb.inertiaTensor.ToString("F4") + " com=" + rb.centerOfMass.ToString("F3"));
        }
        sb.AppendLine();
        sb.AppendLine("-- 직전 " + Mathf.Min(count, 80) + "스텝 (오래된 → 최신) --");
        sb.AppendLine("형식: t | 바디: |v| |w| hingeAngle lim[min,max] motor(v,F) scaleY (상판: comY angDamp pos.y)");
        int show = Mathf.Min(count, 80);
        for (int k = show - 1; k >= 0; k--)
        {
            int idx = (head - 1 - k + steps * 2) % steps;
            sb.Append("t=" + ring[idx, 0].time.ToString("F3"));
            for (int i = 0; i < n; i++)
            {
                var s = ring[idx, i];
                var rb = bodies[i]; if (rb == null) continue;
                if (hinges[i] == null)
                    sb.Append("  |" + rb.gameObject.name + " y=" + s.pos.y.ToString("F2")
                        + " v=" + s.vel.magnitude.ToString("F1") + " w=" + s.angVel.magnitude.ToString("F1")
                        + " comY=" + s.comY.ToString("F2") + " aD=" + s.angDamp.ToString("F2"));
                else
                    sb.Append("  |" + rb.gameObject.name.Replace("Leg_", "").Replace("_Pivot", "")
                        + " v=" + s.vel.magnitude.ToString("F1") + " w=" + s.angVel.magnitude.ToString("F1")
                        + " a=" + s.hingeAngle.ToString("F1") + " lim[" + s.limMin.ToString("F1") + "," + s.limMax.ToString("F1") + "]"
                        + " m(" + s.motorVel.ToString("F0") + "," + s.motorForce.ToString("F0") + ")"
                        + " sY=" + s.legScaleY.ToString("F2"));
            }
            sb.AppendLine();
        }
        string path = Path.Combine(Application.dataPath, "..",
            "NaNDump_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        try { File.WriteAllText(path, sb.ToString()); }
        catch (System.Exception e) { Debug.LogError("[NaNBlackBox] 덤프 저장 실패: " + e.Message); return; }
        Debug.LogError("[NaNBlackBox] NaN 폭발 감지 — 직전 기록 덤프: " + path);
    }
}
