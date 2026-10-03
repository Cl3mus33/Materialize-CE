using System.IO;
using UnityEngine;

/// <summary>
/// Packs the Poly Haven HDRIs (CC0) shipped with Materialize CE: 4096 × 2048, half float, PIZ, 6 of the 10 mantissa
/// bits kept (mean deviation ~0.1 %, invisible; a third smaller). -executeMethod HdriPack.Run
/// </summary>
public static class HdriPack
{
    const string Source = @"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad\hdri4k";

    public static void Run()
    {
        string dst = Path.Combine(Application.dataPath, "StreamingAssets", "HDRI");
        foreach (var old in Directory.GetFiles(dst, "*.exr")) { File.Delete(old); if (File.Exists(old + ".meta")) File.Delete(old + ".meta"); }
        foreach (var file in Directory.GetFiles(Source, "*_4k.hdr"))
        {
            string error = FastImageLoader.DecodeEnvironment(file, 4096, out float[] rgba, out int w, out int h, out bool linear);
            if (error != null) { Debug.Log("HDRIPACK " + error); continue; }
            var halves = new ushort[rgba.Length];
            for (int i = 0; i < halves.Length; i++)
                halves[i] = (i & 3) == 3 ? Mathf.FloatToHalf(1f) : (ushort)(Mathf.FloatToHalf(Mathf.Min(rgba[i], 65000f)) & ~0xF);
            var t = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
            t.SetPixelData(halves, 0);
            t.Apply();
            string name = Path.GetFileNameWithoutExtension(file).Replace("_4k", "") + ".exr";
            File.WriteAllBytes(Path.Combine(dst, name), t.EncodeToEXR(Texture2D.EXRFlags.CompressPIZ));
            Debug.Log($"HDRIPACK {name} {w}x{h} {new FileInfo(Path.Combine(dst, name)).Length / 1048576f:0.0} MB");
            Object.DestroyImmediate(t);
        }
    }
}
