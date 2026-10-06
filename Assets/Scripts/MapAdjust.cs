using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adjustments for any map (Materialize CE), in the spirit of Texalys: levels, contrast, brightness, invert, and
/// for the height its displacement strength and a stretch over the full range. They unfold under the map in the
/// left column. The result shows live on the full material (or on the map alone); Apply writes it into the map
/// (and into the high-precision height when there is one). Adjustments always start from the map as it was
/// before the first one, so going back and forth never degrades it.
/// </summary>
public static class MapAdjust
{
    sealed class Settings
    {
        public float Black = 0, White = 1, OutBlack = 0, OutWhite = 1, Gamma = 1, Contrast = 1, Brightness = 0, Saturation = 1, Strength = 1, Blur = 0;
        public bool Invert, FlipGreen;
        public string BlurT, BlackT, WhiteT, OutBlackT, OutWhiteT, GammaT, ContrastT, BrightnessT, SaturationT, StrengthT;
        public bool Pending;   // changed since the last Apply
        public Settings Copy() => (Settings)MemberwiseClone();
    }

    static readonly Dictionary<MapType, Texture2D> originals = new Dictionary<MapType, Texture2D>();
    static readonly Dictionary<MapType, RenderTexture> originalHD = new Dictionary<MapType, RenderTexture>();
    static readonly Dictionary<MapType, Settings> settings = new Dictionary<MapType, Settings>();
    static readonly Dictionary<MapType, Texture2D> lastResult = new Dictionary<MapType, Texture2D>();

    static bool active;
    static MapType type;
    static string title;
    static Material material;
    static RenderTexture preview;
    static bool dirty, applying;
    public static bool Alone;   // kept for older code paths; the reveal slider replaces it
    static float reveal = 1f;   // starts on the map: Adjust edits it, the slider brings the render in
    static GUIStyle sideStyle;

    public static bool IsActive(MapType t) => active && type == t;
    public static bool HasPending(MapType t) => settings.TryGetValue(t, out var s) && s.Pending;

    /// <summary>Starts adjusting a map (its section was unfolded).</summary>
    public static void Begin(MainGui gui, MapType mapType, string name)
    {
        var current = Current(gui, mapType);
        if (current == null) { active = false; return; }
        type = mapType;
        title = name;
        // The first adjustment keeps a copy of the map; later ones start from it again. A map replaced since
        // (new file, "Set as…", tiling) starts a fresh original.
        lastResult.TryGetValue(type, out var applied);
        if (!originals.TryGetValue(type, out var original) || original == null || (current != applied && current != original))
        {
            originals[type] = Object.Instantiate(current);
            originals[type].wrapMode = TextureWrapMode.Repeat;   // the smoothing wraps around the borders
            settings[type] = new Settings();
            if (originalHD.TryGetValue(type, out var oldHd) && oldHd != null) oldHd.Release();
            originalHD.Remove(type);
            if (type == MapType.height && gui._HDHeightMap != null)
            {
                var copy = new RenderTexture(gui._HDHeightMap.width, gui._HDHeightMap.height, 0, gui._HDHeightMap.format, RenderTextureReadWrite.Linear) { wrapMode = TextureWrapMode.Repeat };
                Graphics.Blit(gui._HDHeightMap, copy);
                originalHD[type] = copy;
            }
        }
        if (material == null) material = new Material(Shader.Find("Hidden/Blit_Adjust")) { hideFlags = HideFlags.HideAndDontSave };
        active = true;
        // Rendered once at the start, so the reveal slider has the map to show.
        dirty = true;
    }

    /// <summary>The section was folded: the preview goes back to the maps as they are. Unapplied settings stay.</summary>
    public static void End(MainGui gui)
    {
        if (!active) return;
        active = false;
        Shader.SetGlobalFloat("_AdjustReveal", 0);
        gui.SetMaterialValues();
    }

    // ---------- Undo / redo of applied adjustments ----------

