using UnityEngine;

/// <summary>
/// 스카이 레이어 앵커(2026-07-08): 매 프레임 카메라 '위치'만 따라간다(회전은 안 따라감).
/// 자식(원거리 은하 쿼드 등)이 카메라와 함께 평행이동해 시차가 0 —
/// 스카이박스 별과 똑같이 '무한히 먼 배경'처럼 보인다.
/// (은하 쿼드가 월드 고정이면 이동할 때 별보다 가까워 보이는 시차가 생김 — 사용자 지적.)
/// </summary>
[DefaultExecutionOrder(1000)]   // CameraFollow(LateUpdate)보다 뒤에 실행 — 1프레임 지연 시차 방지
public class SkyAnchor : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null) transform.position = cam.transform.position;
    }
}
