using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★2026-07-13 타이틀 한/영 스왑 — Title 캔버스(we/are/table Image 3개)에 부착.
/// EN = 원본 손글씨 3조각(we are table), KR = 개구체로 생성한 2조각(우리는/테이블, are는 숨김).
/// L.Korean 변경을 감지해 스프라이트/크기/위치 교체. TitleWobble은 OnEnable에서 베이스를
/// 다시 캡처하므로(같이 수정) 토글로 재초기화한다.
/// </summary>
public class TitleLocalizer : MonoBehaviour
{
    [Tooltip("한글 첫 조각(우리는)")] public Sprite krFirst;
    [Tooltip("한글 둘째 조각(테이블)")] public Sprite krSecond;
    [Tooltip("한글 조각 표시 높이(px)")] public float krHeight = 132f;   // ★2026-07-14 타이틀 확대(사용자 요청)

    Image we, are, table;
    RectTransform weRt, tableRt;
    Sprite enWe, enAre, enTable;
    Vector2 weBasePos, weBaseSize, tableBasePos, tableBaseSize;
    bool lastKorean, inited;
    CanvasGroup visibility;StartScreenMenu home;
    public bool TitleVisible=>visibility!=null&&visibility.alpha>0;

    void Awake()
    {
        we = transform.Find("we").GetComponent<Image>();
        are = transform.Find("are").GetComponent<Image>();
        table = transform.Find("table").GetComponent<Image>();
        weRt = we.rectTransform; tableRt = table.rectTransform;
        enWe = we.sprite; enAre = are.sprite; enTable = table.sprite;
        weBasePos = weRt.anchoredPosition; weBaseSize = weRt.sizeDelta;
        tableBasePos = tableRt.anchoredPosition; tableBaseSize = tableRt.sizeDelta;
        visibility=GetComponent<CanvasGroup>();if(visibility==null)visibility=gameObject.AddComponent<CanvasGroup>();
        visibility.blocksRaycasts=false;visibility.interactable=false;
        home=FindAnyObjectByType<StartScreenMenu>();
        inited = true;
        Apply();
    }

    void Update()
    {
        if (inited && lastKorean != L.Korean) Apply();
    }

    void LateUpdate()
    {
        if(visibility==null){visibility=GetComponent<CanvasGroup>();if(visibility==null)visibility=gameObject.AddComponent<CanvasGroup>();visibility.blocksRaycasts=false;visibility.interactable=false;}
        if(home==null)home=FindAnyObjectByType<StartScreenMenu>();
        if(visibility!=null)visibility.alpha=home!=null&&home.HomeVisible?1:0;
    }

    void Apply()
    {
        lastKorean = L.Korean;
        if (L.Korean && krFirst != null && krSecond != null)
        {
            are.gameObject.SetActive(false);
            we.sprite = krFirst;
            float w1 = krHeight * krFirst.rect.width / krFirst.rect.height;
            weRt.sizeDelta = new Vector2(w1, krHeight);
            weRt.anchoredPosition = weBasePos;

            table.sprite = krSecond;
            // ★2026-07-14 타이틀 정렬 통일(사용자 요청): 두 조각 같은 높이·같은 y 기준(전엔 EN table의 y를 따라 어긋남)
            float h2 = krHeight;
            float w2 = h2 * krSecond.rect.width / krSecond.rect.height;
            tableRt.sizeDelta = new Vector2(w2, h2);
            tableRt.anchoredPosition = new Vector2(weBasePos.x + w1 * 0.5f + w2 * 0.5f + 48f, weBasePos.y);   // 두 단어 사이 띄움
        }
        else
        {
            are.gameObject.SetActive(true);
            we.sprite = enWe; weRt.sizeDelta = weBaseSize; weRt.anchoredPosition = weBasePos;
            table.sprite = enTable; tableRt.sizeDelta = tableBaseSize; tableRt.anchoredPosition = tableBasePos;
            are.sprite = enAre;
        }
        // 워블 베이스 재캡처(OnEnable에서 캡처하도록 수정됨)
        foreach (var w in GetComponentsInChildren<TitleWobble>(true))
        {
            if (!w.gameObject.activeInHierarchy) continue;
            w.enabled = false; w.enabled = true;
        }
    }
}