    struct Step { public MapType Type; public string Name; public Texture2D Map; public RenderTexture Hd; }
    const int MaxSteps = 6;   // a 4K map is about 85 MB with its mipmaps
    static readonly List<Step> undo = new List<Step>(), redo = new List<Step>();

    public static bool CanUndo => undo.Count > 0 && !applying;
    public static bool CanRedo => redo.Count > 0 && !applying;

    static void Push(List<Step> stack, Step step)
    {
        stack.Add(step);
        while (stack.Count > MaxSteps) { Release(stack[0]); stack.RemoveAt(0); }
    }

    static void Release(Step s)
    {
        if (s.Map != null) Object.Destroy(s.Map);
        if (s.Hd != null) s.Hd.Release();
    }

    static void Drop(List<Step> stack)
    {
        foreach (var s in stack) Release(s);
        stack.Clear();
    }

    /// <summary>New project or everything cleared: the kept steps belong to the previous maps.</summary>
    public static void ClearHistory() { Drop(undo); Drop(redo); }

    /// <summary>Ctrl+Z / Ctrl+Y: the map as it was before the last Apply, and back.</summary>
    public static void Undo(MainGui gui) => Swap(gui, undo, redo, "Undone: ");
    public static void Redo(MainGui gui) => Swap(gui, redo, undo, "Redone: ");

    static void Swap(MainGui gui, List<Step> from, List<Step> to, string verb)
    {
        if (from.Count == 0 || applying) return;
        var step = from[from.Count - 1];
        from.RemoveAt(from.Count - 1);
        if (step.Map == null) return;   // destroyed meanwhile (new project)
        bool wasActive = active && type == step.Type;
        if (wasActive) End(gui);
        var current = Current(gui, step.Type);
        to.Add(new Step { Type = step.Type, Name = step.Name, Map = current, Hd = step.Type == MapType.height ? gui._HDHeightMap : null });
        Assign(gui, step.Type, step.Map);
        if (step.Type == MapType.height) gui._HDHeightMap = step.Hd;
        // The adjustment starts again from the restored map.
        if (originals.TryGetValue(step.Type, out var kept) && kept != null) Object.Destroy(kept);
        Forget(step.Type);
        lastResult.Remove(step.Type);
        if (wasActive) Begin(gui, step.Type, step.Name);
        gui.SetMaterialValues();
        Notifications.Info(L.T(verb) + step.Name);
    }

    /// <summary>Forget the kept originals when maps are replaced (new file, new project, clear).</summary>
    public static void Forget(MapType mapType)
    {
        originals.Remove(mapType);
        settings.Remove(mapType);
        lastApplied.Remove(mapType);
        if (originalHD.TryGetValue(mapType, out var hd) && hd != null) hd.Release();
        originalHD.Remove(mapType);
    }

    static Texture2D Current(MainGui gui, MapType t)
    {
        switch (t)
        {
            case MapType.height: return gui._HeightMap;
            case MapType.diffuseOriginal: return gui._DiffuseMap != null ? gui._DiffuseMap : gui._DiffuseMapOriginal;
            case MapType.normal: return gui._NormalMap;
            case MapType.metallic: return gui._MetallicMap;
            case MapType.smoothness: return gui._SmoothnessMap;
            case MapType.edge: return gui._EdgeMap;
            case MapType.ao: return gui._AOMap;
            case MapType.emission: return gui._EmissionMap;
            case MapType.subsurface: return gui._SubsurfaceMap;
        }
        return null;
    }

    static string Slot(MapType t)
    {
        switch (t)
        {
            case MapType.height: return "_DisplacementMap";
            case MapType.diffuseOriginal: return "_DiffuseMap";
            case MapType.normal: return "_NormalMap";
            case MapType.metallic: return "_MetallicMap";
            case MapType.smoothness: return "_SmoothnessMap";
            case MapType.edge: return "_EdgeMap";
            case MapType.emission: return "_MceEmissionMap";   // globals, not material slots
            case MapType.subsurface: return "_MceSubsurfaceMap";
            default: return "_AOMap";
        }
    }

