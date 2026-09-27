using Unity.Netcode;
using UnityEngine.SceneManagement;

/// <summary>
/// ★ 멀티 대응 씬 전환 헬퍼. 게임플레이 씬 이동은 전부 이걸 경유한다.
/// - 세션 중: 호스트만 NGO SceneManager.LoadScene(전원 동기 전환 — 게스트 자동 따라감).
///   게스트 호출은 무시(씬 전환 권한 없음 — 예: 게스트 쪽에서 트리거가 겹쳐 발동해도 안전).
/// - 세션 아님: 평소처럼 로컬 LoadScene.
/// ⚠️ '세션을 끝내고' 이동(메뉴 이탈 등)은 LeaveToLocal 사용.
/// </summary>
public static class NetScene
{
    public static bool IntentionalLeave;
    /// <param name="spawn">★2026-09-18 씬 안 시작 지점 id(SceneSpawnPoints 자식 이름). null이면 씬 기본 위치.</param>
    public static void Load(string sceneName, string spawn = null)
    {
        var nm = NetworkManager.Singleton;
        if(nm!=null&&nm.IsListening&&!nm.IsServer)return;
        CheckpointRuntime.BeforeLoad(sceneName,spawn);
        SpawnOverride.Pending = spawn;
        if (nm != null && nm.IsListening)
        {
            if (nm.IsServer) nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return;   // 게스트: 호스트가 옮겨줄 때까지 대기
        }
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    /// <summary>현재 씬 재시작(멀티에선 호스트만 유효 — 전원 재시작).</summary>
    public static void Reload() => CheckpointRuntime.ReloadCurrent();   // 같은 시작 지점에서 다시

    /// <summary>세션에서 '나'만 빠져나가 로컬로 씬 이동(메인 메뉴 등). 호스트가 부르면 세션 종료(설계대로).</summary>
    public static void LeaveToLocal(string sceneName)
    {
        IntentionalLeave=true;CheckpointRuntime.ForgetRun();
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            _ = NetSession.LeaveAsync();
            nm.Shutdown();
        }
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
