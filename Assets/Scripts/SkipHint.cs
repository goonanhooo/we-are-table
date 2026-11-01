using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★2026-11-01 초반 컷씬·튜토리얼 건너뛰기(스페이스) — 오른쪽 아래 작은 [Space] 건너뛰기 표시 + 입력 판정을 한곳에.
/// 쓰는 곳: HallwayStage(시작 컷씬·다리 소개), KeyTutorial(복도 걷기 안내), HallwayCoach(조교 수업).
/// </summary>
public static class SkipHint
{
    /// <summary>이번 프레임에 스페이스를 눌렀는가(일시정지·편지·치트 중엔 무시 — 치트는 스페이스가 비행).</summary>
    public static bool Pressed()
    {
        var kb = Keyboard.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame && !PauseMenu.InputBlocked && !LetterOverlay.IsOpen && !CheatMode.Active;
    }

    /// <summary>오른쪽 아래: [Space] 건너뛰기 (OnGUI의 Repaint에서 호출).</summary>
    public static void Draw()
    {
        if (Event.current.type != EventType.Repaint || PauseMenu.InputBlocked || LetterOverlay.IsOpen || CheatMode.Active) return;
        UiKit.ApplyKoreanFont();
        JourneyMenuUI.Begin();
        try
        {
            const float keyW = 104f, h = 48f;
            string label = L.T("건너뛰기", "Skip", "スキップ", "跳过");
            float textW = JourneyMenuUI.TextWidth(label, JourneyMenuUI.TXS) + 8f;
            var key = new Rect(1600f - 40f - textW - 12f - keyW, 900f - 40f - h - KeyboardHud.ReservedHeight, keyW, h);   // 미니 키보드가 있으면 그 위로
            JourneyMenuUI.KeyCap(key, "Space");
            JourneyMenuUI.Text(new Rect(key.xMax + 12f, key.y, textW, h), label, JourneyMenuUI.TXS, JourneyMenuUI.Muted);
        }
        finally { JourneyMenuUI.End(); }
    }
}