    static void Assign(MainGui gui, MapType t, Texture2D tex)
    {
        switch (t)
        {
            case MapType.height: gui._HeightMap = tex; break;
            case MapType.diffuseOriginal: if (gui._DiffuseMap != null) gui._DiffuseMap = tex; else gui._DiffuseMapOriginal = tex; break;
            case MapType.normal: gui._NormalMap = tex; break;
            case MapType.metallic: gui._MetallicMap = tex; break;
            case MapType.smoothness: gui._SmoothnessMap = tex; break;
            case MapType.edge: gui._EdgeMap = tex; break;
            case MapType.ao: gui._AOMap = tex; break;
            case MapType.emission: gui._EmissionMap = tex; break;
            case MapType.subsurface: gui._SubsurfaceMap = tex; break;
        }
    }

    static float Mode => type == MapType.normal ? 1 : type == MapType.diffuseOriginal ? 2 : 0;

    static void SetMaterial(Settings s)
    {
        material.SetFloat("_Mode", Mode);
        material.SetFloat("_InBlack", s.Black);
        material.SetFloat("_InWhite", s.White);
        material.SetFloat("_Gamma", s.Gamma);
        material.SetFloat("_OutBlack", s.OutBlack);
        material.SetFloat("_OutWhite", s.OutWhite);
        material.SetFloat("_Contrast", s.Contrast);
        material.SetFloat("_Brightness", s.Brightness);
        material.SetFloat("_Invert", s.Invert ? 1 : 0);
        material.SetFloat("_Saturation", s.Saturation);
        material.SetFloat("_Strength", s.Strength);
        material.SetFloat("_FlipGreen", s.FlipGreen ? 1 : 0);
        material.SetFloat("_HeightMode", type == MapType.height ? 1 : 0);
        material.SetFloat("_PreInvert", type == MapType.smoothness && !Workflow.Gloss ? 1 : 0);
    }

