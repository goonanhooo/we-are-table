using UnityEngine;

/// <summary>
/// 버튼이 눌리면 위(또는 지정 방향)로 미끄러져 열리는 문. LevelButton.onPress → Open() 호출.
/// 닫힌 위치를 시작 위치로 기록하고, Open() 시 openOffset 만큼 openSeconds 동안 이동.
/// </summary>
public class SlidingDoor : MonoBehaviour
{
    [Tooltip("열릴 때 이동량(로컬). 위로 올라가 사라지려면 (0, 문높이, 0).")]
    public Vector3 openOffset = new Vector3(0f, 4f, 0f);
    public float openSeconds = 1.6f;

    Vector3 closedPos;
    bool opening;
    float t;

    void Start() { closedPos = transform.localPosition; }

    public void Open() { opening = true; }
    public bool IsOpen => opening && t >= 1f;

    void Update()
    {
        if (!opening) return;
        t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.05f, openSeconds));
        transform.localPosition = Vector3.Lerp(closedPos, closedPos + openOffset, Mathf.SmoothStep(0f, 1f, t));
    }
}
