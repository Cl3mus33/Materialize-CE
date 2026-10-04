using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

public class MaterialSettings {

	// Materialize CE: how the preview lights the material (0 PBR, 1 Skyrim vanilla, 2 Skyrim Complex Material).
	public int RenderMode = 0;
	// Materialize CE: the project's workflow (-1 = not saved, older projects).
	public int WorkflowSpecular = -1;
	public int WorkflowGloss = -1;
	public float SpecStrength = 1.0f;
	public string SpecStrengthText = "1";
	public float SpecPower = 80.0f;
	public string SpecPowerText = "80";
	public float EnvStrength = 1.0f;
	public string EnvStrengthText = "1";
	// NIF specular colour and the filmic tone curve of the Skyrim modes.
	public float SpecColorR = 1.0f, SpecColorG = 1.0f, SpecColorB = 1.0f;
	public bool SkyrimTone = true;
	// Rendering quality of the PBR preview (0 = off, 1 = full). Materialize CE.
	public float QualitySpecOcclusion = 1f, QualityMicroShadow = 0f, QualitySelfShadow = 1f, QualityFineRelief = 0f, QualitySpecAA = 1f;
	// The modern PBR preview: linear light, GGX, image-based light (off: Materialize's original rendering).
	public bool EnhancedRender = true;
	/// <summary>PBR lit as Skyrim's Community Shaders do (True PBR): gamma-space diffuse, 0.65 scale, strong ambient.</summary>
	public bool CommunityShaders;
	/// <summary>Community Shaders look: the game's light level against the preview's (weather, eye adaptation).</summary>
	public float GameExposure = 1f;
	/// <summary>Preview only: brightness of the emission map.</summary>
	public float EmissionStrength = 1f;
	public string EmissionStrengthText = "1";
	public string GameExposureText = "1";
	// Skyrim modes: the game's offset parallax instead of tessellated displacement.
	public bool SkyrimParallax = true;

	[DefaultValueAttribute(1.0f)]
	public float TexTilingX = 1.0f;
	[DefaultValueAttribute(1.0f)]
	public float TexTilingY = 1.0f;
	[DefaultValueAttribute(0.0f)]
	public float TexOffsetX = 0.0f;
	[DefaultValueAttribute(0.0f)]
	public float TexOffsetY = 0.0f;

	[DefaultValueAttribute("1")]
	public string TexTilingXText;
	[DefaultValueAttribute("1")]
	public string TexTilingYText;
	[DefaultValueAttribute("0")]
	public string TexOffsetXText;
	[DefaultValueAttribute("0")]
	public string TexOffsetYText;

	[DefaultValueAttribute(1.0f)]
	public float Metallic;
	[DefaultValueAttribute(1.0f)]
	public float Smoothness;
	[DefaultValueAttribute(0.5f)]
	public float Parallax;
	[DefaultValueAttribute(1.0f)]
	public float EdgePower;
	[DefaultValueAttribute(1.0f)]
	public float AOPower;

	[DefaultValueAttribute("1")]
	public string MetallicText;
	[DefaultValueAttribute("1")]
	public string SmoothnessText;
	[DefaultValueAttribute("0.5")]
	public string ParallaxText;
	[DefaultValueAttribute("1")]
	public string EdgePowerText;
	[DefaultValueAttribute("1")]
	public string AOPowerText;

	[DefaultValueAttribute(1.0f)]
	public float LightR;
	[DefaultValueAttribute(1.0f)]
	public float LightG;
	[DefaultValueAttribute(1.0f)]
	public float LightB;
	[DefaultValueAttribute(1.0f)]
	public float LightIntensity;
	[DefaultValueAttribute("1")]
	public string LightIntensityText;

	public MaterialSettings(){

		this.TexTilingX = 1.0f;
		this.TexTilingY = 1.0f;
		this.TexOffsetX = 0.0f;
		this.TexOffsetY = 0.0f;

		this.TexTilingXText = "1";
		this.TexTilingYText = "1";
		this.TexOffsetXText = "0";
		this.TexOffsetYText = "0";

		this.Metallic = 1.0f;
		this.Smoothness = 1.0f;
		this.Parallax = 0.5f;
		this.EdgePower = 1.0f;
		this.AOPower = 1.0f;

		this.MetallicText = "1";
		this.SmoothnessText = "1";
		this.ParallaxText = "0.5";
		this.EdgePowerText = "1";
		this.AOPowerText = "1";

		this.LightR = 1.0f;
		this.LightG = 1.0f;
		this.LightB = 1.0f;
		this.LightIntensity = 1.0f;
		this.LightIntensityText = "1";

	}

}

public class MaterialGui : MonoBehaviour {

	MainGui MainGuiScript;

	Texture2D _HeightMap;
	Texture2D _DiffuseMap;
	Texture2D _NormalMap;
	Texture2D _EdgeMap;
	Texture2D _MetallicMap;
	Texture2D _SmoothnessMap;
	Texture2D _AOMap;

