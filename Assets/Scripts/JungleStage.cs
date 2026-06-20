using UnityEngine;

/// <summary>
/// 씬 전환 시 "떨어지던 자세/속도 그대로 이어서 떨어지게" 하기 위한 정적 전달자.
/// Hallway 에서 트랩으로 추락하던 순간의 루트 회전 + 선/각속도를 담아 다음 씬에서 복원한다.
/// </summary>
public static class FallCarry
{
    public static bool active;
    public static float ySpeed;             // (구) y 속도만 — 폴백 호환
    public static bool hasPose;             // 전체 자세 전달 여부
    public static Vector3 velocity;         // 월드 선속도
    public static Vector3 angularVelocity;  // 월드 각속도
    public static Quaternion rotation = Quaternion.identity; // 루트 월드 회전
}

/// <summary>
/// 오픈월드 섬 스테이지 매니저.
/// - Hallway 추락 자세/속도 그대로 공중에서 이어 떨어져 해변에 착지.
/// - 화산에서 흘러나온 용암 강(IslandTerrain.DistanceToRiver)에 빠지면 사망 → 근처 땅 리스폰.
/// 지형/바다/용암/기린은 전부 씬의 실제 편집 가능 오브젝트(이 스크립트는 생성하지 않음).
/// </summary>
public class JungleStage : MonoBehaviour
{
    [Header("낙하 / 착지")]
    [Tooltip("공중 낙하 시작점(해변 위)")]
    public Vector3 spawnPoint = new Vector3(0f, 30f, 0f);
    public float defaultFallSpeed = -14f;

    [Header("리스폰")]
    public Transform respawnPoint;
    public float respawnLift = 1.5f;
    public float respawnCooldown = 1.2f;

    [Header("용암 사망 판정(접촉 기반)")]
    public IslandTerrain island;
    public LavaRiver lava;     // 용암 폭/표면 높이 참조(비우면 자동 검색/폴백)
    [Tooltip("테이블 콜라이더 바닥이 용암 표면에서 이 높이 안(아래)으로 닿으면 사망. = 용암에 '닿으면' 죽음.")]
    public float lavaTouch = 0.35f;
    public float fallbackLavaHalfWidth = 3.5f;
    public float fallbackLavaLift = 1f;
    [Tooltip("리스폰 안전거리 폴백(island.CanyonRimHalf 없을 때).")]
    public float killHalfWidth = 7f;

    Transform table, tableTop;
    Rigidbody[] tableBodies;
    Collider[] tableColliders;
    SceneReset sceneReset;
    float ignoreUntil;
    float lastSafeSide;        // 마지막으로 안전했던(협곡 밖) 강 기준 쪽 → 같은 쪽에 리스폰(건너편 X)

    float LavaHalfWidth => lava != null ? lava.width * 0.5f : fallbackLavaHalfWidth;
    float LavaLift => lava != null ? lava.lift : fallbackLavaLift;

