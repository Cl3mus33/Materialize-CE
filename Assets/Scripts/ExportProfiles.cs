using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Source = ChannelPacker.Source;

public enum NormalStyle { AsIs, OpenGL, DirectX }

/// <summary>One file an export profile writes: its suffix, what goes in each channel, its format.</summary>
[Serializable]
public class ExportOutput
{
    public bool Enabled = true;
    public string Suffix = "";
    public ChannelPacker.Channel[] Channels = new ChannelPacker.Channel[4];
    public FileFormat Format = FileFormat.png;
    public DdsFormat Dds = DdsFormat.BC7;
    /// <summary>Normal maps: converted to the convention the target engine expects.</summary>
    public NormalStyle Normal = NormalStyle.AsIs;
    /// <summary>Largest side of this file in pixels (a parallax map rarely needs 4K); 0 = the maps' own size. Never enlarged.</summary>
    public int MaxSize;

    public bool UsesNormal => Channels.Any(c => c.Source == Source.NormalR || c.Source == Source.NormalG || c.Source == Source.NormalB);
    public bool IsHeightOnly => Channels[0].Source == Source.Height && Channels[1].Source == Source.Height && Channels[2].Source == Source.Height
                                && Channels[3].Source == Source.None && !Channels.Any(c => c.Invert || c.MultiplyAO > 0);

    public ExportOutput Copy() => new ExportOutput
    {
        Enabled = Enabled, Suffix = Suffix, Format = Format, Dds = Dds, Normal = Normal, MaxSize = MaxSize,
        Channels = Channels.Select(ChannelPacker.Copy).ToArray(),
    };
}

/// <summary>A set of files to write in one go (Mixer-style export).</summary>
[Serializable]
public class ExportProfile
{
    public string Name = "New profile";
    public List<ExportOutput> Outputs = new List<ExportOutput>();
    [NonSerialized] public bool BuiltIn;
    /// <summary>The add-on it comes from (Skyrim pack…), or null.</summary>
    [NonSerialized] public string Addon;
    public string Label => Name + (Addon != null ? (Name.IndexOf(Addon, StringComparison.OrdinalIgnoreCase) >= 0 ? "" : "  (" + Addon + ")") : BuiltIn ? "" : "  " + L.T("(yours)"));

    public ExportProfile Copy(string name) => new ExportProfile { Name = name, Outputs = Outputs.Select(o => o.Copy()).ToList() };
}

/// <summary>
/// Export profiles (Materialize CE): built-in ones for common engines, and the user's own, saved as JSON files in
/// Materialize's user folder so they survive updates and can be shared.
/// </summary>
public static class ExportProfiles
{
    public static string Folder => Workspace.Dir(Workspace.ExportProfilesDir);

    static ChannelPacker.Channel C(Source s, bool invert = false, float ao = 0) => new ChannelPacker.Channel { Source = s, Invert = invert, MultiplyAO = ao };
    static ChannelPacker.Channel[] Grey(Source s) => new[] { C(s), C(s), C(s), C(Source.None) };
    static ChannelPacker.Channel[] Rgb(Source r, Source g, Source b, Source a = Source.None) => new[] { C(r), C(g), C(b), C(a) };
    static ChannelPacker.Channel[] Diffuse(float ao = 0) => new[] { C(Source.DiffuseR, false, ao), C(Source.DiffuseG, false, ao), C(Source.DiffuseB, false, ao), C(Source.None) };
    static ChannelPacker.Channel[] Normal(Source alpha = Source.None) => Rgb(Source.NormalR, Source.NormalG, Source.NormalB, alpha);

    static ExportOutput O(string suffix, ChannelPacker.Channel[] channels, FileFormat format, DdsFormat dds = DdsFormat.BC7, NormalStyle normal = NormalStyle.AsIs, bool enabled = true) =>
        new ExportOutput { Suffix = suffix, Channels = channels, Format = format, Dds = dds, Normal = normal, Enabled = enabled };

