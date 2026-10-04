using System.IO;
using UnityEngine;

/// <summary>Shading removal: a known albedo is shaded by a known light, then recovered. -executeMethod DelightTest.Run</summary>
public static class DelightTest
{
    const string Out = @"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad";
    const int N = 512;

    public static void Run()
    {
        var height = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float n = 0, amp = 0.5f, f = 0.012f;
            for (int o = 0; o < 4; o++) { n += amp * Mathf.PerlinNoise(x * f + o * 17.3f, y * f + o * 9.1f); amp *= 0.5f; f *= 2.1f; }
            height[y * N + x] = n;
        }
        foreach (float lightAngle in new[] { 40f, 200f })
        {
            var photoTex = new Texture2D(N, N, TextureFormat.RGBA32, true, false);
            var normalTex = new Texture2D(N, N, TextureFormat.RGBA32, true, true);
            var aoTex = new Texture2D(N, N, TextureFormat.RGBA32, true, true);
            float a = lightAngle * Mathf.Deg2Rad, e = 45f * Mathf.Deg2Rad;
            var L = new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e));
            float flat = 0.35f + 0.65f * L.z;
            var albedo = new Color[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                // Albedo: big patches of two colours, unrelated to the relief.
                float patch = Mathf.PerlinNoise(x * 0.006f + 50, y * 0.006f + 80) > 0.5f ? 1 : 0;
                var alb = Color.Lerp(new Color(0.55f, 0.45f, 0.35f), new Color(0.3f, 0.36f, 0.25f), patch);
                float dx = (height[y * N + (x + 1) % N] - height[y * N + (x + N - 1) % N]) * 30f, dy = (height[((y + 1) % N) * N + x] - height[((y + N - 1) % N) * N + x]) * 30f;
                var nrm = new Vector3(-dx, -dy, 1).normalized;   // OpenGL (green up)
                float shade = (0.35f + 0.65f * Mathf.Max(0, Vector3.Dot(nrm, L))) / flat;
                float ao = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(height[y * N + x] * 1.6f));
                float g = Mathf.Pow(shade * ao, 1 / 2.2f);
                albedo[y * N + x] = alb;
                photoTex.SetPixel(x, y, new Color(alb.r * g, alb.g * g, alb.b * g, 1));
                normalTex.SetPixel(x, y, new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1));
                aoTex.SetPixel(x, y, new Color(ao, ao, ao, 1));
            }
            photoTex.Apply(true); normalTex.Apply(true); aoTex.Apply(true);

            bool found = DelightFit.Fit(photoTex, normalTex, true, out float angle, out float strength);

            var mat = new Material(Shader.Find("Hidden/Blit_Delight"));
            mat.SetTexture("_NormalTex", normalTex); mat.SetTexture("_AOTex", aoTex);
            mat.SetFloat("_HasNormal", 1); mat.SetFloat("_HasAO", 1); mat.SetFloat("_FlipNormalY", 1);
            float fa = angle * Mathf.Deg2Rad;
            mat.SetVector("_LightDir", new Vector3(Mathf.Cos(fa) * Mathf.Cos(e), Mathf.Sin(fa) * Mathf.Cos(e), Mathf.Sin(e)));
            mat.SetFloat("_ShadeStrength", 1f); mat.SetFloat("_AOStrength", 1f);
            var rt = new RenderTexture(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(photoTex, rt, mat, 0);
            RenderTexture.active = rt;
            var result = new Texture2D(N, N, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, N, N), 0, 0); result.Apply();
            RenderTexture.active = null;

            double errPhoto = 0, errResult = 0;
            var ph = photoTex.GetPixels(); var rs = result.GetPixels();
            for (int i = 0; i < albedo.Length; i++) { errPhoto += Mathf.Abs(ph[i].g - albedo[i].g); errResult += Mathf.Abs(rs[i].g - albedo[i].g); }
            Debug.Log($"DELIGHT light {lightAngle:0}: found={found} angle {angle:0.0} strength {strength:0.00} | mean error to the true albedo: photo {errPhoto / albedo.Length:0.0000} -> delit {errResult / albedo.Length:0.0000}");

            if (lightAngle == 200f)
            {
                var o = new Texture2D(N * 3, N, TextureFormat.RGB24, false);
                o.SetPixels(0, 0, N, N, ph); o.SetPixels(N, 0, N, N, rs); o.SetPixels(N * 2, 0, N, N, albedo);
                o.Apply(); File.WriteAllBytes(Path.Combine(Out, "delight.png"), o.EncodeToPNG());
            }
        }
    }
}
