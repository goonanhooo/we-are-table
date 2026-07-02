using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 멀티 로비/연결 UI(F9 토글, NetworkManager 프리팹에 부착 — 전 씬 존재).
/// 흐름(확정 설계): [방 만들기]→방코드 표시 / [코드 입력→참가] → 로비에서 각자 다리 클릭 선택 →
/// 호스트 [게임 시작](다리 고정+세션 잠금=난입 차단). 호스트 퇴장=게스트 세션 종료+씬 재로드.
/// ⚠️ New Input System 전용이라 GUI.Button/TextField 안 먹음 — 클릭은 Rect+Mouse.current,
/// 방코드 입력은 Keyboard.current 직접 캡처(A~Z/0~9/백스페이스).
/// (로컬 백업: MPPM 가상 플레이어로 인터넷 없이 테스트할 때 Shift+F9 → 127.0.0.1 직결 모드)
/// </summary>
public class NetDebugPanel : MonoBehaviour
{
    bool visible = true;
    string codeInput = "";
    bool typing;             // 방코드 입력 중(키 캡처 활성)
    string status = "";
    GUIStyle label, btn, big, small;
    Texture2D white;

    NetworkManager NM => NetworkManager.Singleton;
    NetLegLink Link => FindAnyObjectByType<NetLegLink>();
    static readonly string[] LegNames = { "앞왼발 (WASD)", "앞오른발 (TFGH)", "뒤왼발 (IJKL)", "뒤오른발 (방향키)" };
    // LegControlSet 인덱스 → 표시 순서(테이블 배치 느낌): FL(WASD)=0, FR(TFGH)=1, BL(IJKL)=2, BR(Arrows)=3 그대로 사용

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

