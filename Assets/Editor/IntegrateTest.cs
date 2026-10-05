using System.IO;
using UnityEngine;

/// <summary>Height rebuilt from a normal map (NormalIntegrator) against the true height. -executeMethod IntegrateTest.Run</summary>
public static class IntegrateTest
{
    static readonly string Out = System.IO.Path.Combine(System.IO.Path.GetTempPath(), @"MaterializeCE-tests");

    public static void Run()
    {
        foreach (int n in new[] { 512, 4096 })
        {
            // A tiling relief: periodic bumps at several scales.
            var truth = new float[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = x / (float)n * 2 * Mathf.PI, v = y / (float)n * 2 * Mathf.PI;
                truth[y * n + x] = 0.5f + 0.25f * Mathf.Sin(u * 2) * Mathf.Cos(v * 3) + 0.15f * Mathf.Sin(u * 7 + 1) + 0.1f * Mathf.Cos(v * 11 + u * 5);
            }
            foreach (bool greenUp in new[] { true, false })
            {
                var normal = new Color32[n * n];
                float strength = n / 40f;   // slopes of a believable normal map
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (truth[y * n + (x + 1) % n] - truth[y * n + (x + n - 1) % n]) * 0.5f * strength;
                    float dy = (truth[((y + 1) % n) * n + x] - truth[((y + n - 1) % n) * n + x]) * 0.5f * strength;
                    var nrm = new Vector3(-dx, greenUp ? -dy : dy, 1).normalized;
                    normal[y * n + x] = new Color32((byte)(nrm.x * 127.5f + 127.5f), (byte)(nrm.y * 127.5f + 127.5f), (byte)(nrm.z * 127.5f + 127.5f), 255);
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var solved = NormalIntegrator.Integrate(normal, n, n, greenUp);
                sw.Stop();
                // Correlation with the truth (both are only known up to scale and offset).
                double ma = 0, mb = 0; for (int i = 0; i < truth.Length; i++) { ma += truth[i]; mb += solved[i]; }
                ma /= truth.Length; mb /= truth.Length;
                double sab = 0, saa = 0, sbb = 0;
                for (int i = 0; i < truth.Length; i++) { double a = truth[i] - ma, b = solved[i] - mb; sab += a * b; saa += a * a; sbb += b * b; }
                Debug.Log($"INTEGRATE {n}x{n} {(greenUp ? "OpenGL" : "DirectX")}: correlation {sab / System.Math.Sqrt(saa * sbb):0.0000}, {sw.ElapsedMilliseconds} ms");
                if (n == 512 && greenUp)
                {
                    var t = new Texture2D(n * 2, n, TextureFormat.RGB24, false);
                    float tmin = 1, tmax = 0; foreach (var f in truth) { if (f < tmin) tmin = f; if (f > tmax) tmax = f; }
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float a = (truth[y * n + x] - tmin) / (tmax - tmin), b = solved[y * n + x];
                        t.SetPixel(x, y, new Color(a, a, a)); t.SetPixel(x + n, y, new Color(b, b, b));
                    }
                    t.Apply(); File.WriteAllBytes(Path.Combine(Out, "integrate.png"), t.EncodeToPNG());
                }
            }
        }
    }
}