	Texture2D myColorTexture;

	Material thisMaterial;

	public GameObject LightObject;
	public ObjRotator testRotator;

	public GameObject testObjectParent;
	public GameObject testObject;
	public GameObject testObjectCube;
	public GameObject testObjectCylinder;
	public GameObject testObjectSphere;

	bool planeShown = true;
	bool cubeShown = false;
	bool cylinderShown = false;
	bool sphereShown = false;
	float dispOffset = 0.5f;

	bool settingsInitialized = false;

	Rect windowRect = new Rect (30, 300, 300, 530);
	int extraHeight = 54;

	MaterialSettings MatS;
	public int RenderModeIndex => MatS != null ? MatS.RenderMode : 0;

	void OnDisable () {

		if ( MainGuiScript.hideGui == false ) {

			if (!testObjectParent.activeSelf) {
				testRotator.Reset ();
			}

			testObjectParent.SetActive (true);
			testObjectCube.SetActive (false);
			testObjectCylinder.SetActive (false);
			testObjectSphere.SetActive (false);
		}

	}

	// Use this for initialization
	void Start () {
		InitializeSettings ();
	}

	public void GetValues( ProjectObject projectObject ) {
		InitializeSettings ();
		MatS.WorkflowSpecular = Workflow.Specular ? 1 : 0;
		MatS.WorkflowGloss = Workflow.Gloss ? 1 : 0;
		projectObject.MatS = MatS;
	}

	public void SetValues( ProjectObject projectObject ) {
		InitializeSettings ();
		if (projectObject.MatS != null) { 
			MatS = projectObject.MatS;
			lookWhenChosen = null;   // the project's look is the new reference
			if (MatS.WorkflowSpecular >= 0) Workflow.Specular = MatS.WorkflowSpecular == 1;
			if (MatS.WorkflowGloss >= 0) Workflow.Gloss = MatS.WorkflowGloss == 1;
		} else {
			settingsInitialized = false;
			InitializeSettings ();
		}
	}

	void InitializeSettings() {

		if (settingsInitialized == false) {
			Debug.Log ("Initializing MaterialSettings");
			MatS = new MaterialSettings ();
			myColorTexture = new Texture2D (1, 1, TextureFormat.ARGB32, false);
			settingsInitialized = true;
		}

	}


	// Update is called once per frame
	Shader pbrShader, skyrimShader;
	static readonly string[] renderModes = { "PBR (Unity, Unreal, glTF)", "Skyrim SE (vanilla)", "Skyrim Complex Material" };
	List<RenderPreset> presets;
	string presetName = "PBR Metallic / Roughness", newPresetName = "";
	bool presetListOpen, modeListOpen, namingPreset;
	RenderPreset lookWhenChosen;
	GUIStyle lightingStyle;
	static readonly string[] lightingNames = { "physically based (PBR)", "Skyrim vanilla (Blinn-Phong)", "Skyrim Complex Material" };
	static readonly string[] renderModeTips = {
		"Physically based rendering, as in Unity, Unreal Engine, Godot, Blender, glTF viewers and Skyrim's Community Shaders PBR (they share this model).",
		"Skyrim Special Edition without mods: Blinn-Phong lighting, the gloss map as specular mask, one glossiness for the whole mesh, AO baked into the albedo.",
		"Skyrim Complex Material (ENB, Community Shaders): glossiness per pixel, metal tinting the reflections, environment reflections masked by the metal.",
	};

	/// <summary>Puts the chosen lighting model on the preview material (Materialize CE).</summary>
	void ApplyRenderMode () {
		// The improved PBR preview (specular occlusion, contact shadows, fine relief, specular anti-aliasing).
		// The original PBR preview, and the experimental improved one (Rendering quality > Enhanced rendering).
		if (pbrShader == null) pbrShader = thisMaterial.shader;
		if (enhancedShader == null) enhancedShader = Shader.Find ("Custom/Preview_PBR");
		if (skyrimShader == null) skyrimShader = Shader.Find ("Custom/Preview_Skyrim");
		Shader pbr = MatS.EnhancedRender && enhancedShader != null && enhancedShader.isSupported ? enhancedShader : pbrShader;
		Shader wanted = MatS.RenderMode == 0 || skyrimShader == null ? pbr : skyrimShader;
		if (thisMaterial.shader != wanted) thisMaterial.shader = wanted;
		thisMaterial.SetFloat ("_SkyrimMode", MatS.RenderMode == 2 ? 1.0f : 0.0f);
		thisMaterial.SetFloat ("_SpecStrength", MatS.SpecStrength);
		thisMaterial.SetFloat ("_SpecPower", MatS.SpecPower);
		thisMaterial.SetFloat ("_EnvStrength", MatS.EnvStrength);
		thisMaterial.SetColor ("_SpecColorNif", new Color (MatS.SpecColorR, MatS.SpecColorG, MatS.SpecColorB, 1));
		thisMaterial.SetFloat ("_SkyrimTone", MatS.SkyrimTone ? 1.0f : 0.0f);
		thisMaterial.SetFloat ("_SkyrimParallax", MatS.SkyrimParallax ? 1.0f : 0.0f);
	}

