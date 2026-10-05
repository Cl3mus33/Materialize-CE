# Materialize CE

**Materialize Community Edition** is a fork of [Materialize](https://github.com/BoundingBoxSoftware/Materialize) by Bounding Box Software, the free tool that turns a photo into a full material (height, normal, AO, metallic, smoothness…).

The original has not been updated since 2018. This edition moves it to Unity 6 and reworks the interface, the preview rendering and the export, while keeping the original tools.

> **Alpha test version.** This is an early build shared to collect feedback before a first proper release. Expect bugs and rough edges. Bug reports and, above all, **suggestions for improvement** are very welcome: please open an [issue](https://github.com/Cl3mus33/Materialize-CE/issues).

Windows 64-bit only. Interface in English and French.

## What changes from the original

### Interface
- New layout: a toolbar, the list of maps on the left, the properties of the selected map next to it, material and lighting on the right. The window can be resized, maximised or full screen (F11), and its place is remembered.
- Tooltips on every control, notifications instead of silent failures, native Windows file dialogs.
- A workspace folder (projects, textures, export profiles, render presets, HDRIs, add-ons) and recent projects.
- Mixer-style viewport shortcuts.

### Maps and tools
- Fast loading and saving, done in the background. 16-bit and EXR height maps keep their precision; modern DDS files (BC7, BC5…) can be opened.
- Adjust any map after it is made: levels with a histogram, smoothing, a before / after slider, undo and redo.
- Metallic / Specular and Roughness / Glossiness workflows, switchable at any time.
- New maps: **Curvature** (replaces Edge), **Emission**, **Subsurface**.
- Height from normal by exact integration, next to the original method.
- Sharper normals (Scharr filter) and a horizon-based ambient occlusion, both optional.
- Shading removal in Edit Albedo, guided by the normal and AO maps, with automatic light detection.
- **Retouch**: a clone stamp that paints on every map at once, across the tiling seams.
- Smoother seamless tiling (Splat).

### Preview
- A modern, linear PBR preview (GGX, image-based lighting, specular occlusion, height self-shadowing). The original rendering is kept as a preset.
- HDRI environments read in real HDR, with seven [Poly Haven](https://polyhaven.com) skies included (CC0). The light can follow the sun of the HDRI.
- Render presets: load, save and share the look of the preview.
- Preview on your own model: Wavefront `.obj`, or a game mesh `.nif` (Skyrim LE / SE, Fallout 4). On a mesh made of several parts, choose which parts show the material.

### Export
- **Export profiles**: one click writes all the files a game or engine expects, each with its own channels, name suffix, format, size and normal convention (OpenGL / DirectX). Profiles for Unreal, Unity HDRP and glTF are built in; profiles are plain JSON files you can edit and share.
- A **channel packer** replaces the Property Map: put any map in any channel.
- DDS export with BC1, BC4, BC5 and BC7 compression, linear or sRGB, through Microsoft's texconv (included).
- Batch export: the texture sets of a folder, one after the other, with the current profile.

## Skyrim pack

The Skyrim content is an optional add-on, kept in [`Addons/Skyrim`](Addons/Skyrim):

- export profiles for **vanilla / Complex Material** (diffuse, `_n`, `_p`, `_m`) and for **PBR with Community Shaders** (albedo, `_n`, `_rmaos`, `_p`), with the right compression for each file;
- render presets that imitate the game: vanilla, Complex Material, PBR (Community Shaders);
- three test planes (`.nif`) to check a texture set in NifSkope or in game.

The export presets are ready to use. **The Skyrim previews are a first pass**: not much time went into them yet, and they will need some tuning before they match the game exactly. Comparisons with in-game screenshots are especially useful.

To install it, copy the `Skyrim` folder into the `Addons` folder of your workspace (see its README).

## Building

1. Install **Unity 6000.6.3f1**.
2. Open the project, or build from the command line:

   ```
   Unity.exe -batchmode -quit -projectPath <this folder> -executeMethod BuildScript.BuildWindows
   ```

The build is written to `Build/Windows`. DDS export needs the Microsoft Visual C++ runtime (already present on most PCs).

## Licence and credits

Materialize CE is released under the **GNU General Public License v3.0**, like the original: see [LICENSE](LICENSE).

- Materialize: Bounding Box Software (Mike Voeller).
- [FreeImage](https://freeimage.sourceforge.io/) and the libraries it contains, for reading and writing images.
- [texconv](https://github.com/microsoft/DirectXTex) (DirectXTex, Microsoft, MIT licence), for DDS files.
- NiflyDLL from [PyNifly](https://github.com/BadDogSkyrim/PyNifly) by BadDogSkyrim, built on [nifly](https://github.com/ousnius/nifly) by ousnius (GPL-3.0), for `.nif` meshes.
- HDRIs from [Poly Haven](https://polyhaven.com) (CC0); authors listed in `Assets/StreamingAssets/HDRI/CREDITS.txt`.
- Open Sans font (SIL Open Font Licence).
- Skyrim lighting reference: NifSkope, and the "SP Skyrim Multilayer Parallax Shader" by Darkluke1111.

The full licence texts are in `Assets/StreamingAssets/Licenses`.

Materialize CE is developed by Clemus. If you want to support the work: [Ko-fi](https://ko-fi.com/clemus).
