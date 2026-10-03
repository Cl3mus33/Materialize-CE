using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Opens BC7, BC5 and BC1 DDS files through FastImageLoader. -executeMethod DdsReadTest.Run</summary>
public static class DdsReadTest
{
    public static void Run()
    {
        FastImageLoader.FindTexconv();
        string dir = Path.Combine(Path.GetTempPath(), "mce-ddsread");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        // A normal-like picture: R = X, G = Y, B = Z.
        var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float nx = x / 63f * 0.6f - 0.3f, ny = y / 63f * 0.6f - 0.3f, nz = Mathf.Sqrt(1 - nx * nx - ny * ny);
            t.SetPixel(x, y, new Color(nx * 0.5f + 0.5f, ny * 0.5f + 0.5f, nz * 0.5f + 0.5f, 1));
        }
        t.Apply();
        string png = Path.Combine(dir, "src.png");
        File.WriteAllBytes(png, t.EncodeToPNG());
        string texconv = Path.Combine(Application.streamingAssetsPath, "texconv.exe");
        foreach (var fmt in new[] { "BC7_UNORM", "BC7_UNORM_SRGB", "BC5_UNORM", "BC1_UNORM" })
        {
            var p = Process.Start(new ProcessStartInfo(texconv, $"-nologo -y -f {fmt} -m 1 -sx _{fmt} -o \"{dir}\" \"{png}\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true });
            p.StandardOutput.ReadToEnd(); p.WaitForExit();
            string dds = Path.Combine(dir, "src_" + fmt + ".dds");
            var px = FastImageLoader.Decode(dds);
            if (px.Error != null) { Debug.Log($"DDSREAD {fmt}: ERROR {px.Error}"); continue; }
            // Pixel (48, 16) in BGRA, rows bottom first.
            int i = (16 * px.Width + 48) * 4;
            Color32 src = t.GetPixel(48, 16);
            Debug.Log($"DDSREAD {fmt}: {px.Width}x{px.Height} pixel RGB ({px.Bgra[i + 2]}, {px.Bgra[i + 1]}, {px.Bgra[i]}) source ({src.r}, {src.g}, {src.b})");
        }
    }
}
