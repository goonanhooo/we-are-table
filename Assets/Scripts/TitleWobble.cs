using UnityEngine;

/// <summary>
/// 타이틀 글자(we / are / table)를 위아래로 조금씩 귀엽게 흔드는 애니메이션.
/// UI(RectTransform)면 anchoredPosition.y, 일반 Transform이면 localPosition.y를 sin으로 흔든다.
/// 단어마다 amplitude/speed/phase를 다르게 줘서 제각각 움직이게 한다.
/// timeScale=0(일시정지)에도 움직이도록 unscaledTime 사용.
/// </summary>
public class TitleWobble : MonoBehaviour
{
    [Tooltip("흔들림 크기(픽셀 또는 월드유닛)")] public float amplitude = 8f;
    [Tooltip("흔들림 속도")] public float speed = 2f;
    [Tooltip("위상차(단어마다 다르게)")] public float phase = 0f;

    RectTransform rt;
    Vector2 baseAnchored;
    Vector3 baseLocal;
    bool isUI;

    void Start()
    {
        rt = transform as RectTransform;
        if (rt != null) { isUI = true; baseAnchored = rt.anchoredPosition; }
        else baseLocal = transform.localPosition;
    }

    void Update()
    {
        float y = amplitude * Mathf.Sin(Time.unscaledTime * speed + phase);
        if (isUI) rt.anchoredPosition = baseAnchored + new Vector2(0f, y);
        else transform.localPosition = baseLocal + new Vector3(0f, y, 0f);
    }
}
