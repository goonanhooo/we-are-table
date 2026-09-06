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
    // ★2026-08-20 액자 맵: 화분 퍼즐 해결(문 개방) / 공 수집 마스크 / 애벌레 컷씬 시작
    readonly NetworkVariable<bool> frameSolved = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<int> carriedMask = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> climbCutscene = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // ★2026-09-04 IceCavern 광차: 호스트가 각 광차의 선로 진행도 u를 발행, 게스트는 RailRide.netRemote로 따라감(최대 3대)
    readonly NetworkVariable<float> cartU0 = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<float> cartU1 = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<float> cartU2 = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // ★2026-09-05 빙산 결말 로켓 발사 컷씬(호스트 트리거 → 게스트 PlayRemote)
    readonly NetworkVariable<bool> rocketLaunched = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // ★2026-09-05 우주 맵: 도입 컷씬(로켓 파괴→오징어)·달 흡수
    readonly NetworkVariable<bool> spaceIntroStarted = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> moonCaptured = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // ★2026-09-06 도입 컷씬 시작 서버 시각(게스트가 늦게 합류한 만큼 건너뛰어 재생) / 타이머 맵 CLEAR(달·트랙 등 — 게스트는 트리거 물리가 없어 직접 판정 불가)
    readonly NetworkVariable<double> spaceIntroStartTime = new(0.0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> stageCleared = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // ★2026-09-06 광차 동굴 정거장 도착 컷씬(의자 광차)
    readonly NetworkVariable<bool> cavernChairStarted = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // ★2026-09-06 달 맵 결말(집·부엌·의자) 컷씬
    readonly NetworkVariable<bool> moonEndingStarted = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>디버그/검증용: 현재 발행된 편지 비트마스크.</summary>
    public int LetterBitsDebug => letterBits.Value;

    CageButton cage;
    GiraffeGate gate;
    SnowmanCutscene snowman;
    FrameHallStage frame;
    FrameHallClimb climb;
    RailRide rail;
    RocketLaunchCutscene rocketLaunch;
    SpaceIntroCutscene spaceIntro;
    MoonCapture moonCapture;
    StageTimer stageTimer;
    CavernChairCutscene cavernChair;
    MoonHomeEnding moonEnding;
    bool guestRocket, guestSpaceIntro, guestMoon, guestCleared, guestCavernChair, guestMoonEnding;
    bool guestFrame, guestClimb;
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
        frame = FindAnyObjectByType<FrameHallStage>();
        climb = FindAnyObjectByType<FrameHallClimb>();
        rail = FindAnyObjectByType<RailRide>();
        rocketLaunch = FindAnyObjectByType<RocketLaunchCutscene>();
        spaceIntro = FindAnyObjectByType<SpaceIntroCutscene>();
        moonCapture = FindAnyObjectByType<MoonCapture>();
        stageTimer = FindAnyObjectByType<StageTimer>();
        cavernChair = FindAnyObjectByType<CavernChairCutscene>();
        moonEnding = FindAnyObjectByType<MoonHomeEnding>();
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
            if (climb != null) climb.netGuest = true;
            if (rail != null) rail.netRemote = true;
            if (rocketLaunch != null) rocketLaunch.netRemoteDriven = true;
            if (spaceIntro != null) spaceIntro.netRemoteDriven = true;
            if (moonCapture != null) moonCapture.netRemoteDriven = true;
            if (cavernChair != null) cavernChair.netRemoteDriven = true;
            if (moonEnding != null) moonEnding.netRemoteDriven = true;
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
            if (frame != null && frame.Solved && !frameSolved.Value) frameSolved.Value = true;
            if (climb != null)
            {
                int cm = climb.CarriedMask;
                if (cm != carriedMask.Value) carriedMask.Value = cm;
                if (climb.CutsceneStarted && !climbCutscene.Value) climbCutscene.Value = true;
            }
            if (rocketLaunch != null && rocketLaunch.Started && !rocketLaunched.Value) rocketLaunched.Value = true;
            if (spaceIntro != null && spaceIntro.Started && !spaceIntroStarted.Value) { spaceIntroStartTime.Value = spaceIntro.StartServerTime; spaceIntroStarted.Value = true; }
            if (moonCapture != null && moonCapture.Started && !moonCaptured.Value) moonCaptured.Value = true;
            if (stageTimer != null && stageTimer.Cleared && !stageCleared.Value) stageCleared.Value = true;
            if (cavernChair != null && cavernChair.Started && !cavernChairStarted.Value) cavernChairStarted.Value = true;
            if (moonEnding != null && moonEnding.Started && !moonEndingStarted.Value) moonEndingStarted.Value = true;
            if (rail != null)
            {
                float u0 = rail.CartU(0), u1 = rail.CartU(1), u2 = rail.CartU(2);
                if (Mathf.Abs(u0 - cartU0.Value) > 1e-4f) cartU0.Value = u0;
                if (Mathf.Abs(u1 - cartU1.Value) > 1e-4f) cartU1.Value = u1;
                if (Mathf.Abs(u2 - cartU2.Value) > 1e-4f) cartU2.Value = u2;
            }
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
            if (frameSolved.Value && !guestFrame) { guestFrame = true; if (frame != null) frame.Solve(); }
            if (climb != null)
            {
                climb.NetApplyCarried(carriedMask.Value);
                if (climbCutscene.Value && !guestClimb) { guestClimb = true; climb.PlayRemote(); }
            }
            if (rail != null) { rail.NetApply(0, cartU0.Value); rail.NetApply(1, cartU1.Value); rail.NetApply(2, cartU2.Value); }
            if (rocketLaunched.Value && !guestRocket) { guestRocket = true; if (rocketLaunch != null) rocketLaunch.PlayRemote(); }
            if (spaceIntroStarted.Value && !guestSpaceIntro)
            {
                guestSpaceIntro = true;
                if (spaceIntro != null)
                {
                    float elapsed = spaceIntroStartTime.Value > 0.0 ? (float)(NetworkManager.ServerTime.Time - spaceIntroStartTime.Value) : 0f;
                    spaceIntro.PlayRemote(Mathf.Clamp(elapsed, 0f, 120f));
                }
            }
            if (moonCaptured.Value && !guestMoon) { guestMoon = true; if (moonCapture != null) moonCapture.PlayRemote(); }
            if (stageCleared.Value && !guestCleared) { guestCleared = true; if (stageTimer != null && !stageTimer.Cleared) stageTimer.Clear(); }
            if (cavernChairStarted.Value && !guestCavernChair) { guestCavernChair = true; if (cavernChair != null) cavernChair.PlayRemote(); }
            if (moonEndingStarted.Value && !guestMoonEnding) { guestMoonEnding = true; if (moonEnding != null) moonEnding.PlayRemote(); }
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