	void Update () {

		ApplySettings ();

		testObjectParent.SetActive (planeShown);
		testObjectCube.SetActive (cubeShown);
		testObjectCylinder.SetActive (cylinderShown);
		testObjectSphere.SetActive (sphereShown);
		thisMaterial.SetFloat ("_DispOffset", dispOffset);

	}

	/// <summary>The settings on the full material and the light (Materialize CE: also while a map is adjusted).</summary>
	public void ApplySettings () {
		InitializeSettings ();
		if (thisMaterial == null) thisMaterial = MainGui.instance != null ? MainGui.instance.FullMaterial : null;
		if (thisMaterial == null) return;

		ApplyRenderMode ();
		thisMaterial.SetFloat ("_Metallic", MatS.Metallic);
		thisMaterial.SetFloat ("_Smoothness", MatS.Smoothness );
		thisMaterial.SetFloat ("_Parallax", MatS.Parallax );
		thisMaterial.SetFloat ("_EdgePower", MatS.EdgePower );
		thisMaterial.SetFloat ("_AOPower", MatS.AOPower );

		thisMaterial.SetVector ("_Tiling", new Vector4 ( MatS.TexTilingX, MatS.TexTilingY, MatS.TexOffsetX, MatS.TexOffsetY ));

		LightObject.GetComponent<Light> ().color = new Color (MatS.LightR, MatS.LightG, MatS.LightB);
		LightObject.GetComponent<Light> ().intensity = MatS.LightIntensity;

		Shader.SetGlobalFloat ("_QualitySpecOcclusion", MatS.QualitySpecOcclusion);
		Shader.SetGlobalFloat ("_QualityMicroShadow", MatS.QualityMicroShadow);
		Shader.SetGlobalFloat ("_QualitySelfShadow", MatS.QualitySelfShadow);
		Shader.SetGlobalFloat ("_QualityFineRelief", MatS.QualityFineRelief);
		Shader.SetGlobalFloat ("_QualitySpecAA", MatS.QualitySpecAA);
		Shader.SetGlobalFloat ("_QualitySet", 1f);
		Shader.SetGlobalFloat ("_CsLook", MatS.CommunityShaders && MatS.RenderMode == 0 ? 1f : 0f);
		Shader.SetGlobalFloat ("_CsExposure", Mathf.Max (0.05f, MatS.GameExposure));
		Shader.SetGlobalFloat ("_MceEmissionStrength", MatS.EmissionStrength);
		bool modern = MatS.EnhancedRender || MatS.RenderMode != 0;
		if (shadowsFor != (modern ? 1 : 0)) {
			shadowsFor = modern ? 1 : 0;
			// Modern: sharper cast shadows (4096 shadow map). Original: Materialize's own settings.
			var light = LightObject.GetComponent<Light> ();
			light.shadowCustomResolution = modern ? 4096 : -1;
			light.shadowBias = modern ? 0.02f : 0.1f;
			light.shadowNormalBias = 0.2f;
			// Reflections: 1024 probe (sharp reflections, blurred by the shader); the original's was 256.
			var probe = MainGui.instance != null ? MainGui.instance.reflectionProbe : null;
			if (probe != null) {
				int res = modern ? 1024 : 256;
				if (probe.resolution != res) {
					probe.resolution = res;
					Shader.SetGlobalFloat ("_ProbeMaxMip", Mathf.Log (res, 2));
					probe.RenderProbe ();
				}
			}
		}
	}

	string FloatToString ( float num, int length ) {

		string numString = num.ToString ();
		int numStringLength = numString.Length;
		int lastIndex = Mathf.FloorToInt( Mathf.Min ( (float)numStringLength , (float)length ) );

		return numString.Substring (0, lastIndex);
	}

	void chooseLightColor( int posX, int posY ) {

		MatS.LightR = GUI.VerticalSlider( new Rect ( posX + 10, posY + 5, 30, 100 ), MatS.LightR, 1.0f, 0.0f );
		MatS.LightG = GUI.VerticalSlider( new Rect ( posX + 40, posY + 5, 30, 100 ), MatS.LightG, 1.0f, 0.0f);
		MatS.LightB = GUI.VerticalSlider( new Rect ( posX + 70, posY + 5, 30, 100 ), MatS.LightB, 1.0f, 0.0f);
		MatS.LightIntensity = GUI.VerticalSlider( new Rect ( posX + 120, posY + 5, 30, 100 ), MatS.LightIntensity, 3.0f, 0.0f);

		GUI.Label (new Rect( posX + 10, posY + 110,30,30 ), UiHelp.Content ("R"));
		GUI.Label (new Rect( posX + 40, posY + 110,30,30 ), UiHelp.Content ("G"));
		GUI.Label (new Rect( posX + 70, posY + 110,30,30 ), UiHelp.Content ("B"));
		GUI.Label (new Rect( posX + 100, posY + 110,100,30 ), UiHelp.Content ("Intensity"));

		SetColorTexture ();

		GUI.DrawTexture( new Rect( posX + 170, posY + 5, 100, 100 ), myColorTexture );
	}

