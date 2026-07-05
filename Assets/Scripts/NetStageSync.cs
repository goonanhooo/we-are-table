using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ★멀티: 씬 이벤트/월드 오브젝트 동기화(호스트 권한). 씬마다 NetworkObject와 함께 배치("NetStageSync" GO).
/// - 호스트: 로컬 상태(철창 버튼/구멍 문/눈사람 컷씬)를 감시해 NetworkVariable로 발행.
/// - 게스트: 플래그가 켜지면 같은 연출을 로컬 재생(철창 상승·문 슬라이드·컷씬 카메라/말풍선) —
///   물리 포즈(테이블/눈사람 머리)는 호스트 것이 NetworkTransform으로 온다.
/// - 눈사람 머리: 게스트에선 kinematic(수신 전용), 근접 자동 컷씬도 호스트 판정만.
/// 씬에 해당 오브젝트가 없으면 각 항목은 자동 무시(정글=철창/문, 빙산=눈사람).
/// </summary>
public class NetStageSync : NetworkBehaviour
{
    readonly NetworkVariable<bool> cagePressed = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> gateOpened = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> snowmanPlayed = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    CageButton cage;
    GiraffeGate gate;
    SnowmanCutscene snowman;
    Rigidbody headRb;
    bool guestCage, guestGate, guestSnowman;

    public override void OnNetworkSpawn()
    {
        cage = FindAnyObjectByType<CageButton>();
        gate = FindAnyObjectByType<GiraffeGate>();
        snowman = FindAnyObjectByType<SnowmanCutscene>();
        var head = GameObject.Find("SnowmanHead");
        if (head != null) headRb = head.GetComponent<Rigidbody>();

        if (!IsServer)
        {
            // 게스트: 머리는 호스트 포즈 수신 전용 + 컷씬 근접 자동발동 차단(NetStageSync가 발동)
            if (headRb != null) headRb.isKinematic = true;
            if (snowman != null) snowman.netRemoteDriven = true;
        }
    }

    void Update()
    {
        if (!IsSpawned) return;
        if (IsServer)
        {
            if (cage != null && cage.PressedState && !cagePressed.Value) cagePressed.Value = true;
            if (gate != null && gate.Opened && !gateOpened.Value) gateOpened.Value = true;
            if (snowman != null && snowman.Started && !snowmanPlayed.Value) snowmanPlayed.Value = true;
        }
        else
        {
            if (cagePressed.Value && !guestCage) { guestCage = true; if (cage != null) cage.PlayRemote(); }
            if (gateOpened.Value && !guestGate) { guestGate = true; if (gate != null && !gate.Opened) gate.ForceOpen(); }
            if (snowmanPlayed.Value && !guestSnowman) { guestSnowman = true; if (snowman != null) snowman.PlayRemote(); }
        }
    }
}
