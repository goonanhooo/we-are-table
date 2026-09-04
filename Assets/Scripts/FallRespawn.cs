using UnityEngine;

/// <summary>
/// 추락 리스폰(IceMountain): 테이블이 killY 아래로 떨어지면(산 아래/바다) 지정 XZ(처음 스폰 바닥)로 복원.
/// ArtSceneReset.ResetPoseAtXZ 사용 — 카메라 방향 yaw·지면 안착·다리 초기화 포함.
/// ⚠️ killY는 코스 최저점(착지 패드 윗면 y2.5)보다 낮아야 함 — 패드 위에선 발동 안 되게.
/// </summary>
public class FallRespawn : MonoBehaviour
{
    [Tooltip("이 y 아래로 내려가면 추락으로 판정")]
    public float killY = 1.0f;
    [Tooltip("리스폰 XZ(처음 스폰 바닥 지점)")]
    public Vector2 respawnXZ = new Vector2(0f, 33f);
    [Tooltip("지면 스캔 시작 높이 — 리스폰 지점이 실내(천장 아래)면 천장보다 낮게(예: 34)")]
    public float scanFromY = 500f;
    public float cooldown = 4f;
    [Header("★2026-09-04 지하 동굴 구간(IceCavern) — 테이블 z가 caveMaxZ보다 뒤(-z)면 이 값 사용")]
    [Tooltip("동굴 구간 판정: 테이블 z < 이 값이면 동굴")] public float caveMaxZ = -19f;
    public bool caveOverride = false;
    public float caveKillY = -215f;
    public Vector2 caveRespawnXZ = new Vector2(0f, -391f);
    public float caveScanFromY = -112f;

    Transform board;
    float ignoreUntil;

    void Start()
    {
        var t = GameObject.Find("Table");
        if (t != null) board = t.transform;
    }

    void Update()
    {
        if (board == null || Time.time < ignoreUntil) return;
        bool cave = caveOverride && board.position.z < caveMaxZ;
        float ky = cave ? caveKillY : killY;
        if (board.position.y >= ky) return;
        ignoreUntil = Time.time + cooldown;
        var art = Object.FindAnyObjectByType<ArtSceneReset>();
        if (art == null) return;
        if (cave) art.ResetPoseAtXZ(caveRespawnXZ.x, caveRespawnXZ.y, caveScanFromY);
        else art.ResetPoseAtXZ(respawnXZ.x, respawnXZ.y, scanFromY);
    }
}
