using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What every setting does, shown as a tooltip (Materialize CE). The same title can mean different things in
/// different panels ("Final Contrast", "Edge Amount"…), so each panel window sets <see cref="Panel"/> first and
/// the lookup tries "Panel|Title" before "Title".
/// </summary>
public static class UiHelp
{
    /// <summary>Set by each panel at the top of its window function.</summary>
    public static string Panel = "";

    public static GUIContent Content(string title) => new GUIContent(L.T(title), L.T(Tip(title)));

    public static string Tip(string title)
    {
        string key = (title ?? "").Trim().TrimEnd(':').Trim();
        if (tips.TryGetValue(Panel + "|" + key, out var tip)) return tip;
        return tips.TryGetValue(key, out tip) ? tip : "";
    }

    static readonly Dictionary<string, string> tips = new Dictionary<string, string>
    {
        // ---------- Shared ----------
        { "Final Contrast", "Contrast of the result: above 1 separates dark and bright more, below 1 flattens." },
        { "Final Bias", "Shifts the whole result darker (negative) or brighter (positive)." },
        { "Final Gain", "Overall strength of the result, before contrast and bias." },
        { "Pre Contrast", "Contrast of the source before processing: raise it when the source is flat and details get lost." },
        { "Presets", "Ready-made settings for the sliders below." },
        { "Default", "Back to the default settings." },
        { "Pick Color", "Click, then click a point of the image: that colour becomes the sample." },
        { "Hue", "How far the hue may differ from the picked colour and still count (tolerance)." },
        { "Sat", "Tolerance on saturation: how grey or vivid a pixel may be compared to the sample." },
        { "Lum", "Tolerance on brightness compared to the sample." },
        { "Low", "Lower end of the mask: pixels matching less than this are left out." },
        { "High", "Upper end of the mask: pixels matching more than this count fully." },
        { "Isolate Mask", "Shows only this sample's mask, to check which pixels it selects." },
        { "Use Color Sample 1", "Uses a picked colour to push the matching pixels to their own value." },
        { "Use Color Sample 2", "A second picked colour, with its own value." },
        { "Albedo", "Works from the edited albedo (after Edit Albedo)." },
        { "Original Albedo", "Works from the albedo as it was loaded." },
        { "Use Edited Albedo", "Works from the edited albedo (after Edit Albedo)." },
        { "Use Original Albedo", "Works from the albedo as it was loaded." },
        { "Normal", "Works from the normal map instead of the albedo." },
        { "-", "" },

        // ---------- Height from diffuse ----------
        { "Height|Height Reveal Slider", "Drag to compare the height map with the albedo it comes from." },
        { "Height|Sample Spread", "How far around each pixel the heights are gathered. Larger = broader shapes, smaller = finer relief." },
        { "Height|Sample Spread Boost", "Extra spreading of the large shapes, for strong overall relief." },
        { "Height|Frequency Weight Equalizer", "Each slider is a scale of detail, from the finest (left) to the largest (right): how much it counts in the height." },
        { "Height|Frequency Contrast Equalizer", "Contrast of each scale of detail, from the finest (left) to the largest (right)." },
        { "Height|Details", "Preset favouring fine details: fabric, sand, small grain." },
        { "Height|Displace", "Preset favouring large shapes: stones, bricks, real displacement." },
        { "Height|Cracks", "Preset that deepens thin dark lines: cracks and joints." },
        { "Height|Funky", "An unusual mix of scales, to experiment." },
        { "Height|Height", "Height given to the pixels of this colour sample: raise stones, sink joints." },
        { "Height|Sample Blend", "How strongly the colour samples replace the computed height." },
        { "Height|Set as Height Map", "Keeps this result as the height map." },
        { "Height|Final Contrast", "Contrast of the height: above 1 = deeper relief." },

        // ---------- Normal from height ----------
        { "Normal|Normal Reveal Slider", "Drag to compare the normal map with the height it comes from." },
        { "Normal|Frequency Equalizer", "Strength of each scale of detail in the normal, from the finest (left) to the largest (right)." },
        { "Normal|Smooth", "Preset for soft, rounded relief." },
        { "Normal|Crisp", "Preset for sharp, detailed relief." },
        { "Normal|Mids", "Preset favouring medium-sized details." },
        { "Normal|Angular Intensity", "Strength of the relief guessed from the albedo's lighting (shape recognition)." },
        { "Normal|Angularity Amount", "How flat-faced and angular that guessed relief is, like cut stone." },
        { "Normal|Shape from Albedo (Uncheked from Height)", "Checked: the shape recognition reads the albedo. Unchecked: it reads the height map." },
        { "Normal|Shape Recognition, Rotation, Spread, Bias", "Recognises the light direction baked in the photo: its angle, reach and balance." },
        { "Normal|Precise slopes (Scharr filter)", "Measures each slope from the 8 neighbouring pixels, centred on the pixel: cleaner normals with less noise, not shifted by half a pixel. Uncheck for the original Materialize result." },
        { "Normal|Final Contrast", "Strength of the normal map: above 1 = stronger slopes." },
        { "Normal|Set as Normal Map", "Keeps this result as the normal map." },

        // ---------- AO ----------
        { "AO|AO pixel Spread", "How far around each pixel the occlusion is searched. Larger = broader, softer shadows in the hollows." },
        { "AO|Pixel Depth", "How deep the relief is taken to be: higher = darker crevices." },
        { "AO|Horizon AO (slopes stay clear)", "Measures the occlusion of the depth AO against the surface's own tilt, and lets far relief count less. A plain slope no longer darkens itself; crevices stay dark. Uncheck for the original Materialize AO." },
        { "AO|Blend Normal AO and Depth AO", "Mix between the AO from the normal map (fine details) and the AO from the height map (large shapes)." },
        { "AO|AO Power", "Contrast of the AO: higher = darker, tighter shadows." },
        { "AO|AO Bias", "Shifts the whole AO brighter or darker." },
        { "AO|Set as AO Map", "Keeps this result as the ambient occlusion map." },

        // ---------- Edge ----------
        { "Edge|Frequency Equalizer", "Which scales of detail make edges, from the finest (left) to the largest (right)." },
        { "Edge|Displace", "Preset for large shapes: edges of stones and bricks." },
        { "Edge|Soft", "Preset for soft, wide edges." },
        { "Edge|Tight", "Preset for thin, sharp edges." },
        { "Edge|Edge Amount", "Brightness of the raised edges and ridges (where wear shows first)." },
        { "Edge|Crevice Amount", "Darkness of the hollows and crevices (where dirt gathers)." },
        { "Edge|Pinch", "Makes the edges thinner and sharper." },
        { "Edge|Pillow", "Makes the edges rounder and wider." },
        { "Edge|Set as Edge Map", "Keeps this result as the edge map." },

        // ---------- Edit diffuse ----------
        { "EditDiffuse|Albedo Reveal Slider", "Drag to compare the edited albedo with the original." },
        { "EditDiffuse|Average Color Blur Size", "Size of the blur that estimates the lighting of the photo. Larger = removes broad light gradients." },
        { "EditDiffuse|Overlay Blur Size", "Size of the details kept while the lighting is removed." },
        { "EditDiffuse|Overlay Blur Contrast", "Contrast of those kept details." },
        { "EditDiffuse|Light Mask Power", "How strictly the bright areas are selected for light removal." },
        { "EditDiffuse|Remove Light", "How much of the baked highlights to remove." },
        { "EditDiffuse|Shadow Mask Power", "How strictly the dark areas are selected for shadow removal." },
        { "EditDiffuse|Remove Shadow", "How much of the baked shadows to remove." },
        { "EditDiffuse|Hot Spot Removal", "Evens out small bright spots (reflections, glare)." },
        { "EditDiffuse|Dark Spot Removal", "Evens out small dark spots." },
        { "EditDiffuse|Keep Original Color", "Brings back the photo's original colours on top of the corrected brightness." },
        { "EditDiffuse|Saturation", "Colour intensity: 0 = grey." },
        { "EditDiffuse|Set as Albedo", "Keeps this result as the albedo." },

        // ---------- Metallic ----------
        { "Metallic|Metalic Reveal Slider", "Drag to compare the metallic map with the albedo." },
        { "Metallic|Blur Size", "Softens the metal mask: fewer isolated pixels." },
        { "Metallic|Overlay Blur Size", "Size of the albedo details added back into the mask." },
        { "Metallic|High Pass Overlay", "How much of those details go into the mask: scratches and wear on the metal." },
        { "Metallic|Final Contrast", "Contrast of the metal mask: higher = clearer split between metal and non-metal." },
        { "Metallic|Set as Metallic", "Keeps this result as the metallic map." },

        // ---------- Smoothness ----------
        { "Smoothness|Smoothness Reveal Slider", "Drag to compare the smoothness map with the albedo." },
        { "Smoothness|Metal Smoothness", "Smoothness given to the metal (from the metallic map). Polished metal is high." },
        { "Smoothness|Smooth", "Smoothness given to the pixels of this colour sample." },
        { "Smoothness|Base Smoothness", "Smoothness of everything that is neither metal nor a colour sample." },
        { "Smoothness|Sample Blur Size", "Softens the colour sample masks." },
        { "Smoothness|High Pass Blur Size", "Size of the albedo details added to the smoothness." },
        { "Smoothness|High Pass Overlay", "How much of those details go in: small variations of shine." },
        { "Smoothness|Set as Smoothness", "Keeps this result as the smoothness map (roughness is its inverse)." },

        // ---------- Tiling ----------
        { "Tiling|Technique Overlap", "Blends each border with the opposite one, taller details passing over the others (uses the height map)." },
        { "Tiling|Technique Splat", "Scatters rotated pieces of the texture over itself: breaks the visible repetition." },
        { "Tiling|New Texture Size X", "Width of the tiled texture." },
        { "Tiling|New Texture Size Y", "Height of the tiled texture." },
        { "Tiling|Edge Falloff", "Softness of the blend between the borders: low = sharp cut following the height, high = gentle fade." },
        { "Tiling|Overlap X", "Width of the band blended on the left and right borders." },
        { "Tiling|Overlap Y", "Height of the band blended on the top and bottom borders." },
        { "Tiling|Splat Rotation", "Rotation of the scattered pieces." },
        { "Tiling|Splat Random Rotation", "Random extra rotation per piece." },
        { "Tiling|Splat Scale", "Size of the scattered pieces." },
        { "Tiling|Splat Wooble Amount", "Random shift of each piece, so they do not line up." },
        { "Tiling|Splat Randomize", "Shifts the random placement of the pieces gradually." },
        { "Tiling|New Pattern", "Draws a new random arrangement of the pieces. The pattern stays the same while you move the sliders." },
        { "Tiling|Tiling Test Variables", "Preview only: repeat the texture to check the seams." },
        { "Tiling|Texture Tiling", "How many times the texture repeats on the preview." },
        { "Tiling|Texture Offset X", "Shifts the preview horizontally, to bring a seam into view." },
        { "Tiling|Texture Offset Y", "Shifts the preview vertically, to bring a seam into view." },
        { "Tiling|Set Maps", "Applies the tiling to every map." },

        // ---------- Alignment ----------
        { "Alignment|Alignment Reveal Slider", "Drag to compare before and after the correction." },
        { "Alignment|Preview Map", "Which map is shown while adjusting." },
        { "Alignment|Original Albedo Map", "Adjust while looking at the original albedo." },
        { "Alignment|Albedo Map", "Adjust while looking at the edited albedo." },
        { "Alignment|Height Map", "Adjust while looking at the height map." },
        { "Alignment|Metallic Map", "Adjust while looking at the metallic map." },
        { "Alignment|Smoothness Map", "Adjust while looking at the smoothness map." },
        { "Alignment|Edge Map", "Adjust while looking at the edge map." },
        { "Alignment|AO Map", "Adjust while looking at the ambient occlusion map." },
        { "Alignment|Lens Distort Correction", "Straightens the curved lines of a wide-angle lens (barrel distortion)." },
        { "Alignment|Perspective Correction X", "Corrects a photo taken from the side." },
        { "Alignment|Perspective Correction Y", "Corrects a photo taken from above or below." },
        { "Alignment|Reset Points", "Puts the four corner points back in place." },
        { "Alignment|Set All Maps", "Applies the correction to every map." },

        // ---------- Full material ----------
        { "Material|Preset", "A saved look: render mode, Skyrim settings, multipliers and light. Save your own with the field and button below." },
        { "Material|Render mode", "How the preview lights the material: like PBR engines, or like Skyrim (vanilla or Complex Material). The saved maps do not change." },
        { "Material|Specular Strength", "Skyrim: brightness of the highlights, like Specular Strength in the NIF (BSLightingShaderProperty)." },
        { "Material|Glossiness", "Skyrim: sharpness of the highlights, like Glossiness in the NIF. Higher = smaller, tighter highlights. In Complex Material, the gloss map scales it per pixel." },
        { "Material|Environment", "Skyrim Complex Material: strength of the environment reflections on the metal (the _m red channel)." },
        { "Material|Metallic Multiplier", "Preview only: strength of the metallic map." },
        { "Material|Smoothness Multiplier", "Preview only: strength of the smoothness map." },
        { "Material|Parallax Displacement", "Preview only: how deep the height map looks on the surface." },
        { "Material|Edge Amount", "Preview only: how much the edge map brightens edges and darkens crevices." },
        { "Material|Ambient Occlusion Power", "Preview only: strength of the ambient occlusion." },
        { "Material|Light Color", "Colour of the preview light (R, G, B sliders)." },
        { "Material|Intensity", "Brightness of the preview light." },
        { "Material|R", "Red part of the light colour." },
        { "Material|G", "Green part of the light colour." },
        { "Material|B", "Blue part of the light colour." },
        { "Material|Texture Tiling X", "How many times the textures repeat across the preview shape." },
        { "Material|Texture Tiling Y", "How many times the textures repeat down the preview shape." },
        { "Material|Texture Offset X", "Shifts the textures horizontally on the shape." },
        { "Material|Texture Offset Y", "Shifts the textures vertically on the shape." },
        { "Material|Plane", "Preview on a flat plane." },
        { "Material|Cube", "Preview on a cube." },
        { "Material|Cylinder", "Preview on a cylinder." },
        { "Material|Sphere", "Preview on a sphere." },

        // ---------- Adjust ----------
        { "Adjust|Normal Strength", "Steepness of the normal map's slopes: 0 = flat, 1 = as it is, above 1 = stronger relief." },
        { "Adjust|Flip green (OpenGL ↔ DirectX)", "Inverts the green channel: switches the normal map between OpenGL (Unity, Blender) and DirectX (Skyrim, Unreal)." },
        { "Adjust|Displacement Strength", "Depth of the relief: 0 = flat (all white), 1 = as it is, above 1 = deeper. White stays the top, dark areas go down, as in Texalys." },
        { "Adjust|Stretch to the full range", "Sets the black and white points to the darkest and brightest values of the map: a flat-looking height uses the whole range. Useful when parallax or normals barely show." },
        { "Adjust|Black Point", "Values at or below this become black (levels)." },
        { "Adjust|White Point", "Values at or above this become white (levels)." },
        { "Adjust|Midtones", "Moves the middle greys: above 1 brighter, below 1 darker, black and white unchanged." },
        { "Adjust|Contrast", "Separates dark and bright more (above 1) or less (below 1)." },
        { "Adjust|Reveal", "Slide to compare: on the Map side the adjusted map is shown flat, on the Render side the lit material. All the way to Map shows the map alone." },
        { "Adjust|Smooth (blur)", "Softens the map by a fraction of a pixel up to 4 pixels: removes grain, JPEG blocks and harsh noise without losing the shapes. The borders wrap, so tiling maps stay seamless." },
        { "Adjust|Brightness", "Shifts the whole map darker or brighter." },
        { "Adjust|Saturation", "Colour intensity of the albedo: 0 = grey." },
        { "Adjust|Invert", "Swaps black and white: smoothness ↔ roughness, height ↔ depth." },
        { "Adjust|Reset", "Back to the original map's values." },
        { "Adjust|Cancel", "Closes without changing the map." },
        { "Adjust|Apply", "Writes the adjustment into the map. Adjusting again later starts from the original, so nothing degrades." },

        // ---------- Post process ----------
        { "PostProcess|Enable Post Process", "Turns the preview effects on or off. They never change the saved maps." },
        { "PostProcess|Bloom Threshold", "How bright a highlight must be to glow." },
        { "PostProcess|Bloom Amount", "Strength of the glow around highlights." },
        { "PostProcess|Lens Flare Amount", "Strength of the lens flares." },
        { "PostProcess|Lens Dirt Amount", "Dirt on the virtual lens, visible in the glow." },
        { "PostProcess|Vignette Amount", "Darkens the corners of the view." },
        { "PostProcess|Filmic tone mapping (ACES)", "Rolls the highlights off smoothly, like a film camera, instead of clipping them flat to white: shiny metal and wet surfaces keep their detail. Preview only." },
        { "PostProcess|Exposure", "Brightness of the preview before the tone mapping. Preview only: the saved maps are not affected." },
        { "PostProcess|DOF Max Blur", "Maximum blur of what is out of focus (depth of field)." },
        { "PostProcess|DOF Focal Depth", "Distance that stays sharp." },
        { "PostProcess|DOF Max Distance", "Distance at which the blur is complete." },
        { "PostProcess|Use Auto Focus", "Keeps the point under the centre of the view sharp." },
    };
}
