using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Shared menu drawing. All hit tests use the same 1600 x 900 canvas as rendering.
// ★2026-10-25 '지우개 대소동' 디자인 규칙 적용(사용자 요청): 그 게임 규칙을 이 캔버스 단위로 ×2 —
//   간격 8·16·24·32·48 / 글자 24·28·32·36(굵기 하나, 한글 고딕 Noto Sans CJK) / 높이 72·88·104 /
//   버튼 = 주요(검정)·보통(흰 바탕+진한 선)·비활성(회색 글씨+연한 선) / 선 = 진한 3(누를 수 있는 것)·연한 2(구분), 모서리 8 /
//   색 없음(잉크·종이·회색만 — 다리 색은 '어느 다리인지' 표시할 때만).
//   '움직이는 상자': 모든 상자 테두리가 손그림처럼 살짝 삐뚤고 3장을 번갈아 그리며 끓음(0.75초 주기) + 호버 때 카드가 떠오름 + 화면이 바뀌면 옆에서 미끄러져 들어옴.
public static class JourneyMenuUI
{
    // ── 토큰 ──
    public const float S1 = 8f, S2 = 16f, S3 = 24f, S4 = 32f, S5 = 48f;
    public const int TXS = 24, TS = 28, TM = 32, TL = 36;
    public const float HS = 72f, HM = 88f, HL = 104f;
    public const float Radius = 8f;
    public static readonly Color Ink = new Color32(0x28, 0x2b, 0x2c, 0xff);
    public static readonly Color Accent = Ink;   // 예전 초록 강조색 — 색을 쓰지 않는 규칙이라 잉크
    public static readonly Color Muted = new Color32(0x66, 0x66, 0x66, 0xff);
    public static readonly Color PaperColor = new Color32(0xff, 0xfd, 0xf6, 0xff);
    public static readonly Color Background = new Color32(0xf3, 0xf2, 0xeb, 0xff);
    public static readonly Color LineSoft = new Color32(0xd7, 0xd7, 0xcf, 0xff);
    public static readonly Color White = Color.white;

    /// <summary>⚠️이 프로젝트(Linear)에선 IMGUI가 GUI.color·글자색·DrawTexture 색을 선형 값으로 그대로 써서 전부 밝게 나온다
    /// (잉크 #282b2c → #6e7273 실측). 모든 색을 여기서 선형으로 바꿔 넘기면 화면에 지정한 sRGB 그대로 나온다.</summary>
    public static Color Out(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    public static float Scale => Mathf.Min(Screen.width/1600f, Screen.height/900f);
    public static Vector2 Origin => new Vector2((Screen.width-1600*Scale)/2,(Screen.height-900*Scale)/2);
    public static Vector2 MousePosition => Mouse.current == null ? Vector2.one*-9999 : (new Vector2(Mouse.current.position.ReadValue().x,Screen.height-Mouse.current.position.ReadValue().y)-Origin)/Scale;

    // ── 클릭(Update에서 한 번 샘플 → 이번 프레임 IMGUI 패스들이 한 번만 소비) ──
    static int usedFrame = -1, polledFrame = -1;
    static bool pendingClick, wasBlocked;
    static Vector2 pressedPosition;
    public static void BlockClick(){usedFrame=Time.frameCount;pendingClick=false;}
    public static void PollInput()
    {
        if(polledFrame==Time.frameCount)return;polledFrame=Time.frameCount;
        pendingClick=false;
        bool blocked=PauseMenu.InputBlocked||LetterOverlay.IsOpen;
        if(blocked||wasBlocked){pendingClick=false;wasBlocked=blocked;return;}
        // A disabled button must never retain a click for a later menu state.
        if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&usedFrame!=Time.frameCount)
        {pendingClick=true;pressedPosition=MousePosition;}
    }
    /// <summary>PauseMenu처럼 입력을 막는 쪽이 자기 메뉴를 그릴 때: 막힘 검사 없이 이번 프레임 클릭을 샘플.</summary>
    public static void PollInputUnblocked()
    {
        if(polledFrame==Time.frameCount)return;polledFrame=Time.frameCount;wasBlocked=false;
        pendingClick=Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&usedFrame!=Time.frameCount;
        if(pendingClick)pressedPosition=MousePosition;
    }
    public static bool Click(Rect r, bool enabled=true)
    {
        if (!enabled || !pendingClick || usedFrame == Time.frameCount || Sliding || !r.Contains(pressedPosition)) return false;
        usedFrame=Time.frameCount;pendingClick=false;return true;
    }
    /// <summary>이번 프레임의 클릭이 아직 아무 버튼에도 안 쓰였으면 그 위치(바깥 클릭 닫기용).</summary>
    public static bool UnusedClick(out Vector2 at){at=pressedPosition;return pendingClick&&usedFrame!=Time.frameCount;}

