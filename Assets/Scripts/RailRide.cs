using UnityEngine;

/// <summary>
/// ★2026-09-04 광차 탑승 관리(IceCavern). 테니스공(FixedJoint)이 붙은 광차 = 플레이어 광차 → departDelay 뒤 speed로
/// 자기 선로 끝까지 전진. 끊긴 선로 끝에서 멈추면 옆(나란히 대기 중인) 다음 광차로 옮겨 타고 다시 붙으면 그 광차가 출발.
/// 붙은 다리가 하나도 없으면 정지(내려서 둘러볼 수 있음).
/// 낙하 복구: 테이블이 기준(마지막 플레이어 광차, 없으면 정거장)보다 fallBelow 아래로 떨어지고 아무 데도 안 붙어 있으면 그 위로 되돌림.
/// </summary>
public class RailRide : MonoBehaviour
{
    public MineCart[] carts;
    public float speed = 12f;
    [Tooltip("붙은 뒤 출발까지 대기(초)")] public float departDelay = 1.0f;
    public Transform station;
    [Tooltip("탑승 후: 마지막 광차보다 이만큼 아래로 떨어지면 광차 위로 복구")] public float fallBelow = 8f;
    [Tooltip("탑승 전: 정거장보다 이만큼 아래(동굴 바닥)로 떨어지면 정거장으로 복구 — 시작 방 바닥(-12m)은 걸리지 않게")] public float fallBelowStation = 20f;
    public float recoverCooldown = 3f;

    [Header("탑승 중 카메라 — 가파른 샤프트에서 위/아래를 자유롭게 볼 수 있게 피치 제한 해제")]
    public float ridePitchMin = -80f, ridePitchMax = 85f;

    MineCart player, lastPlayer;
    float departTimer, lastRecover = -99f;
    Transform board; ArticulationBody root;
    CameraFollow follow; float savedMinPitch, savedMaxPitch; bool pitchOverridden;
    public MineCart PlayerCart => player;
    public MineCart LastCart => lastPlayer;

    void Start()
    {
        var b = GameObject.Find("Table"); if (b != null) { board = b.transform; root = b.GetComponent<ArticulationBody>(); }
        var cam = Camera.main; if (cam != null) follow = cam.GetComponent<CameraFollow>();
    }

    void OnDisable() { RestorePitch(); }
    void RestorePitch() { if (pitchOverridden && follow != null) { follow.minPitch = savedMinPitch; follow.maxPitch = savedMaxPitch; } pitchOverridden = false; }

    void FixedUpdate()
    {
        if (carts == null || carts.Length == 0) return;
        MineCart p = null;
        if (lastPlayer != null && lastPlayer.Attached) p = lastPlayer;
        else foreach (var c in carts) if (c != null && c.Attached) { p = c; break; }
        player = p;
        if (player != null)
        {
            if (player != lastPlayer) { lastPlayer = player; departTimer = 0f; }
            if (departTimer < departDelay) departTimer += Time.fixedDeltaTime;
            else if (!player.AtEnd) player.u = Mathf.Min(player.uEnd, player.u + player.track.DuForDistance(player.u, speed * Time.fixedDeltaTime));
        }
        else departTimer = 0f;
        foreach (var c in carts) if (c != null) c.Place();
        // 탑승 중(붙어 있음)엔 카메라 피치 제한 해제, 떼면 복원
        if (follow != null)
        {
            if (player != null && !pitchOverridden) { savedMinPitch = follow.minPitch; savedMaxPitch = follow.maxPitch; follow.minPitch = ridePitchMin; follow.maxPitch = ridePitchMax; pitchOverridden = true; }
            else if (player == null && pitchOverridden) RestorePitch();
        }

        if (board == null || root == null || Time.time - lastRecover < recoverCooldown) return;
        foreach (var c in carts) if (c != null && c.Attached) return;
        Vector3 refPos = lastPlayer != null ? lastPlayer.transform.position + Vector3.up * 1.2f : (station != null ? station.position + Vector3.up * 1.5f : Vector3.zero);
        float limit = lastPlayer != null ? fallBelow : fallBelowStation;
        if (board.position.y < refPos.y - limit)
        {
            lastRecover = Time.time;
            ScreenFader.FadeOut(0.25f);
            root.TeleportRoot(refPos, Quaternion.identity); root.linearVelocity = Vector3.zero; root.angularVelocity = Vector3.zero;
            var rs = Object.FindAnyObjectByType<ArtSceneReset>(); if (rs != null) rs.ResetPoseAtXZ(refPos.x, refPos.z, refPos.y + 1.5f);
            ScreenFader.FadeIn(0.6f);
        }
    }

    /// <summary>[검증용] 광차 i의 u 강제.</summary>
    public void DebugSetU(int i, float u) { if (carts != null && i >= 0 && i < carts.Length && carts[i] != null) { carts[i].u = Mathf.Clamp(u, carts[i].uStart, carts[i].uEnd); carts[i].Place(true); } }
}
