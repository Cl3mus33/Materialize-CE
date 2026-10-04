using UnityEngine;

/// <summary>
/// Finds the direction of the light baked in a photo (Materialize CE): its brightness, in linear light, is fitted
/// against the slopes of the normal map (least squares on brightness = a + b·nx + c·ny). The slopes facing the
/// light are the brighter ones: (b, c) points to it.
/// </summary>
public static class DelightFit
{
    /// <summary>Main thread (reads the small mipmaps of the textures). False when no direction stands out.</summary>
    public static bool Fit(Texture2D photo, Texture2D normal, bool greenUp, out float angleDegrees, out float strength)
    {
        angleDegrees = 0; strength = 0;
        Color32[] p, n; int pw, ph, nw, nh;
        try
        {
            Read(photo, out p, out pw, out ph);
            Read(normal, out n, out nw, out nh);
        }
        catch (System.Exception) { return false; }   // not readable

        const int G = 160;
        double s1 = 0, sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0, sl = 0, slx = 0, sly = 0;
        for (int j = 0; j < G; j++)
            for (int i = 0; i < G; i++)
            {
                float u = (i + 0.5f) / G, v = (j + 0.5f) / G;
                Color32 c = p[Mathf.Min(ph - 1, (int)(v * ph)) * pw + Mathf.Min(pw - 1, (int)(u * pw))];
                Color32 m = n[Mathf.Min(nh - 1, (int)(v * nh)) * nw + Mathf.Min(nw - 1, (int)(u * nw))];
                double lum = System.Math.Pow((0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255.0, 2.2);
                double nx = m.r / 127.5 - 1.0, ny = m.g / 127.5 - 1.0;
                if (!greenUp) ny = -ny;
                s1 += 1; sx += nx; sy += ny; sxx += nx * nx; sxy += nx * ny; syy += ny * ny;
                sl += lum; slx += lum * nx; sly += lum * ny;
            }
        // Normal equations of the 3-parameter fit, solved with determinants.
        double det = Det(s1, sx, sy, sx, sxx, sxy, sy, sxy, syy);
        if (System.Math.Abs(det) < 1e-12) return false;   // a flat normal map
        double a = Det(sl, sx, sy, slx, sxx, sxy, sly, sxy, syy) / det;
        double b = Det(s1, sl, sy, sx, slx, sxy, sy, sly, syy) / det;
        double c2 = Det(s1, sx, sl, sx, sxx, slx, sy, sxy, sly) / det;
        if (a <= 1e-6) return false;
        double k = System.Math.Sqrt(b * b + c2 * c2) / a;   // relative change of brightness per unit of slope
        if (k < 0.03) return false;
        angleDegrees = Mathf.Repeat((float)(System.Math.Atan2(c2, b) * 180.0 / System.Math.PI), 360f);
        // The shading model at 45 degrees of height changes by 0.567 per unit of slope at full strength.
        strength = Mathf.Clamp01((float)(k / 0.567));
        return true;
    }

    /// <summary>The mipmap closest to 256 pixels wide: the fit needs shapes, not grain.</summary>
    static void Read(Texture2D tex, out Color32[] pixels, out int w, out int h)
    {
        int mip = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(1, tex.width) / 256f, 2)), 0, Mathf.Max(0, tex.mipmapCount - 1));
        pixels = tex.GetPixels32(mip);
        w = Mathf.Max(1, tex.width >> mip); h = Mathf.Max(1, tex.height >> mip);
    }

    static double Det(double a, double b, double c, double d, double e, double f, double g, double h, double i)
        => a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
}
