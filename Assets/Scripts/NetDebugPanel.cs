using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 멀티 세션 UI(NetworkManager 프리팹에 부착 — 전 씬 존재).
/// ★ 세션이 활성일 때만 그린다 — 싱글/로컬 플레이 화면에는 아무것도 안 보임.
///   (방 만들기/참가는 시작 화면의 LevelSelect '온라인 멀티' 메뉴가 담당)
/// - 로비(세션 중, 시작 전): 중앙 큰 창 — 방코드 표시 + 다리 2×2 클릭 선택 + 호스트 [게임 시작]/[나가기]
/// - 인게임(시작 후): 우상단 미니(내 다리 + 나가기, F9 숨김)
/// - 호스트 퇴장 → 게스트 세션 종료 + 씬 재로드(설계대로)
/// ⚠️ GUI.Button은 New Input System에서 안 먹음 — Rect+Mouse.current 수동 판정.
/// </summary>
public class NetDebugPanel : MonoBehaviour
{
    bool visible = true;
    string status = "";
    GUIStyle label, btn, big, small;
    Texture2D white;

    NetworkManager NM => NetworkManager.Singleton;
    NetLegLink Link => FindAnyObjectByType<NetLegLink>();
    static readonly string[] LegNames = { "앞왼발 (WASD)", "앞오른발 (TFGH)", "뒤왼발 (IJKL)", "뒤오른발 (방향키)" };
    // 로비에서 호스트가 고르는 맵(LevelSelect.levels와 같은 목록 — 씬 이름 변경 시 둘 다 갱신)
    static readonly string[] MapNames = { "시작", "정글", "눈 덮인 산", "우주", "트랙", "달", "모험", "등반", "경주 (대전)", "축구 (대전)" };
    static readonly string[] MapScenes = { "Hallway", "Jungle", "IceMountain", "Space", "Track", "Moon", "Adventure", "Climb", "Race", "Soccer" };

    void Start() { if (NM != null) NM.OnClientDisconnectCallback += OnDisconnected; }
    void OnDestroy() { if (NM != null) NM.OnClientDisconnectCallback -= OnDisconnected; }

    void OnDisconnected(ulong clientId)
    {
        if (NM == null) return;
        if (!NM.IsServer && clientId == NM.LocalClientId)
        {
            status = "호스트 연결 종료 — 세션 끝";
            _ = NetSession.LeaveAsync();
            NM.Shutdown();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }
    }

    bool InSession => NM != null && (NM.IsHost || NM.IsConnectedClient);
    bool InLobby => InSession && Link != null && !Link.Started;

    // 맵 개수에 따라 로비 높이 가변(3열 그리드 줄 수 기준)
    static float ActionY => 372f + ((MapNames.Length + 2) / 3) * 46f + 10f;   // 시작/나가기 줄 y
    static float PanelH => ActionY + 44f + 30f;                               // + status 줄
    Rect Panel() => InLobby
        ? new Rect((Screen.width - 460f) * 0.5f, (Screen.height - PanelH) * 0.5f, 460f, PanelH)   // 로비=중앙 큰 창
        : new Rect(Screen.width - 260f, 8f, 252f, 96f);                                            // 인게임=우상단 미니
    Rect R(float x, float y, float w, float h) { var p = Panel(); return new Rect(p.x + x, p.y + y, w, h); }
    Rect MapBtn(int i) => R(20f + (i % 3) * 144f, 372f + (i / 3) * 46f, 132f, 40f);   // 3열 그리드

    void Update()
    {
        if (NM == null || !InSession) return;   // ★ 세션 없으면 완전 비활성(싱글 화면 오염 금지)

        var kb = Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame) visible = !visible;
        if (!visible && !InLobby) return;       // 로비는 항상 표시(다리 선택 필수), 미니 패널만 F9 숨김

        var m = Mouse.current;
        if (m == null || !m.leftButton.wasPressedThisFrame) return;
        Vector2 p = m.position.ReadValue();
        Vector2 gm = new Vector2(p.x, Screen.height - p.y);

