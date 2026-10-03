using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The workflow the maps are shown in (Materialize CE), as in Substance: Metallic or Specular colour, Roughness
/// or Glossiness. Materialize still works with metallic and smoothness underneath; the other maps are derived
/// from them when shown, saved or exported (roughness = inverted smoothness, specular = the albedo on metal and
/// 4 % grey elsewhere). Kept in the preferences and in each project.
/// </summary>
public static class Workflow
{
    const string SpecKey = "MaterializeCE.Workflow.Specular", GlossKey = "MaterializeCE.Workflow.Gloss";

    static bool? specular, gloss;

    /// <summary>Specular colour map instead of the metallic mask.</summary>
    public static bool Specular
    {
        get { if (specular == null) specular = PlayerPrefs.GetInt(SpecKey, 0) == 1; return specular.Value; }
        set { if (Specular == value) return; specular = value; PlayerPrefs.SetInt(SpecKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Glossiness (white = shiny) instead of roughness (white = rough).</summary>
    public static bool Gloss
    {
        get { if (gloss == null) gloss = PlayerPrefs.GetInt(GlossKey, 0) == 1; return gloss.Value; }
        set { if (Gloss == value) return; gloss = value; PlayerPrefs.SetInt(GlossKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static string MetalName => Specular ? "Specular" : "Metallic";
    public static string SurfaceName => Gloss ? "Glossiness" : "Roughness";

    /// <summary>The next file opened or pasted into the roughness row is a roughness map: store it inverted.</summary>
    public static bool InvertNextSmoothnessLoad;

    /// <summary>True when this map is shown differently from how Materialize stores it.</summary>
    public static bool IsDerived(MapType type) =>
        (type == MapType.smoothness && !Gloss) || (type == MapType.metallic && Specular);

    /// <summary>The channels that build the map as shown, for Save and Copy of a derived map.</summary>
    public static ChannelPacker.Channel[] Channels(MapType type)
    {
        ChannelPacker.Channel C(ChannelPacker.Source s) => new ChannelPacker.Channel { Source = s };
        if (type == MapType.metallic)
            return new[] { C(ChannelPacker.Source.SpecularR), C(ChannelPacker.Source.SpecularG), C(ChannelPacker.Source.SpecularB), C(ChannelPacker.Source.None) };
        return new[] { C(ChannelPacker.Source.Roughness), C(ChannelPacker.Source.Roughness), C(ChannelPacker.Source.Roughness), C(ChannelPacker.Source.None) };
    }

    // ---------- Derived textures ----------

    static Material material;

    static Material Mat()
    {
        if (material == null) material = new Material(Shader.Find("Hidden/Blit_Workflow")) { hideFlags = HideFlags.HideAndDontSave };
        return material;
    }

    /// <summary>Renders a derived map (pass 0 specular, 1 diffuse for specular, 2 inverted) into a temporary RT.</summary>
    public static RenderTexture Render(Texture main, Texture metal, int pass, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var m = Mat();
        m.SetTexture("_MetalTex", metal != null ? metal : Texture2D.blackTexture);
        var was = RenderTexture.active;
        Graphics.Blit(main, rt, m, pass);
        RenderTexture.active = was;
        return rt;
    }

    sealed class Thumb { public Texture A, B; public RenderTexture Rt; }
    static readonly Dictionary<MapType, Thumb> thumbs = new Dictionary<MapType, Thumb>();

    /// <summary>The small picture of a derived map for the maps list (made in Update, see <see cref="Tick"/>).</summary>
    public static Texture ThumbOf(MapType type) => thumbs.TryGetValue(type, out var t) ? t.Rt : null;

    /// <summary>Called from MainGui.Update: refreshes the derived thumbnails whose maps changed.</summary>
    public static void Tick(MainGui gui)
    {
        Texture2D diffuse = gui._DiffuseMap != null ? gui._DiffuseMap : gui._DiffuseMapOriginal;
        Refresh(MapType.metallic, Specular && gui._MetallicMap != null && diffuse != null, diffuse, gui._MetallicMap, 0);
        Refresh(MapType.smoothness, !Gloss && gui._SmoothnessMap != null, gui._SmoothnessMap, null, 2);
    }

    static void Refresh(MapType type, bool wanted, Texture a, Texture b, int pass)
    {
        thumbs.TryGetValue(type, out var t);
        if (!wanted)
        {
            if (t != null) { if (t.Rt != null) t.Rt.Release(); thumbs.Remove(type); }
            return;
        }
        if (t != null && t.A == a && t.B == b) return;
        if (t == null) thumbs[type] = t = new Thumb();
        if (t.Rt == null) t.Rt = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        t.A = a; t.B = b;
        var m = Mat();
        m.SetTexture("_MetalTex", b != null ? b : Texture2D.blackTexture);
        var was = RenderTexture.active;
        Graphics.Blit(a, t.Rt, m, pass);
        RenderTexture.active = was;
    }
}
