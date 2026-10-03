using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// "Set as …" without freezing (Materialize CE). The panels used ReadPixels, which stalls until the GPU has
/// finished, then Apply(), which rebuilds every mip level on the CPU: at 8K, both freeze the window. Here the
/// pixels come back asynchronously while the interface keeps running, and the mip levels are built on the GPU
/// and copied in. Level 0 (what saving reads) is the exact result, as before.
/// </summary>
public static class GpuReadback
{
    /// <summary>Coroutine: reads <paramref name="source"/> into a new readable RGBA32 texture with mipmaps, then hands it over.</summary>
    public static IEnumerator Into(RenderTexture source, Action<Texture2D> assign)
    {
        int w = source.width, h = source.height;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { filterMode = FilterMode.Trilinear, anisoLevel = 9 };   // sharp at grazing angles, no mip seams
        bool done = false;

        if (SystemInfo.supportsAsyncGPUReadback)
        {
            var request = AsyncGPUReadback.Request(source, 0, TextureFormat.RGBA32);
            while (!request.done) yield return null;
            if (!request.hasError)
            {
                texture.SetPixelData(request.GetData<byte>(), 0);
                done = true;
            }
        }
        if (!done)
        {
            // Fallback: the original synchronous path.
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            texture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = previous;
            texture.Apply(true, false);
            assign(texture);
            yield break;
        }

        // The pixels are already on the GPU: level 0 and the smaller levels (made there) are copied GPU to GPU,
        // instead of uploading 256 MB back for an 8K map and building mips on the CPU. The CPU copy set above
        // stays exact for saving; a later Apply() would rebuild the mips from it.
        var mipped = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            useMipMap = true,
            autoGenerateMips = false,
        };
        mipped.Create();
        Graphics.Blit(source, mipped);
        mipped.GenerateMips();
        for (int level = 0; level < texture.mipmapCount && level < mipped.mipmapCount; level++)
            Graphics.CopyTexture(mipped, 0, level, texture, 0, level);
        mipped.Release();
        UnityEngine.Object.Destroy(mipped);
        assign(texture);
    }
}
