using UnityEngine;

/// <summary>
/// ★2026-09-05 우주오징어 더미(임시 배우). 씬에 배치된 몸통/눈/촉수(자식 Transform 배열)를 흔든다 —
/// 촉수는 뿌리 기준으로 위상차 사인 굽힘, 몸통은 살짝 봅. 이동은 SpaceIntroCutscene이 담당.
/// 정식 모델이 오면 이 GO의 자식만 교체하면 된다(tentacles 배열 비워도 무해).
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

    Quaternion[] baseRot; Vector3 bodyBase; float phase;

    void Awake()
    {
        if (tentacles != null) { baseRot = new Quaternion[tentacles.Length]; for (int i = 0; i < tentacles.Length; i++) baseRot[i] = tentacles[i] != null ? tentacles[i].localRotation : Quaternion.identity; }
        if (body != null) bodyBase = body.localPosition;
        phase = Random.value * 6.28f;
    }

    void Update()
    {
        float t = Time.time * wiggleSpeed + phase;
        if (tentacles != null)
            for (int i = 0; i < tentacles.Length; i++)
            {
                var tt = tentacles[i]; if (tt == null) continue;
                float a = Mathf.Sin(t + i * 0.9f) * wiggleDeg, b = Mathf.Cos(t * 0.7f + i * 1.3f) * wiggleDeg * 0.5f;
                tt.localRotation = baseRot[i] * Quaternion.Euler(a, b, 0f);
            }
        if (body != null) body.localPosition = bodyBase + Vector3.up * Mathf.Sin(t * 0.6f) * bobAmp;
    }
}
