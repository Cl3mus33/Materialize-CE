using System.IO;
using UnityEngine;

/// <summary>Reports, for each source HDRI, whether the app finds a sun and how high it is. -executeMethod HdriSunTest.Run</summary>
public static class HdriSunTest
{
    const string Source = @"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad\hdri4k";

    public static void Run()
    {
        var files = new System.Collections.Generic.List<string>(Directory.GetFiles(Source, "*_4k.hdr"));
        files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath, "StreamingAssets", "HDRI"), "*.exr"));   // the packed ones, as the app reads them
        foreach (var file in files)
        {
            string error = FastImageLoader.DecodeEnvironment(file, 4096, out float[] rgba, out int w, out int h, out bool linear);
            if (error != null) { Debug.Log("HDRISUN " + error); continue; }
            float max = 0; double sum = 0;
            for (int i = 0; i < rgba.Length; i += 4) { float l = 0.2126f * rgba[i] + 0.7152f * rgba[i + 1] + 0.0722f * rgba[i + 2]; sum += l; if (l > max) max = l; }
            var sun = MainGui.FindSun(rgba, w, h);
            string where = sun.HasValue ? $"sun elevation {Mathf.Asin(sun.Value.y) * Mathf.Rad2Deg:0} deg" : "NO SUN";
            Debug.Log($"HDRISUN {Path.GetFileNameWithoutExtension(file)}: {where}, peak {max:0.0}, mean {sum / (rgba.Length / 4):0.000}");
        }
    }
}
