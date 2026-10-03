using UnityEngine;

/// <summary>Checks the Adjust shader's maths. -executeMethod AdjustTest.Run</summary>
public static class AdjustTest
{
    static float Run1(Material m, float input)
    {
        var src = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        src.SetPixel(0, 0, new Color(input, input, input, 1)); src.Apply();
        var rt = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        Graphics.Blit(src, rt, m, 0);
        RenderTexture.active = rt;
        var read = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        read.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = null;
        return read.GetPixel(0, 0).r;
    }

    public static void Run()
    {
        var m = new Material(Shader.Find("Hidden/Blit_Adjust"));
        void Set(float black, float white, float gamma, float contrast, float brightness, bool invert, bool height, float strength)
        {
            m.SetFloat("_Mode", 0); m.SetFloat("_InBlack", black); m.SetFloat("_InWhite", white); m.SetFloat("_Gamma", gamma);
            m.SetFloat("_Contrast", contrast); m.SetFloat("_Brightness", brightness); m.SetFloat("_Invert", invert ? 1 : 0);
            m.SetFloat("_HeightMode", height ? 1 : 0); m.SetFloat("_Strength", strength);
        }
        Set(0, 1, 1, 1, 0, false, false, 1); Debug.Log($"ADJUSTTEST identity 0.3 -> {Run1(m, 0.3f):0.000}");
        Set(0.2f, 0.6f, 1, 1, 0, false, false, 1); Debug.Log($"ADJUSTTEST levels 0.2..0.6, 0.4 -> {Run1(m, 0.4f):0.000} (expect 0.500)");
        Set(0, 1, 1, 1, 0, true, false, 1); Debug.Log($"ADJUSTTEST invert 0.3 -> {Run1(m, 0.3f):0.000} (expect 0.700)");
        Set(0, 1, 1, 1, 0, false, true, 0); Debug.Log($"ADJUSTTEST height strength 0, 0.3 -> {Run1(m, 0.3f):0.000} (expect 1.000)");
        Set(0, 1, 1, 1, 0, false, true, 2); Debug.Log($"ADJUSTTEST height strength 2, 0.8 -> {Run1(m, 0.8f):0.000} (expect 0.600)");
    }
}
