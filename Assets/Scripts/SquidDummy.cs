using UnityEngine;

/// <summary>
/// ★2026-09-05 우주오징어 더미(임시 배우). 씬에 배치된 몸통/눈/촉수(자식 Transform 배열)를 흔든다 —
/// 촉수는 뿌리 기준으로 위상차 사인 굽힘, 몸통은 살짝 봅. 이동은 SpaceIntroCutscene이 담당.
/// ★2026-09-06 다리 4개(테이블처럼 2×2) + 발끝 분사(jets, 테이블 SquidMode 분사와 같은 SquidJet 재질) — SetThrust(k)로
/// 분사량 조절(이동 중 1, 대기 중 소량). 분사 중엔 다리를 뒤로 곧게 모아 로켓처럼.
/// 정식 모델이 오면 이 GO의 자식만 교체하면 된다(tentacles/jets 배열 비워도 무해).
/// </summary>
public class SquidDummy : MonoBehaviour
{
    [Tooltip("촉수 뿌리들(각각 -Z 뒤로 늘어진 캡슐)")] public Transform[] tentacles;
    [Tooltip("몸통(봅 대상)")] public Transform body;
    public float wiggleDeg = 22f;
    public float wiggleSpeed = 2.4f;
    public float bobAmp = 0.15f;
    /// <summary>말풍선 앵커(머리 위)</summary>
    public Transform head;
    [Tooltip("발끝 분사 파티클(촉수마다 하나, 씬 오브젝트) — 방출량은 SetThrust로")] public ParticleSystem[] jets;
    [Tooltip("분사 최대 방출량(개/초)")] public float jetRate = 140f;
    [Tooltip("분사 중 다리 흔들림 축소 비율(1=완전 곧게)")] public float thrustStiffen = 0.75f;

    Quaternion[] baseRot; Vector3 bodyBase; float phase; float thrust;

    /// <summary>분사 세기 0~1(이동 중 1) — 파티클 방출량 + 다리 자세.</summary>
    public void SetThrust(float k)
    {
        thrust = Mathf.Clamp01(k);
        if (jets == null) return;
        foreach (var j in jets) { if (j == null) continue; var em = j.emission; em.rateOverTime = jetRate * thrust; }
    }

    void Awake()
    {
        if (tentacles != null) { baseRot = new Quaternion[tentacles.Length]; for (int i = 0; i < tentacles.Length; i++) baseRot[i] = tentacles[i] != null ? tentacles[i].localRotation : Quaternion.identity; }
        if (body != null) bodyBase = body.localPosition;
        phase = Random.value * 6.28f;
    }

    void Update()
    {
        float t = Time.time * wiggleSpeed + phase;
        float wig = wiggleDeg * (1f - thrustStiffen * thrust);   // 분사 중엔 다리를 뒤로 곧게
        if (tentacles != null)
            for (int i = 0; i < tentacles.Length; i++)
            {
                var tt = tentacles[i]; if (tt == null) continue;
                float a = Mathf.Sin(t + i * 0.9f) * wig, b = Mathf.Cos(t * 0.7f + i * 1.3f) * wig * 0.5f;
                tt.localRotation = Quaternion.Slerp(baseRot[i], Quaternion.identity, thrust * 0.5f) * Quaternion.Euler(a, b, 0f);   // 분사 중 다리를 축 쪽으로 모음
            }
        if (body != null) body.localPosition = bodyBase + Vector3.up * Mathf.Sin(t * 0.6f) * bobAmp;
    }
}
