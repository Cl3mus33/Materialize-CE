using System.IO;
using UnityEngine;

/// <summary>
/// Visual check of the texture coordinates read from a .nif: the mesh is drawn with its own diffuse texture.
/// MCE_NIF = the mesh, MCE_TEX = its diffuse. -executeMethod NifUvTest.Run
/// </summary>
public static class NifUvTest
{
    const string Out = @"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad";

    public static void Run()
    {
        string nif = System.Environment.GetEnvironmentVariable("MCE_NIF"), tex = System.Environment.GetEnvironmentVariable("MCE_TEX");
        var mesh = NifLoader.Load(nif, out string error);
        if (mesh == null) { Debug.Log("NIFUV " + error); return; }
        ObjLoader.Fit(mesh, 8f);
        FastImageLoader.FindTexconv();
        var pixels = FastImageLoader.Decode(tex);
        if (pixels.Error != null) { Debug.Log("NIFUV texture: " + pixels.Error); return; }
        var texture = FastImageLoader.ToTexture(pixels);

        var go = new GameObject("mesh");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Unlit/Texture")) { mainTexture = texture };
        var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.15f, 0.17f, 0.2f);
        cam.orthographic = true; cam.orthographicSize = 4.6f;
        var rt = new RenderTexture(700, 700, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        cam.targetTexture = rt;
        var sheet = new Texture2D(1400, 700, TextureFormat.RGB24, false);
        // Two sides of the mesh: seen along -X and along +X (the flat sides of an axe).
        for (int side = 0; side < 2; side++)
        {
            Vector3 from = side == 0 ? new Vector3(0, 0, -20) : new Vector3(0, 0, 20);
            cam.transform.position = from; cam.transform.LookAt(Vector3.zero);
            cam.Render();
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, 700, 700), side * 700, 0);
            RenderTexture.active = null;
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(Out, "nif_uv_check.png"), sheet.EncodeToPNG());
        Debug.Log($"NIFUV done: {mesh.vertexCount} vertices, bounds {mesh.bounds.size}");
    }
}
