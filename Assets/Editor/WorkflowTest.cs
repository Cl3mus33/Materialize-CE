using UnityEngine;

/// <summary>Checks the Specular / Glossiness derivations. -executeMethod WorkflowTest.Run</summary>
public static class WorkflowTest
{
    static Texture2D Solid(Color32 left, Color32 right)
    {
        var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var px = new Color32[64 * 64];
        for (int i = 0; i < px.Length; i++) px[i] = (i % 64) < 32 ? left : right;
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    public static void Run()
    {
        var gui = new GameObject("WorkflowTest").AddComponent<MainGui>();
        gui._DiffuseMap = Solid(new Color32(200, 100, 50, 255), new Color32(200, 100, 50, 255));
        gui._MetallicMap = Solid(new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255));   // left dielectric, right metal
        gui._SmoothnessMap = Solid(new Color32(64, 64, 64, 255), new Color32(64, 64, 64, 255));
        ChannelPacker.Channel C(ChannelPacker.Source s) => new ChannelPacker.Channel { Source = s };
        var spec = ChannelPacker.Pack(gui, new[] { C(ChannelPacker.Source.SpecularR), C(ChannelPacker.Source.SpecularG), C(ChannelPacker.Source.SpecularB), C(ChannelPacker.Source.None) }, out _);
        var diff = ChannelPacker.Pack(gui, new[] { C(ChannelPacker.Source.DiffuseSpecR), C(ChannelPacker.Source.DiffuseSpecG), C(ChannelPacker.Source.DiffuseSpecB), C(ChannelPacker.Source.None) }, out _);
        var rough = ChannelPacker.Pack(gui, new[] { C(ChannelPacker.Source.Roughness), C(ChannelPacker.Source.Roughness), C(ChannelPacker.Source.Roughness), C(ChannelPacker.Source.None) }, out _);
        Color32 a = spec.GetPixel(10, 10), b = spec.GetPixel(50, 10), c = diff.GetPixel(10, 10), d = diff.GetPixel(50, 10), r = rough.GetPixel(10, 10);
        Debug.Log($"WFTEST specular dielectric {a} (expect ~57 grey), metal {b} (expect 200,100,50)");
        Debug.Log($"WFTEST diffuse(spec) dielectric {c} (expect 200,100,50), metal {d} (expect black)");
        Debug.Log($"WFTEST roughness {r} (expect 191)");
    }
}
