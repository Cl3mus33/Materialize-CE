using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>A saved look for the preview: render mode, Skyrim settings, material multipliers, light.</summary>
[Serializable]
public class RenderPreset
{
    public string Name = "My preset";
    public int Mode;
    /// <summary>Workflow shown with this preset: specular colour instead of metallic, glossiness instead of roughness.</summary>
    public bool SpecularWorkflow, GlossWorkflow;
    public float SpecStrength = 1, SpecPower = 80, EnvStrength = 1;
    public float Metallic = 1, Smoothness = 1, Parallax = 0.5f, EdgePower = 1, AOPower = 1;
    public float LightR = 1, LightG = 1, LightB = 1, LightIntensity = 1;
    public float SpecColorR = 1, SpecColorG = 1, SpecColorB = 1;
    public bool SkyrimTone = true;
    /// <summary>Materialize's original rendering (shader, cubemap, reflections, shadows), as in Materialize 1.78.</summary>
    public bool OriginalRender;
    /// <summary>PBR lit with Community Shaders' formula (Skyrim True PBR).</summary>
    public bool CommunityShaders;
    public float GameExposure = 1;
    [NonSerialized] public bool BuiltIn;
    [NonSerialized] public string Addon;
    public string Label => Name + (Addon != null ? (Name.IndexOf(Addon, StringComparison.OrdinalIgnoreCase) >= 0 ? "" : "  (" + Addon + ")") : BuiltIn ? "" : "  " + L.T("(yours)"));

    public void ApplyTo(MaterialSettings s)
    {
        s.RenderMode = Mode;
        s.SpecColorR = SpecColorR; s.SpecColorG = SpecColorG; s.SpecColorB = SpecColorB;
        s.SkyrimTone = SkyrimTone;
        Workflow.Specular = SpecularWorkflow;
        Workflow.Gloss = GlossWorkflow;
        s.SpecStrength = SpecStrength; s.SpecStrengthText = SpecStrength.ToString();
        s.SpecPower = SpecPower; s.SpecPowerText = SpecPower.ToString();
        s.EnvStrength = EnvStrength; s.EnvStrengthText = EnvStrength.ToString();
        s.Metallic = Metallic; s.MetallicText = Metallic.ToString();
        s.Smoothness = Smoothness; s.SmoothnessText = Smoothness.ToString();
        s.Parallax = Parallax; s.ParallaxText = Parallax.ToString();
        s.EdgePower = EdgePower; s.EdgePowerText = EdgePower.ToString();
        s.AOPower = AOPower; s.AOPowerText = AOPower.ToString();
        s.LightR = LightR; s.LightG = LightG; s.LightB = LightB;
        s.LightIntensity = LightIntensity; s.LightIntensityText = LightIntensity.ToString();
        s.EnhancedRender = !OriginalRender;
        s.CommunityShaders = CommunityShaders;
        s.GameExposure = GameExposure > 0 ? GameExposure : 1; s.GameExposureText = s.GameExposure.ToString();
        RenderPresets.Applied?.Invoke(this);
    }

    public static RenderPreset From(MaterialSettings s, string name) => new RenderPreset
    {
        Name = name, Mode = s.RenderMode, SpecularWorkflow = Workflow.Specular, GlossWorkflow = Workflow.Gloss, SpecStrength = s.SpecStrength, SpecPower = s.SpecPower, EnvStrength = s.EnvStrength,
        Metallic = s.Metallic, Smoothness = s.Smoothness, Parallax = s.Parallax, EdgePower = s.EdgePower, AOPower = s.AOPower,
        LightR = s.LightR, LightG = s.LightG, LightB = s.LightB, LightIntensity = s.LightIntensity,
        SpecColorR = s.SpecColorR, SpecColorG = s.SpecColorG, SpecColorB = s.SpecColorB, SkyrimTone = s.SkyrimTone,
        OriginalRender = s.RenderMode == 0 && !s.EnhancedRender, CommunityShaders = s.CommunityShaders, GameExposure = s.GameExposure,
    };
}

