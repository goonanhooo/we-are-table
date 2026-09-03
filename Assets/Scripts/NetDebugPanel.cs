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

    // ★2026-07-14 로딩/오버레이 겹침 방지: LevelSelect가 아직 떠 있으면(방 만드는 중 로딩 화면 포함)
    //   로비를 그리지도, 클릭을 받지도 않는다 — HostAsync 완료 전 세션이 먼저 열려 둘이 겹치던 문제.
    LevelSelect ls;
    float lsFindAt;
    bool OverlayBlocked
    {
        get
        {
            if (ls == null && Time.unscaledTime > lsFindAt)
            {
                ls = FindAnyObjectByType<LevelSelect>();
                lsFindAt = Time.unscaledTime + 2f;
            }
            return ls != null && ls.Showing;
        }
    }

    // ★맵 선택 3D 프리뷰(StartScreen에만 존재 — 인게임 씬에선 null이라 자동 무시)
    MapSelectPreview preview;
    float previewFindAt;
    int lobbyHover = -1;
    int guestShownSel = -1;
    bool startPending;
    MapSelectPreview Preview
    {
        get
        {
            if (preview == null && Time.unscaledTime > previewFindAt)
            {
                preview = FindAnyObjectByType<MapSelectPreview>();
                previewFindAt = Time.unscaledTime + 2f;
            }
            return preview;
        }
    }
    static readonly string[] LegNames = { "앞왼발 (WASD)", "앞오른발 (TFGH)", "뒤왼발 (IJKL)", "뒤오른발 (방향키)" };
    // 로비에서 호스트가 고르는 맵(LevelSelect.levels와 같은 목록 — 씬 이름 변경 시 둘 다 갱신)
    static readonly string[] MapNames = { "시작", "정글", "테니스장", "눈 덮인 산", "우주", "트랙", "달", "모험", "등반", "경주 (대전)", "축구 (대전)" };
    // ★2026-08-17 영어 지원 — LevelSelect.MapLabel의 영문명과 일치
    static readonly string[] LegNamesEn = { "front left (WASD)", "front right (TFGH)", "back left (IJKL)", "back right (arrows)" };
    static readonly string[] MapNamesEn = { "start", "jungle", "tennis court", "iceberg", "space", "track", "moon", "adventure", "climb", "race (vs)", "soccer (vs)" };
    static string LegName(int i) => L.Korean ? LegNames[i] : LegNamesEn[i];
    static string MapName(int i) => L.Korean ? MapNames[i] : MapNamesEn[i];
    static readonly string[] MapScenes = { "Hallway", "Jungle", "TennisCourt", "IceMountain", "Space", "Track", "Moon", "Adventure", "Climb", "Race", "Soccer" };

    void Start() { if (NM != null) NM.OnClientDisconnectCallback += OnDisconnected; }
    void OnDestroy() { if (NM != null) NM.OnClientDisconnectCallback -= OnDisconnected; }

    void OnDisconnected(ulong clientId)
    {
        if (NM == null) return;
        if (!NM.IsServer && clientId == NM.LocalClientId)
        {
            status = L.T("호스트 연결 종료 — 세션 끝", "host left — session over");
            _ = NetSession.LeaveAsync();
            NM.Shutdown();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }
    }

    bool InSession => NM != null && (NM.IsHost || NM.IsConnectedClient);
    bool InLobby => InSession && Link != null && !Link.Started;

    // ★2026-07-14 로비 = 왼쪽 열(홈 메뉴 계층에 포함, 가운데 창 폐지 — 사용자 요청)
    const float MapRowY = 352f;                                                // 맵 목록 시작 y
    static float ActionY => MapRowY + ((MapNames.Length + 2) / 3) * 44f + 18f; // 시작/나가기 줄 y
    static float PanelH => ActionY + 50f + 28f;                                // + status 줄
    Rect Panel() => InLobby
        ? new Rect(Screen.width * 0.09f, Mathf.Max(24f, Screen.height * 0.34f), 480f, PanelH)  // 로비=왼쪽 열(타이틀 아래)
        : new Rect(Screen.width - 260f, 8f, 252f, 96f);                                        // 인게임=우상단 미니
    Rect R(float x, float y, float w, float h) { var p = Panel(); return new Rect(p.x + x, p.y + y, w, h); }
    Rect MapBtn(int i) => R((i % 3) * 152f, MapRowY + (i / 3) * 44f, 144f, 40f);   // 3열 그리드(텍스트)

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
            if (OverlayBlocked) return;   // 로딩/오버레이 중엔 로비 클릭 무시
            var link = Link;
            for (int i = 0; i < 4; i++)
                if (LegBtn(i).Contains(gm)) { link.ClaimLegRpc((byte)i); return; }   // ★토글(여러 개 선택 가능)
            if (NM.IsHost)
                for (int i = 0; i < MapNames.Length; i++)
                    if (MapBtn(i).Contains(gm))
                    {
                        link.SetMap(i);
                        if (Preview != null) { Preview.Show(i, 1); Preview.Pulse(); }   // 선택 확정 펄스
                        return;
                    }
            if (NM.IsHost && R(0f, ActionY, 300f, 48f).Contains(gm))
            {
                // 게임 시작 = 다리/세션 잠금 + 전원 선택 맵으로 이동(로비는 시작 화면에 있으므로 여기서 로드)
                if (link.MapIndex < 0) { status = L.T("맵을 먼저 선택하세요", "pick a map first"); return; }
                if (startPending) return;
                var pv = Preview;
                if (pv != null)
                {
                    // ★확정 연출(펄스→꺼짐) 후 기존 시작 로직 — 연출 중 재클릭 차단
                    startPending = true;
                    var lk = link;
                    pv.Show(lk.MapIndex, 1);
                    pv.Confirm(() => { startPending = false; lk.StartGame(); NetScene.Load(MapScenes[lk.MapIndex]); });
                }
                else { link.StartGame(); NetScene.Load(MapScenes[link.MapIndex]); }
            }
            if (R(320f, ActionY, 150f, 48f).Contains(gm))
            {
                if (Preview != null) Preview.HideAll();
                LeaveAll();
            }
            // ★스팀 로비: 친구 초대 오버레이 / 방코드 복사
            if (NM.IsHost && SteamLobby.InLobby)
            {
                if (R(0f, ActionY + 52f, 300f, 44f).Contains(gm)) SteamLobby.OpenInviteOverlay();
                if (R(320f, ActionY + 52f, 150f, 44f).Contains(gm))
                { GUIUtility.systemCopyBuffer = NetSession.JoinCode; status = L.T("방코드 복사됨", "room code copied"); }
            }
        }
        else
        {
            if (R(12f, 56f, 228f, 30f).Contains(gm)) LeaveAll();
        }
    }

    Rect LegBtn(int i)
    {
        // 2×2 배치(테이블처럼): 0=앞왼 1=앞오 2=뒤왼 3=뒤오
        float x = (i % 2 == 0) ? 0f : 222f;
        float y = (i < 2) ? 152f : 240f;
        return R(x, y, 206f, 76f);
    }

    void LeaveAll()
    {
        NetLegLink.ResetSessionCache();
        _ = NetSession.LeaveAsync();
        if (NM != null) NM.Shutdown();
        status = L.T("세션 종료", "session ended");
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
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (NM == null || !InSession) return;   // ★ 세션 없으면 아무것도 안 그림
        if (!visible && !InLobby) return;
        EnsureStyles();
        var panel = Panel();
        Vector2 gm = GuiMouse();

        if (InLobby)
        {
            if (OverlayBlocked) return;   // 로딩/오버레이 중엔 로비를 아직 그리지 않음(겹침 방지)
            // ★왼쪽 열 손글씨 레이아웃(홈 메뉴 계층에 포함) — 배경 창/딤 없음
            var link = Link;
            GUI.Label(R(0f, 0f, panel.width, 40f), L.T("함께하기 — 로비", "play together — lobby"), hTitle);
            GUI.Label(R(0f, 46f, panel.width, 40f),
                NM.IsHost ? L.T("방 코드:  ", "room code:  ") + (string.IsNullOrEmpty(NetSession.JoinCode) ? L.T("(로컬)", "(local)") : NetSession.JoinCode)
                          : L.T("참가 완료 — 다리를 고르세요", "joined — pick your legs"), hBig);
            GUI.Label(R(0f, 92f, panel.width, 26f), L.T("다리 클릭 = 선택/해제 — 여러 개 가능 (접속 ", "click a leg to take it — multiple ok (") + NM.ConnectedClientsList.Count + L.T("명)", " joined)"), hSmall);
            GUI.Label(R(0f, 118f, panel.width, 26f), L.T("선택 안 된 다리는 호스트 키보드가 조종합니다", "unclaimed legs follow the host's keyboard"), hSmall);

            for (int i = 0; i < 4; i++)
            {
                long owner = link != null ? link.OwnerOf(i) : -2;
                bool mine = link != null && link.IsMine(i);
                var r = LegBtn(i);
                var col = LegPalette.BySlot[i];
                UiKit.Panel(r);
                // ★다리 고유색으로 채움(호버 색 변화 없음 — 상태에 따라서만 진하기 차이)
                float a = mine ? 0.50f : owner != -2 ? 0.30f : 0.13f;
                UiKit.Fill(r, new Color(col.r, col.g, col.b, a));
                UiKit.Fill(new Rect(r.x, r.y, 10f, r.height), col);   // 왼쪽 진한 색 띠
                string nick = LegPalette.NickOf(owner);
                string who = mine ? "★ " + L.T("나", "me") + " (" + nick + ")" : owner == -2 ? L.T("빈 자리", "open") : nick;
                GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 14f, 30f), LegName(i), tileName);
                GUI.Label(new Rect(r.x + 14f, r.y + 42f, r.width - 14f, 26f), who, tileSub);
            }

            // 맵 선택(호스트=클릭, 게스트=표시만) — 손글씨 목록, 선택 = 밑줄
            int sel = link != null ? link.MapIndex : -1;
            GUI.Label(R(0f, MapRowY - 30f, panel.width, 26f),
                NM.IsHost ? L.T("맵 선택", "choose a map") : L.T("맵 (호스트가 선택): ", "map (host picks): ") + (sel >= 0 ? MapName(sel) : L.T("선택 중…", "picking…")), hSmall);
            for (int i = 0; i < MapNames.Length; i++)
            {
                var r = MapBtn(i);
                bool hov = NM.IsHost && r.Contains(gm);
                GUI.Label(r, MapName(i), mapItem);
                // 선택된 맵 = 밑줄 고정, 호스트 호버 = 밑줄 미리보기(색 변화 없음)
                if ((sel == i || hov) && HandUI.Underline != null)
                {
                    float tw = Mathf.Min(mapItem.CalcSize(new GUIContent(MapName(i))).x + 8f, r.width);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - 8f, tw, 12f), HandUI.Underline, ScaleMode.StretchToFill);
                }
                // ★호스트: 호버 = 3D 프리뷰 전환(방향 = 인덱스 증감)
                if (hov && Preview != null && !startPending && i != lobbyHover)
                {
                    int dir = (lobbyHover >= 0 && i < lobbyHover) ? -1 : 1;
                    Preview.Show(i, dir);
                    lobbyHover = i;
                }
            }
            // ★게스트: 호스트가 고른 맵이 바뀌면 프리뷰 전환
            if (!NM.IsHost && sel >= 0 && sel != guestShownSel && Preview != null)
            {
                Preview.Show(sel, sel < guestShownSel ? -1 : 1);
                guestShownSel = sel;
            }

            if (NM.IsHost) HandBtn(R(0f, ActionY, 300f, 48f), sel >= 0 ? L.T("게임 시작", "start game") : L.T("게임 시작 (맵 선택!)", "start (pick a map!)"), gm);
            else GUI.Label(R(0f, ActionY, 300f, 48f), L.T("호스트 대기 중…", "waiting for host…"), hBig);
            HandBtn(R(320f, ActionY, 150f, 48f), L.T("나가기", "leave"), gm);
            // ★스팀 로비(2026-08-09): 오버레이 친구 초대 + 긴 방코드 복사 버튼
            if (NM.IsHost && SteamLobby.InLobby)
            {
                HandBtn(R(0f, ActionY + 52f, 300f, 44f), L.T("친구 초대 (스팀)", "invite friends (steam)"), gm);
                HandBtn(R(320f, ActionY + 52f, 150f, 44f), L.T("코드 복사", "copy code"), gm);
            }
            if (!string.IsNullOrEmpty(status)) GUI.Label(R(0f, ActionY + 100f, panel.width, 24f), status, hSmall);
            return;
        }
        UiKit.Panel(panel, 0.97f);   // 인게임 미니 패널만 흰 배경 유지

        // 인게임 미니 패널 — 내가 잡은 다리 전부 나열
        var lk = Link;
        string mine2 = "";
        if (lk != null)
            for (int i = 0; i < 4; i++)
                if (lk.IsMine(i)) mine2 += (mine2.Length > 0 ? ", " : "") + LegName(i);
        if (mine2.Length == 0) mine2 = NM.IsHost ? L.T("(빈 다리 전부)", "(all unclaimed legs)") : L.T("(없음)", "(none)");
        GUI.Label(R(12f, 6f, 228f, 48f), (NM.IsHost ? L.T("호스트", "host") : L.T("게스트", "guest")) + " · " + NM.ConnectedClientsList.Count + L.T("명 (F9 숨김)", " players (F9 hides)") + L.T("\n내 다리: ", "\nmy legs: ") + mine2 + " " + status, small);
        Button(R(12f, 56f, 228f, 30f), L.T("나가기", "leave"), gm);
    }

    void Button(Rect r, string text, Vector2 gm) => UiKit.Button(r, text, r.Contains(gm));

    /// <summary>손글씨 버튼(로비): 맨글씨 + 호버 시 살짝 기울임+밑줄 — 색 변화 없음(홈 메뉴와 통일).</summary>
    void HandBtn(Rect r, string text, Vector2 gm)
    {
        bool hover = r.Contains(gm);
        var m = GUI.matrix;
        if (hover) GUIUtility.RotateAroundPivot(-1.2f, new Vector2(r.x, r.center.y));
        GUI.Label(r, text, hAction);
        if (hover && HandUI.Underline != null)
        {
            float tw = Mathf.Min(hAction.CalcSize(new GUIContent(text)).x, r.width);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 8f, tw, 14f), HandUI.Underline, ScaleMode.StretchToFill);
        }
        GUI.matrix = m;
    }

    GUIStyle hTitle, hBig, hSmall, mapItem, tileName, tileSub, hAction;
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

        // ★손글씨 로비(2026-07-14): 홈/LevelSelect와 같은 톤
        hTitle = HandUI.Style(28, HandUI.InkSoft, TextAnchor.MiddleLeft);
        hBig = HandUI.Style(30, HandUI.Ink, TextAnchor.MiddleLeft);
        hSmall = HandUI.Style(18, HandUI.InkSoft, TextAnchor.MiddleLeft);
        mapItem = HandUI.Style(22, HandUI.Ink, TextAnchor.MiddleLeft);
        tileName = HandUI.Style(21, HandUI.Ink, TextAnchor.MiddleCenter);
        tileSub = HandUI.Style(16, HandUI.InkSoft, TextAnchor.MiddleCenter);
        hAction = HandUI.Style(30, HandUI.Ink, TextAnchor.MiddleLeft);
    }
}