    public static List<ExportProfile> BuiltIn() => new List<ExportProfile>
    {
        new ExportProfile { Name = "All maps (PNG)", BuiltIn = true, Outputs = new List<ExportOutput> {
            O("_diffuse", Diffuse(), FileFormat.png), O("_normal", Normal(), FileFormat.png), O("_height", Grey(Source.Height), FileFormat.png),
            O("_metallic", Grey(Source.Metallic), FileFormat.png), O("_smoothness", Grey(Source.Smoothness), FileFormat.png),
            O("_curvature", Grey(Source.Edge), FileFormat.png), O("_ao", Grey(Source.AO), FileFormat.png) } },
        new ExportProfile { Name = "Unreal Engine", BuiltIn = true, Outputs = new List<ExportOutput> {
            O("_BaseColor", Diffuse(), FileFormat.png), O("_Normal", Normal(), FileFormat.png, normal: NormalStyle.DirectX),
            O("_ORM", Rgb(Source.AO, Source.Roughness, Source.Metallic), FileFormat.png), O("_Height", Grey(Source.Height), FileFormat.png, enabled: false) } },
        new ExportProfile { Name = "Unity HDRP", BuiltIn = true, Outputs = new List<ExportOutput> {
            O("_BaseMap", Diffuse(), FileFormat.png), O("_Normal", Normal(), FileFormat.png, normal: NormalStyle.OpenGL),
            O("_MaskMap", Rgb(Source.Metallic, Source.AO, Source.None, Source.Smoothness), FileFormat.png), O("_Height", Grey(Source.Height), FileFormat.png, enabled: false) } },
        new ExportProfile { Name = "Specular / Glossiness (PBR)", BuiltIn = true, Outputs = new List<ExportOutput> {
            O("_diffuse", Rgb(Source.DiffuseSpecR, Source.DiffuseSpecG, Source.DiffuseSpecB), FileFormat.png),
            O("_specular", Rgb(Source.SpecularR, Source.SpecularG, Source.SpecularB, Source.Smoothness), FileFormat.png),
            O("_normal", Normal(), FileFormat.png, normal: NormalStyle.OpenGL), O("_ao", Grey(Source.AO), FileFormat.png),
            O("_height", Grey(Source.Height), FileFormat.png, enabled: false) } },
        new ExportProfile { Name = "glTF / Blender / Godot", BuiltIn = true, Outputs = new List<ExportOutput> {
            O("_BaseColor", Diffuse(), FileFormat.png), O("_Normal", Normal(), FileFormat.png, normal: NormalStyle.OpenGL),
            O("_ORM", Rgb(Source.AO, Source.Roughness, Source.Metallic), FileFormat.png), O("_Height", Grey(Source.Height), FileFormat.png) } },
    };

    /// <summary>Profiles of the add-ons (Skyrim pack…): read-only, like the built-in ones.</summary>
    public static List<ExportProfile> LoadAddons(List<string> errors)
    {
        var list = new List<ExportProfile>();
        foreach (var (addon, file) in Workspace.AddonFiles(Workspace.ExportProfilesDir, ".json"))
        {
            try
            {
                var profile = JsonUtility.FromJson<ExportProfile>(File.ReadAllText(file));
                if (profile == null || profile.Outputs == null) continue;
                foreach (var o in profile.Outputs)
                    if (o.Channels == null || o.Channels.Length != 4 || o.Channels.Any(c => c == null))
                        o.Channels = Grey(Source.None);
                profile.BuiltIn = true;
                profile.Addon = addon;
                list.Add(profile);
            }
            catch (Exception e) { errors.Add(addon + " / " + Path.GetFileName(file) + ": " + e.Message); }
        }
        return list;
    }

    public static List<ExportProfile> LoadUser(List<string> errors)
    {
        var list = new List<ExportProfile>();
        if (!Directory.Exists(Folder)) return list;
        foreach (var file in Directory.GetFiles(Folder, "*.json").OrderBy(f => f))
        {
            try
            {
                var profile = JsonUtility.FromJson<ExportProfile>(File.ReadAllText(file));
                if (profile == null || profile.Outputs == null) continue;
                foreach (var o in profile.Outputs)
                    if (o.Channels == null || o.Channels.Length != 4 || o.Channels.Any(c => c == null))
                        o.Channels = Grey(Source.None);
                list.Add(profile);
            }
            catch (Exception e) { errors.Add(Path.GetFileName(file) + ": " + e.Message); }
        }
        return list;
    }

    public static string FileFor(ExportProfile profile)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(profile.Name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return Path.Combine(Folder, (safe.Length == 0 ? "profile" : safe) + ".json");
    }

    public static void Save(ExportProfile profile)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileFor(profile), JsonUtility.ToJson(profile, true));
    }

    public static void Delete(ExportProfile profile)
    {
        var file = FileFor(profile);
        if (File.Exists(file)) File.Delete(file);
    }
}
