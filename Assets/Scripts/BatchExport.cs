using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Batch export (Materialize CE): a folder of texture sets (stone_albedo.png, stone_normal.png, stone_roughness.png…)
/// is exported set by set with the current export profile, to the current export folder. Each set is loaded like
/// by hand (it replaces the maps in the window), exported under its own name, then the next one.
/// </summary>
public static class BatchExport
{
    public sealed class Set
    {
        public string Name;
        public readonly Dictionary<MapType, string> Files = new Dictionary<MapType, string>();
        public bool RoughnessFile;   // the smoothness slot holds a roughness file: inverted on load
    }

    static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".bmp", ".exr", ".dds" };

    // File-name endings, the longest match wins. Roughness is listed apart: it is the inverse of smoothness.
    static readonly (string Suffix, MapType Type, bool Rough)[] Suffixes =
    {
        ("_basecolor", MapType.diffuseOriginal, false), ("_base_color", MapType.diffuseOriginal, false), ("_albedo", MapType.diffuseOriginal, false),
        ("_diffuse", MapType.diffuseOriginal, false), ("_color", MapType.diffuseOriginal, false), ("_diff", MapType.diffuseOriginal, false),
        ("_col", MapType.diffuseOriginal, false), ("_d", MapType.diffuseOriginal, false),
        ("_normal", MapType.normal, false), ("_normalgl", MapType.normal, false), ("_normaldx", MapType.normal, false),
        ("_nrm", MapType.normal, false), ("_nor", MapType.normal, false), ("_n", MapType.normal, false),
        ("_height", MapType.height, false), ("_displacement", MapType.height, false), ("_disp", MapType.height, false), ("_h", MapType.height, false), ("_p", MapType.height, false),
        ("_metallic", MapType.metallic, false), ("_metalness", MapType.metallic, false), ("_metal", MapType.metallic, false),
        ("_roughness", MapType.smoothness, true), ("_rough", MapType.smoothness, true),
        ("_glossiness", MapType.smoothness, false), ("_gloss", MapType.smoothness, false), ("_smoothness", MapType.smoothness, false),
        ("_ambientocclusion", MapType.ao, false), ("_occlusion", MapType.ao, false), ("_ao", MapType.ao, false),
        ("_curvature", MapType.edge, false),
        ("_emissive", MapType.emission, false), ("_emission", MapType.emission, false), ("_emit", MapType.emission, false), ("_glow", MapType.emission, false), ("_g", MapType.emission, false),
        ("_subsurface", MapType.subsurface, false), ("_sss", MapType.subsurface, false),
    };

    /// <summary>The texture sets of a folder, by file name. Thread-safe; never throws.</summary>
    public static List<Set> Scan(string folder)
    {
        var sets = new Dictionary<string, Set>(StringComparer.OrdinalIgnoreCase);
        string[] files;
        try { files = Directory.GetFiles(folder); } catch { return new List<Set>(); }
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (!Extensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
            string name = Path.GetFileNameWithoutExtension(file);
            var match = Suffixes.Where(s => name.EndsWith(s.Suffix, StringComparison.OrdinalIgnoreCase) && name.Length > s.Suffix.Length)
                                .OrderByDescending(s => s.Suffix.Length).FirstOrDefault();
            if (match.Suffix == null) continue;
            string baseName = name.Substring(0, name.Length - match.Suffix.Length).TrimEnd('_', '-', ' ', '.');
            if (baseName.Length == 0) continue;
            if (!sets.TryGetValue(baseName, out var set)) sets[baseName] = set = new Set { Name = baseName };
            if (set.Files.ContainsKey(match.Type)) continue;   // the first file of a kind wins (sorted by name)
            set.Files[match.Type] = file;
            if (match.Type == MapType.smoothness) set.RoughnessFile = match.Rough;
        }
        return sets.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ---------- Window ----------

    static bool open, running, cancel;
    static string sourceFolder = "";
    static List<Set> found = new List<Set>();
    static Rect windowRect = new Rect(0, 0, 520, 300);
    static Vector2 scroll;
    static int done;
    static string current = "";

    public static bool Running => running;

    public static void Pick(MainGui gui) => gui.StartCoroutine(PickRoutine());

    static IEnumerator PickRoutine()
    {
        yield return null;   // out of OnGUI before the native dialog
        string folder = null;
        try { folder = Workspace.PickFolder("Batch export: the folder that holds the texture sets"); } catch (Exception e) { Notifications.Error(e.Message); }
        if (string.IsNullOrEmpty(folder)) yield break;
        sourceFolder = folder;
        found = Scan(folder);
        if (found.Count == 0) { Notifications.Error("No texture set found in " + folder + " (files named like stone_albedo.png, stone_normal.png…)."); yield break; }
        windowRect.x = (Screen.width - windowRect.width) / 2;
        windowRect.y = (Screen.height - windowRect.height) / 2;
        open = true;
    }

    public static void Draw(MainGui gui)
    {
        if (!open) return;
        windowRect = GUI.Window(86, windowRect, id => DoWindow(gui), L.T("Batch export"));
        GUI.BringWindowToFront(86);
        Tips.Block(windowRect);
    }

    static void DoWindow(MainGui gui)
    {
        float x = 12, y = 26, w = windowRect.width - 24;
        var profile = ExportWindow.Profiles[ExportWindow.Selected];
        GUI.Label(new Rect(x, y, w, 20), string.Format(L.T("{0} texture sets in {1}"), found.Count, sourceFolder));
        y += 22;
        GUI.Label(new Rect(x, y, w, 20), L.T("Profile: ") + profile.Name + "    →    " + ExportWindow.Folder);
        y += 26;

        float listHeight = Mathf.Min(found.Count * 20, 130);
        scroll = GUI.BeginScrollView(new Rect(x, y, w, listHeight), scroll, new Rect(0, 0, w - 20, found.Count * 20));
        for (int i = 0; i < found.Count; i++)
        {
            string maps = string.Join(", ", found[i].Files.Keys.Select(t => t == MapType.diffuseOriginal ? "albedo" : t == MapType.smoothness ? (found[i].RoughnessFile ? "roughness" : "gloss") : t == MapType.edge ? "curvature" : t.ToString()));
            GUI.Label(new Rect(0, i * 20, w - 20, 20), (running && i < done ? "✓ " : running && i == done ? "▸ " : "") + found[i].Name + "  (" + maps + ")");
        }
        GUI.EndScrollView();
        y += listHeight + 8;

        if (running)
        {
            float p = (done + Mathf.Clamp01(ExportWindow.Progress)) / Mathf.Max(1, found.Count);
            GUI.DrawTexture(new Rect(x, y, w, 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(1, 1, 1, 0.08f), 0, 3);
            GUI.DrawTexture(new Rect(x, y, Mathf.Max(6, w * p), 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, Theme.Accent, 0, 3);
            y += 12;
            GUI.Label(new Rect(x, y, w - 110, 20), current);
            if (GUI.Button(new Rect(x + w - 100, y, 100, 24), L.T("Stop"))) cancel = true;
        }
        else
        {
            GUI.Label(new Rect(x, y, w, 34), L.T("Each set replaces the maps in the window, then is exported under its own name. Save your project first."));
            y += 38;
            bool ready = Directory.Exists(ExportWindow.Folder);
            if (!ready) GUI.Label(new Rect(x, y, w - 220, 24), L.T("Choose the export folder first."));
            GUI.enabled = ready;
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            if (GUI.Button(new Rect(x + w - 210, y, 100, 26), L.T("Start"))) gui.StartCoroutine(Run(gui));
            GUI.backgroundColor = old;
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + w - 100, y, 100, 26), L.T("Close"))) open = false;
        }
        windowRect.height = y + 36;
        GUI.DragWindow();
    }

    static IEnumerator Run(MainGui gui)
    {
        var loader = UnityEngine.Object.FindFirstObjectByType<SaveLoadProject>();
        if (loader == null) { Notifications.Error("Batch export: the loader is missing."); yield break; }
        running = true; cancel = false; done = 0;
        int ok = 0;
        foreach (var set in found)
        {
            if (cancel) break;
            current = set.Name;
            MapAdjust.End(gui);
            gui.CloseWindows();
            gui.ClearAllTextures();
            foreach (var pair in set.Files)
            {
                if (pair.Key == MapType.smoothness) Workflow.InvertNextSmoothnessLoad = set.RoughnessFile;
                yield return gui.StartCoroutine(loader.LoadTexture(pair.Key, pair.Value));
            }
            gui.SetMaterialValues();
            yield return null;
            yield return gui.StartCoroutine(ExportWindow.RunAs(gui, set.Name));
            ok++;
            done++;
        }
        running = false;
        current = "";
        Notifications.Info(string.Format(L.T(cancel ? "Batch export stopped: {0} of {1} sets." : "Batch export done: {0} of {1} sets."), ok, found.Count));
    }
}