    // ── 화면 전환: 새 화면은 오른쪽에서 미끄러져 들어오며 나타남(지우개 대소동 로비 페이지처럼) ──
    sealed class Page { public string key; public float start=-10f, lastDrawn=-10f; }
    static readonly Dictionary<string, Page> pages = new Dictionary<string, Page>();
    const float SlideSeconds = .32f, SlideDistance = 96f;
    static float slideX, alpha = 1f, activeSlideStart = -10f;
    static bool Sliding => Time.unscaledTime - activeSlideStart < SlideSeconds * .55f;
    static Matrix4x4 previous;
    static Color previousColor, previousContent;
    static bool previousEnabled;

    /// <summary>입력을 막는 쪽(설정 메뉴)이 자기 메뉴를 그리는 동안엔 호버를 허용.</summary>
    static bool modal;
    static bool HoverOk => modal || (!PauseMenu.InputBlocked && !LetterOverlay.IsOpen);
    public static bool Hover(Rect r) => HoverOk && r.Contains(MousePosition);
    public static void Begin() => Begin(null, null);
    public static void BeginModal(string channel, string page) { Begin(channel, page); modal = true; }
    /// <summary>channel = 그리는 쪽(메뉴 종류), page = 지금 화면. 화면이 바뀌거나 잠깐 안 보이다 다시 그려지면 미끄러져 들어온다.</summary>
    public static void Begin(string channel, string page)
    {
        UiKit.ApplyKoreanFont(); previous=GUI.matrix;previousColor=GUI.color;previousContent=GUI.contentColor;previousEnabled=GUI.enabled;
        slideX = 0f; alpha = 1f;
        if (channel != null)
        {
            if (!pages.TryGetValue(channel, out var p)) { p = new Page(); pages[channel] = p; }
            float now = Time.unscaledTime;
            if (p.key != page || now - p.lastDrawn > .15f) { p.key = page; p.start = now; activeSlideStart = now; }
            if (Event.current == null || Event.current.type == EventType.Repaint) p.lastDrawn = now;
            float u = Mathf.Clamp01((now - p.start) / SlideSeconds), e = 1f - (1f - u) * (1f - u) * (1f - u);
            slideX = (1f - e) * SlideDistance; alpha = Mathf.Lerp(.0f, 1f, Mathf.Clamp01(u * 1.6f));
        }
        GUI.color = new Color(1f, 1f, 1f, alpha); GUI.contentColor = Color.white; GUI.enabled = true;
        GUI.matrix = Matrix4x4.TRS(Origin + new Vector2(slideX * Scale, 0f), Quaternion.identity, Vector3.one * Scale);
    }
    public static void End(){modal=false;GUI.matrix=previous;GUI.color=previousColor;GUI.contentColor=previousContent;GUI.enabled=previousEnabled;}

