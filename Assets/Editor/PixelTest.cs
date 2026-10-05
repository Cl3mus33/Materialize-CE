using System.IO;
using UnityEngine;

/// <summary>Muddy ground with glossy puddles seen from afar: looks for the stair-stepped outlines. -executeMethod PixelTest.Run</summary>
public static class PixelTest
{
    static readonly string Out = System.IO.Path.Combine(System.IO.Path.GetTempPath(), @"MaterializeCE-tests");
    const int N = 2048;
    static float[] h;

    static float H(int x, int y) => h[((y + N) % N) * N + (x + N) % N];

    static Texture2D Make(System.Func<int, int, Color> f, bool linear)
    {
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 9 };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) px[y * N + x] = f(x, y);
        t.SetPixels(px); t.Apply(true);
        return t;
    }

    public static void Run()
    {
        h = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float n = 0, amp = 0.5f, f = 0.004f;
            for (int o = 0; o < 7; o++) { n += amp * Mathf.PerlinNoise(x * f + o * 17.3f, y * f + o * 9.1f); amp *= 0.5f; f *= 2.1f; }
            h[y * N + x] = Mathf.Max(n + 0.06f * Mathf.PerlinNoise(x * 0.31f, y * 0.31f), 0.45f);   // spiky detail, flat puddles   // puddles: flat below 0.45
        }
        var height = Make((x, y) => { float v = H(x, y); return new Color(v, v, v, 1); }, true);
        var normal = Make((x, y) =>
        {
            float dx = (H(x + 1, y) - H(x - 1, y)) * 40f, dy = (H(x, y + 1) - H(x, y - 1)) * 40f;
            var n = new Vector3(-dx, dy, 1).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
        }, true);
        var smooth = Make((x, y) => H(x, y) <= 0.4501f ? new Color(0.95f, 0.95f, 0.95f, 1) : new Color(0.25f, 0.25f, 0.25f, 1), true);
        var albedo = Make((x, y) => H(x, y) <= 0.4501f ? new Color(0.2f, 0.2f, 0.2f, 1) : new Color(0.42f, 0.4f, 0.38f, 1), false);

        var cube = UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/CubeMaps/CubeMap_Bridge.jpg");
        Shader.SetGlobalTexture("_ProbeCubemap", cube);
        Shader.SetGlobalFloat("_ProbeMaxMip", Mathf.Log(cube.width, 2));
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = cube;

        PbrRenderTest.Build(out var mat, out var cam, out var _);
        mat.SetTexture("_DiffuseMap", albedo); mat.SetTexture("_NormalMap", normal); mat.SetTexture("_DisplacementMap", height);
        mat.SetTexture("_SmoothnessMap", smooth); mat.SetTexture("_MetallicMap", Texture2D.blackTexture); mat.SetTexture("_AOMap", Texture2D.whiteTexture);
        mat.SetFloat("_Parallax", 0.5f); mat.SetFloat("_EdgeLength", 3f);
        cam.transform.position = new Vector3(0, 2.2f, -9f); cam.transform.LookAt(new Vector3(0, 0, 1));
        cam.fieldOfView = 40;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf);
        cam.targetTexture = rt;
        Shader.SetGlobalFloat("_CsLook", System.Environment.GetEnvironmentVariable("MCE_CS") == "1" ? 1 : 0);
        if (System.Environment.GetEnvironmentVariable("MCE_EMIS") == "1")
        {
            // Emission in the puddles' left half, subsurface (green) on the right half of the ground.
            Shader.SetGlobalTexture("_MceEmissionMap", Make((x, y) => x < N / 2 && H(x, y) <= 0.4501f ? new Color(1f, 0.45f, 0.1f, 1) : Color.black, false));
            Shader.SetGlobalTexture("_MceSubsurfaceMap", Make((x, y) => x >= N / 2 && H(x, y) > 0.4501f ? new Color(0.2f, 0.9f, 0.3f, 1) : Color.black, false));
            Shader.SetGlobalFloat("_MceEmissionStrength", 1f);
        }
        float pixel = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / 720f;
        var camPos = cam.transform.position;

        string[] names = { "default", "noselfshadow", "nodisplod", "nospecaa", "noshadows" };
        var light = Object.FindFirstObjectByType<Light>();
        light.transform.rotation = Quaternion.Euler(41, System.Environment.GetEnvironmentVariable("MCE_YAW") != null ? float.Parse(System.Environment.GetEnvironmentVariable("MCE_YAW")) : 150, 0);   // like the moonlit HDRI, facing the camera
        string tag = System.Environment.GetEnvironmentVariable("MCE_TAG") ?? "";
        for (int m = 0; m < names.Length; m++)
        {
            Shader.SetGlobalFloat("_QualitySet", 1);
            Shader.SetGlobalFloat("_QualitySpecOcclusion", 1);
            Shader.SetGlobalFloat("_QualityMicroShadow", 0);
            Shader.SetGlobalFloat("_QualitySelfShadow", m == 1 ? 0 : 1);
            Shader.SetGlobalFloat("_QualityFineRelief", 0);
            Shader.SetGlobalFloat("_QualitySpecAA", m == 3 ? 0 : 1);
            Shader.SetGlobalVector("_MceCamPos", new Vector4(camPos.x, camPos.y, camPos.z, m == 2 ? 0 : pixel));
            Shader.SetGlobalFloat("_MceUvPerWorld", 1f / 10f);
            light.shadows = m == 4 ? LightShadows.None : LightShadows.Soft;
            light.shadowCustomResolution = 4096; light.shadowBias = 0.02f;
            cam.Render();
            RenderTexture.active = rt;
            var img = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); img.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Out, "pix_" + tag + names[m] + ".png"), img.EncodeToPNG());
        }
        Debug.Log("PIXELTEST done");
    }
}
