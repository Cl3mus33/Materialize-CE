using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>DDS through each encoder, and an RGBA map. -executeMethod DdsTest.Run, output folder in MATERIALIZE_OUT.</summary>
public static class DdsTest
{
    public static void Run()
    {
        string folder = Environment.GetEnvironmentVariable("MATERIALIZE_OUT");
        Directory.CreateDirectory(folder);
        // 1024² with a diagonal gradient and an alpha ramp: alpha must survive.
        var texture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        var px = new Color32[1024 * 1024];
        for (int y = 0; y < 1024; y++)
            for (int x = 0; x < 1024; x++)
                px[y * 1024 + x] = new Color32((byte)(x / 4), (byte)(y / 4), 128, (byte)(255 - x / 4));
        texture.SetPixels32(px);
        texture.Apply();

        foreach (var encoder in new[] { DdsEncoder.Texconv, DdsEncoder.Nvidia })
            foreach (var format in new[] { DdsFormat.BC7, DdsFormat.BC1, DdsFormat.BC5 })
            {
                DdsExport.Encoder = encoder;
                DdsExport.Format = format;
                DdsExport.ToolPath = "";
                var sw = Stopwatch.StartNew();
                string error = FastImageSaver.Write(FastImageSaver.Prepare(texture, Path.Combine(folder, $"test_{encoder}_{format}"), "dds"));
                string path = Path.Combine(folder, $"test_{encoder}_{format}.dds");
                string magic = File.Exists(path) ? System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 4) : "-";
                Debug.Log($"DDSTEST {encoder} {format}: {(error ?? "ok")} | {sw.ElapsedMilliseconds} ms | magic '{magic}' | {(File.Exists(path) ? new FileInfo(path).Length / 1024 + " KB" : "no file")} | tool {DdsExport.FindTool()}");
            }

        // RGBA PNG and TGA keep their alpha.
        foreach (var ext in new[] { "png", "tga" })
        {
            string error = FastImageSaver.Write(FastImageSaver.Prepare(texture, Path.Combine(folder, "alpha"), ext));
            var back = FastImageLoader.Decode(Path.Combine(folder, "alpha." + ext));
            // BGRA: alpha is byte 3; pixel x = 0 should be 255, x = 1020 about 0.
            string alpha = back.Error ?? $"alpha at left {back.Bgra[3]}, at right {back.Bgra[1020 * 4 + 3]}";
            Debug.Log($"DDSTEST alpha {ext}: {(error ?? "ok")} | {alpha}");
        }
    }
}
