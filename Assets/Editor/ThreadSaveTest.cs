using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Writes each format from a worker thread, as the app does. -executeMethod ThreadSaveTest.Run</summary>
public static class ThreadSaveTest
{
    public static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "materialize-thread-test");
        Directory.CreateDirectory(folder);
        var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        tex.Apply();
        foreach (var ext in new[] { "png", "jpg", "bmp", "tga", "tiff" })
        {
            var job = FastImageSaver.Prepare(tex, Path.Combine(folder, "t"), ext);
            string result;
            try { result = Task.Run(() => FastImageSaver.Write(job)).Result ?? "ok"; }
            catch (System.Exception e) { result = "EXCEPTION " + e.GetBaseException().Message; }
            Debug.Log($"THREADTEST {ext}: {result} | file {(File.Exists(job.Path) ? "exists" : "MISSING")}");
        }
    }
}
