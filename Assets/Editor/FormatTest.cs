using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Loads and saves every image format through FreeImage and prints a fingerprint of each result, so two
/// FreeImage builds can be compared line by line. -executeMethod FormatTest.Run
/// </summary>
public static class FormatTest
{
    static string Fingerprint(FastImageLoader.Pixels p)
    {
        if (p.Error != null) return "ERROR " + p.Error;
        long sum = 0;
        for (int i = 0; i < p.Bgra.Length; i += 7) sum += p.Bgra[i];
        string grey = "";
        if (p.Grey != null)
        {
            double g = 0;
            for (int i = 0; i < p.Grey.Length; i += 7) g += p.Grey[i];
            grey = $" grey={g:0.000}";
        }
        return $"{p.Width}x{p.Height} sum={sum}{grey}";
    }

    public static void Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mce-formats");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        // A test picture: gradients and a pattern, with alpha.
        var t = new Texture2D(96, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 96; x++)
                t.SetPixel(x, y, new Color(x / 95f, y / 63f, ((x / 8 + y / 8) & 1) * 0.8f, 0.5f + 0.5f * (x / 95f)));
        t.Apply();

        var log = new StringBuilder();
        // Written by FreeImage (the saver) in every format it handles.
        foreach (var ext in new[] { "png", "jpg", "tga", "tiff", "bmp" })
        {
            var job = FastImageSaver.Prepare(t, Path.Combine(dir, "saved"), ext);
            string err = FastImageSaver.Write(job);
            string file = Path.Combine(dir, "saved." + ext);
            log.AppendLine($"FMT save {ext}: {(err ?? "ok")} -> load {Fingerprint(FastImageLoader.Decode(file))}");
        }
        // 16-bit height through the saver.
        {
            var job = FastImageSaver.Prepare(t, Path.Combine(dir, "height16"), "png");
            var grey = new float[96 * 64];
            for (int i = 0; i < grey.Length; i++) grey[i] = (i % 96) / 95f * 0.37f + 0.3f;
            job.Grey = grey;
            string err = FastImageSaver.Write(job);
            log.AppendLine($"FMT save png16: {(err ?? "ok")} -> load {Fingerprint(FastImageLoader.Decode(Path.Combine(dir, "height16.png")))}");
        }
        // Floating point: EXR written by Unity, and the bundled Poly Haven EXRs.
        var f = new Texture2D(64, 32, TextureFormat.RGBAFloat, false, true);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++) f.SetPixel(x, y, new Color(x / 8f, y / 16f, 0.25f, 1));
        f.Apply();
        File.WriteAllBytes(Path.Combine(dir, "float.exr"), f.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
        log.AppendLine("FMT load exr: " + Fingerprint(FastImageLoader.Decode(Path.Combine(dir, "float.exr"))));
        string e2 = FastImageLoader.DecodeEnvironment(Path.Combine(dir, "float.exr"), 4096, out float[] rgba, out int w, out int h, out bool linear);
        log.AppendLine($"FMT env exr: {(e2 ?? "ok")} {w}x{h} linear={linear} px(63,31)={(rgba == null ? 0 : rgba[(31 * 64 + 63) * 4]):0.000}");
        foreach (var hdri in Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "HDRI"), "*.exr"))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string e3 = FastImageLoader.DecodeEnvironment(hdri, 4096, out rgba, out w, out h, out linear);
            double s = 0; if (rgba != null) for (int i = 0; i < rgba.Length; i += 997) s += rgba[i];
            log.AppendLine($"FMT hdri {Path.GetFileName(hdri)}: {(e3 ?? "ok")} {w}x{h} sum={s:0.00} {sw.ElapsedMilliseconds} ms");
            break;   // one is enough to compare
        }
        // Radiance HDR: written through FreeImage's own saver is not available here; the EXR path covers floats.
        Debug.Log("FORMATTEST\n" + log);
        File.WriteAllText(Path.Combine(dir, "results.txt"), log.ToString());
    }
}
