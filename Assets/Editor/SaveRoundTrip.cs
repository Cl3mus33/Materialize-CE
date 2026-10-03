using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Saves a picture in every format and reads it back. -executeMethod SaveRoundTrip.Run, source in MATERIALIZE_BENCH.</summary>
public static class SaveRoundTrip
{
    public static void Run()
    {
        string source = Environment.GetEnvironmentVariable("MATERIALIZE_BENCH");
        var px = FastImageLoader.Decode(source);
        if (px.Error != null) { Debug.Log("ROUNDTRIP source failed: " + px.Error); return; }
        var texture = FastImageLoader.ToTexture(px);
        string folder = Path.Combine(Path.GetDirectoryName(source), "saved é");
        Directory.CreateDirectory(folder);
        foreach (var ext in new[] { "png", "jpg", "tga", "bmp", "tiff" })
        {
            var sw = Stopwatch.StartNew();
            var job = FastImageSaver.Prepare(texture, Path.Combine(folder, "roundtrip"), ext);
            long prepare = sw.ElapsedMilliseconds;
            sw.Restart();
            string error = FastImageSaver.Write(job);
            long write = sw.ElapsedMilliseconds;
            var back = error == null ? FastImageLoader.Decode(job.Path) : null;
            int worst = 0;
            if (back != null && back.Error == null)
                for (int i = 0; i < back.Bgra.Length; i += 997) worst = Math.Max(worst, Math.Abs(back.Bgra[i] - px.Bgra[i]));
            Debug.Log($"ROUNDTRIP {ext}: {(error ?? back?.Error ?? "ok")} | main thread {prepare} ms, worker {write} ms | {new FileInfo(job.Path).Length / 1024} KB | max diff {worst}{(ext == "jpg" ? " (lossy)" : "")}");
        }
        string leftovers = string.Join(", ", Directory.GetFiles(folder, "*.saving"));
        Debug.Log("ROUNDTRIP leftovers: " + (leftovers.Length == 0 ? "none" : leftovers));
    }
}
