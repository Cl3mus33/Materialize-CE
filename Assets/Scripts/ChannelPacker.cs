using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Channel packer (Materialize CE), replacing the Property Map's fixed choices: each channel of the output
/// (R, G, B, A) takes any channel of any map, optionally inverted and multiplied by the AO. Presets cover the
/// usual game layouts (Skyrim _n with gloss in alpha, diffuse with baked AO, _rmaos, Complex Material, ORM,
/// Unity mask map). The setup is kept in settings.txt.
/// </summary>
public static class ChannelPacker
{
    public enum Source { None, White, DiffuseR, DiffuseG, DiffuseB, DiffuseGrey, DiffuseAlpha, NormalR, NormalG, NormalB, Height, Metallic, Smoothness, Roughness, AO, Edge,
        SpecularR, SpecularG, SpecularB, DiffuseSpecR, DiffuseSpecG, DiffuseSpecB }

    struct SourceInfo { public string Label, Tip; }

    public static string Label(Source s) => L.T(sources[s].Label);
    public static string Tip(Source s) => L.T(sources[s].Tip);

    static readonly Dictionary<Source, SourceInfo> sources = new Dictionary<Source, SourceInfo>
    {
        { Source.None, new SourceInfo { Label = "Black (none)", Tip = "Leaves the channel black (0)." } },
        { Source.White, new SourceInfo { Label = "White", Tip = "Fills the channel with white (1). Handy for an unused alpha or a full mask." } },
        { Source.DiffuseR, new SourceInfo { Label = "Albedo R", Tip = "Red channel of the albedo (the edited one if there is one, else the original)." } },
        { Source.DiffuseG, new SourceInfo { Label = "Albedo G", Tip = "Green channel of the albedo." } },
        { Source.DiffuseB, new SourceInfo { Label = "Albedo B", Tip = "Blue channel of the albedo." } },
        { Source.DiffuseGrey, new SourceInfo { Label = "Albedo grey", Tip = "Brightness of the albedo (luminance), as one grey channel." } },
        { Source.DiffuseAlpha, new SourceInfo { Label = "Albedo alpha", Tip = "Transparency of the albedo, when the image has one." } },
        { Source.NormalR, new SourceInfo { Label = "Normal R", Tip = "Red of the normal map: the X slope (left / right)." } },
        { Source.NormalG, new SourceInfo { Label = "Normal G", Tip = "Green of the normal map: the Y slope. Invert it to switch between OpenGL and DirectX normals." } },
        { Source.NormalB, new SourceInfo { Label = "Normal B", Tip = "Blue of the normal map: the Z part, mostly bright." } },
        { Source.Height, new SourceInfo { Label = "Height", Tip = "Height / displacement map: white is high." } },
        { Source.Metallic, new SourceInfo { Label = "Metallic", Tip = "Metallic map: white is metal." } },
        { Source.Smoothness, new SourceInfo { Label = "Smoothness", Tip = "Smoothness (= gloss): white is shiny. Skyrim's gloss, Unity's smoothness." } },
        { Source.Roughness, new SourceInfo { Label = "Roughness", Tip = "Roughness = inverted smoothness: white is rough. Unreal, glTF, Skyrim PBR." } },
        { Source.AO, new SourceInfo { Label = "Ambient occlusion", Tip = "AO map: dark in the crevices, white in the open." } },
        { Source.Edge, new SourceInfo { Label = "Edge", Tip = "Edge map: bright on edges and ridges, dark in the hollows." } },
        { Source.SpecularR, new SourceInfo { Label = "Specular R", Tip = "Specular colour (Specular / Glossiness workflow): the albedo on metal, 4 % grey elsewhere. Red channel." } },
        { Source.SpecularG, new SourceInfo { Label = "Specular G", Tip = "Specular colour, green channel." } },
        { Source.SpecularB, new SourceInfo { Label = "Specular B", Tip = "Specular colour, blue channel." } },
        { Source.DiffuseSpecR, new SourceInfo { Label = "Diffuse (spec.) R", Tip = "Diffuse of the Specular / Glossiness workflow: the albedo, black on metal. Red channel." } },
        { Source.DiffuseSpecG, new SourceInfo { Label = "Diffuse (spec.) G", Tip = "Diffuse of the Specular / Glossiness workflow, green channel." } },
        { Source.DiffuseSpecB, new SourceInfo { Label = "Diffuse (spec.) B", Tip = "Diffuse of the Specular / Glossiness workflow, blue channel." } },
    };

