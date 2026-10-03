using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Image loading without freezing the interface (Materialize CE).
/// The original loader read the file with FreeImage, re-encoded it to a temporary PNG, then decoded that PNG
/// again through WWW, all on the main thread: seconds of frozen UI for a 4K texture. Here FreeImage decodes
/// straight to 32-bit pixels on a worker thread, and only the upload to the GPU happens on the main thread.
/// The Unicode entry points are used, so paths with accents ("Créa textures") load instead of failing.
/// </summary>
public static class FastImageLoader
{
    /// <summary>Decoded pixels, bottom row first, BGRA (FreeImage's and Unity's BGRA32 layout).</summary>
    public sealed class Pixels
    {
        public int Width;
        public int Height;
        public byte[] Bgra;
        /// <summary>
        /// Full-precision grey (red channel), bottom row first, for 16-bit and floating-point images: height maps
        /// keep their smooth slopes instead of 256 steps. Null for ordinary 8-bit images.
        /// </summary>
        public float[] Grey;
        public string Error;
    }

    const int FIF_UNKNOWN = -1;

    [DllImport("FreeImage", CharSet = CharSet.Unicode)] static extern int FreeImage_GetFileTypeU(string filename, int size);
    [DllImport("FreeImage", CharSet = CharSet.Unicode)] static extern int FreeImage_GetFIFFromFilenameU(string filename);
    [DllImport("FreeImage")] static extern bool FreeImage_FIFSupportsReading(int fif);
    [DllImport("FreeImage", CharSet = CharSet.Unicode)] static extern IntPtr FreeImage_LoadU(int fif, string filename, int flags);
    [DllImport("FreeImage")] static extern IntPtr FreeImage_ConvertTo32Bits(IntPtr dib);
    [DllImport("FreeImage")] static extern void FreeImage_Unload(IntPtr dib);
    [DllImport("FreeImage")] static extern uint FreeImage_GetWidth(IntPtr dib);
    [DllImport("FreeImage")] static extern uint FreeImage_GetHeight(IntPtr dib);
    [DllImport("FreeImage")] static extern uint FreeImage_GetPitch(IntPtr dib);
    [DllImport("FreeImage")] static extern IntPtr FreeImage_GetBits(IntPtr dib);
    [DllImport("FreeImage")] static extern int FreeImage_GetImageType(IntPtr dib);
    [DllImport("FreeImage")] static extern IntPtr FreeImage_ConvertToStandardType(IntPtr dib, bool scaleLinear);
    [DllImport("FreeImage")] static extern IntPtr FreeImage_ConvertToRGBAF(IntPtr dib);

    const int FIT_BITMAP = 1, FIT_RGB16 = 9, FIT_RGBA16 = 10, FIT_RGBF = 11, FIT_RGBAF = 12;

    /// <summary>Formats Materialize opens (FreeImage 3.17 reads them all).</summary>
    public const string ImageMasks = "*.png;*.jpg;*.jpeg;*.tga;*.bmp;*.tif;*.tiff;*.psd;*.exr;*.hdr;*.dds;*.webp";

