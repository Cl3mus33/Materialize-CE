using UnityEngine;

/// <summary>Checks the channel packer's maths on generated maps. -executeMethod PackerTest.Run</summary>
public static class PackerTest
{
    static Texture2D Solid(Color32 c)
    {
        var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var px = new Color32[64 * 64];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    public static void Run()
    {
        var go = new GameObject("PackerTest");
        var gui = go.AddComponent<MainGui>();
        gui._DiffuseMap = Solid(new Color32(200, 100, 50, 255));
        gui._AOMap = Solid(new Color32(128, 128, 128, 255));
        gui._NormalMap = Solid(new Color32(120, 140, 250, 255));
        gui._SmoothnessMap = Solid(new Color32(64, 64, 64, 255));
        gui._MetallicMap = Solid(new Color32(255, 255, 255, 255));
        gui._HeightMap = Solid(new Color32(30, 30, 30, 255));

        foreach (var preset in ChannelPacker.Presets)
        {
            ChannelPacker.Apply(preset);
            var packed = ChannelPacker.Pack(gui, out string missing);
            Color32 c = packed.GetPixel(10, 10);
            Debug.Log($"PACKTEST {preset.Name}: RGBA = ({c.r}, {c.g}, {c.b}, {c.a}) | format {packed.format} | missing {missing ?? "none"}");
        }
        // Custom: normal green inverted (DirectX), roughness with half AO.
        ChannelPacker.Apply(ChannelPacker.Presets[1]);
        ChannelPacker.Current[1].Invert = true;
        ChannelPacker.Current[3] = new ChannelPacker.Channel { Source = ChannelPacker.Source.Roughness, MultiplyAO = 0.5f };
        var custom = ChannelPacker.Pack(gui, out _);
        Color32 k = custom.GetPixel(10, 10);
        Debug.Log($"PACKTEST custom (G inverted, A = roughness × half AO): ({k.r}, {k.g}, {k.b}, {k.a})");
        string text = ChannelPacker.Serialize();
        ChannelPacker.Apply(ChannelPacker.Presets[0]);
        ChannelPacker.Deserialize(text);
        Debug.Log($"PACKTEST settings round trip: {(ChannelPacker.Serialize() == text ? "same" : "DIFFERENT")} ({text})");
        gui._AOMap = null;
        ChannelPacker.Apply(ChannelPacker.Presets[2]);
        ChannelPacker.Pack(gui, out string absent);
        Debug.Log($"PACKTEST missing AO reported: {absent}");
    }
}
