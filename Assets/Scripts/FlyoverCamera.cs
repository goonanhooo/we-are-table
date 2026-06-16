using UnityEngine;

/// <summary>
/// 시작 화면 배경용 카메라. 한 지점을 바라보며 공중에서 천천히 원을 그리듯 훑는다.
/// </summary>
public class FlyoverCamera : MonoBehaviour
{
    [Tooltip("바라볼 중심점")]
    public Vector3 lookAt = new Vector3(0f, 0.3f, 3f);
    [Tooltip("중심으로부터 수평 거리")]
    public float radius = 11f;
    [Tooltip("높이")]
    public float height = 11f;
    [Tooltip("도는 속도(도/초)")]
    public float speed = 6f;

    float angle;

    void Start()
    {
        // 시작 화면에선 커서 보이게(버튼 클릭용)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LateUpdate()
    {
        angle += speed * Time.deltaTime;
        float r = angle * Mathf.Deg2Rad;
        transform.position = lookAt + new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)) * radius + Vector3.up * height;
        transform.LookAt(lookAt);
    }
}
