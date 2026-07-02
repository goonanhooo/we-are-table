using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>다리 하나의 프레임 입력 상태(눌림 여부). 스윙 4방향 + 기린 신축 2개.</summary>
public struct LegInput
{
    public bool up, down, left, right;   // 앞뒤 스윙 / 긴축 회전
    public bool longer, shorter;         // 기린 모드 다리 신축
    public bool Any => up || down || left || right || longer || shorter;
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

    /// <summary>이 키셋 다리의 현재 프레임 입력.</summary>
    public LegInput Get(LegControlSet set) => current[(int)set];

    public bool IsLocal(LegControlSet set) => source[(int)set] == LegSource.Local;

    /// <summary>입력 소스 전환 — 네트워크 레이어가 원격 플레이어 배정 시 External로.</summary>
    public void SetSource(LegControlSet set, LegSource s)
    {
        source[(int)set] = s;
        if (s == LegSource.External) external[(int)set] = default;   // 이전 잔여 입력 제거
    }

    /// <summary>원격 입력 주입(매 프레임/틱). SetSource(set, External) 상태에서만 반영된다.</summary>
    public void SetExternal(LegControlSet set, LegInput input) => external[(int)set] = input;

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
            current[i] = source[i] == LegSource.Local ? PollLocal(kb, i) : external[i];
    }

    LegInput PollLocal(Keyboard kb, int i)
    {
        if (kb == null) return default;
        var m = Map[i];
        var inp = new LegInput
        {
            up      = kb[m[0]].isPressed,
            down    = kb[m[1]].isPressed,
            left    = kb[m[2]].isPressed,
            right   = kb[m[3]].isPressed,
            longer  = kb[m[4]].isPressed,
            shorter = kb[m[5]].isPressed,
        };
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
