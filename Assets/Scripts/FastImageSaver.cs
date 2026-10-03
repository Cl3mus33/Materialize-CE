using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// Saving maps without freezing the interface and without temporary files (Materialize CE).
/// The original encoded every map to a PNG inside the Materialize_Data folder, then had FreeImage reload it to
/// write BMP / TGA / TIFF, all on the main thread and with non-Unicode paths: slow, broken when Materialize sits
/// in a protected folder, and fed an empty image to FreeImage when anything went wrong. Here the pixels are
/// read once on the main thread, then encoded and written on a worker thread, next to the target first and
/// renamed at the end so a failed save never leaves a half-written file.
/// </summary>
public static class FastImageSaver
{
    [DllImport("FreeImage")] static extern IntPtr FreeImage_Allocate(int width, int height, int bpp, uint red, uint green, uint blue);
    [DllImport("FreeImage")] static extern IntPtr FreeImage_GetBits(IntPtr dib);
    [DllImport("FreeImage")] static extern uint FreeImage_GetPitch(IntPtr dib);
    [DllImport("FreeImage")] static extern void FreeImage_Unload(IntPtr dib);
    [DllImport("FreeImage", CharSet = CharSet.Unicode)] static extern bool FreeImage_SaveU(int fif, IntPtr dib, string filename, int flags);
    [DllImport("FreeImage")] static extern IntPtr FreeImage_AllocateT(int type, int width, int height, int bpp, uint red, uint green, uint blue);

    const int FIT_UINT16 = 2, FIF_PNG = 13;

    const int FIF_BMP = 0, FIF_TARGA = 17, FIF_TIFF = 18;
    const int TIFF_NONE = 0x0800, TARGA_SAVE_RLE = 2;

    /// <summary>What the main thread hands to the worker: the pixels, bottom row first (Unity's order).</summary>
    public sealed class Job
    {
        public Color32[] Pixels;
        public int Width, Height;
        public string Path;
        public string Extension;
        /// <summary>Full-precision height (0..1, bottom row first): PNG and TIFF are then written in 16 bits.</summary>
        public float[] Grey;
    }

