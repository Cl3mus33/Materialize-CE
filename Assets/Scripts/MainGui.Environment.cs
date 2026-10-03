using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Environment and background of the preview (Materialize CE): the built-in environments or your own HDRI
/// (HDR, EXR or an ordinary panorama), its rotation and brightness, and a background that is the environment
/// (sharp to very soft) or a plain colour. Everything is kept for the next launches.
/// Also the "Save project" panel with the maps' file format.
/// </summary>
public partial class MainGui
{
    const string EnvKey = "MaterializeCE.Env.", HdriListKey = "MaterializeCE.Env.Hdris";

    string envSelected;            // "builtin:<index>" or "file:<path>"
    float envRotation, envExposure = 1f, bgBlur = 0.25f, bgBrightness = 0.7f;
    bool bgSolid, envListOpen, envLoading;
    Color bgColor = new Color(0.165f, 0.184f, 0.22f);
    Texture2D envTexture;
    string envAppliedKey;       // the environment the rotation and brightness shown belong to
    Vector3? envSun;            // the HDRI's sun, in the picture's own directions (before the rotation); null: no clear sun
    bool followSun = true;      // the light turns to the HDRI's sun
    float sunAlignedRotation = float.NaN; Vector3? sunAlignedFor;
    string envTextureFor;