    void Start()
    {
        var tgo = GameObject.Find("Table");
        if (tgo != null)
        {
            table = tgo.transform;
            tableBodies = tgo.transform.root.GetComponentsInChildren<Rigidbody>(true);
            tableColliders = tgo.transform.root.GetComponentsInChildren<Collider>(true);
        }
        var ttgo = GameObject.Find("TableTop");
        if (ttgo != null) tableTop = ttgo.transform;

        if (island == null) island = Object.FindAnyObjectByType<IslandTerrain>();
        if (lava == null) lava = Object.FindAnyObjectByType<LavaRiver>();
        sceneReset = Object.FindAnyObjectByType<SceneReset>();
        if (island != null) lastSafeSide = island.SignedSideOfRiver(spawnPoint.x, spawnPoint.z);
        if (respawnPoint == null)
        {
            var rp = GameObject.Find("RespawnPoint");
            if (rp != null) respawnPoint = rp.transform;
        }

        if (table != null && tableBodies != null)
        {
            // 이어받을 자세/속도 결정
            Rigidbody board = table.GetComponent<Rigidbody>();
            Quaternion R = (FallCarry.hasPose && board != null) ? FallCarry.rotation
                          : (board != null ? board.rotation : Quaternion.identity);
            Vector3 vel; Vector3 ang = Vector3.zero;
            if (FallCarry.hasPose)
            {
                vel = FallCarry.velocity;
                ang = FallCarry.angularVelocity;
                if (vel.y > -3f) vel.y = defaultFallSpeed;
            }
            else
            {
                float vy = FallCarry.active ? FallCarry.ySpeed : defaultFallSpeed;
                if (vy > -3f) vy = defaultFallSpeed;
                vel = new Vector3(0f, vy, 0f);
            }
            FallCarry.active = false;
            FallCarry.hasPose = false;

            // ⚠️ 빈 부모 + 동적 자식 → 부모만 옮기면 안 됨. 상판을 spawnPoint·회전 R 로 두고
            // 전체를 강체이동/회전(상대 배치 유지)해 모든 Rigidbody 를 직접 옮긴다 → Hallway 자세 그대로 이어짐.
            if (board != null)
            {
                Quaternion delta = R * Quaternion.Inverse(board.rotation);
                Vector3 pivot = board.position;
                foreach (var rb in tableBodies)
                {
                    if (rb == null) continue;
                    rb.position = spawnPoint + delta * (rb.position - pivot);
                    rb.rotation = delta * rb.rotation;
                    if (!rb.isKinematic) { rb.linearVelocity = vel; rb.angularVelocity = ang; }
                }
                table.root.SetPositionAndRotation(spawnPoint, R);
            }
            else
            {
                table.root.SetPositionAndRotation(spawnPoint, R);
                SetVelocity(vel, ang);
            }
        }
    }

    void FixedUpdate()
    {
        if (table == null || island == null) return;

        // 안전할 때(협곡 밖 단단한 땅) 마지막 '쪽'을 기록 → 빠지면 그 쪽(원래 있던 쪽)에 리스폰.
        if (tableTop != null)
        {
            float dr = island.DistanceToRiver(tableTop.position.x, tableTop.position.z);
            if (dr > island.CanyonRimHalf + 1f)
                lastSafeSide = island.SignedSideOfRiver(tableTop.position.x, tableTop.position.z);
        }
        if (Time.time < ignoreUntil) return;

        // 용암에 '닿으면' 사망: 테이블 콜라이더 바닥이 용암 표면에 닿거나 잠기면.
        if (tableColliders != null)
            foreach (var col in tableColliders)
                if (col != null && !col.isTrigger && TouchesLava(col)) { Respawn(); return; }
    }

    /// <summary>실제 용암 리본 표면의 월드 높이. LavaRiver 메시 생성과 동일하게
    /// **강 중심선 바닥(HeightAt) + lift + 리본 오브젝트의 transform.y** 로 계산 → 보이는 용암과 정확히 일치.
    /// (리본은 좁은 협곡 바닥 중심선 위에 떠 있으므로, 림 위 지형이 아니라 중심선 기준으로 잰다.)</summary>
    float LavaSurfaceY(float x, float z)
    {
        Vector2 cp = island.NearestRiverPoint(x, z);
        float baseY = island.HeightAt(cp.x, cp.y);
        return lava != null ? baseY + lava.lift + lava.transform.position.y : baseY + fallbackLavaLift;
    }

    /// <summary>콜라이더가 용암에 닿았는지: 용암 리본 폭 안 + 콜라이더 바닥이 **실제 용암 표면** 이하.
    /// → 용암에 실제로 닿거나 잠겨야 죽고, 협곡 림(높은 단단한 땅) 위에선 안 죽음.</summary>
    bool TouchesLava(Collider col)
    {
        Bounds b = col.bounds;
        float cx = b.center.x, cz = b.center.z;
        if (island.DistanceToRiver(cx, cz) >= LavaHalfWidth) return false;    // 용암 리본 폭 밖
        return b.min.y <= LavaSurfaceY(cx, cz) + lavaTouch;                   // 바닥이 용암 표면에 닿음/잠김
    }