    // ── 글자: 네 단계만(24·28·32·36), 굵기 하나, 한글 고딕 ──
    static Font font;
    static GUIStyle textStyle, literalStyle;
    public static Font Font { get { if (font == null) font = Resources.Load<Font>("Fonts/CJK/NotoSansCJKjp-Regular"); return font; } }
    public static int Snap(int size) => size <= 25 ? TXS : size <= 30 ? TS : size <= 34 ? TM : TL;
    static GUIStyle Style()
    {
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = true, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            if (Font != null) textStyle.font = Font;
            textStyle.hover.background = textStyle.active.background = null;
        }
        return textStyle;
    }
    public static void Text(Rect r,string text,int size=TS,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var st=Style();st.alignment=align;var c=Out(color??Ink);c.a*=alpha;
        st.normal.textColor=st.hover.textColor=st.active.textColor=st.focused.textColor=c;
        var old=GUI.color;GUI.color=Color.white;
        int s=Snap(size);ThoughtBubbleUI.FittedLabel(r,text,st,s,Mathf.Min(s,18));
        GUI.color=old;
    }
    public static float TextWidth(string text,int size){var st=Style();st.fontSize=Snap(size);return st.CalcSize(new GUIContent(L.Text(text))).x;}
    public static void Literal(Rect r,string text,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft)
    {
        if(literalStyle==null){literalStyle=new GUIStyle(GUI.skin.label){richText=false,wordWrap=false,padding=new RectOffset(0,0,0,0)};if(Font!=null)literalStyle.font=Font;}
        int s=Snap(size);literalStyle.fontSize=s;literalStyle.alignment=align;var c=Out(color);c.a*=alpha;literalStyle.normal.textColor=literalStyle.hover.textColor=literalStyle.active.textColor=literalStyle.focused.textColor=c;
        while(literalStyle.fontSize>16&&literalStyle.CalcSize(new GUIContent(text)).x>r.width)literalStyle.fontSize--;
        while(text.Length>1&&literalStyle.CalcSize(new GUIContent(text)).x>r.width){var parts=System.Globalization.StringInfo.ParseCombiningCharacters(text);if(parts.Length<2)break;text=text.Substring(0,parts[parts.Length-2])+"…";}
        var old=GUI.color;GUI.color=Color.white;GUI.Label(r,text,literalStyle);GUI.color=old;
    }

    // ── 칠하기 ──
    public static void Fill(Rect r,Color c){var old=GUI.color;c=Out(c);c.a*=alpha;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    public static void RoundFill(Rect r,Color c,float radius=Radius)
    {
        if(r.width<=0||r.height<=0)return;var old=GUI.color;GUI.color=Color.white;c.a*=alpha;
        c=Out(c);
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f));
        GUI.color=old;
    }
    public static void Border(Rect r,Color c,float width=2)
    {
        Fill(new Rect(r.x,r.y,r.width,width),c);Fill(new Rect(r.x,r.yMax-width,r.width,width),c);
        Fill(new Rect(r.x,r.y,width,r.height),c);Fill(new Rect(r.xMax-width,r.y,width,r.height),c);
    }

    // ── 손그림 끓는 선(지우개 대소동 hand-borders.js의 IMGUI판) ──
    //   변마다 씨앗이 다른 부드러운 물결(양 끝은 0으로 줄어 모서리 호와 이어짐) + 옆에 옅은 두 번째 획(연필 결).
    //   씨앗 3 × 길이 단계 3 × 프레임 3을 처음 쓸 때 만들어 두고, 0.25초마다 프레임을 바꿔 '끓게' 한다.
    const int TW = 256, TH = 32; const float PxPerUnit = 4f, Band = TH / PxPerUnit;   // 띠 8단위
    static readonly Dictionary<int, Texture2D> strips = new Dictionary<int, Texture2D>();
    static readonly Texture2D[] corners = new Texture2D[8];
    public static int BoilFrame => (int)(Time.unscaledTime * 4f) % 3;
    static float Thick(bool strong) => strong ? 3f : 2f;
    static float Hash(int a) { unchecked { uint x = (uint)a * 747796405u + 2891336453u; x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u; x = (x >> 22) ^ x; return (x & 0xffffff) / 16777216f; } }
    static Texture2D Strip(bool strong, bool vertical, int cls, int seed, int frame)
    {
        int key = (strong ? 1 : 0) | (vertical ? 2 : 0) | (cls << 2) | (seed << 4) | (frame << 6);
        if (strips.TryGetValue(key, out var t) && t != null) return t;
        int waves = cls == 0 ? 2 : cls == 1 ? 4 : 8; float amp = (cls == 0 ? .8f : cls == 1 ? 1.2f : 1.6f) * PxPerUnit;
        int h = seed * 31 + frame * 7 + cls * 101 + (strong ? 3 : 0);
        float p1 = Hash(h) * 6.283f, p2 = Hash(h + 1) * 6.283f, p3 = Hash(h + 2) * 6.283f, pr = Hash(h + 3) * 6.283f;
        float th = Thick(strong) * PxPerUnit;
        int w = vertical ? TH : TW, ht = vertical ? TW : TH;
        t = new Texture2D(w, ht, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[w * ht];
        for (int i = 0; i < TW; i++)
        {
            float u = (i + .5f) / TW, env = Mathf.Sqrt(Mathf.Sin(Mathf.PI * u));
            float off = amp * env * (.6f * Mathf.Sin(6.283f * waves * u + p1) + .3f * Mathf.Sin(6.283f * (2 * waves + 1) * u + p2) + .1f * Mathf.Sin(6.283f * (5 * waves + 3) * u + p3));
            float press = .9f + .14f * Mathf.Sin(6.283f * 1.5f * u + pr);   // 필압
            float c = TH * .5f + off, half = th * .5f * press;
            for (int j = 0; j < TH; j++)
            {
                float y = j + .5f;
                float a = Mathf.Clamp01(half + .5f - Mathf.Abs(y - c));
                float a2 = Mathf.Clamp01(half * .5f + .5f - Mathf.Abs(y - (c + .9f))) * .4f;   // 바로 옆 옅은 두 번째 획
                byte v = (byte)(Mathf.Clamp01(Mathf.Max(a, a2)) * 255f);
                if (vertical) px[i * w + j] = new Color32(255, 255, 255, v); else px[j * w + i] = new Color32(255, 255, 255, v);
            }
        }
        t.SetPixels32(px); t.Apply(false, true); strips[key] = t; return t;
    }
    static Texture2D Corner(bool strong, int which)   // which: 0 TL, 1 TR, 2 BR, 3 BL
    {
        int k = which + (strong ? 4 : 0);
        if (corners[k] != null) return corners[k];
        float S = Radius + Band * .5f; int n = Mathf.RoundToInt(S * PxPerUnit);
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n]; float half = Thick(strong) * PxPerUnit * .5f, R = Radius * PxPerUnit;
        // 중심(GUI 좌표): TL=(n,n) TR=(0,n) BR=(0,0) BL=(n,0)
        float cx = which == 0 || which == 3 ? n : 0, cy = which == 0 || which == 1 ? n : 0;
        for (int gy = 0; gy < n; gy++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + .5f, gy + .5f), new Vector2(cx, cy));
            byte v = (byte)(Mathf.Clamp01(half + .5f - Mathf.Abs(d - R)) * 255f);
            px[(n - 1 - gy) * n + x] = new Color32(255, 255, 255, v);   // 텍스처 y는 아래가 0
        }
        t.SetPixels32(px); t.Apply(false, true); corners[k] = t; return t;
    }
    static int Cls(float len) => len < 240 ? 0 : len < 700 ? 1 : 2;
    static int SeedOf(Rect r) => Mathf.Abs(Mathf.RoundToInt(r.x) * 73856093 ^ Mathf.RoundToInt(r.y) * 19349663 ^ Mathf.RoundToInt(r.width) * 83492791 ^ Mathf.RoundToInt(r.height) * 2654435);
    /// <summary>손그림 테두리(둥근 모서리 8). 선 중심 = r의 가장자리.</summary>
    public static void SketchOutline(Rect r, Color c, bool strong = true, int seed = -1)
    {
        if (Event.current != null && Event.current.type != EventType.Repaint) return;
        if (r.width < 2 * Radius + 2 || r.height < 2 * Radius + 2) { Border(r, c, Thick(strong)); return; }
        if (seed < 0) seed = SeedOf(r);
        int f = BoilFrame; float hb = Band * .5f, S = Radius + hb;
        var old = GUI.color; c = Out(c); c.a *= alpha; GUI.color = c;
        GUI.DrawTexture(new Rect(r.x + Radius, r.y - hb, r.width - 2 * Radius, Band), Strip(strong, false, Cls(r.width), seed % 3, f));
        GUI.DrawTexture(new Rect(r.x + Radius, r.yMax - hb, r.width - 2 * Radius, Band), Strip(strong, false, Cls(r.width), (seed / 3 + 1) % 3, f));
        GUI.DrawTexture(new Rect(r.x - hb, r.y + Radius, Band, r.height - 2 * Radius), Strip(strong, true, Cls(r.height), (seed / 9 + 2) % 3, f));
        GUI.DrawTexture(new Rect(r.xMax - hb, r.y + Radius, Band, r.height - 2 * Radius), Strip(strong, true, Cls(r.height), (seed / 27) % 3, f));
        GUI.DrawTexture(new Rect(r.x - hb, r.y - hb, S, S), Corner(strong, 0));
        GUI.DrawTexture(new Rect(r.xMax - Radius, r.y - hb, S, S), Corner(strong, 1));
        GUI.DrawTexture(new Rect(r.xMax - Radius, r.yMax - Radius, S, S), Corner(strong, 2));
        GUI.DrawTexture(new Rect(r.x - hb, r.yMax - Radius, S, S), Corner(strong, 3));
        GUI.color = old;
    }
    /// <summary>손그림 가로줄(구분선).</summary>
    public static void SketchRule(float x, float y, float width, Color? color = null, bool strong = false)
    {
        if (Event.current != null && Event.current.type != EventType.Repaint) return;
        var c = Out(color ?? LineSoft); var old = GUI.color; c.a *= alpha; GUI.color = c;
        GUI.DrawTexture(new Rect(x, y - Band * .5f, width, Band), Strip(strong, false, Cls(width), Mathf.Abs(Mathf.RoundToInt(x + y)) % 3, BoilFrame));
        GUI.color = old;
    }

    // ── 상자 ──
    public enum Kind { Panel, Card, Primary, Disabled, Soft, Sheet }
    static readonly Dictionary<int, float> lifts = new Dictionary<int, float>();
    static readonly Stack<Matrix4x4> liftStack = new Stack<Matrix4x4>();
    /// <summary>호버 시 부드럽게 4단위 떠오르는 양(0~4). 같은 상자(r)는 같은 값을 이어 씀.</summary>
    public static float Lift(Rect r, bool hover)
    {
        int k = SeedOf(r); lifts.TryGetValue(k, out float v);
        if (Event.current == null || Event.current.type == EventType.Repaint) { v = Mathf.MoveTowards(v, hover ? 4f : 0f, Time.unscaledDeltaTime * 40f); lifts[k] = v; }
        return v;
    }
    /// <summary>상자 하나: Panel(종이+진한 선, 큰 창) · Card(흰+진한 선, 누를 수 있는 것) · Primary(검정) · Disabled(흰+연한 선) · Soft(종이+연한 선, 구분) · Sheet(종이+진한 선+그림자).
    /// selected = 안쪽 진한 선(크기 안 바뀜). lift = 위로 뜬 양(그림자가 남음).</summary>
    public static void Box(Rect r, Kind kind, bool selected = false, float lift = 0f, int seed = -1)
    {
        if (seed < 0) seed = SeedOf(r);
        if (lift > .01f || kind == Kind.Sheet) RoundFill(new Rect(r.x + 2, r.y + 6, r.width, r.height), new Color(0, 0, 0, kind == Kind.Sheet ? .07f : .05f + lift * .012f));
        var b = new Rect(r.x, r.y - lift, r.width, r.height);
        Color fill = kind == Kind.Primary ? Ink : kind == Kind.Panel || kind == Kind.Soft || kind == Kind.Sheet ? PaperColor : White;
        Color line = kind == Kind.Disabled || kind == Kind.Soft ? LineSoft : Ink;
        RoundFill(b, fill);
        SketchOutline(b, line, kind == Kind.Card || kind == Kind.Primary, seed);   // 누를 수 있는 것=진한 3, 창·구분=2
        if (selected) SketchOutline(new Rect(b.x + 6, b.y + 6, b.width - 12, b.height - 12), kind == Kind.Primary ? White : Ink, true, seed + 5);
    }
    /// <summary>카드 시작: 호버면 떠오르고, 그 안에 그리는 글·그림도 함께 뜨게 행렬을 올린다. 반드시 EndCard.</summary>
    public static bool BeginCard(Rect r, bool enabled = true, bool selected = false, Kind kind = Kind.Card)
    {
        bool hover = enabled && Hover(r);
        float lift = Lift(r, hover);
        Box(r, enabled ? kind : Kind.Disabled, selected, lift);
        liftStack.Push(GUI.matrix);
        GUI.matrix = GUI.matrix * Matrix4x4.Translate(new Vector3(0, -lift, 0));
        return hover;
    }
    public static void EndCard() { if (liftStack.Count > 0) GUI.matrix = liftStack.Pop(); }

    /// <summary>예전 종이 카드(호환): 선택 = 흰 카드+안쪽 선, 호버 = 흰 카드, 그 밖 = 종이 창.</summary>
    public static void Paper(Rect r,bool selected=false,bool hover=false) => Box(r, selected || hover ? Kind.Card : Kind.Panel, selected);

    /// <summary>버튼: 주요(검정)·보통(흰+진한 선)·비활성(회색 글씨+연한 선). 호버면 살짝 떠오름.</summary>
    public static bool Button(Rect r,string text,bool enabled=true,bool primary=false)
    {
        bool hover=enabled&&Hover(r);
        float lift=Lift(r,hover);
        Box(r,!enabled?Kind.Disabled:primary?Kind.Primary:Kind.Card,false,lift);
        var tr=new Rect(r.x+S2,r.y-lift,r.width-2*S2,r.height);
        Text(tr,text,r.height>=HM?TM:TS,!enabled?Muted:primary?White:Ink,TextAnchor.MiddleCenter);
        return Click(r,enabled);
    }
    /// <summary>예전 '호버하면 밑줄' 글씨(호환) — 이제 밑줄 없이 글씨만(카드가 떠오르는 것으로 대신).</summary>
    public static void HoverText(Rect r,string text,Rect hit,int size=TS,bool enabled=true,TextAnchor align=TextAnchor.MiddleLeft) => Text(r,text,size,enabled?Ink:Muted,align);

    public static void Line(Vector2 a,Vector2 b,Color c,float width=2)
    {
        // Axis-aligned strokes stay in the current GUI clip group (journey cards).
        if(Mathf.Abs(a.y-b.y)<.001f){Fill(new Rect(Mathf.Min(a.x,b.x),a.y-width/2,Mathf.Abs(b.x-a.x),width),c);return;}
        if(Mathf.Abs(a.x-b.x)<.001f){Fill(new Rect(a.x-width/2,Mathf.Min(a.y,b.y),width,Mathf.Abs(b.y-a.y)),c);return;}
        var m=GUI.matrix;GUI.matrix=m*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
        Fill(new Rect(0,-width/2,Vector2.Distance(a,b),width),c);GUI.matrix=m;
    }
    public static void Dot(Vector2 p,float radius,Color c) => RoundFill(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),c,radius);
    public static void Icon(Rect r,int kind,Color? tint=null)
    {
        var c=tint??Ink;Vector2 p=r.center;float s=Mathf.Min(r.width,r.height)/100f;
        void L(float ax,float ay,float bx,float by)=>Line(p+new Vector2(ax,ay)*s,p+new Vector2(bx,by)*s,c,3*s);
        if(kind==0){ // table, with four readable feet
            L(-35,-19,16,-19);L(16,-19,35,-3);L(35,-3,-16,-3);L(-16,-3,-35,-19);
            L(-35,-19,-35,16);L(16,-19,16,16);L(-16,-3,-16,32);L(35,-3,35,32);
        }else if(kind==1){ // create a room
            L(-37,-2,0,-32);L(0,-32,37,-2);L(-28,-9,-28,34);L(-28,34,28,34);L(28,34,28,-9);
            L(-10,9,10,9);L(0,-1,0,19);
        }else if(kind==2){ // enter a room
            L(5,-34,32,-34);L(32,-34,32,34);L(32,34,5,34);L(-38,0,15,0);L(15,0,0,-15);L(15,0,0,15);
        }else{ // lock
            Border(new Rect(p.x-23*s,p.y-5*s,46*s,38*s),c,3*s);
            L(-14,-5,-14,-24);L(-14,-24,14,-24);L(14,-24,14,-5);Dot(p+new Vector2(0,11)*s,3*s,c);
        }
    }
    public static string Back => L.T("뒤로","Back","戻る","返回");
    public static void BackArrow(Rect r,Color color)
    {
        float x=r.x+S3,y=r.center.y,w=28;
        Line(new Vector2(x+w,y),new Vector2(x,y),color,3);
        Line(new Vector2(x,y),new Vector2(x+11,y-10),color,3);
        Line(new Vector2(x,y),new Vector2(x+11,y+10),color,3);
    }
    /// <summary>뒤로(보통 버튼 + 왼쪽 화살표).</summary>
    public static bool BackButton(Rect r,string label=null,bool enabled=true)
    {
        bool hover=enabled&&Hover(r);
        float lift=Lift(r,hover);Box(r,enabled?Kind.Card:Kind.Disabled,false,lift);
        var lr=new Rect(r.x,r.y-lift,r.width,r.height);var color=enabled?Ink:Muted;BackArrow(lr,color);
        Text(new Rect(lr.x+S3+28+S2,lr.y,lr.width-(S3+28+S2)-S3,lr.height),label??Back,r.height>=HM?TM:TS,color,TextAnchor.MiddleCenter);
        return Click(r,enabled);
    }

    /// <summary>화살표 버튼(‹ ›): 글꼴 기호 대신 선으로 그린 갈매기표.</summary>
    public static bool ArrowButton(Rect r,int dir,bool enabled=true){ArrowVisual(r,dir,enabled);return Click(r,enabled);}
    public static void ArrowVisual(Rect r,int dir,bool enabled=true)
    {
        bool hover=enabled&&Hover(r);float lift=Lift(r,hover);
        Box(r,enabled?Kind.Card:Kind.Disabled,false,lift);
        var c=new Vector2(r.center.x,r.center.y-lift);float a=9f;var ink=enabled?Ink:Muted;
        Line(c+new Vector2(-dir*a*.5f,-a*1.4f),c+new Vector2(dir*a*.7f,0),ink,3);   // dir -1 = ‹, +1 = ›
        Line(c+new Vector2(dir*a*.7f,0),c+new Vector2(-dir*a*.5f,a*1.4f),ink,3);
    }
    /// <summary>클릭은 다른 곳(Update)에서 처리하는 버튼 모양만.</summary>
    public static void ButtonVisual(Rect r,string text,bool enabled=true,bool primary=false)
    {
        bool hover=enabled&&Hover(r);float lift=Lift(r,hover);
        Box(r,!enabled?Kind.Disabled:primary?Kind.Primary:Kind.Card,false,lift);
        Text(new Rect(r.x+S2,r.y-lift,r.width-2*S2,r.height),text,r.height>=HM?TM:TS,!enabled?Muted:primary?White:Ink,TextAnchor.MiddleCenter);
    }
    /// <summary>글자 입력칸: 흰 바탕 + 진한 손그림 선 + 깜빡이는 커서(타이핑은 각 화면 Update가 받음).</summary>
    public static void Field(Rect r,string value,string placeholder="")
    {
        Box(r,Kind.Card);
        var t=new Rect(r.x+S3,r.y,r.width-2*S3,r.height);
        if(!string.IsNullOrEmpty(value)){Literal(t,value+(Time.unscaledTime%1<.55f?"|":" "),TM,Ink);return;}
        if(Time.unscaledTime%1<.55f)Fill(new Rect(t.x,r.center.y-16,3,32),Ink);
        if(!string.IsNullOrEmpty(placeholder))Text(new Rect(t.x+S2,t.y,t.width-S2,t.height),placeholder,TS,Muted);
    }
    /// <summary>선택 카드: [아이콘 → 이름 → (설명) → 아래 버튼]. 카드 전체가 버튼, 호버 시 떠오름.</summary>
    public static bool ChoiceCard(Rect r,int icon,string title,string desc,string action,bool enabled=true,bool selected=false,string badge=null)
    {
        BeginCard(r,enabled,selected);
        Icon(new Rect(r.x+S4,r.y+S4,96,96),icon,enabled?Ink:Muted);
        if(!string.IsNullOrEmpty(badge))Text(new Rect(r.x+r.width*.5f,r.y+S4,r.width*.5f-S4,40),badge,TXS,Muted,TextAnchor.MiddleRight);
        float ty=r.y+S4+96+S3;
        Text(new Rect(r.x+S4,ty,r.width-2*S4,52),title,TL,enabled?Ink:Muted);
        if(!string.IsNullOrEmpty(desc))Text(new Rect(r.x+S4,ty+52+S1,r.width-2*S4,40),desc,TS,Muted);
        if(!string.IsNullOrEmpty(action))
        {
            var b=new Rect(r.x+S4,r.yMax-S4-HS,r.width-2*S4,HS);
            Box(b,enabled?Kind.Primary:Kind.Disabled);
            Text(new Rect(b.x+S2,b.y,b.width-2*S2,b.height),action,TS,enabled?White:Muted,TextAnchor.MiddleCenter);
        }
        EndCard();
        return Click(r,enabled);
    }
    /// <summary>ChoiceCard 높이: 설명이 있으면 한 줄 더.</summary>
    public static float ChoiceCardHeight(bool desc)=>S4+96+S3+52+(desc?S1+40:0)+S4+HS+S4;

    // ── 자판 키캡(다리 선택·조작법) ──
    /// <summary>다리 슬롯별 키(위·왼쪽·아래·오른쪽) — LegLayout과 같은 순서(0 WASD · 1 TFGH · 2 IJKL · 3 [ ; ' Enter).</summary>
    public static readonly string[][] LegKeys={new[]{"W","A","S","D"},new[]{"T","F","G","H"},new[]{"I","J","K","L"},new[]{"[",";","'","Enter"}};
    /// <summary>키캡 하나: 흰 바탕 + 손그림 진한 선 + 글자(Enter처럼 긴 글자는 넓게).</summary>
    public static void KeyCap(Rect r,string label,bool enabled=true)
    {
        Box(r,enabled?Kind.Card:Kind.Disabled);
        Literal(r,label,label.Length>1?TXS:TS,enabled?Ink:Muted,TextAnchor.MiddleCenter);
    }
    public static float KeyWidth(string label,float size)=>label.Length>1?size*1.8f:size;
    /// <summary>뒤집힌 T자 키 묶음(위 1개 + 아래 3개)을 area 안 가운데에 그린다.</summary>
    public static void KeyCluster(Rect area,int slot,bool enabled=true)
    {
        var k=LegKeys[slot];float gap=S1,size=Mathf.Min((area.height-gap)*.5f,48f);
        float w1=KeyWidth(k[1],size),w2=KeyWidth(k[2],size),w3=KeyWidth(k[3],size),row=w1+w2+w3+2*gap;
        float x=area.center.x-row*.5f,y=area.center.y-size-gap*.5f;
        var mid=new Rect(x+w1+gap,y+size+gap,w2,size);
        KeyCap(new Rect(mid.center.x-KeyWidth(k[0],size)*.5f,y,KeyWidth(k[0],size),size),k[0],enabled);
        KeyCap(new Rect(x,y+size+gap,w1,size),k[1],enabled);KeyCap(mid,k[2],enabled);KeyCap(new Rect(mid.xMax+gap,y+size+gap,w3,size),k[3],enabled);
    }
    /// <summary>작은 회색 구역 이름(예: 소리 · 게임).</summary>
    public static void Section(Rect r,string text){Text(new Rect(r.x,r.y,r.width,32),text,TXS,Muted);SketchRule(r.x,r.y+40,r.width);}
    /// <summary>화면 아래 짧은 안내/상태 글.</summary>
    public static void Note(Rect r,string text,TextAnchor align=TextAnchor.MiddleLeft)=>Text(r,text,TXS,Muted,align);

    /// <summary>슬라이더: 연한 둥근 홈 + 잉크 채움 + 흰 손잡이(진한 선).</summary>
    public static void Slider(Rect r, float v)
    {
        v = Mathf.Clamp01(v); float cy = r.center.y, h = 8f, knob = 28f;
        RoundFill(new Rect(r.x, cy - h * .5f, r.width, h), LineSoft, h * .5f);
        RoundFill(new Rect(r.x, cy - h * .5f, r.width * v, h), Ink, h * .5f);
        var k = new Rect(r.x + r.width * v - knob * .5f, cy - knob * .5f, knob, knob);
        RoundFill(k, Ink, knob * .5f); RoundFill(new Rect(k.x + 3, k.y + 3, knob - 6, knob - 6), White, knob * .5f - 3);
    }
    /// <summary>화면 바탕(종이색) — 메뉴 화면 전체.</summary>
    public static void Backdrop(float a = 1f) { var c = Background; c.a = a; Fill(new Rect(-200, -200, 2000, 1300), c); }
    /// <summary>제목(36) + 설명(28 회색).</summary>
    public static void Heading(string title, string description = "", float y = 64f)
    {
        Text(new Rect(80, y, 1440, 52), title, TL);
        if (!string.IsNullOrEmpty(description)) Text(new Rect(80, y + 52 + S1, 1440, 40), description, TS, Muted);
    }
}

