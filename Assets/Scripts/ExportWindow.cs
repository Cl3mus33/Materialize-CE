using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Source = ChannelPacker.Source;

/// <summary>
/// The Export window (Materialize CE), in the spirit of Quixel Mixer's: pick a profile (engine), a name and a
/// folder, check the files to write, and export them all at once. Every file says what goes in each channel,
/// its format and its DDS compression; profiles can be edited, saved under a new name and shared as JSON.
/// </summary>
public static class ExportWindow
{
    public static bool Open;
    static Rect windowRect;
    static List<ExportProfile> profiles;
    static int selected;
    static int editing = -1, choosingChannel = -1;
    static bool choosingProfile, exporting;
    static string status = "";
    static Vector2 scroll;

    const string FolderKey = "MaterializeCE.ExportFolder", BaseKey = "MaterializeCE.ExportBase", ProfileKey = "MaterializeCE.ExportProfile";
    static string folder, baseName;

    static readonly FileFormat[] formats = { FileFormat.png, FileFormat.tga, FileFormat.tiff, FileFormat.jpg, FileFormat.bmp, FileFormat.dds };
    static readonly string[] channelNames = { "R", "G", "B", "A" };
    static readonly int[] sizes = { 0, 4096, 2048, 1024, 512, 256 };
    static readonly string[] sizeLabels = { "Full", "4K", "2K", "1K", "512", "256" };

