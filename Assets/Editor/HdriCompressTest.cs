using System.IO;
using UnityEngine;

/// <summary>Sizes and errors of ways to store a 4K HDRI. -executeMethod HdriCompressTest.Run</summary>
public static class HdriCompressTest
{
    static readonly string Dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), @"MaterializeCE-tests\hdri4k");

    /// <summary>Keeps <paramref name="bits"/> of the 10 mantissa bits of a half: the rest compresses away.</summary>
    static ushort Trim(ushort h, int bits) => (ushort)(h & ~((1 << (10 - bits)) - 1));

    public static void Run()
    {
        string src = Path.Combine(Dir, "toposcope_sunset_4k.hdr");
        string err = FastImageLoader.DecodeEnvironment(src, 4096, out float[] rgba, out int w, out int h, out bool linear);
        if (err != null) { Debug.Log("HDRCOMP " + err); return; }
        Debug.Log($"HDRCOMP source {w}x{h} {new FileInfo(src).Length / 1048576f:0.0} MB");
        foreach (int bits in new[] { 10, 7, 6, 5 })
        {
            var halves = new ushort[w * h * 4];
            for (int i = 0; i < halves.Length; i++)
                halves[i] = (i & 3) == 3 ? Mathf.FloatToHalf(1f) : Trim(Mathf.FloatToHalf(Mathf.Min(rgba[i], 65000f)), bits);
            var t = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
            t.SetPixelData(halves, 0);
            t.Apply();
            foreach (var flag in new[] { Texture2D.EXRFlags.CompressZIP, Texture2D.EXRFlags.CompressPIZ })
            {
                string path = Path.Combine(Dir, $"test_{bits}_{flag}.exr");
                File.WriteAllBytes(path, t.EncodeToEXR(flag));
                // Error measured after reading back through FreeImage, as Materialize will.
                FastImageLoader.DecodeEnvironment(path, 4096, out float[] back, out _, out _, out _);
                double maxRel = 0, sum = 0; int n = 0;
                for (int i = 0; i < back.Length; i += 13)
                {
                    if ((i & 3) == 3 || rgba[i] < 0.01f) continue;
                    double rel = System.Math.Abs(back[i] - rgba[i]) / rgba[i];
                    if (rel > maxRel) maxRel = rel;
                    sum += rel; n++;
                }
                Debug.Log($"HDRCOMP mantissa {bits} bits, {flag}: {new FileInfo(path).Length / 1048576f:0.0} MB, mean error {100 * sum / n:0.000} %, max {100 * maxRel:0.00} %");
            }
            Object.DestroyImmediate(t);
        }
    }
}