    /// <summary>빠진 지점에서 **원래 있던 쪽(side)** 의 가장 가까운 협곡 밖 평평한 고지대를 찾는다. 못 찾으면 쪽 제약 풀고 재시도.</summary>
    Vector3 FindNearestSafe(Vector2 from, float side)
    {
        var a = SearchSafe(from, side);
        if (a.HasValue) return a.Value;
        var b = SearchSafe(from, 0f);            // 같은 쪽에 마땅한 곳 없으면 제약 풀기
        if (b.HasValue) return b.Value;
        return respawnPoint != null ? respawnPoint.position : spawnPoint;
    }

    Vector3? SearchSafe(Vector2 from, float side)
    {
        float rim = island != null ? island.CanyonRimHalf : killHalfWidth;
        float safeDist = rim + 4.5f;            // 협곡 림보다 충분히 바깥(가장자리에서 안 미끄러지게)
        float waterY = island != null ? island.waterLevel : 0f;
        for (float r = 6f; r <= 130f; r += 3f)
        {
            int steps = Mathf.Clamp(Mathf.RoundToInt(r * 1.2f), 12, 56);
            for (int i = 0; i < steps; i++)
            {
                float ang = (i / (float)steps) * Mathf.PI * 2f;
                float x = from.x + Mathf.Cos(ang) * r;
                float z = from.y + Mathf.Sin(ang) * r;
                if (island.DistanceToRiver(x, z) < safeDist) continue;            // 협곡 안/벽 제외
                if (side != 0f && island.SignedSideOfRiver(x, z) != side) continue; // 같은 쪽만(건너편 X)
                float h = island.HeightAt(x, z);
                if (h < waterY + 2.0f) continue;                                  // 물/저지대 제외
                float s = 1.5f;                                                   // 경사 체크(평평한 곳만 → 안 미끄러짐)
                float gx = Mathf.Abs(island.HeightAt(x + s, z) - island.HeightAt(x - s, z));
                float gz = Mathf.Abs(island.HeightAt(x, z + s) - island.HeightAt(x, z - s));
                if ((gx + gz) / (4f * s) > 0.28f) continue;
                return new Vector3(x, h, z);
            }
        }
        return null;
    }

    void Respawn()
    {
        ignoreUntil = Time.time + respawnCooldown;
        // 빠진 자리에서 '원래 있던 쪽'의 가장 가까운 안전한 땅을 찾아 똑바로 복원(건너편 X).
        Vector2 fell = tableTop != null ? new Vector2(tableTop.position.x, tableTop.position.z)
                                        : new Vector2(table.position.x, table.position.z);
        Vector3 safe = FindNearestSafe(fell, lastSafeSide);

        if (sceneReset != null) { sceneReset.ResetPoseAtXZ(safe.x, safe.z); return; }

        // 폴백: SceneReset 이 없을 때만 직접 강체이동.
        if (table == null || tableBodies == null) return;
        Vector3 target = safe;
        target.y += respawnLift;
        Rigidbody refBody = table.GetComponent<Rigidbody>();
        if (refBody == null) foreach (var rb in tableBodies) { if (rb != null) { refBody = rb; break; } }
        if (refBody == null) return;
        Quaternion delta = Quaternion.Inverse(refBody.rotation);
        Vector3 pivot = refBody.position;
        foreach (var rb in tableBodies)
        {
            if (rb == null) continue;
            rb.position = target + delta * (rb.position - pivot);
            rb.rotation = delta * rb.rotation;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }
        table.root.SetPositionAndRotation(target, Quaternion.identity);
    }

    void SetVelocity(Vector3 v, Vector3 ang)
    {
        if (tableBodies == null) return;
        foreach (var rb in tableBodies)
        {
            if (rb == null || rb.isKinematic) continue;
            rb.linearVelocity = v;
            rb.angularVelocity = ang;
        }
    }
}
