using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Moon gravity and a gentle arrival above the existing start position.</summary>
[DefaultExecutionOrder(100)]
public class MoonStage : MonoBehaviour
{
    [Tooltip("달 중력(m/s²). 지구 -9.81의 약 1/6.")]
    public float moonGravity = -1.62f;
    [Header("하늘에서 시작")]
    [Min(0f)] public float arrivalHeight = 16f;
    [Min(.1f)] public float arrivalFallSpeed = 3f;
    public bool IsArriving { get; private set; }
    public Vector3 GroundStart { get; private set; }

    Vector3 orig;
    ArticulationBody body;
    CameraFollow follow;
    Transform home;
    float feetBelowBody, originalDistance;
    bool authority, frameHome, framed;

    void Awake()
    {
        orig=Physics.gravity;
        Physics.gravity=new Vector3(0f,moonGravity,0f);
        MoonHorizon.Ensure(GameObject.Find("MoonTerrain"));
    }
    void Start()
    {
        if(CheckpointRuntime.WillRestore(14))return;
        var reset=Object.FindAnyObjectByType<ArtSceneReset>();
        if(reset==null)return;
        foreach(var ab in reset.GetComponentsInChildren<ArticulationBody>(true))if(ab.isRoot){body=ab;break;}
        if(body==null||arrivalHeight<=0f)return;
        GroundStart=body.transform.position;
        feetBelowBody=.8f;
        foreach(var c in reset.GetComponentsInChildren<Collider>(true))
            if(!c.isTrigger)feetBelowBody=Mathf.Max(feetBelowBody,GroundStart.y-c.bounds.min.y);
        var house=GameObject.Find("House_CozyKitchen")??GameObject.Find("House");
        home=house!=null?house.transform:null;
        follow=Camera.main!=null?Camera.main.GetComponent<CameraFollow>():null;
        originalDistance=follow!=null?follow.distance:6f;
        var nm=Unity.Netcode.NetworkManager.Singleton;
        authority=nm==null||!nm.IsListening||nm.IsServer;
        if(authority)
        {
            // All Awake geometry measurements are complete before the articulation moves.
            Vector3 position=GroundStart+Vector3.up*arrivalHeight;
            var top=reset.transform.root;
            if(top!=body.transform)top.position+=position-body.transform.position;
            body.TeleportRoot(position,body.transform.rotation);
            foreach(var ab in reset.GetComponentsInChildren<ArticulationBody>(true))
            {ab.linearVelocity=Vector3.zero;ab.angularVelocity=Vector3.zero;}
        }
        IsArriving=true;frameHome=home!=null&&follow!=null;
        if(authority)FrameArrival(true);
    }
    void FixedUpdate()
    {
        if(!IsArriving||body==null)return;
        // Player abilities and cheat flight remain available during the descent.
        var squid=body.transform.root.GetComponentInChildren<SquidMode>();
        if(CheatMode.Active||(squid!=null&&squid.active&&!squid.thrustLocked&&squid.JetMask!=0)){FinishArrival();return;}
        if(!authority)return;
        foreach(var hit in Physics.RaycastAll(body.transform.position,Vector3.down,feetBelowBody+.4f,~0,QueryTriggerInteraction.Ignore))
            if(hit.collider.transform.root!=body.transform.root&&hit.normal.y>.35f){FinishArrival();return;}
        Vector3 velocity=body.linearVelocity;
        if(velocity.y < -arrivalFallSpeed){velocity.y=-arrivalFallSpeed;body.linearVelocity=velocity;}
    }
    void LateUpdate()
    {
        if(!IsArriving||body==null||Time.timeScale<=0f||LetterOverlay.IsOpen)return;
        if(!authority)
        {
            // Guests receive the server's height/physics; never teleport the shared table locally.
            if(!framed&&body.transform.position.y<GroundStart.y+arrivalHeight*.25f)return;
            if(framed&&body.transform.position.y<=GroundStart.y+.5f){FinishArrival();return;}
        }
        var mouse=Mouse.current;
        if(framed&&Cursor.lockState==CursorLockMode.Locked&&mouse!=null&&mouse.delta.ReadValue().sqrMagnitude>1f)frameHome=false;
        FrameArrival(!framed);
    }
    void FrameArrival(bool immediately)
    {
        if(!frameHome||follow==null||home==null)return;
        Vector3 toHome=home.position-(body.transform.position+Vector3.up*follow.height);
        float horizontal=new Vector2(toHome.x,toHome.z).magnitude;
        float yaw=Mathf.Atan2(toHome.x,toHome.z)*Mathf.Rad2Deg-12f;
        float pitch=Mathf.Atan2(-toHome.y,Mathf.Max(1f,horizontal))*Mathf.Rad2Deg+7f;
        follow.distance=9f;follow.SetYawPitch(yaw,pitch);
        if(immediately)
        {
            var rotation=Quaternion.Euler(pitch,yaw,0f);
            follow.transform.SetPositionAndRotation(body.transform.position+Vector3.up*follow.height-rotation*Vector3.forward*follow.distance,rotation);
        }
        framed=true;
    }
    void FinishArrival()
    {
        IsArriving=false;
        if(follow!=null&&frameHome&&framed){follow.distance=originalDistance;follow.BlendFrom(1.2f);}
        frameHome=false;
    }
    void OnDestroy(){Physics.gravity=orig;}
    public void CancelArrival()=>FinishArrival();

}
