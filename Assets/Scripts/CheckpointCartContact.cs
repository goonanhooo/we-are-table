using UnityEngine;
public class CheckpointCartContact : MonoBehaviour
{
    void OnCollisionEnter(Collision hit)
    {
        if(hit.collider.GetComponentInParent<ArtSceneReset>()!=null)CheckpointRuntime.Milestone(7);
    }
}
