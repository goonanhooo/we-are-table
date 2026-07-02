/// <summary>
/// 치트모드가 켜졌을 때 '키만 누르면 바로' 쓸 수 있게 해금되는 능력.
/// 각 능력(GiraffeMode·IcePower·…)이 이 인터페이스를 구현하면 CheatMode가 자동으로 찾아 매 프레임 CheatUnlock()을 호출한다.
/// → 앞으로 새 능력을 추가할 때 이 인터페이스만 구현하면 치트모드에서 별도 코드 없이 즉시 사용 가능.
/// </summary>
public interface ICheatUnlockable
{
    /// <summary>해금 처리(예: locked=false / unlocked=true). 매 프레임 호출되므로 멱등이어야 함.</summary>
    void CheatUnlock();
}
