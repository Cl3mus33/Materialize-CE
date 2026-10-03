using UnityEngine;

/// <summary>Checks the smoothing passes of Blit_Adjust. -executeMethod BlurTest.Run</summary>
public static class BlurTest
{
    static float Blur(Material m, Texture2D src, float sigma)
    {
        var a = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGBFloat) { wrapMode = TextureWrapMode.Repeat };
        var b = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGBFloat) { wrapMode = TextureWrapMode.Repeat };
        m.SetFloat("_BlurSigma", sigma);
        m.SetVector("_BlurDir", new Vector4(1, 0, 0, 0)); Graphics.Blit(src, a, m, 1);
        m.SetVector("_BlurDir", new Vector4(0, 1, 0, 0)); Graphics.Blit(a, b, m, 1);
        RenderTexture.active = b;
        var r = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
        r.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); RenderTexture.active = null;
        return r.GetPixel(10, 10).r;
    }

    public static void Run()
    {
        var m = new Material(Shader.Find("Hidden/Blit_Adjust"));
        var t = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) t.SetPixel(x, y, ((x + y) & 1) == 0 ? Color.white : Color.black);
        t.Apply();
        Debug.Log($"BLURTEST checker pixel (10,10) = 1. sigma 0.3 -> {Blur(m, t, 0.3f):0.000}, sigma 1 -> {Blur(m, t, 1f):0.000}, sigma 3 -> {Blur(m, t, 3f):0.000} (towards 0.5)");
    }
}