    static void Render()
    {
        var original = originals[type];
        if (preview == null || preview.width != original.width || preview.height != original.height)
        {
            if (preview != null) preview.Release();
            preview = new RenderTexture(original.width, original.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { wrapMode = TextureWrapMode.Repeat, useMipMap = true, autoGenerateMips = true, filterMode = FilterMode.Trilinear, anisoLevel = 9 };
        }
        Process(original, preview, settings[type]);
    }

    /// <summary>Smoothing (two gaussian passes, in half floats) then the adjustments, from <paramref name="src"/> into <paramref name="dst"/>.</summary>
    static void Process(Texture src, RenderTexture dst, Settings s)
    {
        var was = RenderTexture.active;
        Texture source = src;
        RenderTexture a = null, b = null;
        if (s.Blur > 0.01f)
        {
            a = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            b = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            a.wrapMode = b.wrapMode = TextureWrapMode.Repeat;   // tiling maps stay seamless
            material.SetFloat("_BlurSigma", s.Blur);
            material.SetVector("_BlurDir", new Vector4(1, 0, 0, 0));
            Graphics.Blit(src, a, material, 1);
            material.SetVector("_BlurDir", new Vector4(0, 1, 0, 0));
            Graphics.Blit(a, b, material, 1);
            source = b;
        }
        SetMaterial(s);
        Graphics.Blit(source, dst, material, 0);
        if (a != null) RenderTexture.ReleaseTemporary(a);
        if (b != null) RenderTexture.ReleaseTemporary(b);
        RenderTexture.active = was;
    }

    static void ShowPreview(MainGui gui)
    {
        Shader.SetGlobalTexture("_AdjustRevealMap", preview);
        Shader.SetGlobalFloat("_AdjustRevealInvert", PreInverted ? 1 : 0);
        Shader.SetGlobalFloat("_AdjustReveal", reveal);
        if (Alone) { gui.SetPreviewMaterial(preview); return; }
        gui.SetMaterialValues();
        if (Slot(type).StartsWith("_Mce")) Shader.SetGlobalTexture(Slot(type), preview);
        else gui.FullMaterial.SetTexture(Slot(type), preview);
    }

    /// <summary>Called from MainGui.Update: rendering from OnGUI would draw the interface into the map.</summary>
    public static void Tick(MainGui gui)
    {
        if (!active || !dirty || applying || !originals.ContainsKey(type)) return;
        dirty = false;
        Render();
        ShowPreview(gui);
    }

    static void Slider(ref float y, float w, string name, ref float value, ref string text, float min, float max)
    {
        float before = value;
        GuiHelper.Slider(new Rect(0, y, w, 40), name, value, text, out value, out text, min, max);
        if (value != before) Changed();
        y += 40;
    }

    static void Changed()
    {
        dirty = true;
        settings[type].Pending = true;
    }

    /// <summary>Draws the adjustments at (0, y) in the current group, <paramref name="w"/> wide. Returns the new y.</summary>
    public static float DrawInline(MainGui gui, float y, float w)
    {
        if (!active || !settings.ContainsKey(type)) return y;
        UiHelp.Panel = "Adjust";
        var s = settings[type];
        // Smoothing first: it applies before the other adjustments, on every kind of map.
        Slider(ref y, w, "Smooth (blur)", ref s.Blur, ref s.BlurT, 0f, 4f);
        if (type == MapType.normal)
        {
            Slider(ref y, w, "Normal Strength", ref s.Strength, ref s.StrengthT, 0f, 4f);
            bool flip = GUI.Toggle(new Rect(0, y, w, 22), s.FlipGreen, UiHelp.Content(" Flip green (OpenGL ↔ DirectX)"));
            if (flip != s.FlipGreen) { s.FlipGreen = flip; Changed(); }
            y += 28;
        }
        else
        {
            if (type == MapType.height)
            {
                Slider(ref y, w, "Displacement Strength", ref s.Strength, ref s.StrengthT, 0f, 4f);
                if (GUI.Button(new Rect(0, y, w, 22), UiHelp.Content("Stretch to the full range"))) { StretchLevels(s); Changed(); }
                y += 30;
            }
            DrawLevelsGraph(ref y, w, s);
            Slider(ref y, w, "Black Point", ref s.Black, ref s.BlackT, 0f, 1f);
            Slider(ref y, w, "White Point", ref s.White, ref s.WhiteT, 0f, 1f);
            Slider(ref y, w, "Midtones", ref s.Gamma, ref s.GammaT, 0.2f, 5f);
            Slider(ref y, w, "Output Black", ref s.OutBlack, ref s.OutBlackT, 0f, 1f);
            Slider(ref y, w, "Output White", ref s.OutWhite, ref s.OutWhiteT, 0f, 1f);
            Slider(ref y, w, "Contrast", ref s.Contrast, ref s.ContrastT, 0f, 3f);
            Slider(ref y, w, "Brightness", ref s.Brightness, ref s.BrightnessT, -1f, 1f);
            if (type == MapType.diffuseOriginal) Slider(ref y, w, "Saturation", ref s.Saturation, ref s.SaturationT, 0f, 2f);
            bool inv = GUI.Toggle(new Rect(0, y, w, 22), s.Invert, UiHelp.Content(" Invert"));
            if (inv != s.Invert) { s.Invert = inv; Changed(); }
            y += 26;
        }
        // Reveal: the adjusted map alone on the left of the preview, the lit material on the right.
        GUI.Label(new Rect(0, y, w, 20), UiHelp.Content("Reveal"));
        // One row: "Map", the slider, "Render", centred on the same line.
        float row = y + 24;
        if (sideStyle == null)
        {
            sideStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, padding = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow };
            sideStyle.normal.textColor = new Color(0.67f, 0.69f, 0.75f);
        }
        float mapW = sideStyle.CalcSize(new GUIContent(L.T("Map"))).x + 2, renderW = sideStyle.CalcSize(new GUIContent(L.T("Render"))).x + 2;
        GUI.Label(new Rect(0, row, mapW, 16), L.T("Map"), sideStyle);
        GUI.Label(new Rect(w - renderW, row, renderW, 16), L.T("Render"), sideStyle);
        float r = GUI.HorizontalSlider(new Rect(mapW + 10, row + 2, w - mapW - renderW - 20, 12), 1f - reveal, 0f, 1f);
        if (!Mathf.Approximately(1f - r, reveal))
        {
            reveal = 1f - r;
            Shader.SetGlobalFloat("_AdjustReveal", reveal);
            if (preview == null) dirty = true;
        }
        y += 50;

        float bw = (w - 8) / 3f;
        if (GUI.Button(new Rect(0, y, bw, 24), UiHelp.Content("Reset"))) { settings[type] = new Settings { Pending = true }; dirty = true; }
        GUI.enabled = !applying && s.Pending;
        if (GUI.Button(new Rect(bw + 4, y, bw, 24), L.G("Revert", "Back to the settings of the last Apply."))) Revert(gui);
        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
        if (GUI.Button(new Rect(2 * (bw + 4), y, bw, 24), UiHelp.Content("Apply"))) gui.StartCoroutine(Apply(gui));
        GUI.backgroundColor = old;
        y += 30;
        float hw = (w - 4) / 2f;
        GUI.enabled = CanUndo;
        if (GUI.Button(new Rect(0, y, hw, 22), L.G("↶ Undo", "The map as it was before the last Apply (Ctrl+Z). The last 6 are kept."))) Undo(gui);
        GUI.enabled = CanRedo;
        if (GUI.Button(new Rect(hw + 4, y, hw, 22), L.G("↷ Redo", "Applies again what was undone (Ctrl+Y)."))) Redo(gui);
        GUI.enabled = true;
        return y + 28;
    }

