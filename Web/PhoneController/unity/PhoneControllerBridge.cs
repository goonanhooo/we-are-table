using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// Native/Editor phone adapter. Local configurations remain opt-in.
// Hosted multiplayer rooms are provisioned when the host opens a game session.
[DefaultExecutionOrder(-100)]
public sealed class PhoneControllerBridge : MonoBehaviour
{
    [Serializable] public sealed class Config { public string url, room, key; }
    [Serializable] sealed class Packet { public string type, role, room, key, origin, mode, watchCode, data, code, joinCode, joinQrPng; public bool[] occupiedSlots, nativeSlots; public string[] names; public int slot, bits, viewers; public bool occupied, active, iceFrozen, legSelect; public float x, z, yaw; }
    readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    readonly bool[] occupied = new bool[4];
    readonly int[] bits = new int[4];
    readonly float[] received = new float[4];
    readonly float[] poseReceived = new float[4];
    readonly float[] poseX = new float[4], poseZ = new float[4], poseYaw = new float[4];
    readonly bool[] poseActive = new bool[4];
    readonly bool[] applied = new bool[4];
    TableInput table;
    Config config;
    CancellationTokenSource cancellation;
    ClientWebSocket socket;
    volatile bool connected;
    volatile string connectionStatus = "연결 중";
    float nextFind;
    static PhoneControllerBridge instance;
    Texture2D joinQr;
    string joinQrData = "";
    string joinCode = "";
    bool[] lobbyOccupied = new bool[4];
    bool[] lobbyNative = new bool[4];
    string[] lobbyNames = new string[4];   // 폰 닉네임(웹에서 적은 이름) — 슬롯별
    string publicOrigin = "";
    string watchCode = "";
    int viewerCount;
    bool frameSending;
    string sentMode = "";
    bool sentIceFrozen, sentLegSelect;
    int sentNativeMask = -1;
    float nextNativeMask;
    string sentRoomCode;
    float nextRoomCode;
    readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    static bool webPlayActive;
    public static bool WebPlayActive
    {
        get => webPlayActive;
        set { webPlayActive = value; if (value) EnsureHostedConnection(); }
    }
    [Serializable] sealed class HostedService { public string origin; }

    static void EnsureHostedConnection()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (instance != null) return; // An explicit local/CLI configuration takes precedence.
        var asset = Resources.Load<TextAsset>("PhoneControllerService");
        if (asset == null) return;
        HostedService service;
        try { service = JsonUtility.FromJson<HostedService>(asset.text); } catch { return; }
        if (!Uri.TryCreate(service?.origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) return;
        var go = new GameObject("PhoneControllerBridge");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PhoneControllerBridge>();
        instance.StartCoroutine(instance.ProvisionHostedRoom(uri.GetLeftPart(UriPartial.Authority)));
#endif
    }

