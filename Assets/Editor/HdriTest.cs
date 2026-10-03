using System.IO;
using UnityEngine;

/// <summary>Checks the HDRI decoder on a generated EXR. -executeMethod HdriTest.Run</summary>
public static class HdriTest
{
    public static void Run()
    {
        var t = new Texture2D(64, 32, TextureFormat.RGBAFloat, false, true);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 64; x++)
                t.SetPixel(x, y, y < 16 ? new Color(0.1f, 0.2f, 0.3f, 1) : new Color(8f, 4f, 2f, 1));   // bottom dim, top a bright sky
        t.Apply();
        string path = Path.Combine(Path.GetTempPath(), "hdritest.exr");
        File.WriteAllBytes(path, t.EncodeToEXR(Texture2D.EXRFlags.None));
        string error = FastImageLoader.DecodeEnvironment(path, 32, out float[] rgba, out int w, out int h, out bool linear);
        Debug.Log($"HDRITEST error={error ?? "none"} size={w}x{h} linear={linear}");
        if (error == null)
            Debug.Log($"HDRITEST bottom row=({rgba[0]:0.00},{rgba[1]:0.00},{rgba[2]:0.00}) top row=({rgba[(h - 1) * w * 4]:0.00},{rgba[(h - 1) * w * 4 + 1]:0.00},{rgba[(h - 1) * w * 4 + 2]:0.00}) (expect 0.1,0.2,0.3 / 8,4,2)");
    }
}
