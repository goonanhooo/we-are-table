using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>다리 하나의 프레임 입력 상태(눌림 여부). 스윙 4방향 + 기린 신축 2개.</summary>
public struct LegInput
{
    public bool up, down, left, right;   // 앞뒤 스윙 / 긴축 회전
    public bool longer, shorter;         // 기린 모드 다리 신축
    public bool grab, release;           // ★2026-08-20 테니스공 붙이기/떼기(멀티 RPC 경유용)
    public bool Any => up || down || left || right || longer || shorter || grab || release;
}

/// <summary>Calibrated phone's upright axis, in the table's horizontal plane.</summary>
public struct LegPose
{
    public float x, z, yaw;
}

/// <summary>
/// ★ 멀티플레이 대비 입력 허브(테이블 루트에 부착). 다리(LegController)와 기린(GiraffeMode)은
/// 키보드를 직접 읽지 않고 여기서 LegInput을 받는다.
///
/// - 기본(Local): 이 컴포넌트가 키셋별 로컬 키보드를 폴링해 채운다 → 싱글/카우치 4인 동작 기존과 동일.
/// - 네트워크(External): 후일 네트워크 레이어가 SetSource(set, External) 후 매 프레임
///   SetExternal(set, input)으로 원격 플레이어의 입력을 주입한다. 다리 코드는 전혀 몰라도 됨.
///
/// 키 매핑(신축 키는 각 키셋 '플레이어 자리' 소유 — 멀티에서 그 다리 주인이 늘림):
///   WASD: W/S/A/D + 신축 E(늘)/Q(줄) · TFGH: T/G/F/H + Y/R · IJKL: I/K/J/L + O/U · Arrows: ↑↓←→ + ](늘)/P(줄)
/// 보조 키(altUp 등)는 LegController가 RegisterAltKeys로 등록(Arrows 다리의 [ ' ; Enter).
/// 실행순서 -50: 소비자(LegController/GiraffeMode)보다 먼저 폴링.
/// </summary>
[DefaultExecutionOrder(-50)]
public class TableInput : MonoBehaviour
{
    public enum LegSource { Local = 0, External = 1 }

    readonly LegSource[] source = new LegSource[4];
    readonly LegInput[] current = new LegInput[4];
    readonly LegInput[] external = new LegInput[4];
    readonly LegInput[] pendingPhysics = new LegInput[4];
    readonly LegInput[] physics = new LegInput[4];
    readonly LegPose[] externalPose = new LegPose[4];
    readonly bool[] hasExternalPose = new bool[4];
    readonly Key[][] altKeys = new Key[4][];   // [set] = {up,down,left,right} 보조키(없으면 null)

    // 키셋별 기본 매핑: {up, down, left, right, longer, shorter}
    static readonly Key[][] Map = {
        new[]{ Key.W, Key.S, Key.A, Key.D, Key.E, Key.Q },                                  // WASD
        new[]{ Key.T, Key.G, Key.F, Key.H, Key.Y, Key.R },                                  // TFGH
        new[]{ Key.I, Key.K, Key.J, Key.L, Key.O, Key.U },                                  // IJKL
        new[]{ Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.RightBracket, Key.P }, // Arrows
    };

    /// <summary>테이블 계층 어디서든 입력 허브 찾기.</summary>
    public static TableInput Of(Component c)
    {
        if (c == null) return null;
        var ti = c.GetComponentInParent<TableInput>();
        if (ti == null) ti = c.transform.root.GetComponentInChildren<TableInput>(true);
        return ti;
    }

