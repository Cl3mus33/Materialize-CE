using UnityEngine;
using UnityEditor;

/// <summary>
/// The retouch tool's pointer-to-texture mapping against the renderer: the preview plane is drawn with a texture
/// whose colour is its own coordinate, and the pixel under several screen points is compared with the ray cast.
/// -executeMethod RetouchRayTest.Run
/// </summary>
public static class RetouchRayTest
{
    public static void Run()
    {
        Mesh plane = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Models/box_plane.FBX")) if (o is Mesh m && m.name == "plane") plane = m;
        if (plane == null) { Debug.Log("RAYTEST no plane mesh"); return; }

        const int T = 256;
        var uvTex = new Texture2D(T, T, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) uvTex.SetPixel(x, y, new Color((x + 0.5f) / T, (y + 0.5f) / T, 0, 1));
        uvTex.Apply();

        var go = new GameObject("plane");
        go.AddComponent<MeshFilter>().sharedMesh = plane;
        var mat = new Material(Shader.Find("Unlit/Texture")) { mainTexture = uvTex };
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.transform.rotation = Quaternion.Euler(-55, 20, 5);
        go.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);

        var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(1, 2, -16); cam.transform.LookAt(Vector3.zero);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.blue;
        const int W = 800, H = 600;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        cam.targetTexture = rt;

        bool read = RetouchTool.ReadMesh(plane, go.transform);
        Debug.Log($"RAYTEST mesh read={read} vertices {plane.vertexCount} uvs {plane.uv.Length} triangles {plane.triangles.Length / 3} bounds {plane.bounds}");
        foreach (float tiling in new[] { 1f, 3f })
        {
            mat.mainTextureScale = new Vector2(tiling, tiling);
            var img = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                cam.Render();
                RenderTexture.active = rt;
                img.ReadPixels(new Rect(0, 0, W, H), 0, 0); img.Apply();
                RenderTexture.active = null;
                if (img.GetPixel(400, 300).b < 0.5f) break;
                // The plane has one side: seen from behind, it is turned over.
                go.transform.rotation = Quaternion.Euler(125, 20, 5);
            }
            float worst = 0; int hits = 0, misses = 0;
            foreach (var p in new[] { new Vector2(400, 300), new Vector2(300, 250), new Vector2(520, 340), new Vector2(430, 220), new Vector2(350, 380), new Vector2(10, 10) })
            {
                Color c = img.GetPixel((int)p.x, (int)p.y);
                bool onPlane = c.b < 0.5f;
                bool hit = RetouchTool.RaycastMesh(go.transform, cam, p, new Vector4(tiling, tiling, 0, 0), out Vector2 uv, out Vector3 world);
                if (hit != onPlane) { misses++; Debug.Log($"RAYTEST point {p}: render on plane={onPlane} colour {c}, ray hit={hit}"); continue; }
                if (!hit) continue;
                hits++;
                float du = Mathf.Abs(Mathf.DeltaAngle(uv.x * 360f, c.r * 360f)) / 360f, dv = Mathf.Abs(Mathf.DeltaAngle(uv.y * 360f, c.g * 360f)) / 360f;
                worst = Mathf.Max(worst, Mathf.Max(du, dv));
            }
            Debug.Log($"RAYTEST tiling {tiling}: {hits} points on the plane, worst difference {worst:0.0000} of the texture, hit/miss disagreements {misses}");
        }
    }
}
