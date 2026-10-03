using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Old import path (FreeImage → temp PNG → decode) against FastImageLoader. -executeMethod LoadBenchmark.Run, files in MATERIALIZE_BENCH (;-separated).</summary>
public static class LoadBenchmark
{
    [DllImport("FreeImage")] static extern IntPtr FreeImage_Load(int fif, string filename, int flags);
    [DllImport("FreeImage")] static extern bool FreeImage_Save(int fif, IntPtr dib, string filename, int flags);
    [DllImport("FreeImage")] static extern void FreeImage_Unload(IntPtr dib);
    [DllImport("FreeImage")] static extern int FreeImage_GetFileType(string filename, int size);

    public static void Run()
    {
        foreach (var path in (Environment.GetEnvironmentVariable("MATERIALIZE_BENCH") ?? "").Split(';'))
        {
            if (path.Length == 0) continue;
            // Old: everything on the main thread, through a temporary PNG.
            var sw = Stopwatch.StartNew();
            Texture2D oldTex = null;
            string temp = Path.Combine(Path.GetTempPath(), "materialize-bench.png");
            IntPtr dib = FreeImage_Load(FreeImage_GetFileType(path, 0), path, 0);
            bool saved = dib != IntPtr.Zero && FreeImage_Save(13 /*PNG*/, dib, temp, 0);
            if (dib != IntPtr.Zero) FreeImage_Unload(dib);
            if (saved) { oldTex = new Texture2D(2, 2); oldTex.LoadImage(File.ReadAllBytes(temp)); }
            long oldMs = sw.ElapsedMilliseconds;

            // New: decode (worker thread in the app), then upload.
            sw.Restart();
            var px = FastImageLoader.Decode(path);
            long decodeMs = sw.ElapsedMilliseconds;
            sw.Restart();
            Texture2D newTex = px.Error == null ? FastImageLoader.ToTexture(px) : null;
            long uploadMs = sw.ElapsedMilliseconds;

            string same = "n/a";
            if (oldTex != null && newTex != null)
            {
                int diffs = 0;
                for (int i = 0; i < 2000; i++)
                {
                    int x = (i * 7919) % newTex.width, y = (i * 104729) % newTex.height;
                    Color32 a = oldTex.GetPixel(x, y), b = newTex.GetPixel(x, y);
                    if (Math.Abs(a.r - b.r) > 1 || Math.Abs(a.g - b.g) > 1 || Math.Abs(a.b - b.b) > 1) diffs++;
                }
                same = diffs == 0 ? "identical" : diffs + "/2000 differ";
            }
            Debug.Log($"BENCH {Path.GetFileName(path)}: old {(oldTex == null ? "FAILED" : oldMs + " ms (UI frozen)")} | new decode {decodeMs} ms (background) + upload {uploadMs} ms (UI) | {(px.Error ?? "ok")} | pixels {same}");
        }
    }
}
