using System.IO;
using UnityEngine;

/// <summary>Old PBR preview vs the modern one, same environment, both through the app's ACES. -executeMethod PbrCompareTest.Run</summary>
public static class PbrCompareTest
{
    const string Out = @"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad";

    static Color Aces(Color c)
    {
        Vector3 x = new Vector3(Mathf.Pow(c.r, 2.2f), Mathf.Pow(c.g, 2.2f), Mathf.Pow(c.b, 2.2f));
        System.Func<float, float> f = v => Mathf.Clamp01((v * (2.51f * v + 0.03f)) / (v * (2.43f * v + 0.59f) + 0.14f));
        return new Color(Mathf.Pow(f(x.x), 1 / 2.2f), Mathf.Pow(f(x.y), 1 / 2.2f), Mathf.Pow(f(x.z), 1 / 2.2f));
    }

    public static void Run()
    {
        // The test material and scene of PbrRenderTest, rendered with each shader.
        var cube = UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/CubeMaps/CubeMap_Bridge.jpg");
        Shader.SetGlobalTexture("_ProbeCubemap", cube);
        Shader.SetGlobalFloat("_ProbeMaxMip", Mathf.Log(cube.width, 2));
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = cube;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.32f);
        Shader.SetGlobalFloat("_QualitySet", 0);   // shader defaults
        PbrRenderTest.Build(out var mat, out var cam, out var rt);
        var shaders = new[] { Shader.Find("Custom/SurfacePBS_Tess_Generated"), Shader.Find("Custom/Preview_PBR") };
        string[] names = { "old", "modern" };
        for (int i = 0; i < 2; i++)
        {
            mat.shader = shaders[i];
            cam.Render();
            RenderTexture.active = rt;
            var img = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
            img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            var px = img.GetPixels();
            for (int k = 0; k < px.Length; k++) px[k] = Aces(px[k]);
            var o = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            o.SetPixels(px); o.Apply();
            File.WriteAllBytes(Path.Combine(Out, "cmp_" + names[i] + ".png"), o.EncodeToPNG());
        }
        Debug.Log("PBRCOMPARE done " + (shaders[0] != null) + " " + (shaders[1] != null));
    }
}
