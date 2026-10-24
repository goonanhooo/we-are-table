using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Render the real table meshes without cloning gameplay, physics or network components.</summary>
public sealed class LobbyTablePreview : System.IDisposable
{
    GameObject root;Camera camera;RenderTexture image;
    readonly Transform[] legs=new Transform[4];readonly Material[] legMaterials=new Material[4];
    readonly long[] previousOwners={-2,-2,-2,-2};readonly float[] changedAt={-10,-10,-10,-10};
    Material topMaterial;readonly Vector3[][] corners=new Vector3[4][];
    static readonly int[,] Edges={{0,1},{1,3},{3,2},{2,0},{4,5},{5,7},{7,6},{6,4},{0,4},{1,5},{2,6},{3,7}};
    public bool Ready=>camera!=null&&legs[0]!=null&&legs[1]!=null&&legs[2]!=null&&legs[3]!=null;
    public RenderTexture Texture=>image;
    public void Initialize(Transform source)
    {
        if(root!=null)return;
        root=new GameObject("Lobby table visual"){hideFlags=HideFlags.HideAndDontSave};root.transform.position=new Vector3(0,-10000,0);
        var shader=Resources.Load<Shader>("LobbyTable");if(shader==null)shader=Shader.Find("Universal Render Pipeline/Unlit");
        var body=Find(source,"TableTop");if(body!=null){var original=body.GetComponent<MeshRenderer>().sharedMaterial;Color color=original!=null&&original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):new Color(.78f,.67f,.48f);topMaterial=CopyMesh(body,source,shader,color).GetComponent<MeshRenderer>().sharedMaterial;}
        for(int i=0;i<4;i++)
        {
            var original=LegLayout.Find(source,i);if(original==null)continue;
            legs[i]=CopyMesh(original,source,shader,Color.Lerp(new Color(.86f,.83f,.74f),LegPalette.BySlot[i],.48f));
            legMaterials[i]=legs[i].GetComponent<MeshRenderer>().sharedMaterial;var b=legs[i].GetComponent<MeshFilter>().sharedMesh.bounds;corners[i]=new Vector3[8];
            for(int k=0;k<8;k++)corners[i][k]=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
        }
        var camObject=new GameObject("Lobby table camera"){hideFlags=HideFlags.HideAndDontSave};camObject.transform.SetParent(root.transform,false);camera=camObject.AddComponent<Camera>();
        camera.transform.localPosition=new Vector3(2.7f,1.8f,-3.8f);camera.transform.LookAt(root.transform.position+new Vector3(0,.42f,0));camera.orthographic=true;camera.orthographicSize=1.04f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=JourneyMenuUI.Out(JourneyMenuUI.Background);   // IMGUI에 그대로 그려지므로 바탕(#f3f2eb)과 같게 보이도록 선형값camera.cullingMask=1<<31;camera.nearClipPlane=.1f;camera.farClipPlane=15;
        camera.allowHDR=false;camera.allowMSAA=true;camera.depth=-50;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        image=new RenderTexture(1200,850,24){name="Lobby table preview",hideFlags=HideFlags.HideAndDontSave,antiAliasing=2};image.Create();camera.targetTexture=image;
    }
    static Transform Find(Transform source,string name){foreach(var t in source.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
    Transform CopyMesh(Transform original,Transform source,Shader shader,Color color)
    {
        var go=new GameObject(original.name){hideFlags=HideFlags.HideAndDontSave,layer=31};go.transform.SetParent(root.transform,false);
        go.transform.localPosition=source.InverseTransformPoint(original.position);go.transform.localRotation=Quaternion.Inverse(source.rotation)*original.rotation;
        var s=source.lossyScale;var o=original.lossyScale;go.transform.localScale=new Vector3(o.x/s.x,o.y/s.y,o.z/s.z);
        go.AddComponent<MeshFilter>().sharedMesh=original.GetComponent<MeshFilter>().sharedMesh;var renderer=go.AddComponent<MeshRenderer>();
        var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};material.SetColor("_Color",color);if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
    }
    public void SetVisible(bool visible){if(root!=null&&root.activeSelf!=visible)root.SetActive(visible);}
    public Vector2 LegPoint(int slot,Rect viewport)
    {
        if(legs[slot]==null)return viewport.center;return Project(legs[slot].TransformPoint(legs[slot].GetComponent<MeshFilter>().sharedMesh.bounds.center),viewport);
    }
    Vector2 Project(Vector3 point,Rect viewport){var p=camera.WorldToViewportPoint(point);return new Vector2(viewport.x+p.x*viewport.width,viewport.y+(1-p.y)*viewport.height);}
    public Rect LegBounds(int slot,Rect viewport)
    {
        var rect=new Rect(LegPoint(slot,viewport),Vector2.zero);if(legs[slot]==null)return rect;
        foreach(var c in corners[slot]){var p=Project(legs[slot].TransformPoint(c),viewport);rect.xMin=Mathf.Min(rect.xMin,p.x);rect.xMax=Mathf.Max(rect.xMax,p.x);rect.yMin=Mathf.Min(rect.yMin,p.y);rect.yMax=Mathf.Max(rect.yMax,p.y);}
        return new Rect(rect.x-12,rect.y-10,rect.width+24,rect.height+20);
    }
    public void Draw(Rect viewport,NetLegLink link,int hover)
    {
        if(!Ready)return;GUI.DrawTexture(viewport,image,ScaleMode.StretchToFill);
        for(int i=0;i<4;i++)
        {
            long owner=link.OwnerOf(i);if(owner!=previousOwners[i]){changedAt[i]=Time.unscaledTime;previousOwners[i]=owner;}
            bool chosen=owner!=-2;float age=Time.unscaledTime-changedAt[i];float pulse=chosen?Mathf.Exp(-Mathf.Max(0,age-.22f)*4)*Mathf.SmoothStep(0,1,Mathf.Clamp01((age-.18f)*8)):0;
            Color baseColor=Color.Lerp(new Color(.86f,.83f,.74f),LegPalette.BySlot[i],.48f);Color lit=Color.Lerp(baseColor,LegPalette.BySlot[i],pulse*.45f);
            legMaterials[i].SetColor("_Color",lit);
            if(!chosen&&hover!=i)continue;
            Color ink=LegPalette.BySlot[i];float width=chosen?2.5f+pulse:1.4f;
            for(int e=0;e<12;e++)
            {
                var a=Project(legs[i].TransformPoint(corners[i][Edges[e,0]]),viewport);var b=Project(legs[i].TransformPoint(corners[i][Edges[e,1]]),viewport);
                if(chosen)JourneyMenuUI.Line(a,b,new Color(ink.r,ink.g,ink.b,.10f+pulse*.17f),7+pulse*5);
                JourneyMenuUI.Line(a,b,new Color(ink.r,ink.g,ink.b,chosen?1:.55f),width);
            }
        }
    }
    public void Dispose()
    {
        if(camera!=null)camera.targetTexture=null;if(image!=null){image.Release();Object.Destroy(image);}
        if(topMaterial!=null)Object.Destroy(topMaterial);foreach(var m in legMaterials)if(m!=null)Object.Destroy(m);
        if(root!=null)Object.Destroy(root);root=null;
    }
}
