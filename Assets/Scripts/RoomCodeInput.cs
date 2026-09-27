using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Five-character Steam codes, with compatibility for Relay and old Steam IDs.</summary>
public static class RoomCodeInput
{
    const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    const string Symbols = Letters + "23456789";
    const int InputLimit = 20;

    public static string GenerateSteamCode()
    {
        var chars = new char[5];
        var bytes = new byte[1];
        using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
            for (int i = 0; i < chars.Length; i++)
            {
                string alphabet = i == 0 ? Letters : Symbols;
                int limit = 256 - 256 % alphabet.Length;
                do { random.GetBytes(bytes); } while (bytes[0] >= limit);
                chars[i] = alphabet[bytes[0] % alphabet.Length];
            }
        return new string(chars);
    }

    static bool AlphaNumeric(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        foreach (char c in value)
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z'))) return false;
        return true;
    }
    public static bool IsSteamCode(string value)
    {
        if (value == null || value.Length != 5 || !AlphaNumeric(value)) return false;
        foreach (char c in value) if (c >= 'A' && c <= 'Z') return true;
        return false;
    }
    public static bool IsLegacySteamCode(string value)
    {
        if (value == null || value.Length < 15 || value.Length > 20) return false;
        foreach (char c in value) if (c < '0' || c > '9') return false;
        return ulong.TryParse(value, out _);
    }
    public static bool IsValid(string value) => IsSteamCode(value) || IsLegacySteamCode(value) ||
        (value != null && value.Length == 6 && AlphaNumeric(value));

    public static string Normalize(string value)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in value ?? "")
        {
            if (char.IsWhiteSpace(c) || c == '-') continue;
            result.Append(char.ToUpperInvariant(c));
            // Preserve invalid/overlong input so it cannot become a different valid code by truncation.
            if (result.Length >= 64) break;
        }
        return result.ToString();
    }
    public static string Read(string current, Keyboard kb)
    {
        current = current ?? "";
        bool modifier = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
        if (modifier)
        {
            if (kb.vKey.wasPressedThisFrame) return Normalize(GUIUtility.systemCopyBuffer);
            return current;
        }
        for (Key k = Key.A; k <= Key.Z; k++)
            if (kb[k].wasPressedThisFrame && current.Length < InputLimit) current += k.ToString();
        for (Key k = Key.Digit1; k <= Key.Digit0; k++)
            if (kb[k].wasPressedThisFrame && current.Length < InputLimit) current += k == Key.Digit0 ? "0" : ((int)k - (int)Key.Digit1 + 1).ToString();
        for (Key k = Key.Numpad0; k <= Key.Numpad9; k++)
            if (kb[k].wasPressedThisFrame && current.Length < InputLimit) current += ((int)k - (int)Key.Numpad0).ToString();
        if (kb.backspaceKey.wasPressedThisFrame && current.Length > 0) current = current.Substring(0, current.Length - 1);
        return current;
    }
}