    [Serializable]
    public class Channel
    {
        public Source Source = Source.None;
        public bool Invert;
        /// <summary>0 = no AO, 1 = multiplied by the full AO.</summary>
        public float MultiplyAO;
    }

    public sealed class Preset
    {
        public string Name, Suffix, Tip;
        public Channel[] Channels;
    }

    static Channel C(Source s, bool invert = false, float ao = 0) => new Channel { Source = s, Invert = invert, MultiplyAO = ao };

    public static readonly Preset[] Presets =
    {
        new Preset { Name = "Materialize MSAO", Suffix = "_msao", Tip = "Materialize's original property map: R metallic, G smoothness, B ambient occlusion.",
            Channels = new[] { C(Source.Metallic), C(Source.Smoothness), C(Source.AO), C(Source.None) } },
        new Preset { Name = "Skyrim _n + gloss", Suffix = "_n", Tip = "Skyrim / Fallout normal map: normal in RGB, smoothness (gloss, the specular mask) in alpha.",
            Channels = new[] { C(Source.NormalR), C(Source.NormalG), C(Source.NormalB), C(Source.Smoothness) } },
        new Preset { Name = "Diffuse × AO", Suffix = "_d", Tip = "Albedo multiplied by the ambient occlusion: the diffuse for engines without an AO map (Skyrim vanilla and Complex Material).",
            Channels = new[] { C(Source.DiffuseR, false, 1), C(Source.DiffuseG, false, 1), C(Source.DiffuseB, false, 1), C(Source.None) } },
        new Preset { Name = "Skyrim PBR _rmaos", Suffix = "_rmaos", Tip = "Community Shaders PBR: R roughness, G metallic, B ambient occlusion.",
            Channels = new[] { C(Source.Roughness), C(Source.Metallic), C(Source.AO), C(Source.None) } },
        new Preset { Name = "Skyrim CM _m", Suffix = "_m", Tip = "Skyrim Complex Material (ENB, Community Shaders): R environment mask, G gloss, B metal, A height (parallax).",
            Channels = new[] { C(Source.Metallic), C(Source.Smoothness), C(Source.Metallic), C(Source.Height) } },
        new Preset { Name = "ORM (Unreal, glTF)", Suffix = "_ORM", Tip = "Occlusion, Roughness, Metallic: R ambient occlusion, G roughness, B metallic. Unreal Engine, glTF, Godot, Blender.",
            Channels = new[] { C(Source.AO), C(Source.Roughness), C(Source.Metallic), C(Source.None) } },
        new Preset { Name = "Unity Mask Map", Suffix = "_MaskMap", Tip = "Unity HDRP mask map: R metallic, G ambient occlusion, B detail mask (black), A smoothness.",
            Channels = new[] { C(Source.Metallic), C(Source.AO), C(Source.None), C(Source.Smoothness) } },
    };

    public static Channel[] Current = Presets[0].Channels.Select(Copy).ToArray();
    public static string Suffix = Presets[0].Suffix;
    public static string PresetName = Presets[0].Name;

    public static Channel Copy(Channel c) => new Channel { Source = c.Source, Invert = c.Invert, MultiplyAO = c.MultiplyAO };

    public static void Apply(Preset preset)
    {
        Current = preset.Channels.Select(Copy).ToArray();
        Suffix = preset.Suffix;
        PresetName = preset.Name;
    }