    /// <summary>The packed file reduced so that its largest side is <paramref name="maxSize"/> (through its mipmaps: no aliasing). Never enlarged.</summary>
    static Texture2D Reduce(Texture2D source, int maxSize)
    {
        int largest = Mathf.Max(source.width, source.height);
        if (maxSize <= 0 || largest <= maxSize) return source;
        int w = Mathf.Max(1, Mathf.RoundToInt(source.width * (float)maxSize / largest)), h = Mathf.Max(1, Mathf.RoundToInt(source.height * (float)maxSize / largest));
        source.filterMode = FilterMode.Trilinear;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var was = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var small = new Texture2D(w, h, source.format, false);
        small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        small.Apply(false);
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);
        UnityEngine.Object.Destroy(source);
        return small;
    }

    static ExportProfile Profile => profiles[Mathf.Clamp(selected, 0, profiles.Count - 1)];

    static void Load()
    {
        var errors = new List<string>();
        profiles = ExportProfiles.BuiltIn();
        profiles.AddRange(ExportProfiles.LoadAddons(errors));
        profiles.AddRange(ExportProfiles.LoadUser(errors));
        foreach (var e in errors) Notifications.Error("Export profile ignored: " + e);
        string last = PlayerPrefs.GetString(ProfileKey, "");
        selected = Mathf.Max(0, profiles.FindIndex(p => p.Name == last));
        folder = PlayerPrefs.GetString(FolderKey, "");
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) folder = Workspace.Dir(Workspace.Textures);
        baseName = PlayerPrefs.GetString(BaseKey, "material");
    }

    // ---------- Quick export (the Project & export block of the main interface) ----------

    public static List<ExportProfile> Profiles { get { if (profiles == null) Load(); return profiles; } }
    public static int Selected
    {
        get { if (profiles == null) Load(); return Mathf.Clamp(selected, 0, profiles.Count - 1); }
        set { selected = value; PlayerPrefs.SetString(ProfileKey, Profile.Name); PlayerPrefs.Save(); }
    }
    public static string Folder { get { if (profiles == null) Load(); return folder; } set => folder = value; }
    public static string BaseName { get { if (profiles == null) Load(); return baseName; } set => baseName = value; }
    public static bool Busy => exporting;
    /// <summary>0..1 while exporting: files written, plus a share for the one being written.</summary>
    public static float Progress;
    public static string Status => status;
    public static void Run(MainGui gui) { if (profiles == null) Load(); gui.StartCoroutine(Export(gui)); }
    public static void PickFolder(MainGui gui) { if (profiles == null) Load(); gui.StartCoroutine(ChooseFolder()); }
    public static void Reload() => Load();

    public static void Toggle()
    {
        Open = !Open;
        if (Open && profiles == null) Load();
        if (Open) windowRect = new Rect(Mathf.Max(20, Screen.width / 2 - 390), 120, 780, 520);
    }

    public static void Draw(MainGui gui)
    {
        if (!Open) return;
        windowRect = GUI.Window(79, windowRect, id => DoWindow(gui), L.T("Export"));
        Tips.Block(windowRect);
    }

    static void DoWindow(MainGui gui)
    {
        int x = 12, y = 26, w = (int)windowRect.width - 24;

        // ---------- Profile ----------
        GUI.Label(new Rect(x, y + 2, 60, 22), L.G("Profile", "Which engine or game the files are for. Pick one, adjust it, and save it under your own name."));
        if (GUI.Button(new Rect(x + 60, y, 300, 24), L.G(Profile.Label + "  ▾", "Choose another profile.")))
            choosingProfile = !choosingProfile;
        if (GUI.Button(new Rect(x + 370, y, 90, 24), L.G("Save as…", "Saves this profile under a new name, with your changes.")))
            gui.StartCoroutine(SaveAs());
        GUI.enabled = !Profile.BuiltIn;
        if (GUI.Button(new Rect(x + 466, y, 60, 24), L.G("Save", "Saves your changes to this profile.")))
        {
            ExportProfiles.Save(Profile);
            Notifications.Info("Profile saved: " + Profile.Name);
        }
        if (GUI.Button(new Rect(x + 532, y, 60, 24), L.G("Delete", "Deletes this profile (built-in ones cannot be deleted).")))
        {
            ExportProfiles.Delete(Profile);
            profiles.RemoveAt(selected);
            selected = 0;
        }
        GUI.enabled = true;
        if (GUI.Button(new Rect(x + 598, y, 150, 24), L.G("Profiles folder", "Opens the folder where your profiles are kept as .json files, to back them up or share them.")))
        {
            Directory.CreateDirectory(ExportProfiles.Folder);
            Application.OpenURL("file:///" + ExportProfiles.Folder.Replace('\\', '/'));
        }
        y += 32;

        if (choosingProfile)
        {
            int rows = (profiles.Count + 2) / 3;
            GUI.Box(new Rect(x - 4, y, w + 8, rows * 26 + 8), L.T(""));
            for (int i = 0; i < profiles.Count; i++)
            {
                if (GUI.Button(new Rect(x + (i % 3) * (w / 3), y + 4 + (i / 3) * 26, w / 3 - 6, 23), profiles[i].Label))
                {
                    selected = i;
                    choosingProfile = false;
                    editing = choosingChannel = -1;
                    PlayerPrefs.SetString(ProfileKey, profiles[i].Name);
                }
            }
            y += rows * 26 + 14;
        }

        // ---------- Name and folder ----------
        GUI.Label(new Rect(x, y + 2, 60, 22), L.G("Name", "Base name of the files; each file adds its suffix: stone + _n = stone_n."));
        baseName = GUI.TextField(new Rect(x + 60, y, 180, 22), baseName ?? "");
        GUI.Label(new Rect(x + 250, y + 2, 50, 22), L.G("Folder", "Where the files are written."));
        folder = GUI.TextField(new Rect(x + 300, y, 330, 22), folder ?? "");
        if (GUI.Button(new Rect(x + 636, y, 112, 22), L.G("Choose…", "Pick the folder (and the name) with the Windows dialog.")))
            gui.StartCoroutine(ChooseFolder());
        y += 32;

        // ---------- Files ----------
        GUI.Label(new Rect(x + 24, y, 90, 20), L.G("Suffix", "Added to the name: _n, _d, _rmaos…"));
        GUI.Label(new Rect(x + 120, y, 242, 20), L.G("Content (R, G, B, A)", "What goes in each channel. Click to change it."));
        GUI.Label(new Rect(x + 368, y, 56, 20), L.G("Size", "Largest side of the file. Full = the maps' own size. A parallax or mask map can be smaller than the colour."));
        GUI.Label(new Rect(x + 430, y, 70, 20), L.G("Format", "File format. Click to cycle."));
        GUI.Label(new Rect(x + 506, y, 110, 20), L.G("Compression", "DDS only: BC7 best, BC1 smallest, BC5 normals, BC4 grey."));
        GUI.Label(new Rect(x + 622, y, 100, 20), L.G("Normal", "Normal maps: the convention the engine expects. OpenGL = green up (Unity, Blender, glTF); DirectX = green down (Unreal, Skyrim)."));
        y += 22;

        var outputs = Profile.Outputs;
        float listHeight = Mathf.Min(outputs.Count * 28, 6 * 28);
        scroll = GUI.BeginScrollView(new Rect(x, y, w, listHeight), scroll, new Rect(0, 0, w - 20, outputs.Count * 28));
        for (int i = 0; i < outputs.Count; i++)
        {
            var o = outputs[i];
            int ry = i * 28;
            o.Enabled = GUI.Toggle(new Rect(0, ry + 3, 20, 20), o.Enabled, L.G("", "Write this file."));
            o.Suffix = GUI.TextField(new Rect(24, ry, 90, 22), o.Suffix ?? "");
            string content = string.Join("  ", Enumerable.Range(0, 4).Select(c => channelNames[c] + ": " + Short(o.Channels[c])));
            if (GUI.Button(new Rect(120, ry, 242, 22), L.G(content, "Click to choose what goes in each channel.")))
            {
                editing = editing == i ? -1 : i;
                choosingChannel = -1;
            }
            int sizeIndex = Mathf.Max(0, Array.IndexOf(sizes, o.MaxSize));
            if (GUI.Button(new Rect(368, ry, 56, 22), L.G(sizeLabels[sizeIndex], "Largest side of this file, in pixels. Click to cycle. Full = the maps' own size; smaller maps are never enlarged.")))
                o.MaxSize = sizes[(sizeIndex + 1) % sizes.Length];
            if (GUI.Button(new Rect(430, ry, 70, 22), L.G(o.Format.ToString().ToUpperInvariant(), "Click to change the format.")))
                o.Format = formats[(Array.IndexOf(formats, o.Format) + 1) % formats.Length];
            GUI.enabled = o.Format == FileFormat.dds;
            if (GUI.Button(new Rect(506, ry, 110, 22), L.G(o.Format == FileFormat.dds ? DdsExport.FormatLabels[(int)o.Dds] : "-", o.Format == FileFormat.dds ? DdsExport.FormatHelp[(int)o.Dds] : "")))
                o.Dds = (DdsFormat)(((int)o.Dds + 1) % DdsExport.FormatLabels.Length);
            GUI.enabled = o.UsesNormal;
            string[] normalLabels = { "As is", "OpenGL ↑", "DirectX ↓" };
            if (GUI.Button(new Rect(622, ry, 80, 22), L.G(o.UsesNormal ? normalLabels[(int)o.Normal] : "-",
                "OpenGL ↑ = green up (Unity, Blender, glTF). DirectX ↓ = green inverted (Skyrim, Unreal). The file is converted if Materialize's normal differs. As is = no change.")))
                o.Normal = (NormalStyle)(((int)o.Normal + 1) % 3);
            GUI.enabled = true;
            if (GUI.Button(new Rect(708, ry, 26, 22), L.G("✕", "Remove this file from the profile.")))
            {
                outputs.RemoveAt(i);
                editing = choosingChannel = -1;
                break;
            }
        }
        GUI.EndScrollView();
        y += (int)listHeight + 4;
        if (GUI.Button(new Rect(x, y, 120, 22), L.G("+ Add a file", "Adds a file to this profile.")))
            outputs.Add(new ExportOutput { Suffix = "_new", Channels = new[] { C(Source.None), C(Source.None), C(Source.None), C(Source.None) } });
        y += 30;

        // ---------- Channel editor for the file being edited ----------
        if (editing >= 0 && editing < outputs.Count)
        {
            var o = outputs[editing];
            GUI.Box(new Rect(x - 4, y, w + 8, choosingChannel >= 0 ? 238 : 124), L.T("Content of ") + (baseName ?? "") + o.Suffix);
            y += 22;
            for (int c = 0; c < 4; c++)
            {
                var ch = o.Channels[c];
                GUI.Label(new Rect(x, y + 2, 30, 20), channelNames[c]);
                GUI.enabled = choosingChannel != c;
                if (GUI.Button(new Rect(x + 30, y, 160, 21), L.G(ChannelPacker.Label(ch.Source), ChannelPacker.Tip(ch.Source))))
                    choosingChannel = c;
                GUI.enabled = true;
                ch.Invert = GUI.Toggle(new Rect(x + 200, y + 1, 70, 20), ch.Invert, L.G("Invert", "1 − value: smoothness ↔ roughness."));
                GUI.Label(new Rect(x + 280, y + 1, 50, 20), L.G("× AO", "Darkens this channel with the ambient occlusion (0 = off, 1 = full)."));
                ch.MultiplyAO = Mathf.Round(GUI.HorizontalSlider(new Rect(x + 320, y + 6, 120, 16), ch.MultiplyAO, 0, 1) * 20) / 20;
                GUI.Label(new Rect(x + 448, y + 1, 50, 20), ch.MultiplyAO > 0 ? ch.MultiplyAO.ToString("0.00") : "off");
                y += 24;
            }
            if (choosingChannel >= 0)
            {
                var all = (Source[])Enum.GetValues(typeof(Source));
                for (int i = 0; i < all.Length; i++)
                    if (GUI.Button(new Rect(x + (i % 4) * (w / 4), y + (i / 4) * 23, w / 4 - 4, 21), L.G(ChannelPacker.Label(all[i]), ChannelPacker.Tip(all[i]))))
                    {
                        o.Channels[choosingChannel].Source = all[i];
                        choosingChannel = -1;
                    }
                y += ((all.Length + 3) / 4) * 23 + 4;
            }
            y += 10;
        }

        // The DDS tool is chosen in Settings; a line recalls which one when a file is DDS.
        if (Profile.Outputs.Any(o => o.Enabled && o.Format == FileFormat.dds))
        {
            GUI.Label(new Rect(x, y, w - 180, 20), L.T("DDS: ") + DdsExport.ToolStatus());
            if (GUI.Button(new Rect(x + w - 170, y, 170, 20), L.G("DDS settings…", "Choose the DDS tool (texconv, NVIDIA, your own) in the preferences.")))
                SettingsGui.instance.Toggle();
            y += 28;
        }

        // ---------- Export ----------
        var names = Profile.Outputs.Where(o => o.Enabled).Select(o => (baseName ?? "") + o.Suffix + "." + (o.Format == FileFormat.tiff ? "tiff" : o.Format.ToString())).ToArray();
        GUI.Label(new Rect(x, y, w - 170, 36), names.Length == 0 ? "No file checked." : "Files: " + string.Join(", ", names));
        GUI.enabled = !exporting && names.Length > 0;
        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
        if (GUI.Button(new Rect(x + w - 160, y, 160, 32), L.G(exporting ? "Exporting…" : "Export", "Writes every checked file.")))
            gui.StartCoroutine(Export(gui));
        GUI.backgroundColor = old;
        GUI.enabled = true;
        y += 38;
        if (status.Length > 0) { GUI.Label(new Rect(x, y, w, 20), status); y += 22; }
        if (GUI.Button(new Rect(x + w - 70, y, 70, 22), L.T("Close"))) Open = false;
        windowRect.height = y + 32;

        Tips.Capture(true);
        GUI.DragWindow();
    }

    static ChannelPacker.Channel C(Source s) => new ChannelPacker.Channel { Source = s };

    static string Short(ChannelPacker.Channel c)
    {
        string s = ChannelPacker.Label(c.Source).Replace("Ambient occlusion", "AO").Replace("Black (none)", "–").Replace("Smoothness", "Smooth").Replace("Roughness", "Rough");
        if (c.Invert) s += "⁻";
        if (c.MultiplyAO > 0) s += "×AO";
        return s;
    }

    static IEnumerator SaveAs()
    {
        yield return null;
        string name = null;
        try { name = NativeFileDialog.Show("Save the export profile as…", "*.json", true); } catch (Exception e) { Notifications.Error(e.Message); }
        if (string.IsNullOrEmpty(name)) yield break;
        var copy = Profile.Copy(Path.GetFileNameWithoutExtension(name));
        ExportProfiles.Save(copy);
        profiles.RemoveAll(p => !p.BuiltIn && p.Name == copy.Name);
        profiles.Add(copy);
        selected = profiles.Count - 1;
        PlayerPrefs.SetString(ProfileKey, copy.Name);
        Notifications.Info("Profile saved: " + copy.Name + " (in " + ExportProfiles.Folder + ")");
    }

    static IEnumerator ChooseFolder()
    {
        yield return null;
        string path = null;
        try { path = NativeFileDialog.Show("Export: choose the folder and the name", "*.*", true); } catch (Exception e) { Notifications.Error(e.Message); }
        if (string.IsNullOrEmpty(path)) yield break;
        folder = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        foreach (var o in Profile.Outputs.Where(o => o.Suffix.Length > 0))
            if (name.EndsWith(o.Suffix, StringComparison.OrdinalIgnoreCase)) { name = name.Substring(0, name.Length - o.Suffix.Length); break; }
        baseName = name;
    }

    static IEnumerator PickTool()
    {
        yield return null;
        string path = null;
        try { path = NativeFileDialog.Show("DDS tool (.exe)", "*.exe", false); } catch (Exception e) { Notifications.Error(e.Message); }
        if (path == null) yield break;
        DdsExport.ToolPath = path;
        SettingsGui.instance.SaveDdsSettings();
    }

    /// <summary>For tests and automation: exports a given profile without the window.</summary>
    public static IEnumerator ExportWith(MainGui gui, ExportProfile profile, string toFolder, string name)
    {
        profiles = new List<ExportProfile> { profile };
        selected = 0;
        folder = toFolder;
        baseName = name;
        return Export(gui);
    }

    public static string LastStatus => status;

    static IEnumerator Export(MainGui gui)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { Notifications.Error("Choose an existing folder first."); yield break; }
        if (string.IsNullOrWhiteSpace(baseName)) { Notifications.Error("Give the files a name first."); yield break; }
        PlayerPrefs.SetString(FolderKey, folder);
        PlayerPrefs.SetString(BaseKey, baseName);
        PlayerPrefs.SetString(ProfileKey, Profile.Name);
        exporting = true;
        Progress = 0;
        var outputs = Profile.Outputs.Where(o => o.Enabled).Select(o => o.Copy()).ToList();
        // Materialize's normals follow the Settings style: Maya = OpenGL (green up), Max = DirectX.
        bool appIsOpenGL = SettingsGui.instance != null && SettingsGui.instance.settings.normalMapMayaStyle;
        var previousDds = DdsExport.Format;
        int written = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            for (int index = 0; index < outputs.Count; index++)
            {
                var o = outputs[index];
                Progress = index / (float)Mathf.Max(1, outputs.Count);
                status = "Writing " + baseName + o.Suffix + "… (" + (index + 1) + "/" + outputs.Count + ")";
                yield return null;
                if (o.Normal != NormalStyle.AsIs && (o.Normal == NormalStyle.OpenGL) != appIsOpenGL)
                    foreach (var ch in o.Channels.Where(c => c.Source == Source.NormalG)) ch.Invert = !ch.Invert;

                Texture2D packed = ChannelPacker.Pack(gui, o.Channels, out string missing);
                if (missing != null) Notifications.Error(baseName + o.Suffix + ": empty maps, black in the file: " + missing);
                packed = Reduce(packed, o.MaxSize);
                string ext = o.Format == FileFormat.tiff ? "tiff" : o.Format.ToString();
                var job = FastImageSaver.Prepare(packed, Path.Combine(folder, baseName + o.Suffix), ext);
                UnityEngine.Object.Destroy(packed);
                // A plain height keeps its 16 bits in PNG and TIFF.
                if (o.IsHeightOnly && gui._HDHeightMap != null && (ext == "png" || ext == "tiff")
                    && gui._HDHeightMap.width == job.Width && gui._HDHeightMap.height == job.Height)
                    job.Grey = FastImageSaver.ReadHeight(gui._HDHeightMap);
                DdsExport.Format = o.Dds;
                var task = System.Threading.Tasks.Task.Run(() => FastImageSaver.Write(job));
                // The file being written moves the bar on slowly (DDS compression can take a while).
                float slice = 1f / Mathf.Max(1, outputs.Count), start = Progress;
                float t0 = Time.realtimeSinceStartup;
                while (!task.IsCompleted)
                {
                    float elapsed = Time.realtimeSinceStartup - t0;
                    Progress = start + slice * (1f - 1f / (1f + elapsed * 0.8f)) * 0.95f;
                    yield return null;
                }
                Progress = start + slice;
                if (task.Result != null) Notifications.Error(task.Result);
                else written++;
            }
        }
        finally
        {
            DdsExport.Format = previousDds;
            exporting = false;
            Progress = 1;
        }
        status = written + " of " + outputs.Count + " files written in " + Math.Round(sw.Elapsed.TotalSeconds, 1) + " s to " + folder;
        Notifications.Info(status);
    }
}
