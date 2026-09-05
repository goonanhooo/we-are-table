using UnityEngine;

/// <summary>
/// 우주 씬(Space.unity) 진행 스크립트.
/// - 입장 시 우주오징어 모드 부여(정식 해금 방식 미정 — 임시로 씬이 켬).
/// - 기린 모드 잠금: 신축 키(E/Y/O/])가 오징어 분사 키와 같아 충돌 방지(치트로는 해제 가능).
/// 무중력 자체는 SquidMode(active)가 전 바디 useGravity=false로 적용한다.
/// </summary>
public class SpaceStage : MonoBehaviour
{
    void Start()
    {
        var squid = FindAnyObjectByType<SquidMode>();
        // ★2026-09-05 도입 컷씬(SpaceIntroCutscene)이 있으면 무중력만 켜고 분사는 잠금 — 오징어가 힘을 주면 해제
        bool intro = FindAnyObjectByType<SpaceIntroCutscene>() != null;
        if (squid != null) { squid.unlocked = true; squid.active = true; squid.thrustLocked = intro; }
        var g = FindAnyObjectByType<GiraffeMode>();
        if (g != null) g.locked = true;
    }
}