    static void Revert(MainGui gui)
    {
        // Nothing applied yet: back to the original settings; otherwise the map already holds the last Apply.
        settings[type] = lastApplied.TryGetValue(type, out var a) ? a.Copy() : new Settings();
        settings[type].Pending = false;
        dirty = false;
        if (Alone) { var c = Current(gui, type); if (c != null) gui.SetPreviewMaterial(c); }
        else gui.SetMaterialValues();
    }

    static readonly Dictionary<MapType, Settings> lastApplied = new Dictionary<MapType, Settings>();

    // ---------- Levels graph (as in Texalys) ----------

    static int[] sourceCounts;
    static Texture2D sourceFor;
    static bool sourceInverted;
    static int sourceVersion;
    static Texture2D graphTexture, handleBlack, handleGrey, handleWhite;
    static string graphKey;
    static int dragHandle = -1;   // 0 black, 1 midtones, 2 white
    static GUIStyle statsStyle, markStyle;
    const int GraphHeight = 110, HandleBand = 12;

    static bool PreInverted => type == MapType.smoothness && !Workflow.Gloss;

    /// <summary>The value a grey pixel gets, exactly as the shader computes it (without the smoothing).</summary>
    static float Result(float v, Settings s)
    {
        float g = PreInverted ? 1f - v : v;
        float c = Mathf.Clamp01((g - s.Black) / Mathf.Max(s.White - s.Black, 1e-4f));
        c = Mathf.Pow(c, 1f / Mathf.Max(s.Gamma, 0.01f));
        c = Mathf.Lerp(s.OutBlack, s.OutWhite, c);
        c = Mathf.Clamp01((c - 0.5f) * s.Contrast + 0.5f + s.Brightness);
        if (s.Invert) c = 1f - c;
        if (PreInverted) c = 1f - c;
        if (type == MapType.height) c = Mathf.Clamp01(1f - (1f - c) * s.Strength);
        return c;
    }

