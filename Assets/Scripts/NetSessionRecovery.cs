using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>Finish transport shutdown before offering to join another room.</summary>
public class NetSessionRecovery:MonoBehaviour
{
    static NetSessionRecovery instance;
    public static void Begin()
    {
        if(instance!=null)return;
        var go=new GameObject("Return from disconnected session");DontDestroyOnLoad(go);instance=go.AddComponent<NetSessionRecovery>();
    }
    IEnumerator Start()
    {
        var leave=NetSession.LeaveAsync();var nm=NetworkManager.Singleton;
        if(nm!=null)nm.Shutdown();
        float until=Time.realtimeSinceStartup+3;
        while(nm!=null&&nm.IsListening&&Time.realtimeSinceStartup<until)yield return null;
        while(!leave.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
        Time.timeScale=1;CheckpointRuntime.ForgetRun();SceneManager.LoadScene("StartScreen");yield return null;
        FindAnyObjectByType<LevelSelect>()?.OpenMulti();
        NetSession.SetStatus(L.T("호스트 연결 종료 — 세션 끝","host left — session over"));
        Destroy(gameObject);
    }
    void OnDestroy(){if(instance==this)instance=null;}
}
