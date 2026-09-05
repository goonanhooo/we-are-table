using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-09-06 광차 동굴(IceCavern) → 빙산(IceMountain) 전환. 씬 GO "CaveExit"에 부착.
/// 광차가 눈사람 동굴 종착(공중 y≈100.9)에 닿아 테이블이 내려 동굴 바닥(y≈96)에 서면(zone 안) → 페이드 아웃 → nextScene 로드.
/// 빙산 씬은 눈사람 동굴 안(TablePlayer (0,97,-12))에서 시작하므로 이어지는 느낌.
/// 멀티: 테이블 위치는 NetworkTransform으로 전원 동일 → 각자 로컬 페이드(+다음 로드 후 페이드 인 예약), 로드는 호스트만(NetScene.Load).
/// </summary>
public class CaveExitToMountain : MonoBehaviour
{
    public string nextScene = "IceMountain";
    [Header("도착 판정 구역(월드) — 눈사람 동굴 바닥, 광차(y≥100.9)에 매달린 동안은 제외")]
    public float minY = 94.5f, maxY = 98.8f;
    public float minZ = -17f, maxZ = 24f, halfX = 17f;
    [Tooltip("구역 안에 이 시간 이상 머물면 전환(착지 직후 튐 방지)")] public float dwellSeconds = 0.4f;
    public float fadeSeconds = 1.0f;
    public bool Started => started;

    bool started; Transform table; float dwell;

    void Update()
    {
        if (started) return;
        if (table == null)
        {
            var asr = Object.FindAnyObjectByType<ArtSceneReset>();
            if (asr == null) return;
            foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { table = ab.transform; break; }
            if (table == null) return;
        }
        var p = table.position;
        bool inZone = p.y >= minY && p.y <= maxY && p.z >= minZ && p.z <= maxZ && Mathf.Abs(p.x) <= halfX;
        dwell = inZone ? dwell + Time.deltaTime : 0f;
        if (dwell >= dwellSeconds) StartCoroutine(Go());
    }

    IEnumerator Go()
    {
        started = true;
        ScreenFader.FadeOut(fadeSeconds);
        yield return new WaitForSeconds(fadeSeconds);
        if (!string.IsNullOrEmpty(nextScene)) { ScreenFader.FadeInAfterNextLoad(0.4f, 1.5f); NetScene.Load(nextScene); }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(0f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f), new Vector3(halfX * 2f, maxY - minY, maxZ - minZ));
    }
}
