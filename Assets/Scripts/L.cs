using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One saved language for menus, captions, thought bubbles and control guides.</summary>
public static class L
{
    public enum Language { Korean, English, Japanese, SimplifiedChinese }
    public const string PreferenceKey = "language.code";
    static int cached = -1;
    public static int Version { get; private set; }
    public static event Action Changed;
    [Serializable] public class Entry { public string ko, en, ja, zh; }
    [Serializable] class Catalog { public Entry[] entries; }
    static Dictionary<string, Entry> byEnglish, byText;
    static readonly HashSet<string> missing = new HashSet<string>();
    public static IReadOnlyCollection<string> Missing => missing;
    public static Language Current
    {
        get
        {
            if (cached < 0)
            {
                string code = PlayerPrefs.GetString(PreferenceKey, "");
                cached = code == "en" ? 1 : code == "ja" ? 2 : code == "zh-Hans" ? 3 : code == "ko" ? 0 :
                    PlayerPrefs.HasKey("lang.korean") ? (PlayerPrefs.GetInt("lang.korean", 1) == 1 ? 0 : 1) :
                    Application.systemLanguage == SystemLanguage.Japanese ? 2 :
                    Application.systemLanguage == SystemLanguage.Chinese || Application.systemLanguage == SystemLanguage.ChineseSimplified || Application.systemLanguage == SystemLanguage.ChineseTraditional ? 3 :
                    Application.systemLanguage == SystemLanguage.Korean ? 0 : 1;
            }
            return (Language)cached;
        }
        set
        {
            int next = Mathf.Clamp((int)value, 0, 3);
            bool changed = (int)Current != next;
            cached = next; if(changed) Version++;
            PlayerPrefs.SetString(PreferenceKey, Code);
            PlayerPrefs.SetInt("lang.korean", Korean ? 1 : 0);
            PlayerPrefs.Save(); if(changed){LocaleFonts.RefreshStyles(); Changed?.Invoke();}
        }
    }
    public static void UseSteamDefault(string language)
    {
        if(PlayerPrefs.HasKey(PreferenceKey)||PlayerPrefs.HasKey("lang.korean"))return;
        Current=language=="koreana"?Language.Korean:language=="japanese"?Language.Japanese:
            language=="schinese"||language=="tchinese"?Language.SimplifiedChinese:Language.English;
    }
    public static bool Korean { get => Current == Language.Korean; set => Current = value ? Language.Korean : Language.English; }
    public static string Code => new[] { "ko", "en", "ja", "zh-Hans" }[(int)Current];
    public static string NativeName => new[] { "한국어", "English", "日本語", "简体中文" }[(int)Current];
    public static string LanguageButton => "文 / A   " + NativeName;
    public static void Cycle(int direction = 1) => Current = (Language)((((int)Current + direction) % 4 + 4) % 4);
    static void Load()
    {
        if (byEnglish != null) return;
        byEnglish = new Dictionary<string, Entry>(); byText = new Dictionary<string, Entry>();
        var asset = Resources.Load<TextAsset>("Localization/Strings");
        if (asset == null) { Debug.LogError("Missing Localization/Strings"); return; }
        var data = JsonUtility.FromJson<Catalog>(asset.text);
        foreach (var e in data.entries)
        {
            byEnglish[e.en] = e;
            foreach (var text in new[] { e.ko, e.en, e.ja, e.zh })
                if (!string.IsNullOrEmpty(text)) byText[text] = e;
        }
    }
    static string Pick(Entry e) => Current == Language.Korean ? e.ko : Current == Language.Japanese ? e.ja : Current == Language.SimplifiedChinese ? e.zh : e.en;
    public static string T(string ko, string en)
    {
        if (Korean) return ko;
        if (Current == Language.English) return en;
        Load();
        if (byEnglish.TryGetValue(en, out var e)) return Pick(e);
        if (missing.Add(en)) Debug.LogWarning("[Localization] Missing translation: " + en);
        return en;
    }
    public static string T(string ko, string en, string ja, string zh) => Current == Language.Korean ? ko : Current == Language.Japanese ? ja : Current == Language.SimplifiedChinese ? zh : en;
    public static string F(string ko, string en, params object[] args) => string.Format(T(ko, en), args);
    /// <summary>제목처럼 쓰는 영어 낱말(다리 이름 등)은 첫 글자를 대문자로 — 다른 언어는 그대로.</summary>
    public static string Title(string s) => Current == Language.English && !string.IsNullOrEmpty(s) ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
    public static string Text(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        Load(); if(byText.TryGetValue(text, out var e))return Pick(e);
        // Connection states combine a translated prefix with a room code or error detail.
        foreach(var entry in byEnglish.Values)
        {
            if(!entry.en.EndsWith(": ",StringComparison.Ordinal))continue;
            foreach(var prefix in new[]{entry.ko,entry.en,entry.ja,entry.zh})
                if(!string.IsNullOrEmpty(prefix)&&text.StartsWith(prefix,StringComparison.Ordinal))
                    return Pick(entry)+Text(text.Substring(prefix.Length));
        }
        return text;
    }
}
