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
    [Tooltip("보이는 용암 표면 위 접촉 허용 오차. 테이블의 실제 회전된 콜라이더로 검사한다.")]
    public float lavaTouch = 0.025f;
    public float fallbackLavaHalfWidth = 3.5f;
    public float fallbackLavaLift = 1f;
    [Tooltip("리스폰 안전거리 폴백(island.CanyonRimHalf 없을 때).")]
    public float killHalfWidth = 7f;
    [Header("협곡 완주 확인")]
    // Legacy serialized settings retained for scene compatibility; arrival now uses one planted foot.
    [HideInInspector]
    public float crossingClearance = 0.15f;
    [HideInInspector]
    public float crossingSettleTime = 0.35f;
    [Tooltip("몸통이 반대편 가장자리에 도달하고 발 하나를 디딘 뒤 인정할 시간")]
    public float crossingArrivalTime = 0.12f;

    Transform table, tableTop;
    Rigidbody[] tableBodies;
    Collider[] tableColliders;
    SceneReset sceneReset;
    float ignoreUntil;
    readonly JungleBankTracker bank=new JungleBankTracker();
    BoxCollider[] feet;
    ArticulationBody artRoot;
    float lastSafeSide => bank.SafeSide;
    public bool CanSaveAcrossLava => bank.HasCrossed && bank.IsSettled && island!=null
        && bank.SafeSide!=island.SignedSideOfRiver(spawnPoint.x,spawnPoint.z);
    public Vector3 SafeUprightAnchor => lastSafeSide != 0f ? bank.SafePosition : spawnPoint;

    /// <summary>Manual upright recovery must not skip the canyon or land in lava/water.</summary>
    public bool IsSafeUprightRecovery(Vector3 position, float radius)
    {
        if (island == null) return true;
        float side = lastSafeSide != 0f ? lastSafeSide : island.SignedSideOfRiver(spawnPoint.x, spawnPoint.z);
        if (island.SignedSideOfRiver(position.x, position.z) != side
            || island.DistanceToRiver(position.x, position.z) <= island.CanyonRimHalf + radius + .15f
            || island.HeightAt(position.x, position.z) <= island.waterLevel + 1f) return false;
        if (lava != null && Vector2.Distance(new Vector2(position.x, position.z), island.RiverPoint(0f))
            <= lava.craterPoolRadius + radius + .15f) return false;
        return true;
    }

    float LavaHalfWidth => lava != null ? lava.width * 0.5f : fallbackLavaHalfWidth;

    void Start()
    {
        var tgo = GameObject.Find("Table");
        if (tgo != null)
        {
            table = tgo.transform;
            tableBodies = tgo.transform.root.GetComponentsInChildren<Rigidbody>(true);
            tableColliders = tgo.transform.root.GetComponentsInChildren<Collider>(true);
            artRoot=tgo.GetComponent<ArticulationBody>();
            feet=new BoxCollider[4];
            for(int slot=0;slot<4;slot++)
            {
                var leg=LegLayout.Find(tgo.transform.root,slot);
                if(leg!=null)feet[slot]=leg.GetComponent<BoxCollider>();
            }
        }
        var ttgo = GameObject.Find("TableTop");
        if (ttgo != null) tableTop = ttgo.transform;

        if (island == null) island = Object.FindAnyObjectByType<IslandTerrain>();
        if (lava == null) lava = Object.FindAnyObjectByType<LavaRiver>();
        sceneReset = Object.FindAnyObjectByType<SceneReset>();
        if (respawnPoint == null)
        {
            var rp = GameObject.Find("RespawnPoint");
            if (rp != null) respawnPoint = rp.transform;
        }
        if(island!=null)bank.Reset(island.SignedSideOfRiver(spawnPoint.x,spawnPoint.z),
            respawnPoint!=null?respawnPoint.position:new Vector3(spawnPoint.x,island.HeightAt(spawnPoint.x,spawnPoint.z)+respawnLift,spawnPoint.z));

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

            // ⚠️ NaN 가드: 이전 씬에서 폭발 중 캡처된 값이면 5개 바디 전부에 NaN이 한 번에
            //    주입되어 "position is infinite" ×5가 난다 → 비유한 값은 기본값으로 대체.
            if (!(float.IsFinite(R.x) && float.IsFinite(R.y) && float.IsFinite(R.z) && float.IsFinite(R.w)))
                R = Quaternion.identity;
            if (!(float.IsFinite(vel.x) && float.IsFinite(vel.y) && float.IsFinite(vel.z)))
                vel = new Vector3(0f, defaultFallSpeed, 0f);
            if (!(float.IsFinite(ang.x) && float.IsFinite(ang.y) && float.IsFinite(ang.z)))
                ang = Vector3.zero;

            // ★ ArticulationBody 테이블: TeleportRoot 하나로 전체 사슬 이동(관절 상태 보존)
            var artBoard = table.GetComponent<ArticulationBody>();
            if (artBoard != null)
            {
                artBoard.TeleportRoot(spawnPoint, R);
                artBoard.linearVelocity = vel;
                artBoard.angularVelocity = ang;
            }
            else
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
        if (table == null || island == null || !CheckpointRuntime.Authority) return;
        if (Time.time < ignoreUntil) {bank.CancelPending();return;}

        // 용암에 '닿으면' 사망: 테이블 콜라이더 바닥이 용암 표면에 닿거나 잠기면.
        if (tableColliders != null)
            foreach (var col in tableColliders)
                if (col != null && col.enabled && col.gameObject.activeInHierarchy && !col.isTrigger && TouchesLava(col)) { Respawn(); return; }
        float side;
        bool landed=JungleBankContact.TryReachedSide(island,table,feet,out side);
        if(artRoot!=null&&artRoot.immovable)landed=false;
        Vector3 safe = bank.SafePosition;
        if (landed && side != lastSafeSide)
        {
            var destination = SearchSafe(new Vector2(table.position.x, table.position.z), side);
            if (destination.HasValue) safe = destination.Value;
            else landed = false;
        }
        else if (landed && IsSafeUprightRecovery(table.position, 1.5f)) safe = table.position;
        bank.Observe(landed,side,safe,Time.fixedDeltaTime,crossingArrivalTime);
    }

    /// <summary>LavaRiver가 없는 이전 씬에서만 사용하는 중심선 높이 폴백.</summary>
    float LavaSurfaceY(float x, float z)
    {
        Vector2 cp = island.NearestRiverPoint(x, z);
        float baseY = island.HeightAt(cp.x, cp.y);
        return lava != null ? baseY + lava.lift + lava.transform.position.y : baseY + fallbackLavaLift;
    }

    /// <summary>보이는 표면의 삼각형과 실제 콜라이더의 겹침. 중심이 리본 밖이어도 다리 끝 접촉은 잡는다.</summary>
    bool TouchesLava(Collider col)
    {
        if(lava!=null)return lava.Touches(col,lavaTouch);
        Bounds b = col.bounds;
        float cx = b.center.x, cz = b.center.z;
        if (island.DistanceToRiver(cx, cz) >= LavaHalfWidth) return false;    // 용암 리본 폭 밖
        return b.min.y <= LavaSurfaceY(cx, cz) + lavaTouch;                   // 바닥이 용암 표면에 닿음/잠김
    }

    /// <summary>도착을 인정한 강둑에서 안전한 땅을 찾고, 없으면 마지막 안전 위치를 유지한다.</summary>
    Vector3 FindNearestSafe(Vector2 from, float side)
    {
        var a = SearchSafe(from, side);
        if (a.HasValue) return a.Value;
        // Never silently relax the bank constraint after a failed crossing.
        return bank.SafePosition;
    }

    public void RestoreCheckpointBank(Vector3 position)
    {
        if(island!=null)bank.Restore(island.SignedSideOfRiver(position.x,position.z),position);
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

    /// <summary>★F1 '다시 시작'(2026-07-12): 용암 강을 건넌 뒤면 '지금 있던 쪽'의 안전 지점
    /// (용암 사망 리스폰과 같은 체크포인트 규칙), 아직 안 건넜으면 처음 스폰(해변). true=정글이 처리함.</summary>
    public bool RestartRespawn(ArtSceneReset reset)
    {
        if (island == null || reset == null) return false;
        ignoreUntil = Time.time + respawnCooldown;   // 리스폰 직후 용암 판정 유예(기존 리스폰과 동일)
        bank.CancelPending();
        float spawnSide = island.SignedSideOfRiver(spawnPoint.x, spawnPoint.z);
        if (lastSafeSide != spawnSide)
        {
            Vector2 cur = tableTop != null ? new Vector2(tableTop.position.x, tableTop.position.z)
                        : table != null ? new Vector2(table.position.x, table.position.z)
                        : new Vector2(spawnPoint.x, spawnPoint.z);
            Vector3 safe = FindNearestSafe(cur, lastSafeSide);
            reset.ResetPoseAtXZ(safe.x, safe.z, safe.y + 3f);   // 낮은 스캔 = 야자수 위에 안 얹힘
        }
        else
            reset.ResetPoseAtXZ(spawnPoint.x, spawnPoint.z);    // 처음 스폰 해변(트인 하늘)
        return true;
    }

    void Respawn()
    {
        ignoreUntil = Time.time + respawnCooldown;
        bank.CancelPending();
        // 빠진 자리에서 '원래 있던 쪽'의 가장 가까운 안전한 땅을 찾아 똑바로 복원(건너편 X).
        Vector2 fell = tableTop != null ? new Vector2(tableTop.position.x, tableTop.position.z)
                                        : new Vector2(table.position.x, table.position.z);
        Vector3 safe = FindNearestSafe(fell, lastSafeSide);

        var artReset = Object.FindAnyObjectByType<ArtSceneReset>();   // ★ ArticulationBody 테이블
        if (artReset != null) { artReset.ResetPoseAtXZ(safe.x, safe.z); return; }
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