/// <summary>
/// Render presets (Materialize CE): built-in looks for PBR engines and Skyrim, and the user's own, kept as JSON
/// files in Materialize's user folder (RenderPresets) so they can be backed up and shared.
/// </summary>
public static class RenderPresets
{
    public static string Folder => Workspace.Dir(Workspace.RenderPresetsDir);

    public static List<RenderPreset> BuiltIn() => new List<RenderPreset>
    {
        new RenderPreset { Name = "PBR Metallic / Roughness", Mode = 0, BuiltIn = true },
        new RenderPreset { Name = "PBR Specular / Glossiness", Mode = 0, SpecularWorkflow = true, GlossWorkflow = true, BuiltIn = true },
        new RenderPreset { Name = "Materialize (original)", Mode = 0, OriginalRender = true, BuiltIn = true },
    };

    /// <summary>A preset was chosen (the main window then sets the environment and light for the original look).</summary>
    public static Action<RenderPreset> Applied;

    /// <summary>Same look (everything but the name)?</summary>
    public static bool Same(RenderPreset a, RenderPreset b)
    {
        if (a == null || b == null) return false;
        bool F(float x, float y) => Mathf.Abs(x - y) < 1e-4f;
        return a.Mode == b.Mode && a.SpecularWorkflow == b.SpecularWorkflow && a.GlossWorkflow == b.GlossWorkflow
            && F(a.SpecStrength, b.SpecStrength) && F(a.SpecPower, b.SpecPower) && F(a.EnvStrength, b.EnvStrength)
            && F(a.Metallic, b.Metallic) && F(a.Smoothness, b.Smoothness) && F(a.Parallax, b.Parallax) && F(a.EdgePower, b.EdgePower)
            && F(a.SpecColorR, b.SpecColorR) && F(a.SpecColorG, b.SpecColorG) && F(a.SpecColorB, b.SpecColorB) && a.SkyrimTone == b.SkyrimTone && a.OriginalRender == b.OriginalRender && a.CommunityShaders == b.CommunityShaders && F(a.GameExposure, b.GameExposure)
            && F(a.AOPower, b.AOPower) && F(a.LightR, b.LightR) && F(a.LightG, b.LightG) && F(a.LightB, b.LightB) && F(a.LightIntensity, b.LightIntensity);
    }

    public static List<RenderPreset> All()
    {
        var list = BuiltIn();
        foreach (var (addon, file) in Workspace.AddonFiles(Workspace.RenderPresetsDir, ".json"))
        {
            try
            {
                var p = JsonUtility.FromJson<RenderPreset>(File.ReadAllText(file));
                if (p != null) { p.BuiltIn = true; p.Addon = addon; list.Add(p); }
            }
            catch (Exception e) { Notifications.Error("Render preset ignored: " + addon + " / " + Path.GetFileName(file) + ": " + e.Message); }
        }
        if (!Directory.Exists(Folder)) return list;
        foreach (var file in Directory.GetFiles(Folder, "*.json").OrderBy(f => f))
        {
            try
            {
                var p = JsonUtility.FromJson<RenderPreset>(File.ReadAllText(file));
                if (p != null) list.Add(p);
            }
            catch (Exception e) { Notifications.Error("Render preset ignored: " + Path.GetFileName(file) + ": " + e.Message); }
        }
        return list;
    }

    static string FileFor(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return Path.Combine(Folder, (safe.Length == 0 ? "preset" : safe) + ".json");
    }

    public static void Save(RenderPreset preset)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileFor(preset.Name), JsonUtility.ToJson(preset, true));
    }

    public static void Delete(RenderPreset preset)
    {
        var f = FileFor(preset.Name);
        if (File.Exists(f)) File.Delete(f);
    }
}