    static bool DecodeFloatRgba(IntPtr dib, Pixels result)
    {
        IntPtr rgbaf = FreeImage_ConvertToRGBAF(dib);
        if (rgbaf == IntPtr.Zero) return false;
        try
        {
            int w = (int)FreeImage_GetWidth(rgbaf), h = (int)FreeImage_GetHeight(rgbaf), pitch = (int)FreeImage_GetPitch(rgbaf);
            IntPtr bits = FreeImage_GetBits(rgbaf);
            var row = new float[w * 4];
            var data = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(new IntPtr(bits.ToInt64() + (long)y * pitch), row, 0, w * 4);
                for (int x = 0; x < w; x++)
                {
                    int o = (y * w + x) * 4, i = x * 4;
                    // RGBAF is R, G, B, A; the texture wants B, G, R, A.
                    data[o] = Byte(row[i + 2]); data[o + 1] = Byte(row[i + 1]); data[o + 2] = Byte(row[i]); data[o + 3] = Byte(row[i + 3]);
                }
            }
            result.Width = w; result.Height = h; result.Bgra = data;
            return true;
        }
        finally { FreeImage_Unload(rgbaf); }
    }

    /// <summary>
    /// Red channel at full precision. Single-channel images (16-bit, float) are stretched from their lowest to their
    /// highest value, exactly like the 8-bit version shown in the interface, so both agree.
    /// </summary>
    static float[] DecodeGrey(IntPtr dib, int type)
    {
        IntPtr rgbaf = FreeImage_ConvertToRGBAF(dib);
        if (rgbaf == IntPtr.Zero) return null;
        try
        {
            int w = (int)FreeImage_GetWidth(rgbaf), h = (int)FreeImage_GetHeight(rgbaf), pitch = (int)FreeImage_GetPitch(rgbaf);
            IntPtr bits = FreeImage_GetBits(rgbaf);
            var row = new float[w * 4];
            var grey = new float[w * h];
            float min = float.MaxValue, max = float.MinValue;
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(new IntPtr(bits.ToInt64() + (long)y * pitch), row, 0, w * 4);
                for (int x = 0; x < w; x++)
                {
                    float v = row[x * 4];
                    grey[y * w + x] = v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            bool singleChannel = type != FIT_RGB16 && type != FIT_RGBA16 && type != FIT_RGBF && type != FIT_RGBAF;
            if (singleChannel && max > min)
            {
                float scale = 1f / (max - min);
                for (int i = 0; i < grey.Length; i++) grey[i] = (grey[i] - min) * scale;
            }
            else
            {
                for (int i = 0; i < grey.Length; i++) grey[i] = Mathf.Clamp01(grey[i]);
            }
            return grey;
        }
        finally { FreeImage_Unload(rgbaf); }
    }

    /// <summary>Full-precision height for the tools (RFloat render texture), or null for an 8-bit image. Main thread only.</summary>
    public static RenderTexture ToHeightTexture(Pixels pixels)
    {
        if (pixels.Grey == null) return null;
        var source = new Texture2D(pixels.Width, pixels.Height, TextureFormat.RFloat, false, true);
        source.SetPixelData(pixels.Grey, 0);
        source.Apply(false, false);
        var rt = new RenderTexture(pixels.Width, pixels.Height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        rt.wrapMode = TextureWrapMode.Repeat;
        Graphics.Blit(source, rt);
        UnityEngine.Object.Destroy(source);
        return rt;
    }

    static byte Byte(float v) => (byte)(v <= 0 ? 0 : v >= 1 ? 255 : (int)(v * 255 + 0.5f));

    /// <summary>Reads an image file into 32-bit pixels. Safe to call from any thread; never throws.</summary>
    [DllImport("FreeImage")] static extern IntPtr FreeImage_Rescale(IntPtr dib, int width, int height, int filter);

    /// <summary>
    /// An environment picture (HDR, EXR, or an ordinary panorama) as floating-point RGB for the preview's lighting
    /// (Materialize CE). Wider than <paramref name="maxWidth"/> it is scaled down. HDR / EXR values are linear and
    /// unclamped; <paramref name="linear"/> says so. Rows bottom first. Any thread.
    /// </summary>
    public static string DecodeEnvironment(string path, int maxWidth, out float[] rgba, out int width, out int height, out bool linear)
    {
        rgba = null; width = height = 0; linear = false;
        IntPtr dib = IntPtr.Zero, rgbaf = IntPtr.Zero;
        try
        {
            if (!System.IO.File.Exists(path)) return "File not found: " + path;
            int fif = FreeImage_GetFileTypeU(path, 0);
            if (fif == FIF_UNKNOWN) fif = FreeImage_GetFIFFromFilenameU(path);
            if (fif == FIF_UNKNOWN || !FreeImage_FIFSupportsReading(fif)) return "Unsupported image format: " + path;
            dib = FreeImage_LoadU(fif, path, 0);
            if (dib == IntPtr.Zero) return "Could not read the image (damaged file?): " + path;
            int type = FreeImage_GetImageType(dib);
            linear = type == FIT_RGBF || type == FIT_RGBAF;
            if (type == FIT_BITMAP)
            {
                IntPtr dib32 = FreeImage_ConvertTo32Bits(dib);
                if (dib32 == IntPtr.Zero) return "Could not convert the image: " + path;
                FreeImage_Unload(dib);
                dib = dib32;
            }
            // HDR pictures are read as they are: FreeImage's conversion to RGBA floats clamps them to 1 (no sun left).
            int channels = 4;
            if (type == FIT_RGBF || type == FIT_RGBAF) { rgbaf = dib; dib = IntPtr.Zero; channels = type == FIT_RGBF ? 3 : 4; }
            else rgbaf = FreeImage_ConvertToRGBAF(dib);
            if (rgbaf == IntPtr.Zero) return "Could not convert the image to floating point: " + path;
            int w = (int)FreeImage_GetWidth(rgbaf), h = (int)FreeImage_GetHeight(rgbaf);
            if (w > maxWidth)
            {
                IntPtr small = FreeImage_Rescale(rgbaf, maxWidth, Math.Max(1, (int)((long)h * maxWidth / w)), 1 /* bicubic */);
                if (small != IntPtr.Zero) { FreeImage_Unload(rgbaf); rgbaf = small; }
                w = (int)FreeImage_GetWidth(rgbaf); h = (int)FreeImage_GetHeight(rgbaf);
            }
            int pitch = (int)FreeImage_GetPitch(rgbaf);
            IntPtr bits = FreeImage_GetBits(rgbaf);
            var data = new float[w * h * 4];
            if (channels == 4)
            {
                for (int y = 0; y < h; y++)
                    Marshal.Copy(new IntPtr(bits.ToInt64() + (long)y * pitch), data, y * w * 4, w * 4);
            }
            else
            {
                var row = new float[w * 3];
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(new IntPtr(bits.ToInt64() + (long)y * pitch), row, 0, w * 3);
                    int o = y * w * 4;
                    for (int x = 0; x < w; x++, o += 4)
                    {
                        float r = row[x * 3], g = row[x * 3 + 1], b = row[x * 3 + 2];
                        // Bicubic rescaling can ring below zero around the sun.
                        data[o] = r > 0 ? r : 0; data[o + 1] = g > 0 ? g : 0; data[o + 2] = b > 0 ? b : 0; data[o + 3] = 1f;
                    }
                }
            }
            rgba = data; width = w; height = h;
            return null;
        }
        catch (Exception e) { return "Could not load " + path + ": " + e.Message; }
        finally
        {
            if (rgbaf != IntPtr.Zero) FreeImage_Unload(rgbaf);
            if (dib != IntPtr.Zero) FreeImage_Unload(dib);
        }
    }

    // ---------- DDS through texconv (Materialize CE) ----------

    /// <summary>texconv.exe, found on the main thread at start (Unity paths cannot be read from worker threads).</summary>
    static string texconvPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void FindTexconv()
    {
        string p = System.IO.Path.Combine(Application.streamingAssetsPath, "texconv.exe");
        texconvPath = System.IO.File.Exists(p) ? p : null;
    }

    /// <summary>
    /// DDS files FreeImage cannot read (BC4, BC5, BC6H, BC7, DX10 headers: most modern game textures) are decoded by
    /// texconv into a temporary TGA. Two-channel BC5 normal maps get their blue (Z) channel rebuilt. Null when
    /// texconv is missing or fails, so FreeImage still gets its chance.
    /// </summary>
    static Pixels DecodeDdsWithTexconv(string path)
    {
        if (texconvPath == null) return null;
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MaterializeCE-dds-" + Guid.NewGuid().ToString("N"));
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            bool twoChannelNormal = IsBc5(path);
            var info = new System.Diagnostics.ProcessStartInfo(texconvPath,
                "-nologo -y -ft tga -f " + (IsSrgb(path) ? "R8G8B8A8_UNORM_SRGB" : "R8G8B8A8_UNORM") + " -m 1 " + (twoChannelNormal ? "--reconstruct-z " : "") + "-o \"" + dir + "\" \"" + path + "\"")
            {
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using (var p = System.Diagnostics.Process.Start(info))
            {
                p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } return null; }
                if (p.ExitCode != 0) return null;
            }
            // TGA, not PNG: texconv tags its PNGs "gamma 1.0" and FreeImage would brighten them.
            string png = System.IO.Path.Combine(dir, System.IO.Path.GetFileNameWithoutExtension(path) + ".tga");
            if (!System.IO.File.Exists(png)) return null;
            var result = DecodeFile(png);
            return result.Error == null ? result : null;
        }
        catch (Exception e) { Debug.LogWarning("texconv could not read " + path + ": " + e.Message); return null; }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>
    /// sRGB DDS (…_SRGB formats): read into an sRGB image too, so the colours come out exactly as stored,
    /// without a conversion to linear.
    /// </summary>
    static bool IsSrgb(string path)
    {
        try
        {
            using (var f = System.IO.File.OpenRead(path))
            {
                var h = new byte[148];
                if (f.Read(h, 0, h.Length) < 148 || System.Text.Encoding.ASCII.GetString(h, 84, 4) != "DX10") return false;
                int dxgi = BitConverter.ToInt32(h, 128);
                return dxgi == 29 || dxgi == 72 || dxgi == 75 || dxgi == 78 || dxgi == 91 || dxgi == 93 || dxgi == 99;
            }
        }
        catch { return false; }
    }

    /// <summary>BC5 / ATI2: two channels (normal X and Y).</summary>
    static bool IsBc5(string path)
    {
        try
        {
            using (var f = System.IO.File.OpenRead(path))
            {
                var h = new byte[148];
                if (f.Read(h, 0, h.Length) < 128) return false;
                string fourCC = System.Text.Encoding.ASCII.GetString(h, 84, 4);
                if (fourCC == "ATI2" || fourCC == "BC5U" || fourCC == "BC5S") return true;
                if (fourCC == "DX10")
                {
                    int dxgi = BitConverter.ToInt32(h, 128);
                    return dxgi >= 82 && dxgi <= 84;   // BC5_TYPELESS, BC5_UNORM, BC5_SNORM
                }
            }
        }
        catch { }
        return false;
    }

    public static Pixels Decode(string path)
    {
        // DDS: texconv reads every format (BC1 to BC7); FreeImage only the oldest ones.
        if (path != null && path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path))
        {
            var viaTexconv = DecodeDdsWithTexconv(path);
            if (viaTexconv != null) return viaTexconv;
        }
        return DecodeFile(path);
    }

    static Pixels DecodeFile(string path)
    {
        var result = new Pixels();
        IntPtr dib = IntPtr.Zero, dib32 = IntPtr.Zero;
        try
        {
            if (!System.IO.File.Exists(path)) { result.Error = "File not found: " + path; return result; }
            int fif = FreeImage_GetFileTypeU(path, 0);
            if (fif == FIF_UNKNOWN) fif = FreeImage_GetFIFFromFilenameU(path);
            if (fif == FIF_UNKNOWN || !FreeImage_FIFSupportsReading(fif)) { result.Error = "Unsupported image format: " + path; return result; }

            dib = FreeImage_LoadU(fif, path, 0);
            if (dib == IntPtr.Zero) { result.Error = "Could not read the image (damaged file?): " + path; return result; }

            int type = FreeImage_GetImageType(dib);
            if (type != FIT_BITMAP) result.Grey = DecodeGrey(dib, type);
            if (type == FIT_RGBF || type == FIT_RGBAF)
            {
                // EXR / HDR colour: values clamped to 0..1, as a texture map expects.
                if (!DecodeFloatRgba(dib, result)) result.Error = "Could not convert the floating-point image: " + path;
                return result;
            }
            if (type != FIT_BITMAP && type != FIT_RGB16 && type != FIT_RGBA16)
            {
                // 16-bit or float greyscale (height / displacement maps): stretched from its lowest to its highest value.
                IntPtr standard = FreeImage_ConvertToStandardType(dib, true);
                if (standard == IntPtr.Zero) { result.Error = "Unsupported pixel type (" + type + "): " + path; return result; }
                FreeImage_Unload(dib);
                dib = standard;
            }
            dib32 = FreeImage_ConvertTo32Bits(dib);
            if (dib32 == IntPtr.Zero) { result.Error = "Could not convert the image to 32 bits: " + path; return result; }

            int w = (int)FreeImage_GetWidth(dib32), h = (int)FreeImage_GetHeight(dib32), pitch = (int)FreeImage_GetPitch(dib32);
            IntPtr bits = FreeImage_GetBits(dib32);
            if (w <= 0 || h <= 0 || bits == IntPtr.Zero) { result.Error = "Empty image: " + path; return result; }

            var data = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                Marshal.Copy(new IntPtr(bits.ToInt64() + (long)y * pitch), data, y * w * 4, w * 4);
            result.Width = w;
            result.Height = h;
            result.Bgra = data;
        }
        catch (Exception e)
        {
            result.Error = "Could not load " + path + ": " + e.Message;
        }
        finally
        {
            if (dib32 != IntPtr.Zero && dib32 != dib) FreeImage_Unload(dib32);
            if (dib != IntPtr.Zero) FreeImage_Unload(dib);
        }
        return result;
    }

    /// <summary>Uploads decoded pixels as a texture like the ones WWW used to give (sRGB, mipmaps, readable). Main thread only.</summary>
    public static Texture2D ToTexture(Pixels pixels)
    {
        var texture = new Texture2D(pixels.Width, pixels.Height, TextureFormat.BGRA32, true, false);
        texture.SetPixelData(pixels.Bgra, 0);   // level 0; Apply builds the mipmaps
        texture.Apply(true, false);
        texture.anisoLevel = 9;
        texture.filterMode = FilterMode.Trilinear;
        return texture;
    }
}
