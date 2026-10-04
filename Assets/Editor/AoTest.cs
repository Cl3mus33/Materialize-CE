using System.IO;
using UnityEngine;

/// <summary>Depth AO of an engraved test relief, classic vs horizon. -executeMethod AoTest.Run</summary>
public static class AoTest
{
    const string Out = @"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad";
    const int N = 512;

    static float Relief(int x, int y)
    {
        float h = 0.8f;
        // Engraved strokes of several widths, a wide hollow, a round bump and a plain ramp.
        if (y > 60 && y < 200) { foreach (var (cx, w) in new[] { (60, 6), (120, 14), (200, 30), (300, 60) }) if (Mathf.Abs(x - cx) < w / 2f) h = 0.3f; }
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(130, 330));
        if (d < 70) h = 0.3f;                                   // wide round hollow
        float b = Vector2.Distance(new Vector2(x, y), new Vector2(330, 330));
        if (b < 60) h = 0.8f + 0.2f * Mathf.Sqrt(1 - b * b / 3600f);   // dome
        if (x > 420) h = 0.3f + 0.5f * (y / (float)N);            // long ramp
        return h;
    }

    public static void Run()
    {
        var hs = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) hs[y * N + x] = Relief(x, y);
        // Slight blur: real height maps have no perfectly vertical walls.
        var bl = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float s = 0; int c = 0;
            for (int j = -2; j <= 2; j++) for (int i = -2; i <= 2; i++) { int xx = Mathf.Clamp(x + i, 0, N - 1), yy = Mathf.Clamp(y + j, 0, N - 1); s += hs[yy * N + xx]; c++; }
            bl[y * N + x] = s / c;
        }
        var height = new Texture2D(N, N, TextureFormat.RFloat, false, true);
        height.SetPixelData(bl, 0); height.Apply();
        var normal = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        var px = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float dx = (bl[y * N + Mathf.Min(x + 1, N - 1)] - bl[y * N + Mathf.Max(x - 1, 0)]) * 40f, dy = (bl[Mathf.Min(y + 1, N - 1) * N + x] - bl[Mathf.Max(y - 1, 0) * N + x]) * 40f;
            var n = new Vector3(-dx, -dy, 1).normalized;
            px[y * N + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
        }
        normal.SetPixels(px); normal.Apply();

        var mat = new Material(Shader.Find("Hidden/Blit_Shader"));
        string tag = System.Environment.GetEnvironmentVariable("MCE_TAG") ?? "";
        for (int mode = 0; mode < 2; mode++)
        {
            var work = new RenderTexture(N, N, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
            var blend = new RenderTexture(N, N, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
            mat.SetVector("_ImageSize", new Vector4(N, N, 0, 0));
            mat.SetFloat("_Spread", 50); mat.SetFloat("_Depth", 100); mat.SetFloat("_HorizonAO", mode);
            mat.SetTexture("_MainTex", normal); mat.SetTexture("_HeightTex", height); mat.SetTexture("_BlendTex", blend);
            for (int i = 1; i < 100; i++)
            {
                mat.SetFloat("_BlendAmount", 1f / i); mat.SetFloat("_Progress", i / 100f);
                Graphics.Blit(normal, work, mat, 7);
                Graphics.Blit(work, blend);
            }
            RenderTexture.active = blend;
            var read = new Texture2D(N, N, TextureFormat.RGBAFloat, false, true);
            read.ReadPixels(new Rect(0, 0, N, N), 0, 0); read.Apply();
            RenderTexture.active = null;
            var o = new Texture2D(N, N, TextureFormat.RGB24, false);
            var src = read.GetPixels(); var dst = new Color[src.Length];
            for (int k = 0; k < src.Length; k++) { float v = Mathf.Clamp01(src[k].g); dst[k] = new Color(v, v, v); }
            o.SetPixels(dst); o.Apply();
            File.WriteAllBytes(Path.Combine(Out, "ao_" + tag + (mode == 0 ? "classic" : "horizon") + ".png"), o.EncodeToPNG());
        }
        {
            // The curvature pass (pass 5), raw, before the frequency mix.
            var crt = new RenderTexture(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            mat.SetVector("_ImageSize", new Vector4(N, N, 0, 0)); mat.SetFloat("_BlurContrast", 4f); mat.SetFloat("_FlipNormalY", 1f);
            mat.SetTexture("_MainTex", normal);
            Graphics.Blit(normal, crt, mat, 5);
            RenderTexture.active = crt;
            var cr = new Texture2D(N, N, TextureFormat.RGB24, false);
            cr.ReadPixels(new Rect(0, 0, N, N), 0, 0); cr.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Out, "ao_" + tag + "curvature.png"), cr.EncodeToPNG());
        }
        var hp = new Texture2D(N, N, TextureFormat.RGB24, false); var hc = new Color[N * N];
        for (int k = 0; k < hc.Length; k++) hc[k] = new Color(bl[k], bl[k], bl[k]);
        hp.SetPixels(hc); hp.Apply(); File.WriteAllBytes(Path.Combine(Out, "ao_height.png"), hp.EncodeToPNG());
        Debug.Log("AOTEST done");
    }
}