    public static bool UsesAlpha => Current[3].Source != Source.None;

    public static string Summary(int channel)
    {
        var c = Current[channel];
        string text = sources[c.Source].Label;
        if (c.Invert) text += " inv.";
        if (c.MultiplyAO > 0) text += " ×AO";
        return text;
    }

    // ---------- Settings ----------

    /// <summary>"name|suffix|src,invert,ao;src,invert,ao;…" for settings.txt.</summary>
    public static string Serialize() =>
        PresetName + "|" + Suffix + "|" + string.Join(";", Current.Select(c => c.Source + "," + (c.Invert ? 1 : 0) + "," + c.MultiplyAO.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    public static void Deserialize(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var parts = text.Split('|');
            var channels = parts[2].Split(';').Select(s =>
            {
                var f = s.Split(',');
                return new Channel { Source = (Source)Enum.Parse(typeof(Source), f[0]), Invert = f[1] == "1", MultiplyAO = float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture) };
            }).ToArray();
            if (channels.Length != 4) return;
            Current = channels;
            PresetName = parts[0];
            Suffix = parts[1];
        }
        catch (Exception e) { Debug.LogWarning("Channel packer settings ignored: " + e.Message); }
    }

    // ---------- Packing ----------

    static Material material;

    /// <summary>Builds the packed texture from the current maps. Missing maps count as black, and are listed in <paramref name="missing"/>.</summary>
    public static Texture2D Pack(MainGui gui, out string missing) => Pack(gui, Current, out missing);

