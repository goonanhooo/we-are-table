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
    // 이번 세션에서 주운 편지(비트마스크 — letterId 정렬 인덱스). 과거 수집분은 발행 안 함(입장 즉시 팝업 방지).
    readonly NetworkVariable<int> letterBits = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    CageButton cage;
    GiraffeGate gate;
    SnowmanCutscene snowman;
    Rigidbody headRb;
    LetterPickup[] letters;
    bool[] letterWasActive;
    int guestLetterSeen;
    bool guestCage, guestGate, guestSnowman;

    public override void OnNetworkSpawn()
    {
        cage = FindAnyObjectByType<CageButton>();
        gate = FindAnyObjectByType<GiraffeGate>();
        snowman = FindAnyObjectByType<SnowmanCutscene>();
        var head = GameObject.Find("SnowmanHead");
        if (head != null) headRb = head.GetComponent<Rigidbody>();

        // 편지: letterId 정렬로 호스트/게스트 인덱스 일치
        letters = FindObjectsByType<LetterPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(letters, (a, b) => string.CompareOrdinal(a.letterId, b.letterId));
        letterWasActive = new bool[letters.Length];
        for (int i = 0; i < letters.Length; i++) letterWasActive[i] = letters[i].gameObject.activeSelf;

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
            // 편지: 세션 시작 시 활성이던 봉투가 비활성되면(=이번 세션에 주움) 발행
            for (int i = 0; i < letters.Length && i < 32; i++)
                if (letterWasActive[i] && letters[i] != null && !letters[i].gameObject.activeSelf
                    && (letterBits.Value & (1 << i)) == 0)
                    letterBits.Value |= 1 << i;
        }
        else
        {
            if (cagePressed.Value && !guestCage) { guestCage = true; if (cage != null) cage.PlayRemote(); }
            if (gateOpened.Value && !guestGate) { guestGate = true; if (gate != null && !gate.Opened) gate.ForceOpen(); }
            if (snowmanPlayed.Value && !guestSnowman) { guestSnowman = true; if (snowman != null) snowman.PlayRemote(); }
            // 편지: 호스트가 주우면 전원 함께 수집(협동 공유 진행)
            int fresh = letterBits.Value & ~guestLetterSeen;
            if (fresh != 0)
            {
                for (int i = 0; i < letters.Length && i < 32; i++)
                    if ((fresh & (1 << i)) != 0 && letters[i] != null)
                    {
                        LetterSystem.Collect(letters[i].letterId);
                        LetterOverlay.Show(letters[i].letterId);
                        letters[i].gameObject.SetActive(false);
                    }
                guestLetterSeen = letterBits.Value;
            }
        }
    }
}
