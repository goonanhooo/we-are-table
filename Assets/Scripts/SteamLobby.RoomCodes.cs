using System.Threading;
using System.Threading.Tasks;
#if UNITY_STANDALONE || UNITY_EDITOR
using Steamworks;
using Steamworks.Data;
#endif

public static partial class SteamLobby
{
#if UNITY_STANDALONE || UNITY_EDITOR
    const string GameKey = "wat_game", GameValue = "we-are-table-rooms-v1", CodeKey = "wat_code";
    // Steam permits one lobby query at a time. Do not let overlapping calls cancel each other.
    static readonly SemaphoreSlim codeQuery = new SemaphoreSlim(1, 1);
    static string InvalidCodeError => L.T("잘못된 방코드", "invalid room code", "ルームコードが正しくありません", "房间代码无效");
    static string MissingCodeError => L.T("방을 찾을 수 없어요. 코드를 확인해 주세요. 이미 시작했거나 가득 찬 방일 수도 있어요.", "Room not found. Check the code; the room may have started or be full.", "ルームが見つかりません。コードを確認してください。開始済みか満員の可能性もあります。", "未找到房间。请确认代码；房间也可能已开始或已满。");
    static string DuplicateCodeError => L.T("같은 코드의 방이 있어요. 호스트가 방을 다시 만들어 주세요.", "More than one room has this code. Please ask the host to recreate the room.", "同じコードのルームがあります。ホストにルームを作り直してもらってください。", "有多个房间使用此代码，请让房主重新创建房间。");
    static string CodeServiceError => L.T("방 코드를 발급하지 못했어요. 잠시 후 다시 시도해 주세요.", "Could not create a room code. Please try again shortly.", "ルームコードを発行できませんでした。しばらくしてからお試しください。", "无法生成房间代码，请稍后重试。");

    static async Task<Lobby[]> FindCodeAsync(string code)
    {
        await codeQuery.WaitAsync();
        try
        {
            var pending = SteamMatchmaking.LobbyList.FilterDistanceWorldwide()
                .WithKeyValue(GameKey, GameValue).WithKeyValue(CodeKey, code)
                .WithMaxResults(2).RequestAsync();
            if (await Task.WhenAny(pending, Task.Delay(22000)) != pending)
                throw new System.TimeoutException(L.T("연결 시간이 초과됐어요. 방 코드를 확인하고 다시 시도해 주세요.", "Connection timed out. Check the room code and try again.", "接続がタイムアウトしました。コードを確認して、もう一度お試しください。", "连接超时，请确认房间代码后重试。"));
            // This SDK returns null for both an empty query and a failed query.
            // Hosts must also see their own published lobby before sharing a code.
            return await pending ?? System.Array.Empty<Lobby>();
        }
        finally { codeQuery.Release(); }
    }

    static async Task PublishCodeAsync(Lobby lobby)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string code = RoomCodeInput.GenerateSteamCode();
            if ((await FindCodeAsync(code)).Length != 0) continue;
            // Friends-only lobbies cannot be found by code queries. Public keeps friend invites too.
            if (!lobby.SetData(CodeKey, code) || !lobby.SetPublic() || !lobby.SetJoinable(true))
                throw new System.InvalidOperationException(CodeServiceError);
            for (int check = 0; check < 5; check++)
            {
                var matches = await FindCodeAsync(code);
                if (matches.Length == 1 && matches[0].Id.Value == lobby.Id.Value) return;
                if (matches.Length > 0) break; // Collision, including simultaneous creation: issue another code.
                await Task.Delay(500); // Allow Steam's metadata/search index to catch up.
            }
            lobby.SetJoinable(false);
        }
        throw new System.InvalidOperationException(CodeServiceError);
    }

    static async Task<Lobby?> ResolveCodeAsync(string code)
    {
        var matches = await FindCodeAsync(code);
        if (matches.Length == 0) { LastError = MissingCodeError; return null; }
        // Never silently choose one of two matching rooms.
        if (matches.Length != 1) { LastError = DuplicateCodeError; return null; }
        if (matches[0].GetData("wat_protocol") != Protocol)
        {
            LastError = L.T("게임 버전이 달라요. 모두 같은 버전으로 업데이트해 주세요.", "Game versions differ. Everyone must update to the same version.", "ゲームのバージョンが異なります。全員が同じバージョンに更新してください。", "游戏版本不同，请所有人更新至同一版本。");
            return null;
        }
        return matches[0];
    }
#endif
}
