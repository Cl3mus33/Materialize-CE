using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Interface languages (Materialize CE). English is the source text; every other language is a plain text file in
/// StreamingAssets/Lang (fr.txt…), one "English text ==> translation" pair per line, so anyone can add or correct
/// a language without touching the code. A missing translation shows the English text.
/// </summary>
public static class L
{
    const string Separator = "==>";
    static Dictionary<string, string> table;
    static string lang = "en";

    /// <summary>Language code: "en", "fr"…</summary>
    public static string Language
    {
        get => lang;
        set
        {
            lang = string.IsNullOrEmpty(value) ? "en" : value;
            table = null;
        }
    }

    /// <summary>The languages found in StreamingAssets/Lang, English first: (code, name in that language).</summary>
    public static List<(string Code, string Name)> Available()
    {
        var list = new List<(string, string)> { ("en", "English") };
        try
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "Lang");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.txt"))
                {
                    string code = Path.GetFileNameWithoutExtension(f);
                    string name = code;
                    // First line: "#name ==> Français".
                    using (var r = new StreamReader(f))
                    {
                        var first = r.ReadLine();
                        int sep = first == null ? -1 : first.IndexOf(Separator, StringComparison.Ordinal);
                        if (first != null && first.StartsWith("#name") && sep > 0) name = first.Substring(sep + Separator.Length).Trim();
                    }
                    list.Add((code, name));
                }
        }
        catch (Exception e) { Debug.LogWarning("Languages: " + e.Message); }
        return list;
    }

    static void Load()
    {
        table = new Dictionary<string, string>();
        if (lang == "en") return;
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Lang", lang + ".txt");
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                int sep = line.IndexOf(Separator, StringComparison.Ordinal);
                if (sep <= 0) continue;
                string en = Unescape(line.Substring(0, sep).Trim()), tr = Unescape(line.Substring(sep + Separator.Length).Trim());
                if (tr.Length > 0) table[en] = tr;
            }
        }
        catch (Exception e) { Debug.LogWarning("Language " + lang + ": " + e.Message); }
    }

    static string Unescape(string s) => s.Replace("\\n", "\n");

    /// <summary>The text in the chosen language (the English text when there is no translation).</summary>
    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english) || lang == "en") return english;
        if (table == null) Load();
        if (table.TryGetValue(english, out var t)) return t;
        // Spaces around the text (toggles, "Needs "…) and a final colon are kept as they are.
        string core = english.Trim();
        string end = "";
        if (core.Length > 0 && table.TryGetValue(core, out t))
        {
            int lead0 = english.Length - english.TrimStart().Length;
            int trail0 = english.Length - english.TrimEnd().Length;
            return english.Substring(0, lead0) + t + english.Substring(english.Length - trail0);
        }
        if (core.EndsWith(":")) { core = core.Substring(0, core.Length - 1).TrimEnd(); end = ":"; }
        if (core.Length > 0 && table.TryGetValue(core, out t))
        {
            int lead = english.Length - english.TrimStart().Length;
            int trail = english.Length - english.TrimEnd().Length;
            return english.Substring(0, lead) + t + end + english.Substring(english.Length - trail);
        }
        return english;
    }

    /// <summary>A GUIContent with its text and tooltip translated.</summary>
    public static GUIContent G(string text) => new GUIContent(T(text));
    public static GUIContent G(string text, string tip) => new GUIContent(T(text), T(tip));
    public static GUIContent G(string text, Texture image) => new GUIContent(T(text), image);
    public static GUIContent G(Texture image) => new GUIContent(image);
    public static GUIContent G(Texture image, string tip) => new GUIContent(image, T(tip));
}
