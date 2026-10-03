using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Runs built-in export profiles on generated maps. -executeMethod ExportTest.Run, folder in MATERIALIZE_OUT.</summary>
public static class ExportTest
{
    static Texture2D Solid(Color32 c)
    {
        var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        t.SetPixels32(Enumerable.Repeat(c, 64 * 64).ToArray());
        t.Apply();
        return t;
    }

    public static void Run()
    {
        string folder = System.Environment.GetEnvironmentVariable("MATERIALIZE_OUT");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        var gui = new GameObject("ExportTest").AddComponent<MainGui>();
        gui._DiffuseMap = Solid(new Color32(200, 100, 50, 255));
        gui._AOMap = Solid(new Color32(128, 128, 128, 255));
        gui._NormalMap = Solid(new Color32(120, 140, 250, 255));
        gui._SmoothnessMap = Solid(new Color32(64, 64, 64, 255));
        gui._MetallicMap = Solid(new Color32(0, 0, 0, 255));
        gui._HeightMap = Solid(new Color32(90, 90, 90, 255));
        new GameObject("Settings").AddComponent<SettingsGui>();   // normal style: Max (DirectX) by default

        foreach (var name in new[] { "Skyrim SE (vanilla)", "Unity HDRP" })
        {
            var profile = ExportProfiles.BuiltIn().First(p => p.Name == name);
            var routine = ExportWindow.ExportWith(gui, profile, folder, "stone");
            // Drive the coroutine: the saver runs on a worker thread; wait for it between steps.
            var stack = new System.Collections.Generic.Stack<System.Collections.IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is System.Collections.IEnumerator nested) stack.Push(nested);
                System.Threading.Thread.Sleep(5);
            }
            Debug.Log("EXPORTTEST " + name + ": " + ExportWindow.LastStatus);
        }
        foreach (var f in Directory.GetFiles(folder).OrderBy(f => f))
        {
            string info = new FileInfo(f).Length / 1024 + " KB";
            if (f.EndsWith(".png"))
            {
                var px = FastImageLoader.Decode(f);
                info += $" | pixel BGRA {px.Bgra[0]},{px.Bgra[1]},{px.Bgra[2]},{px.Bgra[3]}";
            }
            if (f.EndsWith(".dds"))
            {
                var b = File.ReadAllBytes(f);
                string fourcc = System.Text.Encoding.ASCII.GetString(b, 84, 4);
                int dxgi = fourcc == "DX10" ? System.BitConverter.ToInt32(b, 128) : 0;
                info += $" | {fourcc}{(dxgi > 0 ? " dxgi " + dxgi : "")}";
            }
            Debug.Log("EXPORTTEST file " + Path.GetFileName(f) + ": " + info);
        }
    }
}
