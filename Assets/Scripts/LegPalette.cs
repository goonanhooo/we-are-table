using UnityEngine;

/// <summary>
/// ★2026-07-14 다리 색/닉네임 공용 팔레트 — 로비 슬롯 순서(0=뒤왼BL, 1=뒤오BR, 2=앞왼FL, 3=앞오FR).
/// 색은 조작법 이미지(FL초록/FR빨강/BL노랑/BR파랑)·LegColorXray와 일치.
/// 닉네임은 자동: 호스트=P1, 게스트=P(clientId+1) — NGO clientId가 접속 순서라 모든 클라에서 동일.
/// </summary>
public static class LegPalette
{
    public static readonly Color[] BySlot =
    {
        new Color(1f, .80f, .15f),      // BL / WASD / yellow
        new Color(.25f, .55f, 1f),      // BR / TFGH / blue
        new Color(.20f, .85f, .30f),    // FL / IJKL / green
        new Color(1f, .20f, .20f),      // FR / arrows / red
    };

    /// <summary>NetLegLink.OwnerOf 값(-2=빈, -1=호스트, 그 외 clientId) → 닉네임.</summary>
    public static string NickOf(long owner)
    {
        if (owner == -1) return "P1";
        if (owner >= 0) return "P" + (owner + 1);
        return "";
    }
}