// 체크포인트 여정 카드 줄(가로 연결). 카드 = 사진 → 번호·이름, 선택 = 안쪽 진한 선, 미도달 = 연한 선 + 자물쇠.
public sealed class CheckpointJourney
{
    public int Selected {get;private set;}
    float scroll;
    readonly JourneyWheel wheel=new JourneyWheel();
    readonly Texture2D[] shots=new Texture2D[16];
    public void Select(int id,bool immediate=false){Selected=Mathf.Clamp(id,0,15);if(immediate)scroll=Selected;}
    public void Update(bool interactive=true)
    {
        if(interactive){var kb=Keyboard.current;int move=0;if(kb!=null){if(kb.leftArrowKey.wasPressedThisFrame)move--;if(kb.rightArrowKey.wasPressedThisFrame)move++;}
            if(Mouse.current!=null)move+=wheel.Read(Mouse.current.scroll.ReadValue(),Time.unscaledTime);Select(Selected+move);}
        else wheel.Reset();
        scroll=Mathf.Lerp(scroll,Selected,1-Mathf.Exp(-Time.unscaledDeltaTime*12));
    }
    public static Texture2D Thumbnail(int id)=>Resources.Load<Texture2D>("CheckpointThumbs/"+id.ToString("00"));
    public void Draw(Rect viewport,uint mask,bool interactive=true)
    {
        // Hit tests remain in canvas coordinates; clipping only affects the artwork.
        const float stride=372, cw=332, ch=268, photoH=176;
        float lineY=viewport.height-JourneyMenuUI.S2;
        GUI.BeginGroup(viewport);
        for(int i=0;i<16;i++){
            float cx=viewport.width/2+(i-scroll)*stride;
            if(cx<-cw||cx>viewport.width+cw)continue;
            bool open=(mask&(1u<<i))!=0, selected=i==Selected;
            var card=new Rect(cx-cw/2,JourneyMenuUI.S3,cw,ch);
            if(i<15)JourneyMenuUI.Line(new Vector2(cx,lineY),new Vector2(cx+stride,lineY),JourneyMenuUI.LineSoft,2);
            JourneyMenuUI.Dot(new Vector2(cx,lineY),selected?JourneyMenuUI.S1:5,open?JourneyMenuUI.Ink:JourneyMenuUI.LineSoft);
            var hit=new Rect(card.x+viewport.x,card.y+viewport.y,cw,ch);
            bool hover=interactive&&open&&hit.Contains(JourneyMenuUI.MousePosition)&&viewport.Contains(JourneyMenuUI.MousePosition);
            float lift=JourneyMenuUI.Lift(hit,hover||selected);
            JourneyMenuUI.Box(card,open?JourneyMenuUI.Kind.Card:JourneyMenuUI.Kind.Disabled,selected,lift);
            var c=new Rect(card.x,card.y-lift,cw,ch);
            Rect photo=new Rect(c.x+JourneyMenuUI.S2,c.y+JourneyMenuUI.S2,cw-2*JourneyMenuUI.S2,photoH);
            if(open){if(shots[i]==null)shots[i]=Thumbnail(i);if(shots[i]!=null)GUI.DrawTexture(photo,shots[i],ScaleMode.ScaleAndCrop);else JourneyMenuUI.Icon(photo,0);}
            else {JourneyMenuUI.RoundFill(photo,JourneyMenuUI.Background,4);JourneyMenuUI.Icon(new Rect(photo.center.x-36,photo.center.y-36,72,72),3,JourneyMenuUI.Muted);}
            float ty=photo.yMax+JourneyMenuUI.S1;
            JourneyMenuUI.Text(new Rect(c.x+JourneyMenuUI.S2,ty,48,c.yMax-ty-JourneyMenuUI.S1),(i+1).ToString("00"),JourneyMenuUI.TXS,JourneyMenuUI.Muted);
            JourneyMenuUI.Text(new Rect(c.x+JourneyMenuUI.S2+48+JourneyMenuUI.S1,ty,cw-2*JourneyMenuUI.S2-56,c.yMax-ty-JourneyMenuUI.S1),CheckpointCatalog.Label(i,mask),JourneyMenuUI.TS,open?JourneyMenuUI.Ink:JourneyMenuUI.Muted);
        }
        GUI.EndGroup();
        for(int i=0;i<16;i++){
            float cx=viewport.center.x+(i-scroll)*stride;
            var r=new Rect(cx-cw/2,viewport.y+JourneyMenuUI.S3,cw,ch);
            if(interactive&&viewport.Contains(JourneyMenuUI.MousePosition)&&JourneyMenuUI.Click(r))Select(i);
        }
    }
}

// Accumulate trackpad deltas, and limit repeated moves during a single gesture.
public sealed class JourneyWheel
{
    float accumulated,lastMove=-10f,lastInput=-10f;
    public void Reset(){accumulated=0;lastInput=-10f;lastMove=-10f;}
    public int Read(Vector2 delta,float now)
    {
        float value=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?delta.x:-delta.y;
        if(Mathf.Abs(value)<.01f)return 0;
        if(now-lastInput>.22f || accumulated*value<0)accumulated=0;
        lastInput=now;accumulated+=value;
        if(Mathf.Abs(accumulated)<1f || now-lastMove<.16f)return 0;
        int direction=accumulated>0?1:-1;accumulated=0;lastMove=now;return direction;
    }
}
