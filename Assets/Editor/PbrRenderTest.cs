using System.IO;
using UnityEngine;

/// <summary>Renders a bumpy test material with the rendering-quality options off and on. -executeMethod PbrRenderTest.Run</summary>
public static class PbrRenderTest
{
    const string Out = @"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad";

    static Texture2D Make(System.Func<int, int, Color> f, bool linear)
    {
        var t = new Texture2D(512, 512, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++) t.SetPixel(x, y, f(x, y));
        t.Apply(true);
        return t;
    }

    static float Bump(int x, int y)
    {
        if (System.Environment.GetEnvironmentVariable("MCE_NOISY") == "1")
        {
            float n = 0, amp = 0.5f, f = 0.02f;
            for (int o = 0; o < 5; o++) { n += amp * Mathf.PerlinNoise(x * f + o * 17.3f, y * f + o * 9.1f); amp *= 0.5f; f *= 2.1f; }
            return Mathf.Clamp01(n);
        }
        // Rounded stones on a grid, plus fine grain.
        float cx = (x % 64) - 32f, cy = (y % 64) - 32f;
        float d = Mathf.Sqrt(cx * cx + cy * cy) / 26f;
        float stone = d < 1 ? Mathf.Sqrt(1 - d * d) : 0;
        return Mathf.Clamp01(0.15f + 0.8f * stone + 0.05f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
    }

    public static void Build(out Material mat, out Camera cam, out RenderTexture rt)
    {
        built = true;
        Run();
        mat = lastMat; cam = lastCam; rt = lastRt;
    }
    static bool built;
    static Material lastMat; static Camera lastCam; static RenderTexture lastRt;

    public static void Run()
    {
        var height = Make((x, y) => { float h = Bump(x, y); return new Color(h, h, h, 1); }, true);
        var normal = Make((x, y) =>
        {
            float dx = (Bump(x + 1, y) - Bump(x - 1, y)) * 3f, dy = (Bump(x, y + 1) - Bump(x, y - 1)) * 3f;
            var n = new Vector3(-dx, dy, 1).normalized;   // DirectX-style green, as Materialize's default
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
        }, true);
        var ao = Make((x, y) =>
        {
            // Realistic AO: white on open ground and on top of the stones, dark only where they meet the ground.
            float cx = (x % 64) - 32f, cy = (y % 64) - 32f, d = Mathf.Sqrt(cx * cx + cy * cy) / 26f;
            float a = 1f - 0.65f * Mathf.Exp(-Mathf.Pow((d - 1f) / 0.12f, 2));
            return new Color(a, a, a, 1);
        }, true);
        var albedo = Make((x, y) => y > 448 ? new Color(1f, 0.78f, 0.34f, 1) : (Bump(x, y) > 0.3f ? new Color(0.62f, 0.58f, 0.52f, 1) : new Color(0.42f, 0.33f, 0.24f, 1)), false);
        var smooth = Make((x, y) => y > 448 ? new Color(0.85f, 0.85f, 0.85f, 1) : (Bump(x, y) > 0.3f ? new Color(0.45f, 0.45f, 0.45f, 1) : new Color(0.2f, 0.2f, 0.2f, 1)), true);
        var metal = Make((x, y) => y > 448 ? Color.white : Color.black, true);

        var mat = new Material(Shader.Find("Custom/Preview_PBR"));
        mat.SetTexture("_DiffuseMap", albedo); mat.SetTexture("_NormalMap", normal); mat.SetTexture("_DisplacementMap", height);
        mat.SetTexture("_AOMap", ao); mat.SetTexture("_SmoothnessMap", smooth); mat.SetTexture("_MetallicMap", metal);
        mat.SetTexture("_EdgeMap", Texture2D.grayTexture);
        mat.SetFloat("_Parallax", 0.4f); mat.SetFloat("_DispOffset", 0.5f); mat.SetFloat("_EdgeLength", 8f);
        mat.SetVector("_Tiling", new Vector4(1, 1, 0, 0));
        mat.SetFloat("_Smoothness", 1); mat.SetFloat("_Metallic", 1); mat.SetFloat("_AOPower", 1); mat.SetFloat("_EdgePower", 0);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Plane);
        quad.GetComponent<Renderer>().sharedMaterial = mat;
        var lightGo = new GameObject("light"); var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional; light.shadows = LightShadows.Soft; light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(22, 40, 0);   // low sun: long shadows
        var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0.9f, -4.2f); cam.transform.LookAt(Vector3.zero);
        cam.backgroundColor = new Color(0.2f, 0.22f, 0.25f); cam.clearFlags = CameraClearFlags.SolidColor;
        RenderSettings.ambientLight = new Color(0.25f, 0.27f, 0.3f);

        var rt = new RenderTexture(900, 600, 24, RenderTextureFormat.ARGBHalf);
        lastMat = mat; lastCam = cam; lastRt = rt;
        if (built) { cam.targetTexture = rt; return; }
        cam.targetTexture = rt;
        string report = "";
        string[] names = { "none", "specocc", "micro", "self", "fine", "specaa", "all" };
        for (int m = 0; m < names.Length; m++)
        {
            Shader.SetGlobalFloat("_QualitySet", 1);
            Shader.SetGlobalFloat("_QualitySpecOcclusion", m == 1 || m == 6 ? 1 : 0);
            Shader.SetGlobalFloat("_QualityMicroShadow", m == 2 || m == 6 ? 0.5f : 0);
            Shader.SetGlobalFloat("_QualitySelfShadow", m == 3 || m == 6 ? 1 : 0);
            Shader.SetGlobalFloat("_QualityFineRelief", m == 4 ? 0.35f : 0);
            Shader.SetGlobalFloat("_QualitySpecAA", m == 5 || m == 6 ? 1 : 0);
            cam.Render();
            RenderTexture.active = rt;
            var img = new Texture2D(900, 600, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, 900, 600), 0, 0); img.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Out, "pbr_" + names[m] + ".png"), img.EncodeToPNG());
            double sum = 0;
            foreach (var c in img.GetPixels()) sum += c.r + c.g + c.b;
            report += $" {names[m]} {sum / (900 * 600 * 3):0.000};";
        }
        Debug.Log("PBRRENDER" + report);
    }
}