    /// <summary>Main thread: reads a high-precision height (the HD height map) for a 16-bit PNG or TIFF.</summary>
    public static float[] ReadHeight(RenderTexture height)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = height;
        var tex = new Texture2D(height.width, height.height, TextureFormat.RFloat, false, true);
        tex.ReadPixels(new Rect(0, 0, height.width, height.height), 0, 0);
        tex.Apply(false, false);
        RenderTexture.active = previous;
        var data = tex.GetPixelData<float>(0).ToArray();
        UnityEngine.Object.Destroy(tex);
        return data;
    }

    /// <summary>Main thread: copies the texture's pixels. The texture must be readable (Materialize's are).</summary>
    public static Job Prepare(Texture2D texture, string pathWithoutExtension, string extension)
    {
        return new Job
        {
            Pixels = texture.GetPixels32(),
            Width = texture.width,
            Height = texture.height,
            Extension = extension.ToLowerInvariant(),
            Path = pathWithoutExtension + "." + extension.ToLowerInvariant(),
        };
    }

    /// <summary>Worker thread: encodes and writes. Returns null on success, else a message for the user.</summary>
    public static string Write(Job job)
    {
        string temp = job.Path + ".saving";
        try
        {
            string folder = System.IO.Path.GetDirectoryName(job.Path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) return "The folder does not exist: " + folder;
            bool alpha = HasAlpha(job.Pixels);
            if (job.Grey != null && job.Grey.Length == job.Width * job.Height && (job.Extension == "png" || job.Extension == "tiff"))
            {
                string error16 = WriteGrey16(job, job.Extension == "png" ? FIF_PNG : FIF_TIFF, temp);
                if (error16 != null) return error16;
                if (File.Exists(job.Path)) File.Delete(job.Path);
                File.Move(temp, job.Path);
                return null;
            }
            switch (job.Extension)
            {
                case "png":
                    File.WriteAllBytes(temp, alpha
                        ? ImageConversion.EncodeArrayToPNG(Rgba(job.Pixels), GraphicsFormat.R8G8B8A8_SRGB, (uint)job.Width, (uint)job.Height)
                        : ImageConversion.EncodeArrayToPNG(Rgb(job.Pixels), GraphicsFormat.R8G8B8_SRGB, (uint)job.Width, (uint)job.Height));
                    break;
                case "jpg":
                    // 95 instead of Unity's default 75: maps are sources for games, not web thumbnails.
                    File.WriteAllBytes(temp, ImageConversion.EncodeArrayToJPG(Rgb(job.Pixels), GraphicsFormat.R8G8B8_SRGB, (uint)job.Width, (uint)job.Height, 0, 95));
                    break;
                case "dds":
                    return WriteDds(job, alpha);
                case "bmp":
                case "tga":
                case "tiff":
                    int fif = job.Extension == "bmp" ? FIF_BMP : job.Extension == "tga" ? FIF_TARGA : FIF_TIFF;
                    int flags = job.Extension == "tiff" ? TIFF_NONE : 0;
                    string error = WriteFreeImage(job, alpha, fif, flags, temp);
                    if (error != null) return error;
                    break;
                default:
                    return "Unknown file format: " + job.Extension;
            }
            if (File.Exists(job.Path)) File.Delete(job.Path);
            File.Move(temp, job.Path);
            return null;
        }
        catch (Exception e)
        {
            return "Could not save " + job.Path + ": " + e.Message;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }

    /// <summary>Lossless PNG in a private temp folder, named like the target (texconv names its output after it), then the DDS tool.</summary>
    static string WriteDds(Job job, bool alpha)
    {
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MaterializeCE-dds-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string png = System.IO.Path.Combine(folder, System.IO.Path.GetFileNameWithoutExtension(job.Path) + ".png");
            File.WriteAllBytes(png, alpha
                ? ImageConversion.EncodeArrayToPNG(Rgba(job.Pixels), GraphicsFormat.R8G8B8A8_SRGB, (uint)job.Width, (uint)job.Height)
                : ImageConversion.EncodeArrayToPNG(Rgb(job.Pixels), GraphicsFormat.R8G8B8_SRGB, (uint)job.Width, (uint)job.Height));
            bool normal = System.IO.Path.GetFileNameWithoutExtension(job.Path).EndsWith("_normal", StringComparison.OrdinalIgnoreCase);
            return DdsExport.Convert(png, job.Path, normal);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    /// <summary>16-bit greyscale: 65 536 levels instead of 256, so slopes rebuilt from the height stay smooth.</summary>
    static string WriteGrey16(Job job, int fif, string temp)
    {
        IntPtr dib = FreeImage_AllocateT(FIT_UINT16, job.Width, job.Height, 16, 0, 0, 0);
        if (dib == IntPtr.Zero) return "Not enough memory to save " + job.Path;
        try
        {
            IntPtr bits = FreeImage_GetBits(dib);
            int pitch = (int)FreeImage_GetPitch(dib);
            var row = new short[pitch / 2];
            for (int y = 0; y < job.Height; y++)
            {
                for (int x = 0; x < job.Width; x++)
                {
                    float v = job.Grey[y * job.Width + x];
                    row[x] = unchecked((short)(ushort)Mathf.RoundToInt(Mathf.Clamp01(v) * 65535f));
                }
                Marshal.Copy(row, 0, new IntPtr(bits.ToInt64() + (long)y * pitch), row.Length);
            }
            return FreeImage_SaveU(fif, dib, temp, fif == FIF_TIFF ? TIFF_NONE : 0) ? null : "FreeImage could not write " + job.Path;
        }
        finally { FreeImage_Unload(dib); }
    }

    static string WriteFreeImage(Job job, bool alpha, int fif, int flags, string temp)
    {
        int bpp = alpha ? 32 : 24, bytes = bpp / 8;
        IntPtr dib = FreeImage_Allocate(job.Width, job.Height, bpp, 0x00FF0000, 0x0000FF00, 0x000000FF);
        if (dib == IntPtr.Zero) return "Not enough memory to save " + job.Path;
        try
        {
            IntPtr bits = FreeImage_GetBits(dib);
            int pitch = (int)FreeImage_GetPitch(dib);
            var row = new byte[pitch];
            for (int y = 0; y < job.Height; y++)
            {
                // FreeImage and Unity both store the bottom row first; FreeImage wants B, G, R(, A).
                for (int x = 0; x < job.Width; x++)
                {
                    Color32 c = job.Pixels[y * job.Width + x];
                    int o = x * bytes;
                    row[o] = c.b; row[o + 1] = c.g; row[o + 2] = c.r;
                    if (alpha) row[o + 3] = c.a;
                }
                Marshal.Copy(row, 0, new IntPtr(bits.ToInt64() + (long)y * pitch), pitch);
            }
            return FreeImage_SaveU(fif, dib, temp, flags) ? null : "FreeImage could not write " + job.Path;
        }
        finally { FreeImage_Unload(dib); }
    }

    static bool HasAlpha(Color32[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++) if (pixels[i].a != 255) return true;
        return false;
    }

    static byte[] Rgba(Color32[] pixels)
    {
        var data = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++) { var c = pixels[i]; data[i * 4] = c.r; data[i * 4 + 1] = c.g; data[i * 4 + 2] = c.b; data[i * 4 + 3] = c.a; }
        return data;
    }

    static byte[] Rgb(Color32[] pixels)
    {
        var data = new byte[pixels.Length * 3];
        for (int i = 0; i < pixels.Length; i++) { var c = pixels[i]; data[i * 3] = c.r; data[i * 3 + 1] = c.g; data[i * 3 + 2] = c.b; }
        return data;
    }
}