    /// <summary>The Poly Haven HDRIs shipped with Materialize CE (CC0), in StreamingAssets/HDRI.</summary>
    static List<string> BundledHdris()
    {
        var list = new List<string>();
        try
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "HDRI");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir))
                    if (f.EndsWith(".exr", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase)) list.Add(f);
            list.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) { Debug.LogWarning("Bundled HDRIs: " + e.Message); }
        return list;
    }

    static string Pretty(string file)
    {
        var words = Path.GetFileNameWithoutExtension(file).Replace('_', ' ').Split(' ');
        for (int i = 0; i < words.Length; i++)
            if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
        return string.Join(" ", words);
    }

    List<string> Hdris()
    {
        var text = PlayerPrefs.GetString(HdriListKey, "");
        var list = new List<string>();
        foreach (var p in text.Split('|')) if (p.Length > 0) list.Add(p);
        return list;
    }

    void SetHdris(List<string> list)
    {
        PlayerPrefs.SetString(HdriListKey, string.Join("|", list.ToArray()));
        PlayerPrefs.Save();
    }

    /// <summary>Called at the end of Start: the saved environment and background.</summary>
    void RestoreEnvironment()
    {
        envSelected = PlayerPrefs.GetString(EnvKey + "Selected", "builtin:0");
        envRotation = PlayerPrefs.GetFloat(EnvKey + "Rotation", 0f);
        envExposure = PlayerPrefs.GetFloat(EnvKey + "Exposure", 1f);
        followSun = PlayerPrefs.GetInt(EnvKey + "FollowSun", 1) == 1;
        bgSolid = PlayerPrefs.GetInt(EnvKey + "BgSolid", 0) == 1;
        bgBlur = PlayerPrefs.GetFloat(EnvKey + "BgBlur", 0.25f);
        bgBrightness = PlayerPrefs.GetFloat(EnvKey + "BgBrightness", 0.7f);
        ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString(EnvKey + "BgColor", "2A2F38"), out bgColor);
        if (reflectionProbe != null)
        {
            // Sharper reflections and background, and no lag while turning the environment.
            reflectionProbe.resolution = 1024;   // the original look sets 256 (MaterialGui.ApplySettings)
            Shader.SetGlobalFloat("_ProbeMaxMip", Mathf.Log(1024, 2));   // the reflections' blur range
            reflectionProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        }
        ApplyEnvironment(true);
        RenderPresets.Applied = OnPresetApplied;
    }

    void SaveEnvironment()
    {
        PlayerPrefs.SetString(EnvKey + "Selected", envSelected);
        PlayerPrefs.SetFloat(EnvKey + "Rotation", envRotation);
        // Rotation and brightness belong to each environment (a night sky needs more light than a bright bridge).
        string owner = envAppliedKey ?? envSelected;
        PlayerPrefs.SetFloat(EnvKey + "Rotation:" + owner, envRotation);
        PlayerPrefs.SetFloat(EnvKey + "Exposure:" + owner, envExposure);
        PlayerPrefs.SetFloat(EnvKey + "Exposure", envExposure);
        PlayerPrefs.SetInt(EnvKey + "FollowSun", followSun ? 1 : 0);
        PlayerPrefs.SetInt(EnvKey + "BgSolid", bgSolid ? 1 : 0);
        PlayerPrefs.SetFloat(EnvKey + "BgBlur", bgBlur);
        PlayerPrefs.SetFloat(EnvKey + "BgBrightness", bgBrightness);
        PlayerPrefs.SetString(EnvKey + "BgColor", ColorUtility.ToHtmlStringRGB(bgColor));
        PlayerPrefs.Save();
    }

    /// <summary>Sends the settings to the shaders and re-renders the reflections. <paramref name="source"/>: the picture changed.</summary>
    void ApplyEnvironment(bool source)
    {
        if (source && envAppliedKey != envSelected)
        {
            // Another environment: its own rotation and brightness (1 and 0 the first time).
            envAppliedKey = envSelected;
            envRotation = PlayerPrefs.GetFloat(EnvKey + "Rotation:" + envSelected, 0f);
            envExposure = PlayerPrefs.GetFloat(EnvKey + "Exposure:" + envSelected, 1f);
        }
        Shader.SetGlobalFloat("_EnvRotation", envRotation * Mathf.Deg2Rad);
        Shader.SetGlobalFloat("_EnvExposure", Mathf.Max(0.001f, envExposure));
        Shader.SetGlobalFloat("_BgSolid", bgSolid ? 1 : 0);
        Shader.SetGlobalColor("_BgColor", bgColor);
        Shader.SetGlobalFloat("_BgBlur", bgBlur);
        Shader.SetGlobalFloat("_BgBrightness", Mathf.Max(0.001f, bgBrightness));
        if (source)
        {
            if (envSelected != null && (envSelected.StartsWith("file:") || envSelected.StartsWith("bundled:")))
            {
                string path = envSelected.StartsWith("bundled:")
                    ? Path.Combine(Application.streamingAssetsPath, "HDRI", envSelected.Substring(8))
                    : envSelected.Substring(5);
                if (envTexture != null && envTextureFor == path) UseEquirect(envTexture);
                else { LoadHdri(path); return; }
            }
            else
            {
                int index = 0;
                if (envSelected != null && envSelected.StartsWith("builtin:")) int.TryParse(envSelected.Substring(8), out index);
                selectedCubemap = Mathf.Clamp(index, 0, CubeMaps.Length - 1);
                Shader.SetGlobalFloat("_EnvEquirect", 0);
                Shader.SetGlobalTexture("_GlobalCubemap", CubeMaps[selectedCubemap]);
            }
        }
        if (reflectionProbe != null) reflectionProbe.RenderProbe();
    }

    void UseEquirect(Texture2D tex)
    {
        Shader.SetGlobalTexture("_GlobalEquirect", tex);
        Shader.SetGlobalFloat("_EnvEquirect", 1);
        if (reflectionProbe != null) reflectionProbe.RenderProbe();
    }

    /// <summary>Decodes the HDRI on a worker thread, then uploads it (half floats, gamma-encoded like the built-in ones).</summary>
    void LoadHdri(string path)
    {
        if (envLoading) return;
        envLoading = true;
        Notifications.Info("Loading the HDRI: " + Path.GetFileName(path) + "…");
        StartCoroutine(LoadHdriRoutine(path));
    }

    System.Collections.IEnumerator LoadHdriRoutine(string path)
    {
        ushort[] halves = null; int w = 0, h = 0; string error = null; Vector3? sun = null;
        var task = Task.Run(() =>
        {
            error = FastImageLoader.DecodeEnvironment(path, 4096, out float[] rgba, out w, out h, out bool linear);
            if (error != null) return;
            sun = FindSun(rgba, w, h);
            halves = new ushort[rgba.Length];
            for (int i = 0; i < rgba.Length; i++)
            {
                float v = rgba[i];
                if ((i & 3) == 3) v = 1f;
                else if (linear) v = Mathf.Pow(Mathf.Clamp(v, 0f, 8f), 1f / 2.2f);   // sun clamped: no blown-out reflections   // Materialize renders in gamma space
                halves[i] = Mathf.FloatToHalf(v);
            }
        });
        while (!task.IsCompleted) yield return null;
        envLoading = false;
        if (task.Exception != null) error = task.Exception.InnerException?.Message ?? task.Exception.Message;
        if (error != null)
        {
            Notifications.Error(error);
            envSelected = "builtin:0";
            SaveEnvironment();
            ApplyEnvironment(true);
            yield break;
        }
        if (envTexture != null) Destroy(envTexture);
        // With mipmaps: the background's blur reads them.
        envTexture = new Texture2D(w, h, TextureFormat.RGBAHalf, true, true) { wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = Path.GetFileName(path) };
        envTexture.SetPixelData(halves, 0);
        envTexture.Apply(true, true);
        envTextureFor = path;
        envSun = sun;
        // Only if it is still the chosen one (another may have been picked meanwhile).
        if (envSelected == "file:" + path || envSelected == "bundled:" + Path.GetFileName(path)) { UseEquirect(envTexture); Notifications.Info("HDRI: " + Path.GetFileName(path)); }
    }

    string EnvName(string key)
    {
        if (key != null && key.StartsWith("file:")) return Path.GetFileNameWithoutExtension(key.Substring(5));
        if (key != null && key.StartsWith("bundled:")) return Pretty(key.Substring(8));
        int index = 0;
        if (key != null && key.StartsWith("builtin:")) int.TryParse(key.Substring(8), out index);
        index = Mathf.Clamp(index, 0, CubeMaps.Length - 1);
        return CubeMaps[index].name.Replace("CubeMap_", "");
    }

    /// <summary>Copied into the workspace's HDRI folder, where it stays listed (Materialize CE).</summary>
    void ImportHdri(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            string dir = Workspace.Dir(Workspace.Hdri);
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
            {
                string copy = Path.Combine(dir, Path.GetFileName(path));
                if (!File.Exists(copy)) File.Copy(path, copy);
                path = copy;
            }
        }
        catch (Exception e) { Notifications.Error(e.Message); }
        envSelected = "file:" + path;
        SaveEnvironment();
        ApplyEnvironment(true);
    }

    /// <summary>The next / previous environment of the list (arrow keys, as in Mixer).</summary>
    void CycleEnvironment(int step)
    {
        var keys = new List<string>();
        for (int i = 0; i < CubeMaps.Length; i++) keys.Add("builtin:" + i);
        foreach (var f in BundledHdris()) keys.Add("bundled:" + Path.GetFileName(f));
        foreach (var (addon, file) in Workspace.AddonFiles(Workspace.Hdri, ".hdr", ".exr", ".png", ".jpg", ".jpeg", ".tif", ".tiff")) keys.Add("file:" + file);
        foreach (var f in YourHdris()) if (File.Exists(f)) keys.Add("file:" + f);
        int at = Mathf.Max(0, keys.IndexOf(envSelected));
        envSelected = keys[((at + step) % keys.Count + keys.Count) % keys.Count];
        SaveEnvironment();
        ApplyEnvironment(true);
        Notifications.Info(L.T("Environment:") + " " + EnvName(envSelected));
    }

    // ---------- Panel ----------

    bool EnvSlider(ref float y, float x, float w, string title, string tip, ref float value, float min, float max)
    {
        GUI.Label(new Rect(x, y, w - 60, 20), L.G(title, tip));
        float v = GUI.HorizontalSlider(new Rect(x, y + 22, w - 60, 12), value, min, max);
        GUI.Label(new Rect(x + w - 52, y + 16, 52, 20), v.ToString(max > 10 ? "0" : "0.00"), mapInfo);
        y += 42;
        if (Mathf.Approximately(v, value)) return false;
        value = v;
        return true;
    }

    /// <summary>Your HDRIs: the workspace's HDRI folder, and files imported by older versions.</summary>
    List<string> YourHdris()
    {
        var list = new List<string>();
        try
        {
            foreach (var f in Directory.GetFiles(Workspace.Dir(Workspace.Hdri)))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".hdr" || ext == ".exr" || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".tif" || ext == ".tiff") list.Add(f);
            }
        }
        catch (Exception e) { Debug.LogWarning("HDRI folder: " + e.Message); }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Hdris()) if (!list.Contains(f)) list.Add(f);
        return list;
    }

    bool EnvItem(float x, ref float y, float w, string key, string label, string tip)
    {
        bool current = envSelected == key;
        bool clicked = GUI.Button(new Rect(x + 12, y, w - 12, 22), L.G((current ? "● " : "") + label, tip), menuItem);
        if (clicked) { envSelected = key; envListOpen = false; SaveEnvironment(); ApplyEnvironment(true); }
        y += 24;
        return clicked;
    }

    float DrawEnvironmentPanel(float x, float y, float w)
    {
        GUI.Label(new Rect(x, y, w, 20), L.T("Environment & background"), mapName);
        y += 26;
        GUI.Label(new Rect(x, y, w, 16), L.T("ENVIRONMENT (LIGHTING AND REFLECTIONS)"), mapInfoLeft);
        y += 20;
        string shown = envLoading ? "Loading…" : EnvName(envSelected);
        if (GUI.Button(new Rect(x, y, w, 26), L.G(shown + "  ▾", "The picture around the material: it lights it and shows in its reflections.")))
            envListOpen = !envListOpen;
        y += 30;
        if (envListOpen)
        {
            GUI.Label(new Rect(x, y + 2, w, 16), L.T("MATERIALIZE"), mapInfoLeft);
            y += 20;
            for (int i = 0; i < CubeMaps.Length; i++) EnvItem(x, ref y, w, "builtin:" + i, EnvName("builtin:" + i), null);
            GUI.Label(new Rect(x, y + 2, w, 16), L.T("POLY HAVEN (CC0)"), mapInfoLeft);
            y += 20;
            foreach (var path in BundledHdris())
            {
                string by = HdriAuthors.TryGetValue(Path.GetFileNameWithoutExtension(path), out var a) ? "\n" + L.T("By") + " " + a : "";
                EnvItem(x, ref y, w, "bundled:" + Path.GetFileName(path), Pretty(path), L.T("HDRI from polyhaven.com, public domain (CC0).") + by);
            }
            // Add-ons (Skyrim pack…): one group each.
            string lastAddon = null;
            foreach (var (addon, file) in Workspace.AddonFiles(Workspace.Hdri, ".hdr", ".exr", ".png", ".jpg", ".jpeg", ".tif", ".tiff"))
            {
                if (addon != lastAddon) { GUI.Label(new Rect(x, y + 2, w, 16), addon.ToUpperInvariant(), mapInfoLeft); y += 20; lastAddon = addon; }
                EnvItem(x, ref y, w, "file:" + file, Pretty(file), file);
            }
            var hdris = YourHdris();
            if (hdris.Count > 0) { GUI.Label(new Rect(x, y + 2, w, 16), L.T("YOURS"), mapInfoLeft); y += 20; }
            var legacy = Hdris();
            foreach (var path in hdris)
            {
                bool exists = File.Exists(path);
                GUI.enabled = exists;
                string key = "file:" + path;
                if (GUI.Button(new Rect(x + 12, y, w - 44, 22), L.G((envSelected == key ? "● " : "") + Path.GetFileNameWithoutExtension(path) + (exists ? "" : "  (missing)"), path), menuItem))
                { envSelected = key; envListOpen = false; SaveEnvironment(); ApplyEnvironment(true); }
                GUI.enabled = true;
                if (legacy.Contains(path) && GUI.Button(new Rect(x + w - 28, y, 28, 22), L.G("×", "Remove from the list (the file stays on disk).")))
                {
                    legacy.Remove(path);
                    SetHdris(legacy);
                    if (envSelected == key) { envSelected = "builtin:0"; SaveEnvironment(); ApplyEnvironment(true); }
                    break;
                }
                y += 24;
            }
            if (GUI.Button(new Rect(x + 12, y, w - 12, 22), L.G("Open my HDRI folder", "Put .hdr / .exr files there: they appear in this list."), menuItem))
                Application.OpenURL("file:///" + Workspace.Dir(Workspace.Hdri).Replace("\\", "/"));
            y += 28;
        }
        if (GUI.Button(new Rect(x, y, w, 26), L.G("Import an HDRI…", "Your own environment: HDR, EXR (Poly Haven, ambientCG…) or an ordinary panorama (JPG, PNG), 2:1 equirectangular.")))
        {
            fileBrowser.fileMasks = "*.hdr;*.exr;*.jpg;*.jpeg;*.png;*.tif;*.tiff";
            fileBrowser.ShowBrowser("Import an HDRI", ImportHdri);
        }
        y += 34;
        bool changed = false;
        changed |= EnvSlider(ref y, x, w, "Rotation", "Turns the environment around the material (degrees).", ref envRotation, 0f, 360f);
        changed |= EnvSlider(ref y, x, w, "Brightness", "Strength of the environment's light and reflections.", ref envExposure, 0f, 3f);
        bool hasSun = EnvUsesPicture() && envSun.HasValue;
        GUI.enabled = hasSun;
        bool follow = GUI.Toggle(new Rect(x, y, w, 22), followSun && hasSun, L.G("☀ Light follows the HDRI's sun",
            hasSun ? "The light comes from the sun of the picture, also when you turn the environment. Shift + right drag still moves it."
                   : "This environment has no clear sun."));
        GUI.enabled = true;
        if (hasSun && follow != followSun) { followSun = follow; sunAlignedFor = null; changed = true; }
        y += 28;

        y += 6;
        GUI.Label(new Rect(x, y, w, 16), L.T("BACKGROUND"), mapInfoLeft);
        y += 20;
        int mode = GUI.Toolbar(new Rect(x, y, w, 24), bgSolid ? 1 : 0, new[] {
            L.G("Environment", "The environment behind the material, sharp or soft."),
            L.G("Plain colour", "A plain colour behind the material; the lighting stays the environment's.") });
        if ((mode == 1) != bgSolid) { bgSolid = mode == 1; changed = true; }
        y += 32;
        if (!bgSolid)
        {
            changed |= EnvSlider(ref y, x, w, "Blur", "0 = sharp, 1 = very soft.", ref bgBlur, 0f, 1f);
            changed |= EnvSlider(ref y, x, w, "Background brightness", "Brightness of the background only.", ref bgBrightness, 0f, 2f);
        }
        else
        {
            float r = bgColor.r, g = bgColor.g, b = bgColor.b;
            changed |= EnvSlider(ref y, x, w, "Red", null, ref r, 0f, 1f);
            changed |= EnvSlider(ref y, x, w, "Green", null, ref g, 0f, 1f);
            changed |= EnvSlider(ref y, x, w, "Blue", null, ref b, 0f, 1f);
            bgColor = new Color(r, g, b);
            var swatches = new[] { (Theme.FrameColour, "Interface"), (Color.black, "Black"), (new Color(0.5f, 0.5f, 0.5f), "Grey"), (Color.white, "White") };
            float sw = (w - 3 * 6) / 4f;
            for (int i = 0; i < swatches.Length; i++)
            {
                var old = GUI.color;
                GUI.color = swatches[i].Item1;
                GUI.DrawTexture(new Rect(x + i * (sw + 6), y, sw, 20), Texture2D.whiteTexture);
                GUI.color = old;
                if (GUI.Button(new Rect(x + i * (sw + 6), y + 22, sw, 20), L.T(swatches[i].Item2))) { bgColor = swatches[i].Item1; changed = true; }
            }
            y += 50;
        }
        if (changed)
        {
            ApplyEnvironment(false);
            SaveEnvironment();
        }
        return y;
    }

    /// <summary>Preset "Materialize (original)": the environment, background and light of Materialize 1.78.</summary>
    void OnPresetApplied(RenderPreset preset)
    {
        if (preset == null || !preset.OriginalRender) return;
        envSelected = "builtin:0";
        envAppliedKey = envSelected;   // the values below are this environment's
        envRotation = 0f; envExposure = 1f;
        bgSolid = false; bgBlur = 0.25f; bgBrightness = 0.7f;
        SaveEnvironment();
        ApplyEnvironment(true);
        ObjRotator.ResetLight();
    }

    bool EnvUsesPicture() => envSelected != null && (envSelected.StartsWith("file:") || envSelected.StartsWith("bundled:")) && envTexture != null;

    /// <summary>The brightest spot of the sky (8×8 blocks of the upper half), if it clearly stands out: the sun.</summary>
    public static Vector3? FindSun(float[] rgba, int w, int h)
    {
        const int B = 8;
        double total = 0; int count = 0;
        float best = 0; int bx = 0, by = 0;
        for (int y0 = h / 2; y0 + B <= h; y0 += B)
            for (int x0 = 0; x0 + B <= w; x0 += B)
            {
                float sum = 0;
                for (int y = y0; y < y0 + B; y += 2)
                    for (int x = x0; x < x0 + B; x += 2)
                    {
                        int i = (y * w + x) * 4;
                        sum += 0.2126f * rgba[i] + 0.7152f * rgba[i + 1] + 0.0722f * rgba[i + 2];
                    }
                total += sum; count++;
                if (sum > best) { best = sum; bx = x0; by = y0; }
            }
        if (count == 0 || best < 4.0 * total / count) return null;   // overcast: no sun to follow
        float u = (bx + B * 0.5f) / w, v = (by + B * 0.5f) / h;
        float phi = (u - 0.5f) * 2f * Mathf.PI, lat = (v - 0.5f) * Mathf.PI;
        return new Vector3(Mathf.Sin(phi) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(phi) * Mathf.Cos(lat));
    }

    /// <summary>Called every frame: turns the light to the sun when the HDRI or its rotation changed.</summary>
    void FollowSun()
    {
        if (!followSun || envLoading) return;
        if (!EnvUsesPicture() || !envSun.HasValue)
        {
            // No sun in this environment (cubemap, overcast sky): the light leaves the last sun's place for its own.
            if (sunAlignedFor.HasValue) { sunAlignedFor = null; ObjRotator.ResetLight(); }
            return;
        }
        if (sunAlignedFor == envSun && sunAlignedRotation == envRotation) return;
        sunAlignedFor = envSun; sunAlignedRotation = envRotation;
        // The shaders read the picture at (c x - s z, y, s x + c z): back to the world's directions.
        float a = envRotation * Mathf.Deg2Rad, s = Mathf.Sin(a), c = Mathf.Cos(a);
        Vector3 d = envSun.Value;
        var world = new Vector3(c * d.x + s * d.z, d.y, -s * d.x + c * d.z);
        // A sun on the horizon would leave the material black: the light keeps its direction around, but not lower
        // than 20 degrees above the ground.
        const float minElevation = 20f;
        if (Mathf.Asin(Mathf.Clamp(world.normalized.y, -1f, 1f)) * Mathf.Rad2Deg < minElevation)
        {
            var flat = new Vector2(world.x, world.z);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector2.up;
            float e = minElevation * Mathf.Deg2Rad;
            world = new Vector3(flat.x * Mathf.Cos(e), Mathf.Sin(e), flat.y * Mathf.Cos(e));
        }
        ObjRotator.SetLightDirection(-world.normalized);
    }

    static readonly Dictionary<string, string> HdriAuthors = new Dictionary<string, string>
    {
        { "bloem_hill_02", "Jenelle van Heerden, Greg Zaal" }, { "castel_st_angelo_roof", "Andreas Mischok" },
        { "dry_cracked_lake", "Dimitrios Savva, Jarod Guest" }, { "golden_gate_hills", "Dimitrios Savva, Jarod Guest" },
        { "hochsal_forest", "Adrian Kubasa" }, { "meadow_2", "Sergej Majboroda" }, { "mud_road_puresky", "Sergey Rudavin, Jarod Guest" },
        { "qwantani_night", "Greg Zaal, Jarod Guest" }, { "red_church", "Grzegorz Wronkowski" }, { "snowy_forest_path_02", "Oliksiy Yakovlyev" },
        { "snowy_hillside_02", "Andreas Mischok" }, { "toposcope_sunset", "Dario Barresi" },
        { "kloofendal_43d_clear", "Greg Zaal" }, { "autumn_field", "Sergej Majboroda" }, { "syferfontein_1d_clear", "Greg Zaal" },
        { "kloofendal_43d_clear_puresky", "Greg Zaal" }, { "autumn_field_puresky", "Jarod Guest, Sergej Majboroda" },
        { "syferfontein_1d_clear_puresky", "Greg Zaal, Jarod Guest" }, { "venice_sunset", "Greg Zaal" }, { "moonlit_golf", "Greg Zaal" },
    };

    // ---------- Save project ----------

    bool saveProjectOpen;
    static readonly FileFormat[] projectFormats = { FileFormat.png, FileFormat.tga, FileFormat.tiff, FileFormat.jpg, FileFormat.bmp };

    void DrawSaveProjectPanel()
    {
        if (!saveProjectOpen) return;
        var r = new Rect(Screen.width / 2 - 190, Screen.height / 2 - 90, 380, 180);
        GUI.Window(82, r, id =>
        {
            GUI.Label(new Rect(14, 28, 352, 20), L.T("File format of the maps:"));
            int current = Array.IndexOf(projectFormats, selectedFormat);
            int chosen = GUI.Toolbar(new Rect(14, 52, 352, 26), current, new[] {
                L.G("PNG", "Lossless, 16-bit height. The usual choice."),
                L.G("TGA", "Lossless, read by every game tool."),
                L.G("TIFF", "Lossless, 16-bit height."),
                L.G("JPG", "Small files, lossy (quality 95)."),
                L.G("BMP", "Uncompressed, large files.") });
            if (chosen != current && chosen >= 0)
            {
                SetFormat(projectFormats[chosen]);
                if (SettingsGui.instance != null) SettingsGui.instance.SaveFormatSetting();
            }
            GUI.Label(new Rect(14, 84, 352, 36), L.T("The maps are written next to the project file. This format is also used by each map's Save button."), wrapStyle);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            if (GUI.Button(new Rect(14, 134, 170, 30), L.T("Choose where to save…")))
            {
                saveProjectOpen = false;
                SetFileMaskProject();
                fileBrowser.ShowBrowser("Save Project", SaveProject);
            }
            GUI.backgroundColor = old;
            if (GUI.Button(new Rect(196, 134, 170, 30), L.T("Cancel"))) saveProjectOpen = false;
            Tips.Capture(true);
        }, "Save project");
        Tips.Block(r);
    }
}
