using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>Old ReadPixels + Apply against GpuReadback, at 8K. -executeMethod ReadbackTest.Run</summary>
public static class ReadbackTest
{
    public static void Run()
    {
        const int N = 8192;
        var src = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        var px = new Color32[N * N];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32((byte)(i % 251), (byte)((i / N) % 253), (byte)(i % 7 * 30), 255);
        src.SetPixels32(px);
        src.Apply();
        var rt = new RenderTexture(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(src, rt);

        // Old: everything blocks.
        var sw = Stopwatch.StartNew();
        RenderTexture.active = rt;
        var old = new Texture2D(N, N, TextureFormat.ARGB32, true, true);
        old.ReadPixels(new Rect(0, 0, N, N), 0, 0);
        old.Apply();
        RenderTexture.active = null;
        long oldMs = sw.ElapsedMilliseconds;

        // New: time spent on the main thread, not counting frames where the interface keeps running.
        Texture2D result = null;
        var routine = GpuReadback.Into(rt, t => result = t);
        long blocking = 0;
        sw.Restart();
        while (true)
        {
            sw.Restart();
            bool more = routine.MoveNext();
            blocking += sw.ElapsedMilliseconds;
            if (!more) break;
            AsyncGPUReadback.WaitAllRequests();   // in the app: frames go by here
        }

        var a = old.GetPixels32(0);
        var b = result.GetPixels32(0);
        int diffs = 0;
        for (int i = 0; i < a.Length; i += 997) if (!a[i].Equals(b[i])) diffs++;
        // What the GPU holds (the preview uses it): read level 0 back from the GPU copy.
        var check = new RenderTexture(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(result, check);
        RenderTexture.active = check;
        var gpu = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        gpu.ReadPixels(new Rect(0, 0, N, N), 0, 0);
        RenderTexture.active = null;
        var g = gpu.GetPixels32();
        int gpuDiffs = 0;
        for (int i = 0; i < a.Length; i += 997) if (!a[i].Equals(g[i])) gpuDiffs++;
        Debug.Log($"READBACK GPU copy: {(gpuDiffs == 0 ? "identical" : gpuDiffs + " differ")}");
        Debug.Log($"READBACK 8K: old {oldMs} ms blocking | new {blocking} ms blocking | level 0 {(diffs == 0 ? "identical" : diffs + " samples differ")} | mip levels {result.mipmapCount} (old {old.mipmapCount})");
    }
}