    IEnumerator ProvisionHostedRoom(string origin)
    {
        while (WebPlayActive && config == null)
        {
            connectionStatus = "폰 연결 준비 중";
            using (var request = new UnityWebRequest(origin + "/api/rooms", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Config created = null;
                    try { created = JsonUtility.FromJson<Config>(request.downloadHandler.text); } catch { }
                    if (created != null && Uri.TryCreate(created.url, UriKind.Absolute, out var socketUri) &&
                        socketUri.Scheme == "wss" && socketUri.Host == new Uri(origin).Host &&
                        !string.IsNullOrEmpty(created.room) && !string.IsNullOrEmpty(created.key))
                    {
                        config = created;
                        Begin();
                        yield break;
                    }
                }
                // The room key stays in memory and never enters a portable build or public URL.
                connectionStatus = "폰 서버 연결 대기";
            }
            yield return new WaitForSecondsRealtime(10);
        }
        if (config == null) Destroy(gameObject);
    }
    public static PhoneControllerBridge Instance => instance;
    public bool Connected => connected;
    public string PublicOrigin => publicOrigin;
    public string WatchCode => watchCode;
    public string JoinCode => joinCode;
    public Texture2D JoinQr => joinQr;
    public bool HasPlayer(int slot) => connected && slot >= 0 && slot < lobbyOccupied.Length && lobbyOccupied[slot];
    /// <summary>그 다리를 맡은 폰의 닉네임(없으면 빈 문자열).</summary>
    public string PhoneName(int slot) => HasPlayer(slot) && lobbyNames != null && slot < lobbyNames.Length ? NetLegLink.CleanPlayerName(lobbyNames[slot]) : "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; WebPlayActive = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (instance != null) return;
#if UNITY_EDITOR
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PhoneController.json"));
#else
        // Portable client builds must never inherit the host's relay credentials.
        string path = Path.Combine(Application.persistentDataPath, "PhoneController.json");
#endif
        foreach (string arg in Environment.GetCommandLineArgs())
            if (arg.StartsWith("--phone-config=")) path = arg.Substring("--phone-config=".Length);
        if (!File.Exists(path)) return;
        try
        {
            var settings = JsonUtility.FromJson<Config>(File.ReadAllText(path));
            if (settings == null || !Uri.TryCreate(settings.url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "ws" && uri.Scheme != "wss") || string.IsNullOrEmpty(settings.room) || string.IsNullOrEmpty(settings.key))
                throw new Exception("Invalid PhoneController.json");
            var go = new GameObject("PhoneControllerBridge");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PhoneControllerBridge>();
            instance.config = settings;
            instance.Begin();
        }
        catch (Exception e) { Debug.LogWarning("[Phone controller] " + e.Message); }
#endif
    }

    void Begin()
    {
        Application.runInBackground = true;
        cancellation = new CancellationTokenSource();
        // Serialize Unity data on the main thread. Socket/receive operations run in the background.
        string hello = JsonUtility.ToJson(new Packet { type = "hello", role = "unity", room = config.room, key = config.key });
        _ = Task.Run(() => ConnectLoop(hello, cancellation.Token));
        StartCoroutine(StreamFrames());
    }

    async Task ConnectLoop(string hello, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using (var client = new ClientWebSocket())
            {
                socket = client;
                try
                {
                    connectionStatus = "연결 중";
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        timeout.CancelAfter(10000);
                        await client.ConnectAsync(new Uri(config.url), timeout.Token).ConfigureAwait(false);
                    }
                    byte[] greeting = Encoding.UTF8.GetBytes(hello);
                    await client.SendAsync(new ArraySegment<byte>(greeting), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                    var buffer = new byte[4096];
                    while (client.State == WebSocketState.Open && !token.IsCancellationRequested)
                    {
                        using (var text = new MemoryStream())
                        {
                            WebSocketReceiveResult result;
                            do
                            {
                                result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                                if (result.MessageType == WebSocketMessageType.Close) break;
                                if (result.MessageType != WebSocketMessageType.Text || text.Length + result.Count > 32768)
                                    throw new Exception("Invalid relay packet");
                                text.Write(buffer, 0, result.Count);
                            } while (!result.EndOfMessage);
                            if (result.MessageType == WebSocketMessageType.Close) break;
                            if (incoming.Count >= 256) throw new Exception("Relay queue overflow");
                            incoming.Enqueue(Encoding.UTF8.GetString(text.ToArray()));
                        }
                    }
                }
                catch (Exception) { connectionStatus = "연결 대기"; }
                finally { connected = false; }
            }
            if (token.IsCancellationRequested) break;
            try { await Task.Delay(2000, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
        }
    }

    async Task SendPacket(string json, ClientWebSocket client)
    {
        try
        {
            await sendLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
            try
            {
                if (client != null && client.State == WebSocketState.Open)
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, cancellation.Token).ConfigureAwait(false);
            }
            finally { sendLock.Release(); }
        }
        catch (Exception) { }
    }

    void ReceiveLobby(Packet packet)
    {
        viewerCount = packet.viewers;
        watchCode = packet.watchCode ?? "";
        joinCode = packet.joinCode ?? "";
        publicOrigin = packet.origin ?? "";
        if (packet.occupiedSlots != null && packet.occupiedSlots.Length == 4) lobbyOccupied = packet.occupiedSlots;
        if (packet.nativeSlots != null && packet.nativeSlots.Length == 4) lobbyNative = packet.nativeSlots;
        if (packet.names != null && packet.names.Length == 4) lobbyNames = packet.names;
        string image = packet.joinQrPng ?? "";
        if (image == joinQrData) return;
        if (joinQr != null) { Destroy(joinQr); joinQr = null; }
        joinQrData = image;
        if (image.Length == 0) return;
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (ImageConversion.LoadImage(texture, Convert.FromBase64String(image))) { joinQr = texture; texture = null; }
        }
        catch (Exception) { }
        finally { if (texture != null) Destroy(texture); }
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        while (incoming.TryDequeue(out string json))
        {
            Packet packet;
            try { packet = JsonUtility.FromJson<Packet>(json); } catch { continue; }
            if (packet == null) continue;
            if (packet.type == "ready") { connected = true; connectionStatus = "연결됨"; sentMode = ""; sentNativeMask = -1; sentRoomCode = null; }
            if (packet.type == "lobby") ReceiveLobby(packet);
            if (packet.type == "input" && packet.slot >= 0 && packet.slot < 4)
            {
                int i = packet.slot;
                occupied[i] = packet.occupied; bits[i] = packet.bits & 255; received[i] = now;
            }
            if (packet.type == "pose" && packet.slot >= 0 && packet.slot < 4)
            {
                int i = packet.slot;
                poseActive[i] = packet.active && float.IsFinite(packet.x) && float.IsFinite(packet.z)
                    && float.IsFinite(packet.yaw) && Mathf.Abs(packet.yaw) <= 3600f
                    && packet.x * packet.x + packet.z * packet.z <= 1.05f;
                poseX[i] = packet.x; poseZ[i] = packet.z; poseYaw[i] = packet.yaw; poseReceived[i] = now;
            }
        }
        // These phones share one authoritative offline simulation. Never mix with NGO client authority.
        var nm = Unity.Netcode.NetworkManager.Singleton;
        bool authoritative = nm == null || !nm.IsListening || nm.IsServer;
        if (table == null && now >= nextFind)
        {
            nextFind = now + .5f;
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            table = follow != null && follow.target != null ? TableInput.Of(follow.target) : null;
            if (table == null) table = UnityEngine.Object.FindAnyObjectByType<TableInput>();
            Array.Clear(applied, 0, applied.Length);
        }
        var netLink = table != null ? table.GetComponent<NetLegLink>() : null;
        int nativeMask = nm != null && nm.IsListening && nm.IsServer && netLink != null ? netLink.NativeMask : 0;
        if (connected && (nativeMask != sentNativeMask || now >= nextNativeMask))
        {
            sentNativeMask = nativeMask; nextNativeMask = now + 1f;
            _ = SendPacket(JsonUtility.ToJson(new Packet { type = "native-slots", bits = nativeMask }), socket);
        }
        string nativeRoomCode = nm != null && nm.IsListening && nm.IsServer ? NetSession.JoinCode : "";
        if (connected && (nativeRoomCode != sentRoomCode || now >= nextRoomCode))
        {
            sentRoomCode = nativeRoomCode; nextRoomCode = now + 2f;
            _ = SendPacket(JsonUtility.ToJson(new Packet { type = "native-room", code = nativeRoomCode }), socket);
        }
        string visual = "normal";
        IcePower selectedIce = null;
        if (WebPlayActive && table != null)
        {
            var root = table.transform.root;
            var giraffe = root.GetComponentInChildren<GiraffeMode>(true);
            var tennis = root.GetComponentInChildren<TennisMode>(true);
            var ice = root.GetComponentInChildren<IcePower>(true);
            var squid = root.GetComponentInChildren<SquidMode>(true);
            if (giraffe != null && giraffe.active) visual = "giraffe";
            else if (ice != null && (ice.Selected || ice.active)) { visual = "ice"; selectedIce = ice; }
            else if (tennis != null && tennis.active) visual = "tennis";
            else if (squid != null && squid.active) visual = "squid";
        }
        bool iceFrozen = selectedIce != null && selectedIce.active;
        // 다리를 고르는 순간(로비 다리 단계 · 웹 다리 방 · 설정의 다리 다시 고르기) = 폰 화면에 큰 다리 버튼
        bool legSelect = NetDebugPanel.LegsPageOpen || LevelSelect.WebLobbyOpen || PauseMenu.LegsScreenOpen;
        if (connected && (visual != sentMode || iceFrozen != sentIceFrozen || legSelect != sentLegSelect))
        {
            sentMode = visual;
            sentIceFrozen = iceFrozen;
            sentLegSelect = legSelect;
            _ = SendPacket(JsonUtility.ToJson(new Packet { type = "visual", mode = visual, iceFrozen = iceFrozen, legSelect = legSelect }), socket);
        }
        if (table == null) return;
        int iceActions = 0;
        for (int i = 0; i < 4; i++)
        {
            bool nativeOwned = (nativeMask & (1 << i)) != 0;
            bool active = WebPlayActive && authoritative && !nativeOwned && connected && socket != null && socket.State == WebSocketState.Open && occupied[i];
            if (active != applied[i])
            {
                if (active) table.SetSource((LegControlSet)i, TableInput.LegSource.External);
                else if (!nativeOwned) table.SetSource((LegControlSet)i, TableInput.LegSource.Local);
                applied[i] = active;
            }
            if (active)
            {
                // A stale held key must never continue moving after a lost network connection.
                byte value = now - received[i] <= .65f && Time.timeScale > 0 && !PauseMenu.InputBlocked && !LetterOverlay.IsOpen ? (byte)bits[i] : (byte)0;
                iceActions |= value & 48;
                table.SetExternal((LegControlSet)i, Decode(value));
                bool validPose = poseActive[i] && now - poseReceived[i] <= .65f
                    && Time.timeScale > 0 && !PauseMenu.InputBlocked && !LetterOverlay.IsOpen;
                if (validPose) table.SetExternalPose((LegControlSet)i, poseX[i], poseZ[i], poseYaw[i]);
                else table.ClearExternalPose((LegControlSet)i);
            }
        }
        if (selectedIce != null)
        {
            // Release wins when different phones press opposite actions together.
            if ((iceActions & 32) != 0) selectedIce.SetFrozen(false);
            else if ((iceActions & 16) != 0) selectedIce.SetFrozen(true);
        }
    }

    IEnumerator StreamFrames()
    {
        var endOfFrame = new WaitForEndOfFrame();
        float nextFrame = 0f;
        while (true)
        {
            yield return endOfFrame;
            if (!connected || viewerCount == 0 || frameSending || Time.realtimeSinceStartup < nextFrame) continue;
            nextFrame = Time.realtimeSinceStartup + .125f;
            Texture2D full = null, small = null;
            RenderTexture scaled = null, old = RenderTexture.active;
            try
            {
                full = ScreenCapture.CaptureScreenshotAsTexture();
                if (full == null) continue;
                int width = Mathf.Min(960, full.width);
                int height = Mathf.Max(1, Mathf.RoundToInt(width * (float)full.height / full.width));
                scaled = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(full, scaled);
                RenderTexture.active = scaled;
                small = new Texture2D(width, height, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                small.Apply(false, false);
                byte[] jpg = ImageConversion.EncodeToJPG(small, 54);
                if (jpg.Length > 170000) continue;
                frameSending = true;
                _ = SendFrame(JsonUtility.ToJson(new Packet { type = "frame", data = Convert.ToBase64String(jpg) }));
            }
            catch (Exception e) { Debug.LogWarning("[Phone controller] frame: " + e.Message); }
            finally
            {
                RenderTexture.active = old;
                if (scaled != null) RenderTexture.ReleaseTemporary(scaled);
                if (small != null) Destroy(small);
                if (full != null) Destroy(full);
            }
        }
    }

    async Task SendFrame(string json)
    {
        try { await SendPacket(json, socket); }
        finally { frameSending = false; }
    }

    public static LegInput Decode(byte b) => new LegInput {
        up=(b&1)!=0, down=(b&2)!=0, left=(b&4)!=0, right=(b&8)!=0,
        longer=(b&16)!=0, shorter=(b&32)!=0, grab=(b&64)!=0, release=(b&128)!=0
    };

    /// <summary>연결 상태 글(내부 값은 한국어) → 지금 언어.</summary>
    public string StatusText
    {
        get
        {
            string s = connectionStatus;
            switch (s)
            {
                case "연결 중": return L.T("연결 중", "Connecting", "接続中", "连接中");
                case "폰 연결 준비 중": return L.T("폰 연결 준비 중", "Preparing phone link", "スマホ接続を準備中", "正在准备手机连接");
                case "폰 서버 연결 대기": return L.T("폰 서버 연결 대기", "Waiting for phone server", "スマホサーバー待ち", "等待手机服务器");
                case "연결 대기": return L.T("연결 대기", "Waiting to connect", "接続待ち", "等待连接");
                case "연결됨": return L.T("연결됨", "Connected", "接続済み", "已连接");
                default: return L.Text(s);
            }
        }
    }
    public void DrawInvite(Rect area)
    {
        // 지우개 대소동 규칙: 종이 창 안에 [QR 카드 → 입장 코드] + [제목 → 설명 → 다리 4줄(색 점만 다리 색)]
        const float S2 = JourneyMenuUI.S2, S3 = JourneyMenuUI.S3, S4 = JourneyMenuUI.S4;
        JourneyMenuUI.Box(area, JourneyMenuUI.Kind.Panel);
        float size = Mathf.Min(area.height - 2 * S4 - 56f - S2, 320f);
        var card = new Rect(area.x + S4, area.y + S4, size, size);
        JourneyMenuUI.Box(card, JourneyMenuUI.Kind.Card);
        var image = new Rect(card.x + S2, card.y + S2, size - 2 * S2, size - 2 * S2);
        if (JoinQr != null) GUI.DrawTexture(image, JoinQr, ScaleMode.ScaleToFit);
        else JourneyMenuUI.Text(image, L.T("QR 연결 대기", "Waiting for QR", "QR接続待ち", "等待二维码"), JourneyMenuUI.TXS, JourneyMenuUI.Muted, TextAnchor.MiddleCenter);
        string code = JoinCode;
        if (code.Length == 8) code = code.Substring(0, 4) + " " + code.Substring(4);
        JourneyMenuUI.Literal(new Rect(card.x, card.yMax + S2, size, 56f), code, JourneyMenuUI.TL, JourneyMenuUI.Ink, TextAnchor.MiddleCenter);
        float x = card.xMax + S4 + S3, width = area.xMax - x - S4;
        JourneyMenuUI.Text(new Rect(x, area.y + S4, width, 52f), L.T("같은 QR로 함께 입장", "One QR for everyone", "同じQRでみんな参加", "同一个二维码，一起加入"), JourneyMenuUI.TL);
        JourneyMenuUI.SketchRule(x, area.y + S4 + 52f + S3, width);
        float cellWidth = (width - S2) / 2f, top = area.y + S4 + 52f + S3 + S4;
        for (int i = 0; i < 4; i++)
        {
            var row = new Rect(x + (i % 2) * (cellWidth + S2), top + (i / 2) * (JourneyMenuUI.HS + S2), cellWidth, JourneyMenuUI.HS);
            bool phone = HasPlayer(i), pc = !phone && lobbyNative[i];
            JourneyMenuUI.Box(row, phone || pc ? JourneyMenuUI.Kind.Card : JourneyMenuUI.Kind.Soft, phone);
            JourneyMenuUI.LegStripe(row, LegPalette.BySlot[i]);
            JourneyMenuUI.Text(new Rect(row.x + S3, row.y, row.width * .5f - S3, row.height), L.Title(LegLayout.Name(i)), JourneyMenuUI.TXS);
            string nick = phone ? PhoneName(i) : "";
            string state = phone ? (nick.Length > 0 ? nick : L.T("폰 연결됨", "Phone", "スマホ接続中", "手机已连接")) : pc ? "PC" : L.T("빈 다리", "Available", "空き", "空闲");
            JourneyMenuUI.Text(new Rect(row.x + row.width * .5f, row.y, row.width * .5f - S3, row.height), state, JourneyMenuUI.TXS, phone || pc ? JourneyMenuUI.Ink : JourneyMenuUI.Muted, TextAnchor.MiddleRight);
        }
    }

    void OnGUI()
    {
        if (!WebPlayActive) return;
        int count = 0; foreach (bool p in occupied) if (p && connected) count++;
        JourneyMenuUI.Begin();
        try { JourneyMenuUI.Note(new Rect(JourneyMenuUI.S2, 900f - JourneyMenuUI.S2 - 32f, 640f, 32f), L.T("폰 ", "Phones ", "スマホ ", "手机 ") + count + "/4   " + StatusText); }
        finally { JourneyMenuUI.End(); }
    }

    void OnDestroy()
    {
        cancellation?.Cancel(); socket?.Abort();
        if (table != null) for (int i=0;i<4;i++) if(applied[i]) table.SetSource((LegControlSet)i, TableInput.LegSource.Local);
        if (instance == this) instance = null;
        if (joinQr != null) Destroy(joinQr);
        cancellation?.Dispose();
        sendLock.Dispose();
    }
}
