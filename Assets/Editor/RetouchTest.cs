using UnityEngine;

/// <summary>The clone stamp's dab on a test map: copies the source, respects the brush, wraps. -executeMethod RetouchTest.Run</summary>
public static class RetouchTest
{
    public static void Run()
    {
        const int N = 256;
        // Left half black, right half white.
        var src = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) src.SetPixel(x, y, x < N / 2 ? Color.black : Color.white);
        src.Apply();
        var work = new RenderTexture(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { wrapMode = TextureWrapMode.Repeat };
        Graphics.Blit(src, work);
        var mat = new Material(Shader.Find("Hidden/Blit_Retouch"));

        // Paint at (0.25, 0.5) (black) from (0.75, 0.5) (white), radius 0.1, hard.
        RetouchTool.DabInto(work, mat, new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0f), 0.1f, 0.9f, 1f);
        // And across the left border, at (0.0, 0.2), from the white side: the dab must wrap to the right edge too...
        RetouchTool.DabInto(work, mat, new Vector2(0.0f, 0.2f), new Vector2(0.25f, 0f), 0.05f, 0.9f, 1f);

        RenderTexture.active = work;
        var read = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        read.ReadPixels(new Rect(0, 0, N, N), 0, 0); read.Apply();
        RenderTexture.active = null;
        float At(float u, float v) => read.GetPixel((int)(u * N), (int)(v * N)).r;
        Debug.Log($"RETOUCH centre of the dab {At(0.25f, 0.5f):0.00} (expect 1), outside the brush {At(0.25f, 0.8f):0.00} (expect 0), " +
                  $"source untouched {At(0.75f, 0.5f):0.00} (expect 1) | wrap: left edge {At(0.01f, 0.2f):0.00} (expect 0, reads black at +0.25), " +
                  $"right edge {At(0.99f, 0.2f):0.00} (expect 0: white replaced by the black read at 0.24)");
    }
}
