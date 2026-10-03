using UnityEngine;

/// <summary>Which way Materialize's normal green points, per Settings style. -executeMethod NormalConventionTest.Run</summary>
public static class NormalConventionTest
{
    public static void Run()
    {
        const int N = 64;
        // A bump in the middle; the image's top is the last row in Unity textures.
        var tex = new Texture2D(N, N, TextureFormat.RFloat, false, true);
        var data = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            data[y * N + x] = Mathf.Exp(-((x - N / 2f) * (x - N / 2f) + (y - N / 2f) * (y - N / 2f)) / 80f);
        tex.SetPixelData(data, 0); tex.Apply();

        var mat = new Material(Shader.Find("Hidden/Blit_Shader"));
        mat.SetVector("_ImageSize", new Vector4(N, N, 0, 0));
        mat.SetFloat("_BlurContrast", 5f);
        mat.SetFloat("_ShapeRecognition", 0);
        mat.SetTexture("_LightTex", Texture2D.blackTexture);
        mat.SetTexture("_LightBlurTex", Texture2D.blackTexture);
        var slope = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        Graphics.Blit(tex, slope, mat, 3);
        foreach (int flip in new[] { 0, 1 })
        {
            mat.SetTexture("_BlurTex0", slope);
            foreach (var n in new[] { "_BlurTex1", "_BlurTex2", "_BlurTex3", "_BlurTex4", "_BlurTex5", "_BlurTex6" }) mat.SetTexture(n, slope);
            mat.SetFloat("_Blur0Weight", 1); for (int i = 1; i < 7; i++) mat.SetFloat("_Blur" + i + "Weight", 0);
            mat.SetFloat("_FinalContrast", 1); mat.SetFloat("_Angularity", 0); mat.SetFloat("_AngularIntensity", 0);
            mat.SetInt("_FlipNormalY", flip);
            var outRT = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Graphics.Blit(slope, outRT, mat, 4);
            RenderTexture.active = outRT;
            var read = new Texture2D(N, N, TextureFormat.RGBAFloat, false, true);
            read.ReadPixels(new Rect(0, 0, N, N), 0, 0); read.Apply();
            RenderTexture.active = null;
            // Upper side of the bump in the image (higher row index = nearer the top of the picture).
            float gTop = read.GetPixel(N / 2, N / 2 + 8).g, rRight = read.GetPixel(N / 2 + 8, N / 2).r;
            string style = flip == 1 ? "Maya style" : "Max style (default)";
            Debug.Log($"NORMALTEST {style}: green on the bump's upper side {gTop:0.00} ({(gTop > 0.5f ? "OpenGL, green up" : "DirectX, green down")}), red on its right side {rRight:0.00}");
        }
    }
}