    /// <summary>Builds a texture from any four channel choices (the export profiles use this).</summary>
    public static Texture2D Pack(MainGui gui, Channel[] channels, out string missing)
    {
        if (material == null) material = new Material(Shader.Find("Hidden/Blit_Channel_Pack")) { hideFlags = HideFlags.HideAndDontSave };
        var absent = new List<string>();
        bool needsAO = false;
        var temps = new List<RenderTexture>();
        for (int i = 0; i < 4; i++)
        {
            var c = channels[i];
            Texture tex = TextureOf(gui, c.Source, temps, out Vector4 pick, out bool forceInvert, out string name);
            if (tex == null) { absent.Add(name); tex = Texture2D.blackTexture; pick = new Vector4(1, 0, 0, 0); }
            material.SetTexture("_Tex" + i, tex);
            material.SetVector("_Pick" + i, pick);
            if (c.MultiplyAO > 0) needsAO = true;
            material.SetVector("_Invert", SetComponent(material.GetVector("_Invert"), i, (c.Invert ^ forceInvert) ? 1 : 0));
            material.SetVector("_MulAO", SetComponent(material.GetVector("_MulAO"), i, c.MultiplyAO));
        }
        if (needsAO && gui._AOMap == null) absent.Add("Ambient occlusion");
        material.SetTexture("_AOTex", gui._AOMap != null ? (Texture)gui._AOMap : Texture2D.whiteTexture);

        Vector2 size = gui.GetSize();
        var target = RenderTexture.GetTemporary((int)size.x, (int)size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        Graphics.Blit(Texture2D.blackTexture, target, material, 0);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        bool alpha = channels[3].Source != Source.None;
        var result = new Texture2D(target.width, target.height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, true);
        result.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        result.Apply(true, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        foreach (var rt in temps) RenderTexture.ReleaseTemporary(rt);
        missing = absent.Count == 0 ? null : string.Join(", ", absent.Distinct().ToArray());
        return result;
    }

    static Vector4 SetComponent(Vector4 v, int i, float value) { v[i] = value; return v; }

    static Texture TextureOf(MainGui gui, Source source, List<RenderTexture> temps, out Vector4 pick, out bool forceInvert, out string name)
    {
        pick = new Vector4(1, 0, 0, 0);
        forceInvert = false;
        name = sources[source].Label;
        Texture2D diffuse = gui._DiffuseMap != null ? gui._DiffuseMap : gui._DiffuseMapOriginal;
        switch (source)
        {
            case Source.None: return Texture2D.blackTexture;
            case Source.White: return Texture2D.whiteTexture;
            case Source.DiffuseR: name = "Albedo"; return diffuse;
            case Source.DiffuseG: name = "Albedo"; pick = new Vector4(0, 1, 0, 0); return diffuse;
            case Source.DiffuseB: name = "Albedo"; pick = new Vector4(0, 0, 1, 0); return diffuse;
            case Source.DiffuseGrey: name = "Albedo"; pick = new Vector4(0.2126f, 0.7152f, 0.0722f, 0); return diffuse;
            case Source.DiffuseAlpha: name = "Albedo"; pick = new Vector4(0, 0, 0, 1); return diffuse;
            case Source.NormalR: name = "Normal"; return gui._NormalMap;
            case Source.NormalG: name = "Normal"; pick = new Vector4(0, 1, 0, 0); return gui._NormalMap;
            case Source.NormalB: name = "Normal"; pick = new Vector4(0, 0, 1, 0); return gui._NormalMap;
            case Source.Height: return gui._HeightMap;
            case Source.Metallic: return gui._MetallicMap;
            case Source.Smoothness: return gui._SmoothnessMap;
            case Source.Roughness: name = "Smoothness"; forceInvert = true; return gui._SmoothnessMap;
            case Source.AO: return gui._AOMap;
            case Source.Edge: return gui._EdgeMap;
            case Source.SpecularR: case Source.SpecularG: case Source.SpecularB:
            case Source.DiffuseSpecR: case Source.DiffuseSpecG: case Source.DiffuseSpecB:
            {
                bool spec = source <= Source.SpecularB;
                name = "Albedo";
                if (diffuse == null) return null;
                int component = spec ? source - Source.SpecularR : source - Source.DiffuseSpecR;
                pick = new Vector4(component == 0 ? 1 : 0, component == 1 ? 1 : 0, component == 2 ? 1 : 0, 0);
                // Without a metallic map everything is dielectric: specular 4 % grey, diffuse = albedo.
                var rt = Workflow.Render(diffuse, gui._MetallicMap, spec ? 0 : 1, diffuse.width, diffuse.height);
                temps.Add(rt);
                return rt;
            }
        }
        return null;
    }

    // ---------- Window ----------

    public static bool WindowOpen;
    static int choosing = -1;
    static Rect windowRect = new Rect(0, 0, 640, 470);
    static readonly string[] channelNames = { "R (red)", "G (green)", "B (blue)", "A (alpha)" };

    public static void DrawWindow(MainGui gui)
    {
        if (!WindowOpen) return;
        if (windowRect.x == 0) windowRect.position = new Vector2(Mathf.Max(20, Screen.width / 2 - 320), 290);
        windowRect = GUI.Window(77, windowRect, id => DoWindow(gui), L.T("Channel Packer: build one texture from several maps"));
        Tips.Block(windowRect);
    }

    static void DoWindow(MainGui gui)
    {
        int x = 12, y = 26;
        GUI.Label(new Rect(x, y, 300, 20), L.G("Presets", "Ready-made layouts for common engines. Pick one, then adjust any channel if needed."));
        y += 20;
        for (int i = 0; i < Presets.Length; i++)
        {
            var p = Presets[i];
            int col = i % 4, row = i / 4;
            var old = GUI.backgroundColor;
            if (p.Name == PresetName) GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            if (GUI.Button(new Rect(x + col * 154, y + row * 26, 150, 23), L.G(p.Name, p.Tip + " Suffix: " + p.Suffix)))
            {
                Apply(p);
                choosing = -1;
            }
            GUI.backgroundColor = old;
        }
        y += 2 * 26 + 10;

        for (int ch = 0; ch < 4; ch++)
        {
            var c = Current[ch];
            GUI.Label(new Rect(x, y + 2, 70, 20), L.G(channelNames[ch], "Output channel " + channelNames[ch] + " of the packed texture."));
            GUI.enabled = choosing != ch;
            if (GUI.Button(new Rect(x + 72, y, 150, 22), L.G(sources[c.Source].Label, "Click to choose what goes into " + channelNames[ch] + ". " + sources[c.Source].Tip)))
                choosing = ch;
            GUI.enabled = true;
            bool invert = GUI.Toggle(new Rect(x + 232, y + 2, 70, 20), c.Invert,
                L.G("Invert", "1 − value: turns smoothness into roughness, or flips the normal's green (OpenGL ↔ DirectX)."));
            if (invert != c.Invert) { c.Invert = invert; PresetName = "Custom"; }
            GUI.Label(new Rect(x + 306, y + 2, 60, 20), L.G("× AO", "Multiplies this channel by the ambient occlusion: darkens the crevices. 0 = off, 1 = full AO."));
            float ao = GUI.HorizontalSlider(new Rect(x + 350, y + 6, 100, 16), c.MultiplyAO, 0, 1);
            ao = Mathf.Round(ao * 20) / 20;
            if (Math.Abs(ao - c.MultiplyAO) > 0.001f) { c.MultiplyAO = ao; PresetName = "Custom"; }
            GUI.Label(new Rect(x + 456, y + 2, 40, 20), c.MultiplyAO > 0 ? c.MultiplyAO.ToString("0.00") : "off");
            y += 28;
        }

        // Source picker for the channel being chosen.
        if (choosing >= 0)
        {
            GUI.Box(new Rect(x - 4, y, 620, 118), L.T("Source for ") + channelNames[choosing]);
            var all = (Source[])Enum.GetValues(typeof(Source));
            for (int i = 0; i < all.Length; i++)
            {
                int col = i % 4, row = i / 4;
                if (GUI.Button(new Rect(x + col * 152, y + 22 + row * 23, 148, 21), L.G(sources[all[i]].Label, sources[all[i]].Tip)))
                {
                    Current[choosing].Source = all[i];
                    PresetName = "Custom";
                    choosing = -1;
                }
            }
            y += 124;
        }
        else y += 8;

        GUI.Label(new Rect(x, y + 2, 80, 20), L.G("File suffix", "Added to the file name when saving: stone + _n = stone_n.dds."));
        Suffix = GUI.TextField(new Rect(x + 80, y, 110, 22), Suffix);
        GUI.Label(new Rect(x + 200, y + 2, 420, 20), UsesAlpha ? "Saved with alpha (32-bit)." : "No alpha: saved as RGB (24-bit).");
        y += 32;

        if (GUI.Button(new Rect(x, y, 120, 28), L.G("Preview", "Shows the packed texture on the preview plane.")))
            gui.PreviewPacked();
        if (GUI.Button(new Rect(x + 130, y, 120, 28), L.G("Save…", "Builds the packed texture and saves it in the file format chosen in Saving Options (DDS included).")))
            gui.SavePacked(false);
        GUI.enabled = gui.HasQuickSavePath;
        if (GUI.Button(new Rect(x + 260, y, 120, 28), L.G("Quick Save", "Saves next to the last map you saved, without asking.")))
            gui.SavePacked(true);
        GUI.enabled = true;
        if (GUI.Button(new Rect(x + 390, y, 150, 28), L.G("Keep as default", "Remembers this setup for the next launches.")))
            SettingsGui.instance.SavePackerSettings();
        if (GUI.Button(new Rect(x + 550, y, 60, 28), L.T("Close")))
        {
            WindowOpen = false;
            choosing = -1;
        }
        windowRect.height = y + 40;

        Tips.Capture(true);
        GUI.DragWindow();
    }
}