    // ── 레이아웃 ──
    Rect Panel() => InLobby
        ? new Rect((Screen.width - 460f) * 0.5f, (Screen.height - 420f) * 0.5f, 460f, 420f)   // 로비=중앙 큰 창
        : new Rect(Screen.width - 260f, 8f, 252f, InSession ? 96f : 214f);                     // 대기/인게임=우상단
    Rect R(float x, float y, float w, float h) { var p = Panel(); return new Rect(p.x + x, p.y + y, w, h); }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame && !typing) visible = !visible;
        if (!visible || NM == null) return;

        // 방코드 타이핑 캡처
        if (typing && kb != null)
        {
            for (Key k = Key.A; k <= Key.Z; k++)
                if (kb[k].wasPressedThisFrame && codeInput.Length < 8) codeInput += k.ToString();
            for (Key k = Key.Digit1; k <= Key.Digit0; k++)
                if (kb[k].wasPressedThisFrame && codeInput.Length < 8) codeInput += (k == Key.Digit0 ? "0" : ((int)k - (int)Key.Digit1 + 1).ToString());
            if (kb.backspaceKey.wasPressedThisFrame && codeInput.Length > 0) codeInput = codeInput.Substring(0, codeInput.Length - 1);
            if (kb.enterKey.wasPressedThisFrame && codeInput.Length >= 4) { typing = false; _ = NetSession.JoinAsync(codeInput); }
            if (kb.escapeKey.wasPressedThisFrame) typing = false;
        }

        var m = Mouse.current;
        if (m == null || !m.leftButton.wasPressedThisFrame) return;
        Vector2 p = m.position.ReadValue();
        Vector2 gm = new Vector2(p.x, Screen.height - p.y);

        if (!InSession)
        {
            if (R(12f, 44f, 228f, 38f).Contains(gm)) { status = ""; _ = NetSession.HostAsync(); }          // 방 만들기
            else if (R(12f, 96f, 228f, 34f).Contains(gm)) { typing = true; codeInput = ""; }               // 코드 입력칸
            else if (R(12f, 136f, 228f, 38f).Contains(gm) && codeInput.Length >= 4) { typing = false; _ = NetSession.JoinAsync(codeInput); }  // 참가
            else typing = false;
            return;
        }

        if (InLobby)
        {
            var link = Link;
            for (int i = 0; i < 4; i++)
                if (LegBtn(i).Contains(gm)) { link.ClaimLegRpc((byte)i); return; }
            if (NM.IsHost && R(20f, 348f, 200f, 44f).Contains(gm)) link.StartGame();
            if (R(240f, 348f, 200f, 44f).Contains(gm)) LeaveAll();
        }
        else // 인게임 미니 패널
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
        _ = NetSession.LeaveAsync();
        if (NM != null) NM.Shutdown();
        status = "세션 종료";
        SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    void OnGUI()
    {
        if (!visible || NM == null) return;
        EnsureStyles();
        var panel = Panel();
        Fill(panel, new Color(0.08f, 0.08f, 0.11f, InLobby ? 0.95f : 0.85f));

        if (!InSession)
        {
            GUI.Label(R(12f, 8f, 228f, 30f), "멀티플레이 (F9 숨김)", label);
            Button(R(12f, 44f, 228f, 38f), "방 만들기 (친구 초대)");
            // 코드 입력칸
            Fill(R(12f, 96f, 228f, 34f), typing ? new Color(0.15f, 0.25f, 0.4f, 1f) : new Color(0.16f, 0.16f, 0.2f, 1f));
            GUI.Label(R(20f, 98f, 214f, 30f), codeInput.Length > 0 ? codeInput : (typing ? "코드 입력 중…" : "클릭해서 방 코드 입력"), label);
            Button(R(12f, 136f, 228f, 38f), "참가");
            GUI.Label(R(12f, 178f, 228f, 32f), status + " " + NetSession.Status, small);
            return;
        }

        if (InLobby)
        {
            var link = Link;
            GUI.Label(R(0f, 12f, panel.width, 34f), "— 로비 —", big);
            if (NM.IsHost)
                GUI.Label(R(0f, 48f, panel.width, 34f), "방 코드:  " + (string.IsNullOrEmpty(NetSession.JoinCode) ? "(로컬)" : NetSession.JoinCode), big);
            GUI.Label(R(0f, 86f, panel.width, 26f), "자기가 조종할 다리를 클릭하세요 (접속 " + NM.ConnectedClientsList.Count + "명)", small);
            GUI.Label(R(0f, 112f, panel.width, 26f), "선택 안 된 다리는 호스트 키보드가 조종합니다", small);

            for (int i = 0; i < 4; i++)
            {
                long owner = link != null ? link.OwnerOf(i) : -2;
                bool mine = link != null && link.IsMine(i);
                var r = LegBtn(i);
                Fill(r, mine ? new Color(0.2f, 0.55f, 0.3f, 1f) : owner == -2 ? new Color(0.22f, 0.32f, 0.45f, 1f) : new Color(0.3f, 0.3f, 0.34f, 1f));
                string who = mine ? "★ 나" : owner == -2 ? "빈 자리" : owner == -1 ? "호스트" : "P" + owner;
                GUI.Label(new Rect(r.x, r.y + 10f, r.width, 28f), LegNames[i], btn);
                GUI.Label(new Rect(r.x, r.y + 42f, r.width, 26f), who, small);
            }

            if (NM.IsHost) Button(R(20f, 348f, 200f, 44f), "게임 시작");
            else GUI.Label(R(20f, 348f, 200f, 44f), "호스트 대기 중…", btn);
            Button(R(240f, 348f, 200f, 44f), "나가기");
            return;
        }

        // 인게임 미니 패널
        var lk = Link;
        string mine2 = lk != null && lk.MyLeg.HasValue ? LegNames[(int)lk.MyLeg.Value] : "(관전/호스트 전체)";
        GUI.Label(R(12f, 6f, 228f, 48f), (NM.IsHost ? "호스트" : "게스트") + " · " + NM.ConnectedClientsList.Count + "명\n내 다리: " + mine2, small);
        Button(R(12f, 56f, 228f, 30f), "나가기");
    }

    void Button(Rect r, string text) { Fill(r, new Color(0.24f, 0.36f, 0.52f, 1f)); GUI.Label(r, text, btn); }
    void Fill(Rect r, Color c) { var pc = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = pc; }
    void EnsureStyles()
    {
        if (label != null) return;
        white = Texture2D.whiteTexture;
        label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        label.normal.textColor = Color.white;
        btn = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btn.normal.textColor = Color.white;
        big = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        big.normal.textColor = Color.white;
        small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        small.normal.textColor = new Color(0.8f, 0.85f, 0.9f, 1f);
    }
}