        if (InLobby)
        {
            var link = Link;
            for (int i = 0; i < 4; i++)
                if (LegBtn(i).Contains(gm)) { link.ClaimLegRpc((byte)i); return; }   // ★토글(여러 개 선택 가능)
            if (NM.IsHost)
                for (int i = 0; i < MapNames.Length; i++)
                    if (MapBtn(i).Contains(gm)) { link.SetMap(i); return; }
            if (NM.IsHost && R(20f, ActionY, 200f, 44f).Contains(gm))
            {
                // 게임 시작 = 다리/세션 잠금 + 전원 선택 맵으로 이동(로비는 시작 화면에 있으므로 여기서 로드)
                if (link.MapIndex < 0) { status = "맵을 먼저 선택하세요"; return; }
                link.StartGame();
                NetScene.Load(MapScenes[link.MapIndex]);
            }
            if (R(240f, ActionY, 200f, 44f).Contains(gm)) LeaveAll();
        }
        else
        {
            if (R(12f, 56f, 228f, 30f).Contains(gm)) LeaveAll();
        }
    }

    Rect LegBtn(int i)
    {
        // 2×2 배치(테이블처럼): 0=앞왼 1=앞오 2=뒤왼 3=뒤오
        float x = (i % 2 == 0) ? 20f : 240f;
        float y = (i < 2) ? 150f : 240f;
        return R(x, y, 200f, 76f);
    }

    void LeaveAll()
    {
        NetLegLink.ResetSessionCache();
        _ = NetSession.LeaveAsync();
        if (NM != null) NM.Shutdown();
        status = "세션 종료";
        SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    Vector2 GuiMouse()
    {
        var m = Mouse.current;
        if (m == null) return new Vector2(-1f, -1f);
        Vector2 p = m.position.ReadValue();
        return new Vector2(p.x, Screen.height - p.y);
    }

    void OnGUI()
    {
        if (NM == null || !InSession) return;   // ★ 세션 없으면 아무것도 안 그림
        if (!visible && !InLobby) return;
        EnsureStyles();
        var panel = Panel();
        UiKit.Panel(panel, 0.97f);   // 흰 배경 + 얇은 검은 테두리
        Vector2 gm = GuiMouse();

        if (InLobby)
        {
            var link = Link;
            GUI.Label(R(0f, 12f, panel.width, 34f), "— 로비 —", big);
            GUI.Label(R(0f, 48f, panel.width, 34f),
                NM.IsHost ? "방 코드:  " + (string.IsNullOrEmpty(NetSession.JoinCode) ? "(로컬)" : NetSession.JoinCode)
                          : "참가 완료 — 다리를 고르세요", big);
            GUI.Label(R(0f, 86f, panel.width, 26f), "다리 클릭 = 선택/해제 — 여러 개 가능 (접속 " + NM.ConnectedClientsList.Count + "명)", small);
            GUI.Label(R(0f, 112f, panel.width, 26f), "선택 안 된 다리는 호스트 키보드가 조종합니다", small);

            for (int i = 0; i < 4; i++)
            {
                long owner = link != null ? link.OwnerOf(i) : -2;
                bool mine = link != null && link.IsMine(i);
                var r = LegBtn(i);
                UiKit.Panel(r);
                if (mine) UiKit.Fill(r, new Color(0f, 0f, 0f, 0.14f));            // 내 자리 = 진한 음영
                else if (owner != -2) UiKit.Fill(r, new Color(0f, 0f, 0f, 0.05f)); // 남의 자리 = 옅은 음영
                else if (r.Contains(gm)) UiKit.Fill(r, new Color(0f, 0f, 0f, 0.08f));
                string who = mine ? "★ 나 (다시 클릭=해제)" : owner == -2 ? "빈 자리" : owner == -1 ? "호스트" : "P" + owner;
                GUI.Label(new Rect(r.x, r.y + 10f, r.width, 28f), LegNames[i], btn);
                GUI.Label(new Rect(r.x, r.y + 42f, r.width, 26f), who, small);
            }

            // 맵 선택(호스트=클릭, 게스트=표시만)
            int sel = link != null ? link.MapIndex : -1;
            GUI.Label(R(0f, 340f, panel.width, 26f), NM.IsHost ? "맵 선택:" : "맵 (호스트가 선택): " + (sel >= 0 ? MapNames[sel] : "선택 중…"), small);
            for (int i = 0; i < MapNames.Length; i++)
            {
                var r = MapBtn(i);
                UiKit.Panel(r);
                if (sel == i) UiKit.Fill(r, new Color(0f, 0f, 0f, 0.16f));
                else if (NM.IsHost && r.Contains(gm)) UiKit.Fill(r, new Color(0f, 0f, 0f, 0.08f));
                GUI.Label(r, MapNames[i], btn);
            }

            if (NM.IsHost) Button(R(20f, ActionY, 200f, 44f), sel >= 0 ? "게임 시작" : "게임 시작 (맵 선택!)", gm);
            else GUI.Label(R(20f, ActionY, 200f, 44f), "호스트 대기 중…", btn);
            Button(R(240f, ActionY, 200f, 44f), "나가기", gm);
            if (!string.IsNullOrEmpty(status)) GUI.Label(R(0f, ActionY + 48f, panel.width, 22f), status, small);
            return;
        }

        // 인게임 미니 패널 — 내가 잡은 다리 전부 나열
        var lk = Link;
        string mine2 = "";
        if (lk != null)
            for (int i = 0; i < 4; i++)
                if (lk.IsMine(i)) mine2 += (mine2.Length > 0 ? ", " : "") + LegNames[i];
        if (mine2.Length == 0) mine2 = NM.IsHost ? "(빈 다리 전부)" : "(없음)";
        GUI.Label(R(12f, 6f, 228f, 48f), (NM.IsHost ? "호스트" : "게스트") + " · " + NM.ConnectedClientsList.Count + "명 (F9 숨김)\n내 다리: " + mine2 + " " + status, small);
        Button(R(12f, 56f, 228f, 30f), "나가기", gm);
    }

    void Button(Rect r, string text, Vector2 gm) => UiKit.Button(r, text, r.Contains(gm));
    void EnsureStyles()
    {
        if (label != null) return;
        white = Texture2D.whiteTexture;
        Color ink = new Color(0.08f, 0.08f, 0.08f, 1f);
        label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        label.normal.textColor = ink;
        btn = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btn.normal.textColor = ink;
        big = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        big.normal.textColor = ink;
        small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        small.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    }
}
