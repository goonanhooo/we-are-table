using UnityEngine;
using Unity.Netcode;

/// <summary>Four real foot sockets lead into a quiet, wordless kitchen finale.</summary>
public class MoonKitchenEnding : MonoBehaviour
{
    [System.Serializable] public class Shot
    {
        public string label;
        public Transform from, to, lookAt;
        public float seconds = 12f, fov = 48f;
    }
    public Transform tablePlace, chair, chairSeat;
    public Transform[] sockets = new Transform[4]; // anatomical FL, FR, BL, BR (independent of input slots)
    public Renderer[] socketRims = new Renderer[4];
    public Shot[] shots;
    public GameObject friends;
    public Light[] warmLights;
    public ParticleSystem steam;
    public float settleSeconds = .65f, socketHalfWidth = .18f;
    [HideInInspector] public bool netRemoteDriven;
    public bool Started { get; private set; }
    public bool Finished { get; private set; }
    public int SocketMask { get; private set; }
    public int ShotIndex { get; private set; } = -1;
    public double StartServerTime { get; private set; }
    public float Elapsed { get; private set; }
    public float Duration { get { float total = 0f; if (shots != null) foreach (var shot in shots) total += shot.seconds; return total; } }
    ArticulationBody table;
    ArtLegController[] legs;
    Camera cam;
    float settled, localStart;
    Vector3 tableStart, chairStart, cameraStart;
    Quaternion tableRotation, chairRotation, cameraRotation;
    float cameraFov;
    float[] lightIntensities;
    MaterialPropertyBlock block;
    bool remote;

