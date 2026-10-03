Skyrim pack for Materialize CE
==============================

Export profiles, render presets and test meshes for Skyrim Special Edition.


EXPORT PROFILES (Export block > profile list)

- Skyrim SE
    (diffuse)  albedo x AO .............................. BC7
    _n         normal (DirectX) + gloss in alpha ........ BC7
    _p         height, for the Parallax shader .......... BC4
    _m         R environment mask, G gloss, B metal,
               A height ................................. BC7
  The _m works for vanilla "Environment Map" shaders (they read its red channel) and for Complex Material.

- Skyrim PBR (Community Shaders)
    (diffuse), _n, _rmaos (roughness, metallic, AO), _p


RENDER PRESETS (Material & lighting > Preview)

- Skyrim SE
  The vanilla lighting, following NifSkope's Skyrim shader: specular masked by the gloss (the _n alpha) and not
  scaled by the light's angle, environment map added to the colour before the lighting (it darkens in shade),
  masked by the metallic map (the _m red channel).
- Skyrim Complex Material
  Gloss per pixel, metal tinting the highlights and reflections (ENB / Community Shaders).
- Skyrim PBR (Community Shaders)
  Physically based lighting, Metallic / Roughness workflow.

In the Skyrim modes, "Game-like parallax" (on by default) shows the height the way the game does: a texture
offset (height x 0.08 - 0.04), much flatter than Materialize's tessellated relief. Untick it to see the full relief.


TEST MESHES (Test meshes folder)

Three 2 m x 2 m planes (140 x 140 units), 16 x 16 quads, UVs 0..1, to check your textures in NifSkope or in game:

- MaterializeCE_TestPlane.nif            Default shader:          diffuse + _n
- MaterializeCE_TestPlane_Parallax.nif   Parallax shader:         diffuse + _n + _p
- MaterializeCE_TestPlane_EnvMap.nif     Environment Map shader:  diffuse + _n + _m, cubemap
                                         textures\cubemaps\dynamic1pxcubemap_black.dds (Dynamic Cubemaps,
                                         Community Shaders: replaced in game by the real surroundings)

They all read textures\MaterializeCE\TestPlane*.dds. In Materialize CE: Export with the "Skyrim SE" profile,
name "TestPlane", into your game's Data\Textures\MaterializeCE folder, then open the planes.
To place one in game, put it in Data\Meshes and use it on a static object in the Creation Kit.


INSTALL

Copy this "Skyrim" folder into the "Addons" folder of your Materialize CE folder
(the one chosen at the first launch, e.g. Documents\Materialize CE\Addons),
or into the "Addons" folder next to Materialize.exe. Restart Materialize CE.


SKYRIM'S CUBEMAPS (optional, for in-game looking reflections in the preview)

Download "HDRIs for all of Skyrim's cubemaps" by its author on Nexus Mods
(https://www.nexusmods.com/skyrimspecialedition/mods/44400) and put its .png files
in this pack's "HDRI" folder (Addons\Skyrim\HDRI). They appear under "SKYRIM" in Environment.
They are not included here: they are Bethesda's game assets, shared by their own mod author.


CREDITS

Skyrim lighting reference: NifSkope, fo76utils fork (BSD licence) and the "SP Skyrim Multilayer Parallax Shader"
by Darkluke1111. Materialize CE: https://github.com/Cl3mus33
