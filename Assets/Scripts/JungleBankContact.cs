using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Ground contact queries for canyon arrival and fully landed poses.</summary>
public static class JungleBankContact
{
    static readonly RaycastHit[] hits=new RaycastHit[24];

    // The body has reached the far rim and at least one foot has purchase there.
    // Trailing legs may still hang over the canyon; they no longer invalidate progress.
    public static bool TryReachedSide(IslandTerrain island, Transform body, BoxCollider[] feet, out float side)
    {
        side = 0f;
        if (island == null || body == null || feet == null) return false;
        side = island.SignedSideOfRiver(body.position.x, body.position.z);
        if (side == 0f || island.DistanceToRiver(body.position.x, body.position.z) < island.CanyonRimHalf - .35f) return false;
        var physics = body.gameObject.scene.GetPhysicsScene();
        foreach (var foot in feet)
        {
            if (foot == null || !foot.enabled || !foot.gameObject.activeInHierarchy) continue;
            Vector3 p = foot.transform.TransformPoint(foot.center + Vector3.down * (foot.size.y * .5f));
            if (island.SignedSideOfRiver(p.x, p.z) != side || island.DistanceToRiver(p.x, p.z) < island.CanyonRimHalf) continue;
            int count = physics.Raycast(p + Vector3.up * .12f, Vector3.down, hits, .32f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(body.root) && hits[i].normal.y >= .6f
                    && hits[i].point.y > island.waterLevel + 1f) return true;
        }
        return false;
    }

    public static bool TryLandedSide(IslandTerrain island,Transform body,Collider[] parts,
        BoxCollider[] feet,float clearance,float footGap,out float side)
    {
        side=0f;
        if(island==null||body==null||parts==null||feet==null||feet.Length!=4)return false;
        side=island.SignedSideOfRiver(body.position.x,body.position.z);
        if(side==0f)return false;
        int solidParts=0;
        foreach(var col in parts)
        {
            if(col==null||!col.enabled||!col.gameObject.activeInHierarchy||col.isTrigger)continue;
            solidParts++;
            var box=col as BoxCollider;var bounds=col.bounds;
            for(int corner=0;corner<8;corner++)
            {
                var sign=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                Vector3 p=box!=null?box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,sign))
                    :bounds.center+Vector3.Scale(bounds.extents,sign);
                if(island.SignedSideOfRiver(p.x,p.z)!=side || island.DistanceToRiver(p.x,p.z)<=island.CanyonRimHalf+clearance)
                    return false;
            }
        }
        if(solidParts==0)return false;
        int grounded=0;
        var physics=body.gameObject.scene.GetPhysicsScene();
        foreach(var foot in feet)
        {
            if(foot==null||!foot.enabled||!foot.gameObject.activeInHierarchy)return false;
            Vector3 p=foot.transform.TransformPoint(foot.center+Vector3.down*(foot.size.y*.5f));
            int count=physics.Raycast(p+Vector3.up*.12f,Vector3.down,hits,footGap+.12f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.collider.transform.IsChildOf(body.root)||hit.normal.y<.6f)continue;
                if(hit.point.y<p.y-footGap)continue;
                grounded++;break;
            }
        }
        return grounded>=2;
    }
}

/// <summary>Remember a bank after the crossing condition remains true briefly.</summary>
public sealed class JungleBankTracker
{
    public float SafeSide { get; private set; }
    public Vector3 SafePosition { get; private set; }
    public bool HasCrossed { get; private set; }
    public bool IsSettled { get; private set; }
    float startingSide,pendingSide,landedTime;
    public void Reset(float side,Vector3 position)
    {
        startingSide=SafeSide=side;SafePosition=position;HasCrossed=false;CancelPending();
    }
    public void CancelPending(){pendingSide=0;landedTime=0;IsSettled=false;}
    public void Observe(bool landed,float side,Vector3 position,float dt,float settleTime)
    {
        if(!landed||side==0){CancelPending();return;}
        if(side!=pendingSide){pendingSide=side;landedTime=0;}
        landedTime+=Mathf.Max(0,dt);IsSettled=landedTime>=settleTime;
        if(!IsSettled)return;
        SafeSide=side;SafePosition=position;
        if(side!=startingSide)HasCrossed=true;
    }
    public void Restore(float side,Vector3 position)
    {
        if(side==0)return;
        SafeSide=side;SafePosition=position;HasCrossed=side!=startingSide;CancelPending();
    }
}
