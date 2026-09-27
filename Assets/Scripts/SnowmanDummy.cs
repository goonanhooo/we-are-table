using UnityEngine;

/// <summary>
/// 눈사람(일단 더미). 테이블이 가까이 오면 '얼음 능력'을 부여한다(IcePower.unlocked = true). 한 번만.
/// 부여되면 살짝 위아래로 반기는 연출 + 콘솔 로그. 실제 대화/연출은 나중에.
/// </summary>
public class SnowmanDummy : MonoBehaviour
{
    [Tooltip("테이블 강체가 이 거리 안이면 능력 부여.")]
    public float grantRadius = 4.5f;

    Rigidbody[] tableBodies;
    IcePower power;
    bool granted;
    Vector3 baseScale;
    float t;

    void Start()
    {
        var tp = GameObject.Find("TablePlayer");
        if (tp != null)
        {
            tableBodies = tp.GetComponentsInChildren<Rigidbody>(true);
            power = tp.GetComponent<IcePower>();
        }
        baseScale = transform.localScale;
    }

    void Update()
    {
        if (granted)
        {
            // 획득 축하 살짝 통통
            t += Time.deltaTime;
            float s = 1f + Mathf.Sin(t * 6f) * 0.04f * Mathf.Clamp01(2f - t);
            transform.localScale = baseScale * s;
            return;
        }
        if (tableBodies == null || power == null) return;
        Vector3 p = transform.position;
        foreach (var rb in tableBodies)
        {
            if (rb == null) continue;
            if (Vector3.Distance(rb.position, p) < grantRadius)
            {
                granted = true;
                power.unlocked = true;
                AbilityHud.Acquire(power, "ice");
                Debug.Log("⛄ 눈사람의 얼음 능력 획득! 4번 키로 사용.");
                break;
            }
        }
    }
}
