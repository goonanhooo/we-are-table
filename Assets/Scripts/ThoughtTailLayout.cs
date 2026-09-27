using UnityEngine;

/// <summary>Place puffs using distances between the artwork's visible outlines.</summary>
public static class ThoughtTailLayout
{
    [System.Serializable] class Shape { public string name; public Vector2[] points; }
    [System.Serializable] class Data { public Shape[] shapes; }
    static Data data;
    static Rect cachedBody; static Vector2 cachedTarget; static Rect[] cached;
    static Rect fitBody,fitAvoid,fitResult;static bool hasFit;
    static readonly float[] widths={55f,38f,25f};
    static void Load()
    {
        if(data!=null)return;
        var asset=Resources.Load<TextAsset>("ThoughtBubbles/Outlines");
        data=JsonUtility.FromJson<Data>(asset.text);
    }
    static int BodyShape(Rect body)=>Mathf.Abs(body.height/body.width-ThoughtBubbleUI.WideHeight(1))<.05f?4:0;
    public static float Gap(Rect body)=>12f*body.width/543f;
    static Vector2 Point(Rect r,Vector2 p)=>r.position+Vector2.Scale(r.size,p);
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static Vector2 Support(Rect rect,Vector2[] polygon,Vector2 direction)
    {
        Vector2 localDirection=Vector2.Scale(rect.size,direction),best=polygon[0];
        float score=Vector2.Dot(best,localDirection);
        for(int i=1;i<polygon.Length;i++)
        {
            float next=Vector2.Dot(polygon[i],localDirection);
            if(next>score){score=next;best=polygon[i];}
        }
        return Point(rect,best);
    }
    static Vector2 Closest(Vector2 a,Vector2 b,out float t)
    {
        Vector2 edge=b-a;t=Mathf.Clamp01(-Vector2.Dot(a,edge)/Mathf.Max(.0000001f,edge.sqrMagnitude));return a+t*edge;
    }
    static void Reduce(ref Vector2 a,ref Vector2 b,ref int count,float t)
    {
        if(t<=0){count=1;}else if(t>=1){a=b;count=1;}else count=2;
    }
    public static float Distance(Rect a,int shapeA,Rect b,int shapeB)
    {
        Load();var pa=data.shapes[shapeA].points;var pb=data.shapes[shapeB].points;
        // GJK distance on the convex ink outlines: a few support searches instead of
        // testing every pair of edges for each position in the layout search.
        Vector2 direction=a.center-b.center;if(direction.sqrMagnitude<.0001f)direction=Vector2.right;
        Vector2 p=Support(a,pa,direction)-Support(b,pb,-direction),q=Vector2.zero,v=p;int count=1;
        for(int iteration=0;iteration<32;iteration++)
        {
            float squared=v.sqrMagnitude;if(squared<.000001f)return 0;
            Vector2 w=Support(a,pa,-v)-Support(b,pb,v);
            if(squared-Vector2.Dot(v,w)<=.0001f*Mathf.Sqrt(squared))break;
            if(count==1)
            {
                q=w;v=Closest(p,q,out float t);Reduce(ref p,ref q,ref count,t);
                continue;
            }
            float c0=Cross(q-p,-p),c1=Cross(w-q,-q),c2=Cross(p-w,-w);
            if(Mathf.Abs(Cross(q-p,w-p))>.000001f&&
                ((c0>=0&&c1>=0&&c2>=0)||(c0<=0&&c1<=0&&c2<=0)))return 0;
            Vector2 pq=Closest(p,q,out float tpq),qw=Closest(q,w,out float tqw),wp=Closest(w,p,out float twp);
            if(pq.sqrMagnitude<=qw.sqrMagnitude&&pq.sqrMagnitude<=wp.sqrMagnitude)
            {v=pq;Reduce(ref p,ref q,ref count,tpq);}
            else if(qw.sqrMagnitude<=wp.sqrMagnitude)
            {p=q;q=w;v=qw;Reduce(ref p,ref q,ref count,tqw);}
            else
            {q=p;p=w;v=wp;Reduce(ref p,ref q,ref count,twp);}
        }
        return v.magnitude;
    }
    public static Rect[] Build(Rect body,Vector2 target)
    {
        if(cached!=null&&cachedBody==body&&cachedTarget==target)return cached;
        Load();var result=new Rect[3];float k=body.width/543f,gap=Gap(body);
        Vector2 direction=(target-body.center).normalized;if(direction.sqrMagnitude<.5f)direction=Vector2.up;
        Rect previous=body;int previousShape=BodyShape(body);
        for(int i=0;i<3;i++)
        {
            float size=widths[i]*k;float ratio=i==1?65f/67f:i==2?56f/55f:1f;
            var next=new Rect(0,0,size,size*ratio);
            float low=0,high=Mathf.Max(previous.width,previous.height)+size+gap;
            for(int step=0;step<13;step++)
            {
                float along=(low+high)*.5f;next.center=previous.center+direction*along;
                if(Distance(previous,previousShape,next,i+1)<gap)low=along;else high=along;
            }
            next.center=previous.center+direction*high;result[i]=next;previous=next;previousShape=i+1;
        }
        cachedBody=body;cachedTarget=target;cached=result;return result;
    }
    static bool Clear(Rect body,Rect avoid)
    {
        var safe=avoid;safe.xMin-=18;safe.xMax+=18;safe.yMin-=18;safe.yMax+=18;
        if(body.Overlaps(safe))return false;
        foreach(var puff in Build(body,avoid.center))
            if(puff.Overlaps(safe)||puff.xMin<12||puff.xMax>1908||puff.yMin<12||puff.yMax>1068)return false;
        var tip=Build(body,avoid.center)[2];
        return Vector2.Dot(avoid.center-tip.center,avoid.center-body.center)>0;
    }
    public static Rect Fit(Rect body,Rect avoid)
    {
        if(hasFit&&fitBody==body&&fitAvoid==avoid)return fitResult;
        fitBody=body;fitAvoid=avoid;fitResult=FitUncached(body,avoid);hasFit=true;return fitResult;
    }
    static Rect FitUncached(Rect body,Rect avoid)
    {
        if(Clear(body,avoid))return body;
        Rect best=body;float score=float.PositiveInfinity;
        for(int y=0;y<3;y++)for(int x=0;x<3;x++)
        {
            var candidate=body;
            candidate.position=new Vector2(Mathf.Lerp(24,1896-body.width,x*.5f),Mathf.Lerp(90,990-body.height,y*.5f));
            if(!Clear(candidate,avoid))continue;
            float d=(candidate.center-body.center).sqrMagnitude;
            if(d<score){score=d;best=candidate;}
        }
        if(!float.IsInfinity(score))
        {
            float lo=0,hi=1;
            for(int i=0;i<10;i++)
            {
                float t=(lo+hi)*.5f;var candidate=body;candidate.position=Vector2.Lerp(body.position,best.position,t);
                if(Clear(candidate,avoid))hi=t;else lo=t;
            }
            best.position=Vector2.Lerp(body.position,best.position,hi);
        }
        return best;
    }
    public static Rect TargetBounds(Camera camera,Transform target)
    {
        var renderer=target.GetComponent<Renderer>();
        if(renderer==null)return new Rect(960,540,0,0);
        var bounds=renderer.bounds;Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        for(int i=0;i<8;i++)
        {
            var sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
            if(ThoughtBubbleUI.GuiPoint(camera,bounds.center+Vector3.Scale(bounds.extents,sign),out var p)){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
}
