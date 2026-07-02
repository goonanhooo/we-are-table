using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// [개발용] 멀티 연결 패널(우상단, F9 토글) — 로비/방코드 UI(3~4단계)가 생기기 전까지의 임시 UI.
/// Host 시작 / 같은 PC 접속(127.0.0.1, Multiplayer Play Mode 가상 플레이어용) / 연결 종료.
/// GUI.Button은 New Input System에서 안 먹으므로 PauseMenu처럼 Rect+Mouse.current로 직접 클릭 판정.
/// 호스트 퇴장 시(게스트 입장) 세션 종료 → 현재 씬 재로드로 로컬 상태 복구.
/// </summary>
public class NetDebugPanel : MonoBehaviour
{
    bool visible = true;
    GUIStyle label, btn;
    Texture2D white;
    string status = "";

    NetworkManager NM => NetworkManager.Singleton;

    void Start()
    {
        if (NM != null) NM.OnClientDisconnectCallback += OnDisconnected;
    }
    void OnDestroy()
    {
        if (NM != null) NM.OnClientDisconnectCallback -= OnDisconnected;
    }

    // 게스트: 호스트가 나가면(자기 연결이 끊기면) 세션 종료 → 씬 재로드로 로컬 복구
    void OnDisconnected(ulong clientId)
    {
        if (NM == null) return;
        if (!NM.IsServer && clientId == NM.LocalClientId)
        {
            status = "호스트 연결 종료 — 세션 끝";
            NM.Shutdown();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }
    }

    Rect PanelRect() => new Rect(Screen.width - 250f, 8f, 242f, 168f);
    Rect BtnRect(int i) => new Rect(Screen.width - 242f, 64f + i * 40f, 226f, 34f);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame) visible = !visible;
        if (!visible || NM == null) return;

        var m = Mouse.current;
        if (m == null || !m.leftButton.wasPressedThisFrame) return;
        Vector2 p = m.position.ReadValue();
        Vector2 gm = new Vector2(p.x, Screen.height - p.y);

        bool idle = !NM.IsClient && !NM.IsServer;
        if (idle)
        {
            if (BtnRect(0).Contains(gm))
            {
                SetLoopback();
                if (NM.StartHost()) status = "호스트 시작 (127.0.0.1:7777)";
            }
            else if (BtnRect(1).Contains(gm))
            {
                SetLoopback();
                status = NM.StartClient() ? "접속 시도..." : "접속 실패";
            }
        }
        else if (BtnRect(2).Contains(gm))
        {
            NM.Shutdown();
            status = "연결 종료";
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }
    }

    void SetLoopback()
    {
        var utp = NM.GetComponent<UnityTransport>();
        if (utp != null) utp.SetConnectionData("127.0.0.1", 7777);
    }

    void OnGUI()
    {
        if (!visible || NM == null) return;
        if (label == null)
        {
            white = Texture2D.whiteTexture;
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true };
            label.normal.textColor = Color.white;
            btn = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            btn.normal.textColor = Color.white;
        }
        var panel = PanelRect();
        Fill(panel, new Color(0.08f, 0.08f, 0.1f, 0.85f));

        string head;
        bool idle = !NM.IsClient && !NM.IsServer;
        if (idle) head = "멀티 (개발용, F9 숨김)";
        else if (NM.IsHost) head = "호스트 — 접속 " + NM.ConnectedClientsList.Count + "명";
        else head = "게스트" + (NM.IsConnectedClient ? " — 접속됨" : " — 접속 중...");
        var link = FindAnyObjectByType<NetLegLink>();
        string legs = link != null && (NM.IsHost || NM.IsConnectedClient) ? "\n" + link.OwnersDebug() : "";
        string mine = link != null && link.MyLeg.HasValue ? "\n내 다리: " + link.MyLeg.Value : "";
        GUI.Label(new Rect(panel.x + 8f, panel.y + 4f, panel.width - 16f, 56f), head + legs + mine + "\n" + status, label);

        if (idle)
        {
            Button(BtnRect(0), "Host 시작");
            Button(BtnRect(1), "같은 PC 접속 (테스트)");
        }
        else Button(BtnRect(2), "연결 종료");
    }

    void Button(Rect r, string text)
    {
        Fill(r, new Color(0.22f, 0.32f, 0.45f, 1f));
        GUI.Label(r, text, btn);
    }
    void Fill(Rect r, Color c)
    { var pc = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = pc; }
}
