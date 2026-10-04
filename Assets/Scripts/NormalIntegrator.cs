using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Height from a normal map by integration (Materialize CE): the surface whose slopes match the normals best
/// (Frankot-Chellappa, solved with a Fourier transform). Exact for a clean normal map, where Materialize's own
/// method only approximates the shape by sampling around each pixel. The surface is solved as a repeating one,
/// which suits tiling textures; a non-tiling normal map can get a slight overall tilt.
/// </summary>
public static class NormalIntegrator
{
    /// <summary>The largest side solved at full size: beyond it the map is solved smaller and enlarged (memory).</summary>
    const int MaxSide = 4096;

    /// <summary>
    /// Thread-safe. <paramref name="normal"/>: the map's pixels, bottom row first. <paramref name="greenUp"/>: OpenGL
    /// normals (green = slope facing up); false for DirectX. Returns heights in 0..1, bottom row first.
    /// </summary>
    public static float[] Integrate(Color32[] normal, int width, int height, bool greenUp)
    {
        int w = PowerOfTwo(width), h = PowerOfTwo(height);
        var re = new float[w * h];
        var im = new float[w * h];
        // Slopes: dz/dx in the real part, dz/dy in the imaginary one (a single transform gives both).
        Parallel.For(0, h, y =>
        {
            int sy = h == height ? y : Mathf.Min(height - 1, (int)((y + 0.5f) * height / h));
            for (int x = 0; x < w; x++)
            {
                int sx = w == width ? x : Mathf.Min(width - 1, (int)((x + 0.5f) * width / w));
                Color32 c = normal[sy * width + sx];
                float nx = c.r / 127.5f - 1f, ny = c.g / 127.5f - 1f, nz = Mathf.Max(c.b / 127.5f - 1f, 0.08f);
                if (!greenUp) ny = -ny;
                re[y * w + x] = -nx / nz;
                im[y * w + x] = -ny / nz;
            }
        });

        Fft2D(re, im, w, h, false);

        // P and Q from G = P + iQ (P and Q are transforms of real fields), then Z = -i (u P + v Q) / (u² + v²).
        var zr = new float[w * h];
        var zi = new float[w * h];
        Parallel.For(0, h, y =>
        {
            int my = (h - y) % h;
            float v = (y <= h / 2 ? y : y - h) * (2f * Mathf.PI / h);
            for (int x = 0; x < w; x++)
            {
                int mx = (w - x) % w;
                float u = (x <= w / 2 ? x : x - w) * (2f * Mathf.PI / w);
                float gr = re[y * w + x], gi = im[y * w + x];
                float cr = re[my * w + mx], ci = -im[my * w + mx];   // conj(G(-k))
                float pr = 0.5f * (gr + cr), pi = 0.5f * (gi + ci);
                float qr = 0.5f * (gi - ci), qi = -0.5f * (gr - cr);
                float d = u * u + v * v;
                if (d < 1e-12f) { zr[y * w + x] = 0; zi[y * w + x] = 0; continue; }
                float ar = u * pr + v * qr, ai = u * pi + v * qi;
                // -i * (ar + i ai) = ai - i ar
                zr[y * w + x] = ai / d;
                zi[y * w + x] = -ar / d;
            }
        });

        Fft2D(zr, zi, w, h, true);

        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < zr.Length; i++) { float z = zr[i]; if (z < min) min = z; if (z > max) max = z; }
        float scale = max > min ? 1f / (max - min) : 0f;

        var result = new float[width * height];
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                float z;
                if (w == width && h == height) z = zr[y * w + x];
                else
                {
                    // Bilinear, wrapping (the solved surface repeats).
                    float fx = (x + 0.5f) * w / width - 0.5f, fy = (y + 0.5f) * h / height - 0.5f;
                    int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                    float tx = fx - x0, ty = fy - y0;
                    int xa = ((x0 % w) + w) % w, xb = (xa + 1) % w, ya = ((y0 % h) + h) % h, yb = (ya + 1) % h;
                    z = Mathf.Lerp(Mathf.Lerp(zr[ya * w + xa], zr[ya * w + xb], tx), Mathf.Lerp(zr[yb * w + xa], zr[yb * w + xb], tx), ty);
                }
                result[y * width + x] = Mathf.Clamp01((z - min) * scale);
            }
        });
        return result;
    }

    static int PowerOfTwo(int n)
    {
        int p = Mathf.ClosestPowerOfTwo(Mathf.Clamp(n, 16, MaxSide));
        return Mathf.Min(p, MaxSide);
    }

    static void Fft2D(float[] re, float[] im, int w, int h, bool inverse)
    {
        Parallel.For(0, h, y => Fft1D(re, im, y * w, 1, w, inverse));
        Parallel.For(0, w, x => Fft1D(re, im, x, w, h, inverse));
    }

    /// <summary>In-place radix-2 transform of n values starting at <paramref name="offset"/>, <paramref name="stride"/> apart.</summary>
    static void Fft1D(float[] re, float[] im, int offset, int stride, int n, bool inverse)
    {
        var r = new float[n];
        var i = new float[n];
        for (int k = 0; k < n; k++) { r[k] = re[offset + k * stride]; i[k] = im[offset + k * stride]; }

        for (int a = 1, b = 0; a < n; a++)
        {
            int bit = n >> 1;
            for (; (b & bit) != 0; bit >>= 1) b ^= bit;
            b ^= bit;
            if (a < b) { float t = r[a]; r[a] = r[b]; r[b] = t; t = i[a]; i[a] = i[b]; i[b] = t; }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = 2 * Math.PI / len * (inverse ? 1 : -1);
            double wr = Math.Cos(angle), wi = Math.Sin(angle);
            for (int start = 0; start < n; start += len)
            {
                double cr = 1, ci = 0;   // doubles: thousands of rotations are chained
                int half = len >> 1;
                for (int k = 0; k < half; k++)
                {
                    int p = start + k, q = p + half;
                    float tr = (float)(r[q] * cr - i[q] * ci), ti = (float)(r[q] * ci + i[q] * cr);
                    r[q] = r[p] - tr; i[q] = i[p] - ti;
                    r[p] += tr; i[p] += ti;
                    double ncr = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = ncr;
                }
            }
        }
        float norm = inverse ? 1f / n : 1f;
        for (int k = 0; k < n; k++) { re[offset + k * stride] = r[k] * norm; im[offset + k * stride] = i[k] * norm; }
    }
}
