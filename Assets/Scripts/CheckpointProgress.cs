using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Versioned, platform-independent progress file suitable for Steam Auto-Cloud.</summary>
public static class CheckpointProgress
{
    [Serializable] public class Snapshot
    {
        public int id, powers, world;
        // Optional in older version-1 saves: 0=off, 1=giraffe, 2=tennis, 3=ice, 4=squid.
        public int activePower;
        public string scene; public Vector3 position; public float yaw;
        public float[] carts; public bool hasPosition;
    }
    [Serializable] class Save { public int version=1; public List<Snapshot> points=new List<Snapshot>(); }
    static Save data;
    #if UNITY_EDITOR || WAT_RELEASE_AUDIT
    public static string ValidationSavePath;
#endif
    public static string SavePath
    {
        get {
#if UNITY_EDITOR || WAT_RELEASE_AUDIT
            if(!string.IsNullOrEmpty(ValidationSavePath))return ValidationSavePath;
#endif
            return Path.Combine(Application.persistentDataPath,"progress","checkpoints-v1.json");
        }
    }
    public static string SaveError {get;private set;}
    public static uint Mask
    {
        get { Load();uint mask=1;foreach(var s in data.points)if(Valid(s))mask|=1u<<s.id;return mask; }
    }
    public static bool IsUnlocked(int id)=>CheckpointCatalog.Valid(id)&&(Mask&(1u<<id))!=0;
    public static bool Valid(Snapshot s)=>s!=null&&CheckpointCatalog.Matches(s.id,s.scene)&&s.powers>=0&&s.powers<=15&&
        float.IsFinite(s.position.x)&&float.IsFinite(s.position.y)&&float.IsFinite(s.position.z)&&s.position.sqrMagnitude<100000000f&&float.IsFinite(s.yaw)&&
        (CheckpointCatalog.All[s.id].SceneStart||s.hasPosition);
    static void Load()
    {
        if(data!=null)return;data=Read(SavePath)??Read(SavePath+".bak")??new Save();
        data.points.RemoveAll(s=>!Valid(s));
    }
    static Save Read(string path)
    {
        if(!File.Exists(path))return null;
        try{var s=JsonUtility.FromJson<Save>(File.ReadAllText(path));return s!=null&&s.version==1&&s.points!=null?s:null;}
        catch(Exception e){Debug.LogWarning("[Checkpoints] Cannot read save: "+e.Message);return null;}
    }
    public static Snapshot Get(int id)
    {
        Load();foreach(var s in data.points)if(s.id==id&&Valid(s))return JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(s));
        return id==0?new Snapshot{id=0,scene="Hallway"}:null;
    }
    public static bool Store(Snapshot point)
    {
        if(!Valid(point))return false;Load();
        var next=new Save{points=new List<Snapshot>(data.points)};
        next.points.RemoveAll(s=>s.id==point.id);next.points.Add(point);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(next,true));
            if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
            data=next;SaveError=null;return true;
        }
        catch(Exception e){SaveError=e.Message;Debug.LogError("[Checkpoints] Save failed: "+e.Message);return false;}
    }
    public static void ReloadFromDisk(){data=null;Load();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetCache(){data=null;SaveError=null;}
}
