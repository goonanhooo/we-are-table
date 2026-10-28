using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 닉네임 입력칸 타이핑(한글 IME 조합 포함). Begin~End 사이에만 키보드 글자를 받는다.
/// 글자는 onTextInput, 조합 중인 글자는 onIMECompositionChange, 지우기는 화면 Update가 Backspace()로 넘긴다
/// (IMGUI TextField는 이 프로젝트의 New Input System에서 안 먹음).
/// </summary>
public class NicknameTyping
{
    public const int MaxLength = 16;
    public string Text { get; private set; } = "";
    string composing = "";
    int composedFrame = -10;
    Keyboard bound;

    public bool Active => bound != null;
    /// <summary>화면에 보일 글자 = 확정된 글자 + 조합 중인 글자.</summary>
    public string Shown => Text + composing;

    public void Begin(string start)
    {
        End();
        Text = Clip(start); composing = "";
        bound = Keyboard.current; if (bound == null) return;
        bound.onTextInput += OnChar; bound.onIMECompositionChange += OnCompose;
        bound.SetIMEEnabled(true);
    }

    public void End()
    {
        if (bound != null)
        {
            bound.onTextInput -= OnChar; bound.onIMECompositionChange -= OnCompose;
            bound.SetIMEEnabled(false);
        }
        bound = null; composing = "";
    }

    /// <summary>조합 창이 입력칸 옆에 뜨도록(화면 픽셀, 왼쪽 위 원점).</summary>
    public void SetCursor(Vector2 screenPixel) { if (bound != null) bound.SetIMECursorPosition(screenPixel); }

    /// <summary>Backspace: 조합 중이면 IME가 처리하므로 확정된 글자만 지운다.</summary>
    public void Backspace()
    {
        if (composing.Length > 0 || Time.frameCount - composedFrame <= 1 || Text.Length == 0) return;
        var e = StringInfo.ParseCombiningCharacters(Text);
        Text = Text.Substring(0, e[e.Length - 1]);
    }

    void OnChar(char c)
    {
        if (char.IsControl(c) || c == '<' || c == '>' || (c >= '\u202a' && c <= '\u202e') || (c >= '\u2066' && c <= '\u2069')) return;
        composing = "";
        Text = Clip(Text + c);
    }

    void OnCompose(IMECompositionString s) { composing = s.ToString(); composedFrame = Time.frameCount; }

    static string Clip(string v)
    {
        v ??= "";   // 공백·금지 글자 정리는 저장할 때(NetLegLink.SetMyName) — 여기서 다듬으면 단어 사이 띄어쓰기를 못 친다
        var e = StringInfo.ParseCombiningCharacters(v);
        return e.Length > MaxLength ? v.Substring(0, e[MaxLength]) : v;
    }
}
