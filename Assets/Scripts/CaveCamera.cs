using UnityEngine;

/// <summary>
/// 테이블(보드)이 동굴 방 AABB 안에 들어오면 메인 카메라를 '동굴 방을 비추는 고정 시점'으로 전환하고,
/// 나가면 다시 CameraFollow(추적 카메라)로 복귀한다. 트리거 콜라이더 없이 위치 판정만 사용.
/// 동굴 방을 하나의 정해진 구도로 보여주기 위함(인테리어 연출).
/// </summary>
public class CaveCamera : MonoBehaviour
{
    [Header("동굴 방 영역(월드 AABB) — 이 안에 보드가 있으면 고정 카메라")]
    public Vector3 roomCenter = new Vector3(0f, 32f, 0f);
    public Vector3 roomSize = new Vector3(34f, 16f, 30f);

    [Header("고정 카메라 시점")]
    public Vector3 camPos = new Vector3(0f, 37f, 28f);
    public Vector3 camLook = new Vector3(0f, 30f, 2f);
    [Tooltip("전환 부드럽게(0=즉시).")]
    public float blend = 6f;

    Transform board;
    Camera cam;
    MonoBehaviour follow;   // CameraFollow (있으면)
    bool inside;

    void Start()
    {
        cam = Camera.main;
        if (cam != null) follow = cam.GetComponent("CameraFollow") as MonoBehaviour;
        var b = GameObject.Find("Table");
        if (b != null) board = b.transform;
    }

    bool Inside(Vector3 p)
    {
        Vector3 d = p - roomCenter;
        return Mathf.Abs(d.x) <= roomSize.x * 0.5f
            && Mathf.Abs(d.y) <= roomSize.y * 0.5f
            && Mathf.Abs(d.z) <= roomSize.z * 0.5f;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        if (board == null) { var b = GameObject.Find("Table"); if (b) board = b.transform; else return; }

        bool now = Inside(board.position);
        if (now && !inside) { inside = true; if (follow != null) follow.enabled = false; }
        else if (!now && inside) { inside = false; if (follow != null) follow.enabled = true; }

        if (inside)
        {
            Quaternion targetRot = Quaternion.LookRotation((camLook - camPos).normalized, Vector3.up);
            if (blend > 0.01f)
            {
                float k = 1f - Mathf.Exp(-blend * Time.deltaTime);
                cam.transform.position = Vector3.Lerp(cam.transform.position, camPos, k);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, k);
            }
            else { cam.transform.position = camPos; cam.transform.rotation = targetRot; }
        }
    }
}
