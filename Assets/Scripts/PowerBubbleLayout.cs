using UnityEngine;

/// <summary>Reserve the HUD and the whole table while pointing at the current practice leg.</summary>
public static class PowerBubbleLayout
{
    static readonly Rect ScreenSafe = new Rect(24, 140, 1872, 916);
    static Rect Expand(Rect r, float amount) => new Rect(r.x-amount,r.y-amount,r.width+2*amount,r.height+2*amount);
    static bool Inside(Rect r) => r.xMin>=ScreenSafe.xMin && r.xMax<=ScreenSafe.xMax && r.yMin>=ScreenSafe.yMin && r.yMax<=ScreenSafe.yMax;
    public static bool Clear(Rect bubble, Rect table, Vector2 target)
    {
        if(!Inside(bubble) || bubble.Overlaps(Expand(table,20)))return false;
        foreach(var puff in ThoughtTailLayout.Build(bubble,target))
            if(!Inside(puff) || puff.Overlaps(Expand(table,16)))return false;
        return true;
    }
    public static Rect Place(Rect table, Rect focus, float width, bool preferLeft)
    {
        float height=ThoughtBubbleUI.WideHeight(width);Vector2 target=focus.center;
        float xmin=ScreenSafe.xMin,xmax=ScreenSafe.xMax-width,ymin=ScreenSafe.yMin,ymax=ScreenSafe.yMax-height;
        Rect best=new Rect(preferLeft?xmin:xmax,ymin,width,height);float score=float.PositiveInfinity;
        for(int x=0;x<4;x++)for(int y=0;y<4;y++)
        {
            float px=x==0?xmin:x==1?xmax:x==2?target.x-width-120:target.x+120;
            float py=y==0?ymin:y==1?target.y-height-130:y==2?target.y-height*.5f:ymax;
            var candidate=new Rect(Mathf.Clamp(px,xmin,xmax),Mathf.Clamp(py,ymin,ymax),width,height);
            if(!Clear(candidate,table,target))continue;
            var tip=ThoughtTailLayout.Build(candidate,target)[2];
            float distance=Vector2.Distance(tip.center,target);
            float sidePenalty=(candidate.center.x<target.x)==preferLeft?0:110;
            float cost=distance+sidePenalty+Mathf.Abs(candidate.y-220)*.06f;
            if(cost<score){score=cost;best=candidate;}
        }
        if(float.IsPositiveInfinity(score))return best;
        // Bring the tip close to the table, keeping exactly the same ink-to-ink puff gaps.
        Vector2 direction=(target-best.center).normalized;float lo=0,hi=Vector2.Distance(best.center,target);
        for(int n=0;n<10;n++)
        {
            float amount=(lo+hi)*.5f;var next=best;next.position+=direction*amount;
            if(Clear(next,table,target))lo=amount;else hi=amount;
        }
        best.position+=direction*lo;return best;
    }
}
