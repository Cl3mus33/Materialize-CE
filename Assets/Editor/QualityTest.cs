using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Measures the CE quality changes on synthetic heights. -executeMethod QualityTest.Run, temp folder in MATERIALIZE_OUT.</summary>
public static class QualityTest
{
    const int N = 256;

    static RenderTexture HeightRT(System.Func<int, int, float> f)
    {
        var tex = new Texture2D(N, N, TextureFormat.RFloat, false, true);
        var data = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) data[y * N + x] = f(x, y);
        tex.SetPixelData(data, 0);
        tex.Apply();
        var rt = new RenderTexture(N, N, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) { wrapMode = TextureWrapMode.Repeat };
        Graphics.Blit(tex, rt);
        return rt;
    }

    static Color[] Read(RenderTexture rt)
    {
        RenderTexture.active = rt;
        var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        t.Apply();
        RenderTexture.active = null;
        return t.GetPixels();
    }

    public static void Run()
    {
        var mat = new Material(Shader.Find("Hidden/Blit_Shader"));
        mat.SetVector("_ImageSize", new Vector4(N, N, 0, 0));
        mat.SetFloat("_ShapeRecognition", 0);
        mat.SetTexture("_LightTex", Texture2D.blackTexture);
        mat.SetTexture("_LightBlurTex", Texture2D.blackTexture);
        float strength = 20f;
        mat.SetFloat("_BlurContrast", strength);

        // A gentle slope along X with noise of ±1/255 (like an 8-bit photo-derived height).
        var rng = new System.Random(3);
        float slope = 0.5f / N;
        var noisy = HeightRT((x, y) => 0.25f + x * slope + ((float)rng.NextDouble() - 0.5f) / 255f);
        var outRT = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        // Expected normal of the clean slope: (-s, 0, 1) normalised, s = slope × strength.
        var expected = new Vector3(-slope * strength, 0, 1).normalized;
        foreach (float scharr in new[] { 0f, 1f })
        {
            mat.SetFloat("_ScharrNormal", scharr);
            Graphics.Blit(noisy, outRT, mat, 3);
            var px = Read(outRT);
            double err = 0; int n = 0;
            for (int y = 8; y < N - 8; y++) for (int x = 8; x < N - 8; x++)
            {
                var c = px[y * N + x];
                var v = new Vector3(c.r * 2 - 1, c.g * 2 - 1, c.b * 2 - 1).normalized;
                err += Vector3.Angle(v, expected); n++;
            }
            Debug.Log($"QUALITY normal {(scharr > 0 ? "Scharr" : "classic")}: mean error {err / n:0.00}° from the true slope");
        }

        // AO on a pure slope (nothing to occlude) and in a pit.
        mat.SetFloat("_Spread", 50f);
        mat.SetFloat("_Depth", 100f);
        mat.SetFloat("_BlendAmount", 1f);
        mat.SetTexture("_BlendTex", Texture2D.blackTexture);
        var flatNormal = new Texture2D(4, 4); flatNormal.SetPixels(Enumerable.Repeat(new Color(0.5f, 0.5f, 1, 1), 16).ToArray()); flatNormal.Apply();
        var slopeRT = HeightRT((x, y) => x / (float)N);
        var pit = HeightRT((x, y) => Mathf.Clamp01(Mathf.Sqrt((x - N / 2f) * (x - N / 2f) + (y - N / 2f) * (y - N / 2f)) / 40f));
        var aoRT = new RenderTexture(N, N, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
        foreach (float horizon in new[] { 0f, 1f })
        {
            mat.SetFloat("_HorizonAO", horizon);
            float slopeAO = 0, pitAO = 0;
            // Average over directions, like the panel's 99 passes.
            for (int d = 0; d < 16; d++)
            {
                mat.SetFloat("_Progress", d / 16f);
                mat.SetTexture("_HeightTex", slopeRT);
                Graphics.Blit(flatNormal, aoRT, mat, 7);
                slopeAO += Read(aoRT)[(N / 2) * N + N / 2].g / 16f;
                mat.SetTexture("_HeightTex", pit);
                Graphics.Blit(flatNormal, aoRT, mat, 7);
                pitAO += Read(aoRT)[(N / 2) * N + N / 2].g / 16f;
            }
            Debug.Log($"QUALITY depth AO {(horizon > 0 ? "horizon" : "classic")}: plain slope {slopeAO:0.00} (1 = no occlusion), pit bottom {pitAO:0.00}");
        }

        // 16-bit height: saved and read back.
        string folder = System.Environment.GetEnvironmentVariable("MATERIALIZE_OUT");
        Directory.CreateDirectory(folder);
        var grey = new float[N * N];
        for (int i = 0; i < grey.Length; i++) grey[i] = (i % N) / (float)(N - 1) * 0.1f;   // a gentle ramp: only 26 levels in 8 bits
        var dummy = new Texture2D(N, N, TextureFormat.RGBA32, false);
        foreach (var ext in new[] { "png", "tiff" })
        {
            var job = FastImageSaver.Prepare(dummy, Path.Combine(folder, "height16"), ext);
            job.Grey = grey;
            string error = FastImageSaver.Write(job);
            var back = FastImageLoader.Decode(job.Path);
            int levels8 = back.Bgra == null ? 0 : Enumerable.Range(0, N).Select(x => back.Bgra[x * 4]).Distinct().Count();
            int levelsHd = back.Grey == null ? 0 : back.Grey.Take(N).Distinct().Count();
            Debug.Log($"QUALITY height16 {ext}: {(error ?? back.Error ?? "ok")} | distinct levels across the ramp: 8-bit view {levels8}, high precision {levelsHd} of {N}");
        }
    }
}
