using UnityEngine;

/// <summary>
/// ★2026-07-14 다리 색/닉네임 공용 팔레트 — 로비 슬롯 순서(0=앞왼FL, 1=앞오FR, 2=뒤왼BL, 3=뒤오BR).
/// 색은 조작법 이미지(FL초록/FR빨강/BL노랑/BR파랑)·LegColorXray와 일치.
/// 닉네임은 자동: 호스트=P1, 게스트=P(clientId+1) — NGO clientId가 접속 순서라 모든 클라에서 동일.
/// </summary>
public static class LegPalette
{
    public static readonly Color[] BySlot =
    {
        new Color(0.20f, 0.85f, 0.30f),   // 앞왼 FL 초록 (WASD)
        new Color(1f,    0.20f, 0.20f),   // 앞오 FR 빨강 (TFGH)
        new Color(1f,    0.80f, 0.15f),   // 뒤왼 BL 노랑 (IJKL)
        new Color(0.25f, 0.55f, 1f),      // 뒤오 BR 파랑 (방향키)
    };

    /// <summary>NetLegLink.OwnerOf 값(-2=빈, -1=호스트, 그 외 clientId) → 닉네임.</summary>
    public static string NickOf(long owner)
    {
        if (owner == -1) return "P1";
        if (owner >= 0) return "P" + (owner + 1);
        return "";
    }
}