    bool HasAuthority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    void Start()
    {
        if (friends != null) friends.SetActive(false);
        lightIntensities = new float[warmLights.Length];
        for (int i = 0; i < warmLights.Length; i++) if (warmLights[i] != null) lightIntensities[i] = warmLights[i].intensity;
    }
    bool FindTable()
    {
        if (table != null) return true;
        var reset = FindAnyObjectByType<ArtSceneReset>(FindObjectsInactive.Include);
        if (reset == null) return false;
        foreach (var ab in reset.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { table = ab; break; }
        legs = reset.GetComponentsInChildren<ArtLegController>(true);
        return table != null;
    }
    public static Vector3 FootPoint(ArtLegController leg)
    {
        var box = leg.GetComponentInChildren<BoxCollider>();
        return box != null ? box.transform.TransformPoint(box.center + Vector3.down * box.size.y * .5f) : leg.transform.position;
    }
    public bool ContainsFootPoint(int slot, Vector3 point)
    {
        if (slot < 0 || slot >= sockets.Length || sockets[slot] == null) return false;
        Vector3 p = sockets[slot].InverseTransformPoint(point);
        return Mathf.Abs(p.x) < socketHalfWidth && Mathf.Abs(p.z) < socketHalfWidth && p.y > -.085f && p.y < .035f;
    }
    public int SampleSockets()
    {
        if (!FindTable()) return 0;
        int mask = 0;
        foreach (var leg in legs) { int i = LegLayout.SocketIndex((int)leg.controlSet); if (ContainsFootPoint(i, FootPoint(leg))) mask |= 1 << i; }
        return mask;
    }
    void Update()
    {
        if (Started || netRemoteDriven || !HasAuthority || Time.timeScale <= 0f || !FindTable()) return;
        SocketMask = SampleSockets();
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < socketRims.Length; i++) if (socketRims[i] != null)
        {
            bool seated = (SocketMask & (1 << i)) != 0;
            block.SetColor("_EmissionColor", seated ? new Color(.55f,.29f,.08f) : Color.black);
            socketRims[i].SetPropertyBlock(block);
        }
        bool still = table.linearVelocity.sqrMagnitude < .45f * .45f;
        settled = SocketMask == 15 && still ? settled + Time.deltaTime : 0f;
        if (settled >= settleSeconds) Begin(false, 0f);
    }
    public void PlayRemote(float elapsed = 0f) { if (!Started) Begin(true, elapsed); }
    void Begin(bool guest, float elapsed)
    {
        if (Started || !FindTable() || tablePlace == null) return;
        remote = guest; Started = true; localStart = Time.time - Mathf.Max(0f, elapsed);
        var nm = NetworkManager.Singleton;
        StartServerTime = nm != null && nm.IsListening ? nm.ServerTime.Time - elapsed : Time.time - elapsed;
        tableStart = table.transform.position; tableRotation = table.transform.rotation;
        if (!remote)
        {
            table.immovable = true; table.linearVelocity = Vector3.zero; table.angularVelocity = Vector3.zero;
            foreach (var leg in legs) { leg.HoldVertical(1000f); leg.enabled = false; }
            foreach (var joint in table.GetComponentsInChildren<ArticulationBody>()) if (!joint.isRoot)
            {
                var drive=joint.xDrive;drive.target=joint.jointPosition.dofCount>0?joint.jointPosition[0]*Mathf.Rad2Deg:0f;
                drive.targetVelocity=0;drive.stiffness=10000;drive.damping=500;drive.forceLimit=10000;joint.xDrive=drive;
            }

        }
        foreach (var reset in table.transform.root.GetComponentsInChildren<ArtSceneReset>()) reset.enabled = false;
        foreach (var tutorial in table.transform.root.GetComponentsInChildren<KeyTutorial>()) tutorial.enabled = false;
        foreach (var hud in table.transform.root.GetComponentsInChildren<AbilityHud>()) hud.enabled = false;
        foreach (var xray in table.transform.root.GetComponentsInChildren<LegColorXray>()) xray.SetOn(false, false); // Ending hides the overlay without changing the saved option.
        if (chair != null) { chairStart = chair.position; chairRotation = chair.rotation; }
        cam = Camera.main;
        if (cam != null)
        {
            cameraStart = cam.transform.position; cameraRotation = cam.transform.rotation; cameraFov = cam.fieldOfView;
            foreach (var behaviour in new Behaviour[] {cam.GetComponent<CameraFollow>(),cam.GetComponent<CameraGroundGuard>(),cam.GetComponent<CameraOcclusionFade>()})
                if (behaviour != null) behaviour.enabled = false;
        }
        StageTimer.Suppress = true;
        if (StageTimer.Instance != null) StageTimer.Instance.enabled = false;
        
        if (steam != null) steam.Play();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
    }
    void LateUpdate()
    {
        if (!Started) return;
        Elapsed = Mathf.Max(0f, Time.time - localStart);
        float dock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Elapsed / 2.8f));
        if (!remote && table != null && Elapsed < 3f)
            table.TeleportRoot(Vector3.Lerp(tableStart,tablePlace.position,dock),Quaternion.Slerp(tableRotation,tablePlace.rotation,dock));
        if (chair != null && chairSeat != null)
        {
            float slide = Mathf.SmoothStep(0f,1f,Mathf.Clamp01((Elapsed-1.5f)/3.5f));
            chair.SetPositionAndRotation(Vector3.Lerp(chairStart,chairSeat.position,slide),Quaternion.Slerp(chairRotation,chairSeat.rotation,slide));
        }
        for(int i=0;i<warmLights.Length;i++) if(warmLights[i]!=null)
            warmLights[i].intensity=lightIntensities[i]*Mathf.Lerp(1f,1.18f,Mathf.SmoothStep(0,1,Mathf.Clamp01(Elapsed/7f)));
        float time = Elapsed;
        if (shots != null && shots.Length > 0 && cam != null)
        {
            int index = 0;
            while(index<shots.Length-1 && time>=shots[index].seconds) {time-=shots[index].seconds;index++;}
            ShotIndex=index;var shot=shots[index];
            if(friends!=null && friends.activeSelf!=(index==shots.Length-1))friends.SetActive(index==shots.Length-1);
            float u=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/shot.seconds));
            Vector3 pos=Vector3.Lerp(shot.from.position,shot.to.position,u);
            Quaternion rot=Quaternion.LookRotation(shot.lookAt.position-pos,Vector3.up);
            float intro=Mathf.SmoothStep(0,1,Mathf.Clamp01(Elapsed/3f));
            cam.transform.SetPositionAndRotation(Vector3.Lerp(cameraStart,pos,intro),Quaternion.Slerp(cameraRotation,rot,intro));
            cam.fieldOfView=Mathf.Lerp(cameraFov,shot.fov,intro);
        }
        if (!Finished && Elapsed >= Duration)
        {
            Finished = true;
            if (!remote)
            {
                if (StageTimer.Instance != null) StageTimer.Instance.Clear();
                Progress.MarkCleared("Moon_CozyKitchen");
                CheckpointRuntime.Milestone(15);
            }
        }
    }
    void OnGUI()
    {
        if (!Started || shots == null || ShotIndex < 1) return;
        // Short, quiet dips between long held compositions. No speech or tutorial overlays.
        float time=Elapsed;for(int i=0;i<ShotIndex;i++)time-=shots[i].seconds;
        float remaining=shots[ShotIndex].seconds-time;
        float alpha=Mathf.Clamp01(1f-time/.65f);
        if(ShotIndex<shots.Length-1)alpha=Mathf.Max(alpha,Mathf.Clamp01(1f-remaining/.65f));
        if(alpha<=0)return;var old=GUI.color;GUI.color=new Color(0,0,0,alpha);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
    }
    public void Replay(){if(HasAuthority)Begin(false,0f);}

}
