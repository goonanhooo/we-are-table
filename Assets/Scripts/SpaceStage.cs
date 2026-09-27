using UnityEngine;

/// <summary>
/// 우주 씬(Space.unity) 진행 스크립트.
/// 무중력은 환경 속성. 활성 능력은 CheckpointRuntime이 이전 씬에서 이어받는다.
/// 우주오징어는 기존 도입 컷씬의 획득 순간에 해금된다.
/// </summary>
public class SpaceStage : MonoBehaviour
{
    void Start()
    {
        var reset=FindAnyObjectByType<ArtSceneReset>();
        if(reset!=null)
            foreach(var body in reset.GetComponentsInChildren<ArticulationBody>(true))body.useGravity=false;
    }
}