    void Start()
    {
        // ★ NGO NetworkRigidbody는 네트워크 스폰 전(Awake)에 바디를 kinematic으로 만든다 —
        //   세션 없는 싱글/로컬 플레이에서 테이블이 공중에 얼어붙는 원인. 오프라인이면 동적으로 복원.
        //   (온라인은 스폰 시 NetworkRigidbody가 권한 기준으로 다시 설정: 호스트=동적, 게스트=kinematic)
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
            foreach (var rb in transform.root.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = false;

        // ★ 다리끼리 충돌 무시: 앞다리 뒤스윙 + 뒷다리 앞스윙이 겹치는 순간 서로 박아
        //   임펄스 킥(측정: impulse 10, Δv 8)으로 테이블이 '툭' 튀던 원인. 다리-다리 충돌은
        //   게임플레이 기능이 아니므로 무시해도 체감 손실 없음(지형/상판 충돌은 그대로).
        var legCols = new System.Collections.Generic.List<Collider[]>();
        foreach (var lc in transform.root.GetComponentsInChildren<LegController>(true))
            legCols.Add(lc.GetComponentsInChildren<Collider>(true));
        for (int a = 0; a < legCols.Count; a++)
            for (int b = a + 1; b < legCols.Count; b++)
                foreach (var ca in legCols[a])
                    foreach (var cb in legCols[b])
                        if (ca != null && cb != null) Physics.IgnoreCollision(ca, cb, true);
    }

    /// <summary>이 키셋 다리의 현재 프레임 입력.</summary>
    public LegInput Get(LegControlSet set) => current[(int)set];
    // Every physics consumer sees the same sample; short presses survive until one tick.
    public LegInput GetPhysics(LegControlSet set) => physics[(int)set];

    public bool IsLocal(LegControlSet set) => source[(int)set] == LegSource.Local;

    /// <summary>입력 소스 전환 — 네트워크 레이어가 원격 플레이어 배정 시 External로.</summary>
    public void SetSource(LegControlSet set, LegSource s)
    {
        source[(int)set] = s;
        current[(int)set] = pendingPhysics[(int)set] = physics[(int)set] = default;
        hasExternalPose[(int)set] = false;
        if (s == LegSource.External) external[(int)set] = default;   // 이전 잔여 입력 제거
    }

    /// <summary>원격 입력 주입(매 프레임/틱). SetSource(set, External) 상태에서만 반영된다.</summary>
    public void SetExternal(LegControlSet set, LegInput input)
    {
        int i = (int)set;
        external[i] = input;
        if (source[i] == LegSource.External) pendingPhysics[i] = Merge(pendingPhysics[i], input);
    }

    public void SetExternalPose(LegControlSet set, float x, float z, float yaw)
    {
        int i = (int)set;
        if (source[i] != LegSource.External || !float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(yaw)) return;
        Vector2 tilt = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
        externalPose[i] = new LegPose { x = tilt.x, z = tilt.y, yaw = Mathf.Clamp(yaw, -3600f, 3600f) };
        hasExternalPose[i] = true;
    }

    public void ClearExternalPose(LegControlSet set) => hasExternalPose[(int)set] = false;

    public bool TryGetExternalPose(LegControlSet set, out LegPose pose)
    {
        int i = (int)set;
        pose = externalPose[i];
        return source[i] == LegSource.External && hasExternalPose[i];
    }

    /// <summary>보조 키 등록(LegController가 Awake에서 호출 — Arrows 다리의 추가 키 등).</summary>
    public void RegisterAltKeys(LegControlSet set, Key up, Key down, Key left, Key right)
    {
        if (up == Key.None && down == Key.None && left == Key.None && right == Key.None) return;
        altKeys[(int)set] = new[] { up, down, left, right };
    }

    void Update()
    {
        var kb = Keyboard.current;
        for (int i = 0; i < 4; i++)
        {
            current[i] = source[i] == LegSource.Local ? PollLocal(kb, i) : external[i];
            pendingPhysics[i] = source[i] == LegSource.Local && (PauseMenu.InputBlocked || LetterOverlay.IsOpen)
                ? default : Merge(pendingPhysics[i], current[i]);
        }
    }

    void FixedUpdate()
    {
        for (int i = 0; i < 4; i++)
        {
            bool blocked = source[i] == LegSource.Local && (PauseMenu.InputBlocked || LetterOverlay.IsOpen);
            physics[i] = blocked ? default : Merge(pendingPhysics[i], source[i] == LegSource.Local ? current[i] : external[i]);
            pendingPhysics[i] = default;
        }
    }

    static LegInput Merge(LegInput a, LegInput b) => new LegInput
    {
        up = a.up || b.up, down = a.down || b.down, left = a.left || b.left, right = a.right || b.right,
        longer = a.longer || b.longer, shorter = a.shorter || b.shorter,
        grab = a.grab || b.grab, release = a.release || b.release
    };

    /// <summary>키셋 하나의 로컬 키보드 상태(방향키 보조 키 포함) — 게스트가 자기 다리 입력을 읽어 서버로 보낼 때도 사용.</summary>
    // ★2026-08-20 테니스 붙이기/떼기 키(신축/분사와 공유) — 키셋 순서 WASD/TFGH/IJKL/Arrows
    static readonly Key[] GrabKeys = { Key.E, Key.Y, Key.O, Key.RightBracket };
    static readonly Key[] ReleaseKeys = { Key.Q, Key.R, Key.U, Key.P };

    public static LegInput PollKeyset(LegControlSet set)
    {
        var kb = Keyboard.current;
        if (kb == null || PauseMenu.InputBlocked || LetterOverlay.IsOpen) return default;
        var m = Map[(int)set];
        int k = (int)set;
        return new LegInput
        {
            up      = kb[m[0]].isPressed || (k==3 && kb[Key.LeftBracket].isPressed),
            down    = kb[m[1]].isPressed || (k==3 && kb[Key.Quote].isPressed),
            left    = kb[m[2]].isPressed || (k==3 && kb[Key.Semicolon].isPressed),
            right   = kb[m[3]].isPressed || (k==3 && kb[Key.Enter].isPressed),
            longer  = kb[m[4]].isPressed,
            shorter = kb[m[5]].isPressed,
            grab    = k >= 0 && k < 4 && kb[GrabKeys[k]].isPressed,
            release = k >= 0 && k < 4 && kb[ReleaseKeys[k]].isPressed,
        };
    }

    LegInput PollLocal(Keyboard kb, int i)
    {
        if (kb == null || PauseMenu.InputBlocked || LetterOverlay.IsOpen) return default;
        var inp = PollKeyset((LegControlSet)i);
        var alt = altKeys[i];
        if (alt != null)
        {
            if (alt[0] != Key.None && kb[alt[0]].isPressed) inp.up = true;
            if (alt[1] != Key.None && kb[alt[1]].isPressed) inp.down = true;
            if (alt[2] != Key.None && kb[alt[2]].isPressed) inp.left = true;
            if (alt[3] != Key.None && kb[alt[3]].isPressed) inp.right = true;
        }
        return inp;
    }
}