	void SetColorTexture(){

		Color[] colorArray = new Color[1];
		colorArray [0] = new Color (MatS.LightR, MatS.LightG, MatS.LightB, 1.0f);

		myColorTexture.SetPixels (colorArray);
		myColorTexture.Apply ();

	}

	void DoMyWindow ( int windowID ) {
		DrawContents (10, 30);
		Tips.Capture (true);
		GUI.DragWindow();
	}

	/// <summary>The settings inside the left column of the new interface (Materialize CE). Returns the height used.</summary>
	public float DrawInline ( float y ) {
		InitializeSettings ();
		return DrawContents (0, (int)y) - y;
	}

	float DrawContents ( int offsetX, int offsetY ) {
		UiHelp.Panel = "Material";
		int top = offsetY;

		// Materialize CE: preset (a saved look), workflow and render mode.
		if (presets == null) presets = RenderPresets.All ();
		var chosen = presets.Find (x => x.Name == presetName);
		var current = RenderPreset.From (MatS, presetName);
		if (lookWhenChosen == null) lookWhenChosen = current;
		bool modified = !RenderPresets.Same (lookWhenChosen, current);
		GUI.Label (new Rect (offsetX, offsetY + 2, 60, 22), L.G ("Preview", "What the preview renders the material for: the engine or game. Each choice sets the lighting model, the workflow and the settings below. Adjust them, then save your own with Save as new."));
		string shown = (chosen == null ? L.T ("Custom") : chosen.Label) + (modified ? "  " + L.T ("(modified)") : "");
		if (GUI.Button (new Rect (offsetX + 60, offsetY, 220, 24), new GUIContent (shown + "  ▾", L.T ("Choose the engine or game: PBR (Unity, Unreal, glTF), Skyrim…")))) {
			presetListOpen = !presetListOpen;
		}
		offsetY += 28;
		if (presetListOpen) {
			for (int i = 0; i < presets.Count; i++) {
				if (GUI.Button (new Rect (offsetX + 60, offsetY, 220, 22), presets[i].Label)) {
					presets[i].ApplyTo (MatS);
					presetName = presets[i].Name;
					lookWhenChosen = RenderPreset.From (MatS, presetName);
					presetListOpen = false;
				}
				offsetY += 24;
			}
			offsetY += 4;
		}
		bool yours = chosen != null && !chosen.BuiltIn;
		GUI.enabled = yours && modified;
		if (GUI.Button (new Rect (offsetX + 60, offsetY, 70, 22), L.G("Save", "Saves the changes into this preset of yours. Built-in presets cannot be changed: use Save as new."))) {
			RenderPresets.Save (RenderPreset.From (MatS, presetName));
			presets = RenderPresets.All ();
			lookWhenChosen = RenderPreset.From (MatS, presetName);
			Notifications.Info ("Render preset saved: " + presetName);
		}
		GUI.enabled = true;
		if (GUI.Button (new Rect (offsetX + 134, offsetY, 90, 22), L.G("Save as new…", "Keeps the current look as a new preset of yours."))) {
			namingPreset = !namingPreset;
			newPresetName = chosen != null && !chosen.BuiltIn ? presetName + " 2" : "My preset";
		}
		GUI.enabled = yours;
		if (GUI.Button (new Rect (offsetX + 228, offsetY, 52, 22), L.G("Delete", "Deletes this preset of yours."))) {
			RenderPresets.Delete (chosen);
			presets = RenderPresets.All ();
			presetName = "Custom";
		}
		GUI.enabled = true;
		offsetY += 26;
		if (namingPreset) {
			newPresetName = GUI.TextField (new Rect (offsetX + 60, offsetY, 130, 22), newPresetName ?? "");
			if (GUI.Button (new Rect (offsetX + 194, offsetY, 40, 22), L.T("OK")) && !string.IsNullOrWhiteSpace (newPresetName)) {
				string name = newPresetName.Trim ();
				var existing = presets.Find (x => x.Name == name);
				if (existing != null && existing.BuiltIn) {
					Notifications.Error ("A built-in preset has this name: choose another one.");
				} else {
					RenderPresets.Save (RenderPreset.From (MatS, name));
					presets = RenderPresets.All ();
					presetName = name;
					lookWhenChosen = RenderPreset.From (MatS, name);
					namingPreset = false;
					Notifications.Info ("Render preset saved: " + name);
				}
			}
			if (GUI.Button (new Rect (offsetX + 238, offsetY, 42, 22), L.T("×"))) namingPreset = false;
			offsetY += 26;
		}
		offsetY += 6;
		// Workflow, as in Substance: which maps you work with.
		GUI.Label (new Rect (offsetX, offsetY + 2, 60, 22), L.G("Metal", "How metal is described: a metallic mask (white = metal) or a specular colour map (the albedo on metal, 4 % grey elsewhere)."));
		int spec = GUI.Toolbar (new Rect (offsetX + 60, offsetY, 220, 22), Workflow.Specular ? 1 : 0, new[] {
			L.G("Metallic", "Metallic / Roughness workflow: Unreal, Unity, glTF, Blender, Skyrim PBR and Complex Material."),
			L.G("Specular colour", "Specular / Glossiness workflow: older PBR engines. The specular map is made from the albedo and the metallic mask.") });
		Workflow.Specular = spec == 1;
		offsetY += 26;
		GUI.Label (new Rect (offsetX, offsetY + 2, 60, 22), L.G("Surface", "Roughness (white = rough) or glossiness (white = shiny, also called smoothness). One is the inverse of the other."));
		int gloss = GUI.Toolbar (new Rect (offsetX + 60, offsetY, 220, 22), Workflow.Gloss ? 1 : 0, new[] {
			L.G("Roughness", "White = rough. Unreal, glTF, Blender, Skyrim PBR."),
			L.G("Glossiness", "White = shiny (smoothness). Unity, Skyrim vanilla and Complex Material.") });
		Workflow.Gloss = gloss == 1;
		offsetY += 32;
		if (lightingStyle == null) {
			lightingStyle = new GUIStyle (GUI.skin.label) { fontSize = 11, wordWrap = true };
			lightingStyle.normal.textColor = new Color (0.6f, 0.64f, 0.7f);
		}
		GUI.Label (new Rect (offsetX, offsetY, 280, 30), new GUIContent (L.T ("Lighting:") + " " + L.T (MatS.RenderMode == 0 && MatS.CommunityShaders ? "Skyrim PBR (Community Shaders)" : lightingNames[MatS.RenderMode]), L.T (renderModeTips[MatS.RenderMode])), lightingStyle);
		offsetY += 26;
		if (MatS.RenderMode > 0) {
			GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Specular Strength", MatS.SpecStrength, MatS.SpecStrengthText, out MatS.SpecStrength, out MatS.SpecStrengthText, 0.0f, 5.0f );
			offsetY += 40;
			GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Glossiness", MatS.SpecPower, MatS.SpecPowerText, out MatS.SpecPower, out MatS.SpecPowerText, 1.0f, 500.0f );
			offsetY += 40;
			GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Environment", MatS.EnvStrength, MatS.EnvStrengthText, out MatS.EnvStrength, out MatS.EnvStrengthText, 0.0f, 3.0f );
			offsetY += 40;
			// NIF specular colour: three small sliders and the swatch.
			GUI.Label (new Rect (offsetX, offsetY, 120, 20), L.G("Specular Colour", "The NIF's Specular Color: tints the highlights (white in most vanilla meshes)."));
			var sc = new Color (MatS.SpecColorR, MatS.SpecColorG, MatS.SpecColorB);
			var oldColor = GUI.color;
			GUI.color = sc;
			GUI.DrawTexture (new Rect (offsetX + 250, offsetY + 2, 30, 16), Texture2D.whiteTexture);
			GUI.color = oldColor;
			offsetY += 22;
			MatS.SpecColorR = GUI.HorizontalSlider (new Rect (offsetX, offsetY, 86, 12), MatS.SpecColorR, 0f, 1f);
			MatS.SpecColorG = GUI.HorizontalSlider (new Rect (offsetX + 97, offsetY, 86, 12), MatS.SpecColorG, 0f, 1f);
			MatS.SpecColorB = GUI.HorizontalSlider (new Rect (offsetX + 194, offsetY, 86, 12), MatS.SpecColorB, 0f, 1f);
			offsetY += 20;
			MatS.SkyrimParallax = GUI.Toggle (new Rect (offsetX, offsetY, 280, 22), MatS.SkyrimParallax, L.G (" Game-like parallax", "As in Skyrim: the height map only shifts the texture (height × 0.08), no real relief. Off: the relief is displaced (tessellation), much deeper than in the game."));
			offsetY += 26;
			MatS.SkyrimTone = GUI.Toggle (new Rect (offsetX, offsetY, 280, 22), MatS.SkyrimTone, L.G(" Filmic tone curve (NifSkope look)", "Softens the highlights and lifts the mid-tones like NifSkope-style previews. Off: plain colours."));
			offsetY += 28;
		}
		extraHeight = offsetY - top;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Metallic Multiplier", MatS.Metallic, MatS.MetallicText, out MatS.Metallic, out MatS.MetallicText, 0.0f, 2.0f );
		offsetY += 40;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), Workflow.Gloss ? "Glossiness Multiplier" : "Smoothness Multiplier", MatS.Smoothness, MatS.SmoothnessText, out MatS.Smoothness, out MatS.SmoothnessText, 0.0f, 2.0f );
		offsetY += 40;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Parallax Displacement", MatS.Parallax, MatS.ParallaxText, out MatS.Parallax, out MatS.ParallaxText, 0.0f, 2.0f );
		offsetY += 40;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Curvature Amount", MatS.EdgePower, MatS.EdgePowerText, out MatS.EdgePower, out MatS.EdgePowerText, 0.0f, 2.0f );
		offsetY += 40;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Ambient Occlusion Power", MatS.AOPower, MatS.AOPowerText, out MatS.AOPower, out MatS.AOPowerText, 0.0f, 2.0f );
		offsetY += 40;

		if (MatS.RenderMode == 0 && MatS.CommunityShaders) {
			if (string.IsNullOrEmpty (MatS.GameExposureText)) MatS.GameExposureText = MatS.GameExposure.ToString ();
			GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Game exposure", MatS.GameExposure, MatS.GameExposureText, out MatS.GameExposure, out MatS.GameExposureText, 0.25f, 4.0f );
			offsetY += 40;
		}

		if (MainGuiScript != null && MainGuiScript._EmissionMap != null) {
			if (string.IsNullOrEmpty (MatS.EmissionStrengthText)) MatS.EmissionStrengthText = MatS.EmissionStrength.ToString ();
			GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Emission Strength", MatS.EmissionStrength, MatS.EmissionStrengthText, out MatS.EmissionStrength, out MatS.EmissionStrengthText, 0.0f, 4.0f );
			offsetY += 40;
		}

		GUI.Label (new Rect (offsetX, offsetY, 250, 30), UiHelp.Content ("Light Color"));
		chooseLightColor ( offsetX, offsetY + 20 );
		offsetY += 160;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Texture Tiling X", MatS.TexTilingX, MatS.TexTilingXText, out MatS.TexTilingX, out MatS.TexTilingXText, 0.1f, 5.0f );
		offsetY += 30;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Texture Tiling Y", MatS.TexTilingY, MatS.TexTilingYText, out MatS.TexTilingY, out MatS.TexTilingYText, 0.1f, 5.0f );
		offsetY += 50;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Texture Offset X", MatS.TexOffsetX, MatS.TexOffsetXText, out MatS.TexOffsetX, out MatS.TexOffsetXText, -1.0f, 1.0f );
		offsetY += 30;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Texture Offset Y", MatS.TexOffsetY, MatS.TexOffsetYText, out MatS.TexOffsetY, out MatS.TexOffsetYText, -1.0f, 1.0f );
		offsetY += 50;

		// Rendering quality of the PBR preview: each improvement can be compared on / off.
		if (MatS.RenderMode == 0) {
			if (qualityHead == null) {
				qualityHead = new GUIStyle (GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, padding = new RectOffset (0, 0, 0, 0), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
				qualityHead.normal.textColor = Theme.Accent;
			}
			GUI.Label (new Rect (offsetX, offsetY, 280, 20), L.T ("RENDERING QUALITY"), qualityHead);
			offsetY += 26;
			MatS.EnhancedRender = GUI.Toggle (new Rect (offsetX, offsetY, 280, 22), MatS.EnhancedRender, L.G (" Modern rendering (linear PBR)", "Lighting computed in linear light with a GGX BRDF and the environment's light (as in Substance, Marmoset, Mixer), plus reflection occlusion, contact and relief shadows, reflection anti-aliasing. Off: Materialize's original rendering."));
			offsetY += 32;
			if (MatS.EnhancedRender) {
			QualitySlider (ref offsetY, offsetX, "Reflection occlusion", "Reflections fade in crevices and where a normal-mapped reflection would point under the surface. 0 = off.", ref MatS.QualitySpecOcclusion);
			QualitySlider (ref offsetY, offsetX, "Contact shadows (AO)", "Direct light is also shaded by the AO at the bottom of crevices. 0 = off.", ref MatS.QualityMicroShadow);
			QualitySlider (ref offsetY, offsetX, "Relief shadows", "Small shadows cast by the height map itself towards the light, finer than the shadow map. 0 = off.", ref MatS.QualitySelfShadow);
			QualitySlider (ref offsetY, offsetX, "Fine relief (parallax)", "Parallax occlusion for the detail the tessellation cannot follow, down to the texel. Best on flat, finely detailed materials (sand, fabric, concrete); off by default because it can smear on steep relief. 0 = off.", ref MatS.QualityFineRelief);
			QualitySlider (ref offsetY, offsetX, "Reflection anti-aliasing", "Widens highlights where the normal changes faster than a pixel, so they stop shimmering. 0 = off.", ref MatS.QualitySpecAA);
			}
			offsetY += 6;
		}

		if (GUI.Button (new Rect (offsetX, offsetY, 60, 30), UiHelp.Content ("Plane"))) SetShape (0);
		if (GUI.Button (new Rect (offsetX + 70, offsetY, 60, 30), UiHelp.Content ("Cube"))) { RestoreCube (); SetShape (1); }
		if (GUI.Button (new Rect (offsetX + 140, offsetY, 70, 30), UiHelp.Content ("Cylinder"))) SetShape (2);
		if (GUI.Button (new Rect (offsetX + 220, offsetY, 60, 30), UiHelp.Content ("Sphere"))) SetShape (3);
		offsetY += 34;
		// Materialize CE: your own model, in place of the cube.
		if (GUI.Button (new Rect (offsetX, offsetY, 280, 24), new GUIContent (customMeshName.Length > 0 ? L.T ("Mesh: ") + customMeshName : L.T ("Load a mesh (.obj, .nif)…"), L.T ("Shows the material on your own model: a Wavefront .obj, or a game mesh (.nif of Skyrim LE / SE or Fallout 4), with UVs. It replaces the cube; the Cube button brings the cube back.")))) StartCoroutine (PickMesh ());

		return offsetY + 30;

	}

	// ---------- Viewport toggles (Materialize CE, Mixer's M / D / T) ----------

	int shape;
	float parallaxBeforeOff = -1, tilingBeforeX = -1, tilingBeforeY = -1;

	/// <summary>0 plane, 1 cube, 2 cylinder, 3 sphere.</summary>
	Mesh originalCubeMesh, customMesh;
	string customMeshName = "";

	IEnumerator PickMesh () {
		yield return null;   // out of OnGUI before the native dialog
		string path = null;
		try { path = NativeFileDialog.Show ("Open a mesh (.obj, .nif)", "*.obj;*.nif", false); } catch (System.Exception e) { Notifications.Error (e.Message); }
		if (string.IsNullOrEmpty (path)) yield break;
		string error;
		var mesh = path.EndsWith (".nif", System.StringComparison.OrdinalIgnoreCase) ? NifLoader.Load (path, out error) : ObjLoader.Load (path, out error);
		if (mesh == null) { Notifications.Error (error); yield break; }
		var filter = testObjectCube.GetComponentInChildren<MeshFilter> (true);
		if (filter == null) { Notifications.Error ("No mesh slot on the preview cube."); yield break; }
		if (originalCubeMesh == null) originalCubeMesh = filter.sharedMesh;
		// Same size on screen as the cube it replaces.
		var cubeSize = originalCubeMesh != null ? originalCubeMesh.bounds.size : Vector3.one;
		ObjLoader.Fit (mesh, Mathf.Max (cubeSize.x, Mathf.Max (cubeSize.y, cubeSize.z)));
		if (customMesh != null) Destroy (customMesh);
		customMesh = mesh;
		filter.sharedMesh = mesh;
		customMeshName = mesh.name;
		SetShape (1);
		Notifications.Info ("Mesh loaded: " + mesh.name + " (" + mesh.vertexCount + " vertices).");
	}

	void RestoreCube () {
		if (originalCubeMesh == null) return;
		var filter = testObjectCube.GetComponentInChildren<MeshFilter> (true);
		if (filter != null) filter.sharedMesh = originalCubeMesh;
		customMeshName = "";
	}

	public void SetShape (int s) {
		shape = ((s % 4) + 4) % 4;
		planeShown = shape == 0; cubeShown = shape == 1; cylinderShown = shape == 2; sphereShown = shape == 3;
		dispOffset = shape == 0 ? 1.0f : 0.25f;
		if (shape == 0) Shader.DisableKeyword ("TOP_PROJECTION"); else Shader.EnableKeyword ("TOP_PROJECTION");
		if (testObjectParent != null) {
			testObjectParent.SetActive (planeShown);
			testObjectCube.SetActive (cubeShown);
			testObjectCylinder.SetActive (cylinderShown);
			testObjectSphere.SetActive (sphereShown);
		}
	}

	public void NextShape () { SetShape (shape + 1); }

	/// <summary>Displacement off and back on (the previous depth is kept).</summary>
	public bool DisplacementOn { get { InitializeSettings (); return MatS.Parallax > 0.0001f; } }

	public void ToggleDisplacement () {
		InitializeSettings ();
		if (MatS.Parallax > 0.0001f) { parallaxBeforeOff = MatS.Parallax; MatS.Parallax = 0; }
		else MatS.Parallax = parallaxBeforeOff > 0 ? parallaxBeforeOff : 0.5f;
		MatS.ParallaxText = MatS.Parallax.ToString ();
		Notifications.Info (L.T (MatS.Parallax > 0 ? "Displacement on" : "Displacement off"));
	}

	/// <summary>Tiling preview: 3 × 3 to check the seams, and back.</summary>
	public void ToggleTiling () {
		InitializeSettings ();
		if (MatS.TexTilingX == 1 && MatS.TexTilingY == 1) {
			tilingBeforeX = tilingBeforeY = 1;
			MatS.TexTilingX = MatS.TexTilingY = 3;
		} else {
			MatS.TexTilingX = tilingBeforeX > 0 ? tilingBeforeX : 1;
			MatS.TexTilingY = tilingBeforeY > 0 ? tilingBeforeY : 1;
			if (MatS.TexTilingX != 1 || MatS.TexTilingY != 1) { MatS.TexTilingX = MatS.TexTilingY = 1; }
		}
		MatS.TexTilingXText = MatS.TexTilingX.ToString ();
		MatS.TexTilingYText = MatS.TexTilingY.ToString ();
	}

	Shader enhancedShader;
	int shadowsFor = -1;   // the shadows and reflections set for: 1 modern, 0 original
	GUIStyle qualityHead;

	GUIStyle qualityLabel, qualityValue;

	/// <summary>A title line, then the slider and its value on the next one (no overlap with any font).</summary>
	void QualitySlider (ref int y, int x, string title, string tip, ref float value) {
		if (qualityLabel == null) {
			qualityLabel = new GUIStyle (GUI.skin.label) { padding = new RectOffset (0, 0, 0, 0), alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow };
			qualityValue = new GUIStyle (qualityLabel) { alignment = TextAnchor.MiddleRight };
		}
		GUI.Label (new Rect (x, y, 270, 20), L.G (title, tip), qualityLabel);
		value = GUI.HorizontalSlider (new Rect (x, y + 25, 228, 12), value, 0f, 1f);
		GUI.Label (new Rect (x + 236, y + 21, 44, 20), value.ToString ("0.00"), qualityValue);
		y += 46;
	}

	void OnGUI () {
		Theme.Apply ();
		// In the new interface these settings live in the left column.
		if (UiShell.Active) return;

		windowRect.width = 300;
		windowRect.height = 590 + extraHeight;

		windowRect = UiShell.Dock (windowRect);

		windowRect = GUI.Window(14, windowRect, DoMyWindow, L.T("Full Material"));

		Tips.Block (windowRect);

	}

	public void Initialize() {

		InitializeSettings ();

		MainGuiScript = MainGui.instance;
		thisMaterial = MainGuiScript.FullMaterial;

		thisMaterial.SetTexture ("_DisplacementMap", MainGuiScript._TextureGrey);
		thisMaterial.SetTexture ("_DiffuseMap", MainGuiScript._TextureGrey);
		thisMaterial.SetTexture ("_NormalMap", MainGuiScript._TextureNormal);
		thisMaterial.SetTexture ("_MetallicMap", MainGuiScript._TextureBlack);
		thisMaterial.SetTexture ("_SmoothnessMap", MainGuiScript._TextureGrey);
		thisMaterial.SetTexture ("_AOMap", MainGuiScript._TextureWhite);
		thisMaterial.SetTexture ("_EdgeMap", MainGuiScript._TextureGrey);
		thisMaterial.SetFloat ("_DispOffset", 1.0f );

		_HeightMap = MainGuiScript._HeightMap;

		if (MainGuiScript._DiffuseMap != null) {
			_DiffuseMap = MainGuiScript._DiffuseMap;
		} else {
			_DiffuseMap = MainGuiScript._DiffuseMapOriginal;
		}
		_NormalMap = MainGuiScript._NormalMap;
		_EdgeMap = MainGuiScript._EdgeMap;
		_MetallicMap = MainGuiScript._MetallicMap;
		_SmoothnessMap = MainGuiScript._SmoothnessMap;
		_AOMap = MainGuiScript._AOMap;

		if (_HeightMap != null) { thisMaterial.SetTexture ("_DisplacementMap", _HeightMap); }
		if (_DiffuseMap != null) { thisMaterial.SetTexture ("_DiffuseMap", _DiffuseMap); }
		if (_NormalMap != null) { thisMaterial.SetTexture ("_NormalMap", _NormalMap); }
		if (_MetallicMap != null) { thisMaterial.SetTexture ("_MetallicMap", _MetallicMap); }
		if (_SmoothnessMap != null) { thisMaterial.SetTexture ("_SmoothnessMap", _SmoothnessMap); }
		if (_AOMap != null) { thisMaterial.SetTexture ("_AOMap", _AOMap); }
		if (_EdgeMap != null) { thisMaterial.SetTexture ("_EdgeMap", _EdgeMap); }

		testObject.GetComponent<Renderer>().material = thisMaterial;
		testObjectCube.GetComponent<Renderer>().material = thisMaterial;
		testObjectCylinder.GetComponent<Renderer>().material = thisMaterial;
		testObjectSphere.GetComponent<Renderer>().material = thisMaterial;

	}

}

