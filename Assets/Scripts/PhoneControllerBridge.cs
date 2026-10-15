using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Opt-in native/Editor adapter. No scene/prefab modification or Steam session is needed.
// The relay creates PhoneController.json in the project root (or beside the player EXE).
[DefaultExecutionOrder(-100)]
public sealed class PhoneControllerBridge : MonoBehaviour
{
    [Serializable] public sealed class Config { public string url, room, key; }
    [Serializable] sealed class Packet { public string type, role, room, key, origin, mode, watchCode, data, code; public string[] codes, qrPng; public bool[] occupiedSlots; public int slot, bits, power, viewers; public bool occupied, active; public float x, z, yaw; }
    readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    readonly bool[] occupied = new bool[4];
    readonly int[] bits = new int[4];
    readonly float[] received = new float[4];
    readonly float[] poseReceived = new float[4];
    readonly float[] poseX = new float[4], poseZ = new float[4], poseYaw = new float[4];
    readonly bool[] poseActive = new bool[4];
    readonly bool[] applied = new bool[4];
    readonly float[] powerPulseUntil = new float[4];
    TableInput table;
    Config config;
    CancellationTokenSource cancellation;
    ClientWebSocket socket;
    volatile bool connected;
    volatile string connectionStatus = "연결 중";
    float nextFind;
    static PhoneControllerBridge instance;
    readonly Texture2D[] qr = new Texture2D[4];
    string[] codes = Array.Empty<string>();
    bool[] lobbyOccupied = new bool[4];
    string publicOrigin = "";
    string watchCode = "";
    int viewerCount;
    bool frameSending;
    string sentMode = "";
    int sentNativeMask = -1;
    float nextNativeMask;
    string sentRoomCode;
    float nextRoomCode;
    readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    public static bool WebPlayActive { get; set; }
    public static PhoneControllerBridge Instance => instance;
    public bool Connected => connected;
    public string PublicOrigin => publicOrigin;
    public string WatchCode => watchCode;
    public string Code(int slot) => slot >= 0 && slot < codes.Length ? codes[slot] : "";
    public bool HasPlayer(int slot) => connected && slot >= 0 && slot < lobbyOccupied.Length && lobbyOccupied[slot];
    public Texture2D Qr(int slot) => slot >= 0 && slot < qr.Length ? qr[slot] : null;
    public static bool PowerHeld(int power) => WebPlayActive && instance != null && power >= 2 && power <= 5
        && Time.unscaledTime < instance.powerPulseUntil[power - 2];

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
        if (packet.codes == null || packet.codes.Length != 4) return;
        bool changed = publicOrigin != (packet.origin ?? "") || codes.Length != 4;
        if (!changed) for (int i = 0; i < 4; i++) if (codes[i] != packet.codes[i]) changed = true;
        codes = packet.codes;
        publicOrigin = packet.origin ?? "";
        if (packet.occupiedSlots != null && packet.occupiedSlots.Length == 4) lobbyOccupied = packet.occupiedSlots;
        if (changed)
            for (int i = 0; i < 4; i++)
            {
                if (qr[i] != null) { Destroy(qr[i]); qr[i] = null; }
                if (packet.qrPng == null || packet.qrPng.Length != 4 || string.IsNullOrEmpty(packet.qrPng[i])) continue;
                try
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(texture, Convert.FromBase64String(packet.qrPng[i]))) qr[i] = texture;
                    else Destroy(texture);
                }
                catch (Exception) { }
            }
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
            if (WebPlayActive && packet.type == "power" && packet.slot >= 0 && packet.slot < 4 && occupied[packet.slot])
                SelectPower(packet.power);
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
        if (WebPlayActive && table != null)
        {
            var root = table.transform.root;
            var giraffe = root.GetComponentInChildren<GiraffeMode>(true);
            var tennis = root.GetComponentInChildren<TennisMode>(true);
            var ice = root.GetComponentInChildren<IcePower>(true);
            var squid = root.GetComponentInChildren<SquidMode>(true);
            if (giraffe != null && giraffe.active) visual = "giraffe";
            else if (ice != null && ice.active) visual = "ice";
            else if (tennis != null && tennis.active) visual = "tennis";
            else if (squid != null && squid.active) visual = "squid";
        }
        if (connected && visual != sentMode)
        {
            sentMode = visual;
            _ = SendPacket(JsonUtility.ToJson(new Packet { type = "visual", mode = visual }), socket);
        }
        if (table == null) return;
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
                table.SetExternal((LegControlSet)i, Decode(value));
                bool validPose = poseActive[i] && now - poseReceived[i] <= .65f
                    && Time.timeScale > 0 && !PauseMenu.InputBlocked && !LetterOverlay.IsOpen;
                if (validPose) table.SetExternalPose((LegControlSet)i, poseX[i], poseZ[i], poseYaw[i]);
                else table.ClearExternalPose((LegControlSet)i);
            }
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

    void SelectPower(int power)
    {
        if (table == null || !connected || !AbilityHud.CanReadInput(table)) return;
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;
        var root = table.transform.root;
        if (power == 2)
        {
            var g = root.GetComponentInChildren<GiraffeMode>(true);
            if (g != null && !g.locked) { g.active = !g.active; if (g.active) AbilitySelection.Select(g); powerPulseUntil[0] = Time.unscaledTime + .18f; }
        }
        else if (power == 3)
        {
            var t = root.GetComponentInChildren<TennisMode>(true);
            if (t != null && t.unlocked) { t.SetActive(!t.active); powerPulseUntil[1] = Time.unscaledTime + .18f; }
        }
        else if (power == 4)
        {
            var ice = root.GetComponentInChildren<IcePower>(true);
            if (ice != null && ice.unlocked) { ice.SetActive(!ice.active); powerPulseUntil[2] = Time.unscaledTime + .18f; }
        }
        else if (power == 5)
        {
            var squid = root.GetComponentInChildren<SquidMode>(true);
            if (squid != null && squid.unlocked && !squid.thrustLocked)
            { squid.active = !squid.active; if (squid.active) AbilitySelection.Select(squid); powerPulseUntil[3] = Time.unscaledTime + .18f; }
        }
    }

    void OnGUI()
    {
        if (!WebPlayActive) return;
        int count = 0; foreach (bool p in occupied) if (p && connected) count++;
        GUI.Label(new Rect(12, Screen.height - 30, 420, 26), "폰 " + count + "/4 · " + connectionStatus);
    }

    void OnDestroy()
    {
        cancellation?.Cancel(); socket?.Abort();
        if (table != null) for (int i=0;i<4;i++) if(applied[i]) table.SetSource((LegControlSet)i, TableInput.LegSource.Local);
        if (instance == this) instance = null;
        foreach (var texture in qr) if (texture != null) Destroy(texture);
        cancellation?.Dispose();
        sendLock.Dispose();
    }
}