    static void EnsureSourceHistogram()
    {
        var o = originals[type];
        if (sourceFor == o && sourceInverted == PreInverted && sourceCounts != null) return;
        sourceCounts = new int[256];
        var px = o.GetPixels32();
        int step = Mathf.Max(1, px.Length / 1000000);
        for (int i = 0; i < px.Length; i += step) sourceCounts[PreInverted ? 255 - px[i].r : px[i].r]++;
        sourceFor = o;
        sourceInverted = PreInverted;
        sourceVersion++;
        graphKey = null;
    }

    static int[] ResultCounts(Settings s)
    {
        var counts = new int[256];
        for (int i = 0; i < 256; i++)
        {
            if (sourceCounts[i] == 0) continue;
            // The histogram is shown in the terms of the row (roughness when it is), so is the result.
            float v = PreInverted ? 1f - i / 255f : i / 255f;
            float r = Result(v, s);
            if (PreInverted) r = 1f - r;
            counts[Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255)] += sourceCounts[i];
        }
        return counts;
    }

    static float[] Normalised(int[] counts)
    {
        // Square root keeps small populations visible next to a dominant peak.
        float max = 0;
        foreach (var c in counts) max = Mathf.Max(max, Mathf.Sqrt(c));
        var n = new float[counts.Length];
        for (int i = 0; i < n.Length; i++) n[i] = max > 0 ? Mathf.Sqrt(counts[i]) / max : 0;
        return n;
    }

    static string Stats(int[] counts)
    {
        long total = 0, sum = 0;
        int lo = -1, hi = 0;
        for (int i = 0; i < 256; i++) { if (counts[i] == 0) continue; total += counts[i]; sum += (long)counts[i] * i; if (lo < 0) lo = i; hi = i; }
        if (total == 0) return "-";
        long half = total / 2, acc = 0;
        int median = 0;
        for (int i = 0; i < 256; i++) { acc += counts[i]; if (acc > half) { median = i; break; } }
        return string.Format(L.T("median {0} · mean {1} · range {2}–{3}"), median, Mathf.RoundToInt((float)sum / total), lo, hi);
    }

    static Texture2D Triangle(Color fill)
    {
        const int w = 13, h = HandleBand - 1;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
        var outline = new Color(0.42f, 0.42f, 0.42f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Apex at the top (texture rows go up, so y = h - 1 is the top).
                float row = (h - 1 - y) / (float)(h - 1);
                float half = row * (w - 1) / 2f;
                float d = Mathf.Abs(x - (w - 1) / 2f);
                Color c = d <= half - 1f ? fill : d <= half + 0.5f ? outline : Color.clear;
                if (y == 0 && d <= half + 0.5f) c = outline;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    static void DrawLevelsGraph(ref float y, float w, Settings s)
    {
        EnsureSourceHistogram();
        int gw = Mathf.Max(32, (int)w), gh = GraphHeight;
        var result = ResultCounts(s);
        string key = gw + "|" + string.Join(",", result) + "|" + sourceVersion;
        if (graphTexture == null || graphTexture.width != gw || key != graphKey)
        {
            if (graphTexture == null || graphTexture.width != gw)
                graphTexture = new Texture2D(gw, gh, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            var px = new Color32[gw * gh];
            var bg = new Color32(26, 29, 35, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            var src = Normalised(sourceCounts);
            var res = Normalised(result);
            var bar = new Color32(112, 116, 128, 255);
            // Source: grey bars.
            for (int x = 0; x < gw; x++)
            {
                int bin = Mathf.Clamp(x * 256 / gw, 0, 255);
                int bh = Mathf.RoundToInt(src[bin] * (gh - 4));
                for (int yy = 0; yy < bh; yy++) px[yy * gw + x] = bar;
            }
            // Result: accent line.
            Color32 accent = Theme.Accent;
            int prev = -1;
            for (int x = 0; x < gw; x++)
            {
                float fb = x * 256f / gw;
                int b0 = Mathf.Clamp((int)fb, 0, 255), b1 = Mathf.Min(255, b0 + 1);
                int yv = Mathf.RoundToInt(Mathf.Lerp(res[b0], res[b1], fb - b0) * (gh - 4));
                int a = prev < 0 ? yv : Mathf.Min(prev, yv), b = prev < 0 ? yv : Mathf.Max(prev, yv);
                for (int yy = a; yy <= b + 1; yy++) if (yy >= 0 && yy < gh) px[yy * gw + x] = accent;
                prev = yv;
            }
            // Mid grey (127): where a neutral displacement sits.
            int mx = Mathf.RoundToInt(gw * 127.5f / 255f);
            var dash = new Color32(170, 176, 190, 255);
            for (int yy = 0; yy < gh; yy++) if ((yy / 3) % 2 == 0) px[yy * gw + mx] = dash;
            graphTexture.SetPixels32(px);
            graphTexture.Apply();
            graphKey = key;
        }
        var g = new Rect(0, y, gw, gh);
        GUI.DrawTexture(g, graphTexture);
        if (markStyle == null)
        {
            markStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, padding = new RectOffset(0, 0, 0, 0) };
            markStyle.normal.textColor = new Color(0.67f, 0.69f, 0.75f);
            statsStyle = new GUIStyle(markStyle) { fontSize = 11 };
        }
        GUI.Label(new Rect(gw * 127.5f / 255f + 3, y + 2, 30, 14), "127", markStyle);

        // Handles: black point, midtones, white point, dragged along the band under the graph.
        if (handleBlack == null) { handleBlack = Triangle(Color.black); handleGrey = Triangle(new Color(0.55f, 0.55f, 0.55f)); handleWhite = Triangle(Color.white); }
        float band = y + gh + 2;
        float mid = s.Black + (s.White - s.Black) * Mathf.Pow(0.5f, s.Gamma);
        float[] pos = { s.Black, mid, s.White };
        var tex = new[] { handleBlack, handleGrey, handleWhite };
        for (int i = 0; i < 3; i++) GUI.DrawTexture(new Rect(Mathf.Clamp(pos[i] * gw - 6, 0, gw - 13), band, 13, HandleBand - 1), tex[i]);   // kept whole at the edges

        var e = Event.current;
        var area = new Rect(0, y, gw, gh + HandleBand + 2);
        if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
        {
            float x = e.mousePosition.x / gw;
            dragHandle = 0;
            for (int i = 1; i < 3; i++) if (Mathf.Abs(pos[i] - x) < Mathf.Abs(pos[dragHandle] - x)) dragHandle = i;
            MoveHandle(s, x);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && dragHandle >= 0) { MoveHandle(s, e.mousePosition.x / gw); e.Use(); }
        else if (e.rawType == EventType.MouseUp) dragHandle = -1;
        y += gh + HandleBand + 6;

        GUI.Label(new Rect(0, y, gw, 16), L.T("File:") + " " + Stats(sourceCounts), statsStyle);
        GUI.Label(new Rect(0, y + 16, gw, 16), L.T("Result:") + " " + Stats(result), statsStyle);
        y += 40;
    }

    static void MoveHandle(Settings s, float x)
    {
        x = Mathf.Clamp01(x);
        switch (dragHandle)
        {
            case 0: s.Black = Mathf.Round(Mathf.Min(x, s.White - 0.02f) * 100f) / 100f; s.BlackT = s.Black.ToString("0.##"); break;
            case 2: s.White = Mathf.Round(Mathf.Max(x, s.Black + 0.02f) * 100f) / 100f; s.WhiteT = s.White.ToString("0.##"); break;
            case 1:
                float t = Mathf.Clamp((x - s.Black) / Mathf.Max(1e-3f, s.White - s.Black), 0.02f, 0.98f);
                s.Gamma = Mathf.Round(Mathf.Clamp(Mathf.Log(t) / Mathf.Log(0.5f), 0.2f, 5f) * 100f) / 100f;
                s.GammaT = s.Gamma.ToString("0.##");
                break;
        }
        Changed();
    }

    /// <summary>Black and white points at 0.5 % and 99.5 % of the original's values (as Texalys' stretch).</summary>
    static void StretchLevels(Settings s)
    {
        var px = originals[type].GetPixels32();
        var counts = new int[256];
        for (int i = 0; i < px.Length; i += 3) counts[px[i].r]++;
        int total = 0; foreach (var c in counts) total += c;
        int lo = 0, hi = 255, acc = 0;
        for (int i = 0; i < 256; i++) { acc += counts[i]; if (acc > total * 0.005f) { lo = i; break; } }
        acc = 0;
        for (int i = 255; i >= 0; i--) { acc += counts[i]; if (acc > total * 0.005f) { hi = i; break; } }
        if (hi <= lo) return;
        s.Black = lo / 255f; s.White = hi / 255f;
        s.BlackT = s.Black.ToString("0.###"); s.WhiteT = s.White.ToString("0.###");
    }

    static IEnumerator Apply(MainGui gui)
    {
        applying = true;
        // Out of OnGUI first: rendering there would draw the interface into the map.
        yield return null;
        var t = type;
        Render();
        Texture2D result = null;
        yield return gui.StartCoroutine(GpuReadback.Into(preview, tex => result = tex));
        var old = Current(gui, t);
        Assign(gui, t, result);
        lastResult[t] = result;
        // The map as it was goes on the undo stack (Ctrl+Z) instead of being destroyed.
        RenderTexture oldHd = t == MapType.height ? gui._HDHeightMap : null;
        if (old != null && old != originals[t]) Push(undo, new Step { Type = t, Name = title, Map = old, Hd = oldHd });
        else oldHd = null;
        Drop(redo);
        // The high-precision height follows, adjusted at full precision.
        if (t == MapType.height && originalHD.TryGetValue(t, out var hdOriginal) && hdOriginal != null)
        {
            var hd = new RenderTexture(hdOriginal.width, hdOriginal.height, 0, hdOriginal.format, RenderTextureReadWrite.Linear) { wrapMode = TextureWrapMode.Repeat };
            Process(hdOriginal, hd, settings[t]);
            if (gui._HDHeightMap != null && gui._HDHeightMap != oldHd) gui._HDHeightMap.Release();   // kept when it is on the undo stack
            gui._HDHeightMap = hd;
        }
        // Applied means done: the adjusted map becomes the starting point and the sliders go back to neutral, so
        // coming back to this map never adds the same adjustment a second time.
        var previousOriginal = originals[t];
        originals[t] = Object.Instantiate(result);
        originals[t].wrapMode = TextureWrapMode.Repeat;
        if (previousOriginal != null && previousOriginal != result) Object.Destroy(previousOriginal);
        if (originalHD.TryGetValue(t, out var previousHd) && previousHd != null) previousHd.Release();
        originalHD.Remove(t);
        if (t == MapType.height && gui._HDHeightMap != null)
        {
            var copy = new RenderTexture(gui._HDHeightMap.width, gui._HDHeightMap.height, 0, gui._HDHeightMap.format, RenderTextureReadWrite.Linear) { wrapMode = TextureWrapMode.Repeat };
            Graphics.Blit(gui._HDHeightMap, copy);
            originalHD[t] = copy;
        }
        settings[t] = new Settings();
        lastApplied.Remove(t);
        dirty = true;
        if (Alone) gui.SetPreviewMaterial(result); else gui.SetMaterialValues();
        Notifications.Info(title + " adjusted.");
        applying = false;
    }
}
