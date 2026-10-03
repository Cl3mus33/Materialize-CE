
using System;
using System.Net;
using System.IO;
using System.Text;
using FreeImageAPI;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

public enum Textures {
	height,
	diffuse,
	diffuseOriginal,
	specular,
	roughness,
	normal,
	msao
}

public enum PropChannelMap {
	None,
	Height,
	Metallic,
	Smoothness,
	Edge,
	Ao,
	AoEdge
}


public partial class MainGui : MonoBehaviour {

	public static MainGui instance;

	string message = "";
	float alpha = 1.0f;
	char pathChar = '/';

	//Rect toolsWindowRect = new Rect( 0, 0, 500, 500 );
	//int toolsWindowID = 50;
	string toolsWindowTitle = "Texture Tools";

	MapType mapTypeToLoad;
	Texture2D textureToLoad;
	Texture2D textureToSave;
	string mapType = "";

	public GameObject HeightFromDiffuseGuiObject;
	HeightFromDiffuseGui HeightFromDiffuseGuiScript;

	public GameObject NormalFromHeightGuiObject;
	NormalFromHeightGui NormalFromHeightGuiScript;

	public GameObject EdgeFromNormalGuiObject;
	EdgeFromNormalGui EdgeFromNormalGuiScript;

	public GameObject AOFromNormalGuiObject;
	AOFromNormalGui AOFromNormalGuiScript;

	public GameObject EditDiffuseGuiObject;
	EditDiffuseGui EditDiffuseGuiScript;

	public GameObject MetallicGuiObject;
	MetallicGui MetallicGuiScript;

	public GameObject SmoothnessGuiObject;
	SmoothnessGui SmoothnessGuiScript;

	public GameObject MaterialGuiObject;
	MaterialGui MaterialGuiScript;

	public GameObject PostProcessGuiObject;
	PostProcessGui PostProcessGuiScript;

	public GameObject TilingTextureMakerGuiObject;
	TilingTextureMakerGui TilingTextureMakerGuiScript;

	public AlignmentGui AlignmentGuiScript;

	public GameObject SuggestionGuiObject;
	SuggestionGui SuggestionGuiScript;

	public GameObject SaveLoadProjectObject;
	SaveLoadProject SaveLoadProjectScript;

	public GameObject CommandListExecutorObject;
	CommandListExecutor CommandListExecutorScript;

	public GameObject SettingsGuiObject;
	SettingsGui SettingsGuiScript;

	public Texture2D _TextureBlack;
	public Texture2D _TextureWhite;
	public Texture2D _TextureGrey;
	public Texture2D _TextureNormal;
	
	public RenderTexture _HDHeightMap;
	public Texture2D _HeightMap;
	public Texture2D _DiffuseMap;
	public Texture2D _DiffuseMapOriginal;
	public Texture2D _NormalMap;
	public Texture2D _MetallicMap;
	public Texture2D _SmoothnessMap;
	public Texture2D _EdgeMap;
	public Texture2D _AOMap;

	public Texture2D _PropertyMap;

	public Material FullMaterialRef;
	public Material FullMaterial;
	public Material SampleMaterialRef;
	public Material SampleMaterial;

	public GameObject testObject;
	public GameObject testObjectCube;
	public GameObject testObjectCylinder;
	public GameObject testObjectSphere;

    //public Material skyboxMaterial;
	public ReflectionProbe reflectionProbe;
	public Cubemap[] CubeMaps;
	int selectedCubemap = 0;

	Material thisMaterial;

	float _Falloff = 0.1f;
	float _OverlapX = 0.2f;
	float _OverlapY = 0.2f;
	bool _SmoothBlend = false;
	float _GamaCorrection = 2.2f;

	bool busySaving = false;

	public FileFormat selectedFormat = FileFormat.tga;
	bool bmpSelected = true;
	bool jpgSelected = false;
	bool pngSelected = true;
	bool tgaSelected = false;
	bool tiffSelected = false;
	bool ddsSelected = false;

	public bool hideGui = false;
	Camera thisCamera;
	Vector3 CameraTargetPos = Vector3.zero;
	Vector3 CameraOffsetPos = Vector3.zero;

	bool clearTextures = false;

	List<GameObject> objectsToUnhide;

	public FileBrowser fileBrowser;

	Shader PropertyCompShader;
	Material PropertyCompMaterial;

	public string QuicksavePath = "";
	public string QuicksavePathHeight = "";
	public string QuicksavePathDiffuse = "";
	public string QuicksavePathNormal = "";
	public string QuicksavePathMetallic = "";
	public string QuicksavePathSmoothness = "";
	public string QuicksavePathEdge = "";
	public string QuicksavePathAO = "";
	public string QuicksavePathProperty = "";

	public PropChannelMap propRed = PropChannelMap.None;
	public PropChannelMap propGreen = PropChannelMap.None;
	public PropChannelMap propBlue = PropChannelMap.None;
	public PropChannelMap propAlpha = PropChannelMap.None;
	bool propRedChoose = false;
	bool propGreenChoose = false;
	bool propBlueChoose = false;
	bool propAlphaChoose = false;

	private ClipboardImageHelper.ClipboardImage CIH;

	[DllImport ("FreeImage")]
	private static extern FIBITMAP FreeImage_Load( FREE_IMAGE_FORMAT fif, string filename, int flags );

	[DllImport ("FreeImage")]
	private static extern void FreeImage_Unload( FIBITMAP dib );

	[DllImport ("FreeImage")]
	private static extern bool FreeImage_Save( FREE_IMAGE_FORMAT fif, FIBITMAP dib, string filename, FREE_IMAGE_SAVE_FLAGS flags );

	[DllImport ("FreeImage")]
	private static extern int FreeImage_GetHeight( FIBITMAP dib );

	[DllImport ("FreeImage")]
	private static extern int FreeImage_GetWidth( FIBITMAP dib );

	[DllImport ("FreeImage")]
	private static extern bool FreeImage_GetPixelColor( FIBITMAP dib, int x, int y, RGBQUAD value );

	[DllImport ("FreeImage")]
	private static extern FIBITMAP FreeImage_MakeThumbnail( FIBITMAP dib, int max_pixel_size, bool convert );

	void Start () {

		MainGui.instance = this;

		_HeightMap = null;
		_HDHeightMap = null;
		_DiffuseMap = null;
		_DiffuseMapOriginal = null;
		_NormalMap = null;
		_MetallicMap = null;
		_SmoothnessMap = null;
		_EdgeMap = null;
		_AOMap = null;

		//fileBrowser = this.GetComponent<FileBrowser> ();

        PropertyCompShader = Shader.Find ("Hidden/Blit_Property_Comp");
		PropertyCompMaterial = new Material (PropertyCompShader);

		thisCamera = Camera.main;
		CameraTargetPos = thisCamera.transform.position;
		CameraOffsetPos = CameraTargetPos;

		Shader.SetGlobalFloat ("_GamaCorrection", _GamaCorrection);

		FullMaterial = new Material ( FullMaterialRef.shader );
		FullMaterial.CopyPropertiesFromMaterial (FullMaterialRef);
		//FullMaterial = null;
		//FullMaterial = tempFullMaterial;

		SampleMaterial = new Material ( SampleMaterialRef.shader );
		SampleMaterial.CopyPropertiesFromMaterial (SampleMaterialRef);
		//SampleMaterial = null;
		//SampleMaterial = tempSampleMaterial;

		HeightFromDiffuseGuiScript = HeightFromDiffuseGuiObject.GetComponent<HeightFromDiffuseGui>();
		NormalFromHeightGuiScript = NormalFromHeightGuiObject.GetComponent<NormalFromHeightGui>();
		EdgeFromNormalGuiScript = EdgeFromNormalGuiObject.GetComponent<EdgeFromNormalGui>();
		AOFromNormalGuiScript = AOFromNormalGuiObject.GetComponent<AOFromNormalGui>();
		EditDiffuseGuiScript = EditDiffuseGuiObject.GetComponent<EditDiffuseGui>();
		MetallicGuiScript = MetallicGuiObject.GetComponent<MetallicGui> ();
		SmoothnessGuiScript = SmoothnessGuiObject.GetComponent<SmoothnessGui>();
		MaterialGuiScript = MaterialGuiObject.GetComponent<MaterialGui>();
		PostProcessGuiScript = PostProcessGuiObject.GetComponent<PostProcessGui> ();
		TilingTextureMakerGuiScript = TilingTextureMakerGuiObject.GetComponent<TilingTextureMakerGui>();
		SuggestionGuiScript = SuggestionGuiObject.GetComponent<SuggestionGui>();
		SaveLoadProjectScript = SaveLoadProjectObject.GetComponent<SaveLoadProject>();
		CommandListExecutorScript = CommandListExecutorObject.GetComponent<CommandListExecutor> ();
		SettingsGuiScript = SettingsGuiObject.GetComponent<SettingsGui> ();

		SettingsGuiScript.LoadSettings();

		//HeightFromNormalGuiScript = HeightFromNormalGuiObject.GetComponent<HeightFromNormalGui>();

		if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) {
			pathChar = '\\';
		}

		CIH = new ClipboardImageHelper.ClipboardImage ();

		testObject.GetComponent<Renderer>().material = FullMaterial;
		SetMaterialValues();

		reflectionProbe.RenderProbe();

	}

	public void SetPreviewMaterial( Texture2D textureToPreview ) {
		CloseWindows();
		if (textureToPreview != null) {
			FixSizeMap (textureToPreview);
			SampleMaterial.SetTexture("_MainTex", textureToPreview );
			testObject.GetComponent<Renderer>().material = SampleMaterial;
		}
	}

	public void SetPreviewMaterial( RenderTexture textureToPreview ) {
		CloseWindows();
		if (textureToPreview != null) {
			FixSizeMap (textureToPreview);
			SampleMaterial.SetTexture("_MainTex", textureToPreview );
			testObject.GetComponent<Renderer>().material = SampleMaterial;
		}
	}

	public void SetMaterialValues() {
		
		Shader.SetGlobalTexture ("_GlobalCubemap", CubeMaps[selectedCubemap] );

		if (_HeightMap != null) { 
			FullMaterial.SetTexture ("_DisplacementMap", _HeightMap); 
		} else {
			FullMaterial.SetTexture ("_DisplacementMap", _TextureGrey); 
		}

		if (_DiffuseMap != null) { 
			FullMaterial.SetTexture ("_DiffuseMap", _DiffuseMap); 
		} else if (_DiffuseMapOriginal != null) { 
			FullMaterial.SetTexture ("_DiffuseMap", _DiffuseMapOriginal); 
		} else {
			FullMaterial.SetTexture ("_DiffuseMap", _TextureGrey); 
		}

		if (_NormalMap != null) {
			FullMaterial.SetTexture ("_NormalMap", _NormalMap);
		} else {
			FullMaterial.SetTexture ("_NormalMap", _TextureNormal);
		}

		if (_MetallicMap != null) { 
			FullMaterial.SetTexture ("_MetallicMap", _MetallicMap); 
		} else {
			FullMaterial.SetTexture ("_MetallicMap", _TextureBlack); 
		}

		if (_SmoothnessMap != null) { 
			FullMaterial.SetTexture ("_SmoothnessMap", _SmoothnessMap); 
		} else {
			FullMaterial.SetTexture ("_SmoothnessMap", _TextureBlack); 
		}

		if (_AOMap != null) { 
			FullMaterial.SetTexture ("_AOMap", _AOMap); 
		} else {
			FullMaterial.SetTexture ("_AOMap", _TextureWhite); 
		}

		if (_EdgeMap != null) { 
			FullMaterial.SetTexture ("_EdgeMap", _EdgeMap); 
		} else {
			FullMaterial.SetTexture ("_EdgeMap", _TextureGrey);
		}

		testObject.GetComponent<Renderer>().material = FullMaterial;

		FullMaterial.SetVector ("_Tiling", new Vector4 (1, 1, 0, 0));

	}

	public void CloseWindows() {
		HeightFromDiffuseGuiScript.Close ();
		NormalFromHeightGuiScript.Close ();
		EdgeFromNormalGuiScript.Close ();
		AOFromNormalGuiScript.Close ();
		EditDiffuseGuiScript.Close ();
		MetallicGuiScript.Close ();
		SmoothnessGuiScript.Close ();
		TilingTextureMakerGuiScript.Close ();
		AlignmentGuiScript.Close ();
		MaterialGuiObject.SetActive (false);
		PostProcessGuiObject.SetActive (false);
		//SettingsGuiObject.SetActive (false);
		//SuggestionGuiObject.SetActive (false);
	}

	void HideWindows() {

		objectsToUnhide = new List<GameObject> ();

		if (HeightFromDiffuseGuiObject.activeSelf) {
			objectsToUnhide.Add( HeightFromDiffuseGuiObject );
		}

		if (NormalFromHeightGuiObject.activeSelf) {
			objectsToUnhide.Add( NormalFromHeightGuiObject );
		}

		if (EdgeFromNormalGuiObject.activeSelf) {
			objectsToUnhide.Add( EdgeFromNormalGuiObject );
		}

		if (AOFromNormalGuiObject.activeSelf) {
			objectsToUnhide.Add( AOFromNormalGuiObject );
		}

		if (EditDiffuseGuiObject.activeSelf) {
			objectsToUnhide.Add( EditDiffuseGuiObject );
		}

		if (MetallicGuiObject.activeSelf) {
			objectsToUnhide.Add( MetallicGuiObject );
		}

		if (SmoothnessGuiObject.activeSelf) {
			objectsToUnhide.Add( SmoothnessGuiObject );
		}

		if (MaterialGuiObject.activeSelf) {
			objectsToUnhide.Add( MaterialGuiObject );
		}

		if( PostProcessGuiObject.activeSelf){
			objectsToUnhide.Add( PostProcessGuiObject );
		}

		if (TilingTextureMakerGuiObject.activeSelf) {
			objectsToUnhide.Add( TilingTextureMakerGuiObject );
		}

		//if (SettingsGuiObject.activeSelf) {
		//	objectsToUnhide.Add ( SettingsGuiObject );
		//}

		HeightFromDiffuseGuiObject.SetActive (false);
		NormalFromHeightGuiObject.SetActive (false);
		EdgeFromNormalGuiObject.SetActive (false);
		AOFromNormalGuiObject.SetActive (false);
		EditDiffuseGuiObject.SetActive (false);
		MetallicGuiObject.SetActive (false);
		SmoothnessGuiObject.SetActive (false);
		MaterialGuiObject.SetActive (false);
		PostProcessGuiObject.SetActive (false);
		TilingTextureMakerGuiObject.SetActive (false);
		//SettingsGuiObject.SetActive (false);

	}

	bool environmentRestored;

	void Update() {
		if (!environmentRestored) { environmentRestored = true; RestoreEnvironment (); }
		MapAdjust.Tick (this);
		if (UiShell.Active && MaterialGuiScript != null && !MaterialGuiObject.activeSelf) MaterialGuiScript.ApplySettings ();
		Workflow.Tick (this);
		SendViewGlobals ();
		FollowSun ();
	}

	/// <summary>Camera position and pixel size, and the preview shape's texture density: the displacement's level of detail.</summary>
	void SendViewGlobals() {
		var cam = Camera.main;
		if (cam == null || testObject == null) return;
		float pixel = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
		Shader.SetGlobalVector ("_MceCamPos", new Vector4 (cam.transform.position.x, cam.transform.position.y, cam.transform.position.z, pixel));
		var size = testObject.GetComponent<Renderer> ().bounds.size;
		Shader.SetGlobalFloat ("_MceUvPerWorld", 1f / Mathf.Max (0.001f, Mathf.Max (size.x, Mathf.Max (size.y, size.z))));
	}

	void ShowFullMaterial() {
		CloseWindows();
		FixSize();
		MaterialGuiObject.SetActive(true);
		MaterialGuiScript.Initialize();
	}

	void Fullscreen() {
		WindowMode.Toggle ();
	}

	void SetFileMaskImage() {
		fileBrowser.fileMasks = FastImageLoader.ImageMasks;
	}
	void SetFileMaskProject() {
		fileBrowser.fileMasks = "*.mtz";
	}
	
	void OnGUI () {
		Theme.Apply ();
		if (UiShell.Active) {
			DrawShell ();
		} else {
			DrawMainGui ();
		}
		SupportLinks.Draw ();
		ChannelPacker.DrawWindow (this);
		ExportWindow.Draw (this);
		Tips.Capture ();
	}

	void DrawMainGui () {

		//==================================================//
		// 					Unhidable Buttons				//
		//==================================================//

		if (GUI.Button (new Rect(Screen.width - 80, Screen.height - 40, 70, 30), L.G("Quit", "Closes Materialize."))) {
			Application.Quit();
		}

		// Was disabled in the original, which left no way out of full screen. F11 toggles too.
		bool f11 = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F11;
		if (GUI.Button (new Rect (Screen.width - 190, Screen.height - 40, 100, 30), WindowMode.IsFullScreen ? "Windowed" : "Full Screen") || f11) {
			Fullscreen();
			if (f11) Event.current.Use ();
		}

		// Suggestions used to be posted over plain HTTP to the original author's server, which is gone.
		if (false && GUI.Button (new Rect(Screen.width - 260, 10, 140, 30), L.T("Make Suggestion"))) {
			SuggestionGuiObject.SetActive(true);
		}

		if( hideGui == false ){
			if (GUI.Button (new Rect(Screen.width - 110, 10, 100, 30), L.G("Hide Gui", "Hides the panels to look at the preview."))) {
				hideGui = true;
				HideWindows();
				//CameraTargetPos = new Vector3(0,0,-10);
			}
		} else {
			if (GUI.Button (new Rect(Screen.width - 110, 10, 100, 30), L.G("Show Gui", "Shows the panels again."))) {
				hideGui = false;
				for( int i = 0; i< objectsToUnhide.Count; i++ ){
					objectsToUnhide[i].SetActive(true);
				}
				//CameraTargetPos = CameraOffsetPos;
			}
			return;
		}

		//==================================================//
		// 						Main Gui					//
		//==================================================//


		int spacingX = 130;
		int spacingY = 150;

		int offsetX = 20;
		int offsetY = 20;


		//==============================//
		// 			Height Map			//
		//==============================//

		GUI.Box( new Rect (offsetX, offsetY, 110, 250), L.T("Height Map") );
		
		if ( _HeightMap != null ) {
			GUI.DrawTexture (new Rect(offsetX + 5, offsetY + 25, 100, 100), _HeightMap );
		}

		// Paste 
		if (GUI.Button (new Rect(offsetX + 5, offsetY + 130, 20, 20), L.G("P", "Paste: puts the image from the clipboard into this map."))) {
			mapTypeToLoad = MapType.height;
			PasteFile ();
		}

		if (_HeightMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Copy
		if (GUI.Button (new Rect(offsetX + 30, offsetY + 130, 20, 20), L.G("C", "Copy: puts this map on the clipboard, to paste it in another program."))) {
			textureToSave = _HeightMap;
			CopyFile ();
		}

		GUI.enabled = true;

		// Open
		if (GUI.Button (new Rect(offsetX + 60, offsetY + 130, 20, 20), L.G("O", "Open: loads an image file into this map (PNG, JPG, TGA, TIFF, EXR, DDS...)."))) {
			mapTypeToLoad = MapType.height;
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Open Height Map", this.OpenFile);
		}

		if (_HeightMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Save
		if (GUI.Button (new Rect(offsetX + 85, offsetY + 130, 20, 20), L.G("S", "Save: saves this map to a file, in the format chosen in Saving Options."))) {
			textureToSave = _HeightMap;
			mapType = "_height";
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Save Height Map", this.SaveFile);
		}


		if (_HeightMap == null || QuicksavePathHeight == "") { GUI.enabled = false; } else { GUI.enabled = true; }

		// Quick Save
		if (GUI.Button (new Rect(offsetX + 15, offsetY + 160, 80, 20), L.G("Quick Save", "Saves this map again to the last file you saved it to, without asking."))) {
			textureToSave = _HeightMap;
			mapType = "_height";
			SaveFile(QuicksavePathHeight);
		}

		if (_HeightMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + 15, offsetY + 190, 80, 20), L.G("Preview", "Shows this map alone on the preview plane."))) {
			SetPreviewMaterial( _HeightMap );
			//SetPreviewMaterial( _HDHeightMap );
		}
		GUI.enabled = true;

		if (_DiffuseMapOriginal == null && _DiffuseMap == null && _NormalMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + 5, offsetY + 220, 50, 20), L.G("Create", "Create the height map from the albedo: bright = high. Adjust the frequencies to keep the right details."))) {
			CloseWindows();
			FixSize();
			HeightFromDiffuseGuiObject.SetActive(true);
			HeightFromDiffuseGuiScript.NewTexture();
			HeightFromDiffuseGuiScript.DoStuff();
		}
		GUI.enabled = true;

		if (_HeightMap == null ){ GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + 60, offsetY + 220, 45, 20), L.G("Clear", "Empties this map."))) {
			ClearTexture(MapType.height);
			CloseWindows ();
			SetMaterialValues ();
			FixSize ();
		}
		GUI.enabled = true;


		//==============================//
		// 			Diffuse Map			//
		//==============================//

		GUI.Box( new Rect (offsetX + spacingX, offsetY, 110, 250), L.T("Albedo Map") );

		if (_DiffuseMap != null) {
			GUI.DrawTexture (new Rect (offsetX + spacingX + 5, offsetY + 25, 100, 100), _DiffuseMap);
		} else if (_DiffuseMapOriginal != null) {
			GUI.DrawTexture (new Rect (offsetX + spacingX + 5, offsetY + 25, 100, 100), _DiffuseMapOriginal);
		}

		// Paste 
		if (GUI.Button (new Rect(offsetX + spacingX  + 5, offsetY + 130, 20, 20), L.G("P", "Paste: puts the image from the clipboard into this map."))) {
			mapTypeToLoad = MapType.diffuseOriginal;
			PasteFile ();
		}

		if ( _DiffuseMapOriginal == null && _DiffuseMap == null ) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Copy
		if (GUI.Button (new Rect(offsetX + spacingX  + 30, offsetY + 130, 20, 20), L.G("C", "Copy: puts this map on the clipboard, to paste it in another program."))) {
			if( _DiffuseMap != null ){
				textureToSave = _DiffuseMap;
			}else{
				textureToSave = _DiffuseMapOriginal;
			}
			CopyFile ();
		}

		GUI.enabled = true;

		// Open
		if (GUI.Button (new Rect(offsetX + spacingX + 60, offsetY + 130, 20, 20), L.G("O", "Open: loads an image file into this map (PNG, JPG, TGA, TIFF, EXR, DDS...)."))) {
			mapTypeToLoad = MapType.diffuseOriginal;
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Open Albedo Map", this.OpenFile );
		}
		
		if ( _DiffuseMapOriginal == null && _DiffuseMap == null ) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Save
		if (GUI.Button (new Rect(offsetX + spacingX + 85, offsetY + 130, 20, 20), L.G("S", "Save: saves this map to a file, in the format chosen in Saving Options."))) {
			if( _DiffuseMap != null ){
				textureToSave = _DiffuseMap;
			}else{
				textureToSave = _DiffuseMapOriginal;
			}
			mapType = "_diffuse";
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Save Albedo Map", this.SaveFile );
		}

		if ( ( _DiffuseMapOriginal == null && _DiffuseMap == null ) || QuicksavePathDiffuse == "") { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Quick Save
		if (GUI.Button (new Rect(offsetX + spacingX + 15, offsetY + 160, 80, 20), L.G("Quick Save", "Saves this map again to the last file you saved it to, without asking."))) {
			if( _DiffuseMap != null ){
				textureToSave = _DiffuseMap;
			}else{
				textureToSave = _DiffuseMapOriginal;
			}
			mapType = "_diffuse";
			SaveFile(QuicksavePathDiffuse);
		}

		if ( _DiffuseMapOriginal == null && _DiffuseMap == null ) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX + 15, offsetY + 190, 80, 20), L.G("Preview", "Shows this map alone on the preview plane."))) {
			if( _DiffuseMap != null ){
				SetPreviewMaterial( _DiffuseMap );
			}else{
				SetPreviewMaterial( _DiffuseMapOriginal );
			}
		}

		if ( _DiffuseMapOriginal == null ) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX + 5, offsetY + 220, 50, 20), L.G("Edit", "Edit the albedo: remove baked lighting and shadows, adjust colours and contrast."))) {
			CloseWindows();
			FixSize();
			EditDiffuseGuiObject.SetActive(true);
			EditDiffuseGuiScript.NewTexture();
			EditDiffuseGuiScript.DoStuff();
		}

		if ( _DiffuseMapOriginal == null && _DiffuseMap == null ) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX + 60, offsetY + 220, 45, 20), L.G("Clear", "Empties this map."))) {
			ClearTexture(MapType.diffuse);
			CloseWindows ();
			SetMaterialValues ();
			FixSize ();
		}
		GUI.enabled = true;


		//==============================//
		// 			Normal Map			//
		//==============================//
		
		GUI.Box( new Rect (offsetX + spacingX * 2, offsetY, 110, 250), L.T("Normal Map") );
		
		if ( _NormalMap != null ) {
			GUI.DrawTexture (new Rect(offsetX + spacingX * 2 + 5, offsetY + 25, 100, 100), _NormalMap );
		}

		// Paste 
		if (GUI.Button (new Rect(offsetX + spacingX * 2  + 5, offsetY + 130, 20, 20), L.G("P", "Paste: puts the image from the clipboard into this map."))) {
			mapTypeToLoad = MapType.normal;
			PasteFile ();
		}

		if (_NormalMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Copy
		if (GUI.Button (new Rect(offsetX + spacingX * 2  + 30, offsetY + 130, 20, 20), L.G("C", "Copy: puts this map on the clipboard, to paste it in another program."))) {
			textureToSave = _NormalMap;
			CopyFile ();
		}

		GUI.enabled = true;

		//Open
		if (GUI.Button (new Rect(offsetX + spacingX * 2 + 60, offsetY + 130, 20, 20), L.G("O", "Open: loads an image file into this map (PNG, JPG, TGA, TIFF, EXR, DDS...)."))) {
			mapTypeToLoad = MapType.normal;
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Open Normal Map", this.OpenFile );
		}

		if (_NormalMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Save
		if (GUI.Button (new Rect(offsetX + spacingX * 2 + 85, offsetY + 130, 20, 20), L.G("S", "Save: saves this map to a file, in the format chosen in Saving Options."))) {
			textureToSave = _NormalMap;
			mapType = "_normal";
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Save Normal Map", this.SaveFile );
		}

		if (_NormalMap == null || QuicksavePathNormal == "") { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Quick Save
		if (GUI.Button (new Rect(offsetX + spacingX * 2 + 15, offsetY + 160, 80, 20), L.G("Quick Save", "Saves this map again to the last file you saved it to, without asking."))) {
			textureToSave = _NormalMap;
			mapType = "_normal";
			SaveFile(QuicksavePathNormal);
		}
		
		if (_NormalMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 2 + 15, offsetY + 190, 80, 20), L.G("Preview", "Shows this map alone on the preview plane."))) {
			SetPreviewMaterial( _NormalMap );
		}

		if (_HeightMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect (offsetX + spacingX * 2 + 5, offsetY + 220, 50, 20), L.G("Create", "Create the normal map from the height map."))) {
			CloseWindows ();
			FixSize ();
			NormalFromHeightGuiObject.SetActive (true);
			NormalFromHeightGuiScript.NewTexture ();
			NormalFromHeightGuiScript.DoStuff ();
		}

		if (_NormalMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 2 + 60, offsetY + 220, 45, 20), L.G("Clear", "Empties this map."))) {
			ClearTexture(MapType.normal);
			CloseWindows ();
			SetMaterialValues ();
			FixSize ();
		}
		GUI.enabled = true;


		//==============================//
		// 			Metallic Map		//
		//==============================//
		
		GUI.Box( new Rect (offsetX + spacingX * 3, offsetY, 110, 250), L.T("Metallic Map") );
		
		if ( _MetallicMap != null ) {
			GUI.DrawTexture (new Rect(offsetX + spacingX * 3 + 5, offsetY + 25, 100, 100), _MetallicMap );
		}

		// Paste 
		if (GUI.Button (new Rect(offsetX + spacingX * 3  + 5, offsetY + 130, 20, 20), L.G("P", "Paste: puts the image from the clipboard into this map."))) {
			mapTypeToLoad = MapType.metallic;
			PasteFile ();
		}

		if (_MetallicMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Copy
		if (GUI.Button (new Rect(offsetX + spacingX * 3  + 30, offsetY + 130, 20, 20), L.G("C", "Copy: puts this map on the clipboard, to paste it in another program."))) {
			textureToSave = _MetallicMap;
			CopyFile ();
		}

		GUI.enabled = true;

		//Open
		if (GUI.Button (new Rect(offsetX + spacingX * 3 + 60, offsetY + 130, 20, 20), L.G("O", "Open: loads an image file into this map (PNG, JPG, TGA, TIFF, EXR, DDS...)."))) {
			mapTypeToLoad = MapType.metallic;
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Open Metallic Map", this.OpenFile );
			//UniFileBrowser.use.OpenFileWindow (OpenFile);
		}

		if (_MetallicMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Save
		if (GUI.Button (new Rect(offsetX + spacingX * 3 + 85, offsetY + 130, 20, 20), L.G("S", "Save: saves this map to a file, in the format chosen in Saving Options."))) {
			textureToSave = _MetallicMap;
			mapType = "_metallic";
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Save Metallic Map", this.SaveFile );
			//UniFileBrowser.use.SaveFileWindow (SaveFile);
		}

		if (_MetallicMap == null || QuicksavePathMetallic == "") { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Quick Save
		if (GUI.Button (new Rect(offsetX + spacingX * 3 + 15, offsetY + 160, 80, 20), L.G("Quick Save", "Saves this map again to the last file you saved it to, without asking."))) {
			textureToSave = _MetallicMap;
			mapType = "_metallic";
			SaveFile(QuicksavePathMetallic);
		}
		
		if (_MetallicMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 3 + 15, offsetY + 190, 80, 20), L.G("Preview", "Shows this map alone on the preview plane."))) {
			SetPreviewMaterial( _MetallicMap );
		}

		if ( _DiffuseMapOriginal == null && _DiffuseMap == null ){ GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect (offsetX + spacingX * 3 + 5, offsetY + 220, 50, 20), L.G("Create", "Create the metallic map by picking the colour of the metal in the albedo."))) {
			CloseWindows();
			FixSize();

			MetallicGuiObject.SetActive(true);
			MetallicGuiScript.NewTexture();
			MetallicGuiScript.DoStuff();
		}

		if (_MetallicMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 3 + 60, offsetY + 220, 45, 20), L.G("Clear", "Empties this map."))) {
			ClearTexture(MapType.metallic);
			CloseWindows ();
			SetMaterialValues ();
			FixSize ();
		}
		GUI.enabled = true;


		//==============================//
		// 		Smoothness Map			//
		//==============================//
		
		GUI.Box( new Rect (offsetX + spacingX * 4, offsetY, 110, 250), L.T("Smoothness Map") );
		
		if ( _SmoothnessMap != null ) {
			GUI.DrawTexture (new Rect(offsetX + spacingX * 4 + 5, offsetY + 25, 100, 100), _SmoothnessMap );
		}

		// Paste 
		if (GUI.Button (new Rect(offsetX + spacingX * 4  + 5, offsetY + 130, 20, 20), L.G("P", "Paste: puts the image from the clipboard into this map."))) {
			mapTypeToLoad = MapType.smoothness;
			PasteFile ();
		}

		if (_SmoothnessMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Copy
		if (GUI.Button (new Rect(offsetX + spacingX * 4  + 30, offsetY + 130, 20, 20), L.G("C", "Copy: puts this map on the clipboard, to paste it in another program."))) {
			textureToSave = _SmoothnessMap;
			CopyFile ();
		}

		GUI.enabled = true;

		//Open
		if (GUI.Button (new Rect(offsetX + spacingX * 4 + 60, offsetY + 130, 20, 20), L.G("O", "Open: loads an image file into this map (PNG, JPG, TGA, TIFF, EXR, DDS...)."))) {
			mapTypeToLoad = MapType.smoothness;
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Open Smoothness Map", this.OpenFile );
		}
		
		if (_SmoothnessMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Save
		if (GUI.Button (new Rect(offsetX + spacingX * 4 + 85, offsetY + 130, 20, 20), L.G("S", "Save: saves this map to a file, in the format chosen in Saving Options."))) {
			textureToSave = _SmoothnessMap;
			mapType = "_smoothness";
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Save Smoothness Map", this.SaveFile );
		}

		if (_SmoothnessMap == null || QuicksavePathSmoothness == "") { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Quick Save
		if (GUI.Button (new Rect(offsetX + spacingX * 4 + 15, offsetY + 160, 80, 20), L.G("Quick Save", "Saves this map again to the last file you saved it to, without asking."))) {
			textureToSave = _SmoothnessMap;
			mapType = "_smoothness";
			SaveFile(QuicksavePathSmoothness);
		}
		
		if (_SmoothnessMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 4 + 15, offsetY + 190, 80, 20), L.G("Preview", "Shows this map alone on the preview plane."))) {
			SetPreviewMaterial( _SmoothnessMap );
		}

		if ( _DiffuseMapOriginal == null && _DiffuseMap == null ){ GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect (offsetX + spacingX * 4 + 5, offsetY + 220, 50, 20), L.G("Create", "Create the smoothness (gloss) map from the albedo, the metallic and picked colours. Roughness is its inverse."))) {
			CloseWindows();
			FixSize();
			SmoothnessGuiObject.SetActive(true);
			SmoothnessGuiScript.NewTexture();
			SmoothnessGuiScript.DoStuff();
		}

		if (_SmoothnessMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 4 + 60, offsetY + 220, 45, 20), L.G("Clear", "Empties this map."))) {
			ClearTexture(MapType.smoothness);
			CloseWindows ();
			SetMaterialValues ();
			FixSize ();
		}
		GUI.enabled = true;


		//==============================//
		// 			Edge Map			//
		//==============================//
		
		GUI.Box( new Rect (offsetX + spacingX * 5, offsetY, 110, 250), L.T("Edge Map") );
		
		if ( _EdgeMap != null ) {
			GUI.DrawTexture (new Rect(offsetX + spacingX * 5 + 5, offsetY + 25, 100, 100), _EdgeMap );
		}

		// Paste 
		if (GUI.Button (new Rect(offsetX + spacingX * 5  + 5, offsetY + 130, 20, 20), L.G("P", "Paste: puts the image from the clipboard into this map."))) {
			mapTypeToLoad = MapType.edge;
			PasteFile ();
		}

		if (_EdgeMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Copy
		if (GUI.Button (new Rect(offsetX + spacingX * 5  + 30, offsetY + 130, 20, 20), L.G("C", "Copy: puts this map on the clipboard, to paste it in another program."))) {
			textureToSave = _EdgeMap;
			CopyFile ();
		}

		GUI.enabled = true;
		
		//Open
		if (GUI.Button (new Rect(offsetX + spacingX * 5 +60, offsetY + 130, 20, 20), L.G("O", "Open: loads an image file into this map (PNG, JPG, TGA, TIFF, EXR, DDS...)."))) {
			mapTypeToLoad = MapType.edge;
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Open Edge Map", this.OpenFile );
		}

		if (_EdgeMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Save
		if (GUI.Button (new Rect(offsetX + spacingX * 5 + 85, offsetY + 130, 20, 20), L.G("S", "Save: saves this map to a file, in the format chosen in Saving Options."))) {
			textureToSave = _EdgeMap;
			mapType = "_edge";
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Save Edge Map", this.SaveFile );
		}

		if (_EdgeMap == null || QuicksavePathEdge == "") { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Quick Save
		if (GUI.Button (new Rect(offsetX + spacingX * 5 + 15, offsetY + 160, 80, 20), L.G("Quick Save", "Saves this map again to the last file you saved it to, without asking."))) {
			textureToSave = _EdgeMap;
			mapType = "_edge";
			SaveFile(QuicksavePathEdge);
		}
		
		if (_EdgeMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 5 + 15, offsetY + 190, 80, 20), L.G("Preview", "Shows this map alone on the preview plane."))) {
			SetPreviewMaterial( _EdgeMap );
		}

		if ( _NormalMap == null ){ GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 5 + 5, offsetY + 220, 50, 20), L.G("Create", "Create the edge map from the normal map: edges and ridges bright, hollows dark."))) {
			CloseWindows();
			FixSize();
			EdgeFromNormalGuiObject.SetActive(true);
			EdgeFromNormalGuiScript.NewTexture();
			EdgeFromNormalGuiScript.DoStuff();
		}

		if (_EdgeMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 5 + 60, offsetY + 220, 45, 20), L.G("Clear", "Empties this map."))) {
			ClearTexture(MapType.edge);
			CloseWindows ();
			SetMaterialValues ();
			FixSize ();
		}
		GUI.enabled = true;

		//==============================//
		// 			AO Map				//
		//==============================//
		
		GUI.Box( new Rect (offsetX + spacingX * 6, offsetY, 110, 250), L.T("AO Map") );
		
		if ( _AOMap != null ) {
			GUI.DrawTexture (new Rect(offsetX + spacingX * 6 + 5, offsetY + 25, 100, 100), _AOMap );
		}


		// Paste 
		if (GUI.Button (new Rect(offsetX + spacingX * 6  + 5, offsetY + 130, 20, 20), L.G("P", "Paste: puts the image from the clipboard into this map."))) {
			mapTypeToLoad = MapType.ao;
			PasteFile ();
		}

		if (_AOMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Copy
		if (GUI.Button (new Rect(offsetX + spacingX * 6  + 30, offsetY + 130, 20, 20), L.G("C", "Copy: puts this map on the clipboard, to paste it in another program."))) {
			textureToSave = _AOMap;
			CopyFile ();
		}

		GUI.enabled = true;
		
		//Open
		if (GUI.Button (new Rect(offsetX + spacingX * 6 + 60, offsetY + 130, 20, 20), L.G("O", "Open: loads an image file into this map (PNG, JPG, TGA, TIFF, EXR, DDS...)."))) {
			mapTypeToLoad = MapType.ao;
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Open AO Map", this.OpenFile );
		}
		
		if (_AOMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }

		// Save
		if (GUI.Button (new Rect(offsetX + spacingX * 6 + 85, offsetY + 130, 20, 20), L.G("S", "Save: saves this map to a file, in the format chosen in Saving Options."))) {
			textureToSave = _AOMap;
			mapType = "_ao";
			SetFileMaskImage();
			fileBrowser.ShowBrowser( "Save AO Map", this.SaveFile );
		}

		if (_AOMap == null || QuicksavePathAO == "") { GUI.enabled = false; } else { GUI.enabled = true; }
		
		// Quick Save
		if (GUI.Button (new Rect(offsetX + spacingX * 6 + 15, offsetY + 160, 80, 20), L.G("Quick Save", "Saves this map again to the last file you saved it to, without asking."))) {
			textureToSave = _AOMap;
			mapType = "_ao";
			SaveFile(QuicksavePathAO);
		}
		
		if (_AOMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 6 + 15, offsetY + 190, 80, 20), L.G("Preview", "Shows this map alone on the preview plane."))) {
			SetPreviewMaterial( _AOMap );
		}

		if ( _NormalMap == null && _HeightMap == null ){ GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 6 + 5, offsetY + 220, 50, 20), L.G("Create", "Create the ambient occlusion from the normal and height maps: crevices darker."))) {
			CloseWindows();
			FixSize();
			AOFromNormalGuiObject.SetActive(true);
			AOFromNormalGuiScript.NewTexture();
			AOFromNormalGuiScript.DoStuff();
		}

		if (_AOMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + spacingX * 6 + 60, offsetY + 220, 45, 20), L.G("Clear", "Empties this map."))) {
			ClearTexture(MapType.ao);
			CloseWindows ();
			SetMaterialValues ();
			FixSize ();
		}
		GUI.enabled = true;


		//==============================//
		// 		Map Saving Options		//
		//==============================//

		offsetX = offsetX + spacingX * 7;

		// Materialize CE: one place for exporting, as in Quixel Mixer; the details live in the Export window.
		GUI.Box( new Rect (offsetX, offsetY, 230, 250), L.T("Export & Project") );

		var oldColor = GUI.backgroundColor;
		GUI.backgroundColor = new Color (0.55f, 0.8f, 1f);
		if (GUI.Button (new Rect (offsetX + 10, offsetY + 26, 210, 44), L.G("Export…", "Writes all the files for your engine in one go: Skyrim, Unreal, Unity, glTF… with your own saved profiles."))) {
			ExportWindow.Toggle ();
		}
		GUI.backgroundColor = oldColor;

		GUI.Label (new Rect (offsetX + 12, offsetY + 80, 120, 22), L.G("Single map format", "Format used by the S (save) button of each map."));
		if (GUI.Button (new Rect (offsetX + 140, offsetY + 80, 80, 22), L.G(selectedFormat.ToString ().ToUpperInvariant (), "Click to change: PNG, TGA, TIFF, JPG, BMP, DDS."))) {
			FileFormat[] order = { FileFormat.png, FileFormat.tga, FileFormat.tiff, FileFormat.jpg, FileFormat.bmp, FileFormat.dds };
			SetFormat (order[(System.Array.IndexOf (order, selectedFormat) + 1) % order.Length]);
		}

		if ( _NormalMap == null ){ GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX + 10, offsetY + 116, 210, 26), L.G("Flip Normal Y", "Flips the green channel of the normal map: switches between OpenGL (Maya style) and DirectX (Max style) normals."))) {
			FlipNormalMapY();
		}
		GUI.enabled = true;

		if (GUI.Button (new Rect(offsetX + 10, offsetY + 170, 102, 30), L.G("Save Project", "Saves every map and every setting as a Materialize project (.mtz) with its images."))) {
			SetFileMaskProject();
			fileBrowser.ShowBrowser( "Save Project", this.SaveProject );
		}
		if (GUI.Button (new Rect(offsetX + 118, offsetY + 170, 102, 30), L.G("Load Project", "Opens a Materialize project (.mtz)."))) {
			SetFileMaskProject();
			fileBrowser.ShowBrowser( "Load Project", this.LoadProject );
		}


		//==========================//
		// 		View Buttons		//
		//==========================//

		offsetX = 430;
		offsetY = 280;

		if (GUI.Button (new Rect(offsetX, offsetY, 100, 40), L.G("Post Process", "Bloom, vignette and other effects of the 3D preview."))) {
			if( PostProcessGuiObject.activeSelf == true ){
				PostProcessGuiObject.SetActive(false);
			}else{
				PostProcessGuiObject.SetActive(true);
			}
		}

		offsetX += 110;

		if (GUI.Button (new Rect(offsetX, offsetY, 80, 40), L.G("Show Full\r\nMaterial", "Shows every map together on the preview: the finished material."))) {
			CloseWindows();
			FixSize();
			MaterialGuiObject.SetActive(true);
			MaterialGuiScript.Initialize();
		}

		offsetX += 90;

		if (GUI.Button (new Rect(offsetX, offsetY, 80, 40), L.G("Next\r\nCube Map", "Changes the environment lighting the preview."))) {
			selectedCubemap += 1;
			if( selectedCubemap >= CubeMaps.Length ){
				selectedCubemap = 0;
			}

            //skyboxMaterial.SetTexture ("_Tex", CubeMaps[selectedCubemap] );
			Shader.SetGlobalTexture ("_GlobalCubemap", CubeMaps[selectedCubemap] );
			reflectionProbe.RenderProbe();
		}

		offsetX += 90;

		if (_HeightMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX, offsetY, 60, 40), L.G("Tile\r\nMaps", "Makes the maps tile without visible seams, and sets their repetition."))) {
			CloseWindows();
			FixSize();
			TilingTextureMakerGuiObject.SetActive(true);
			TilingTextureMakerGuiScript.Initialize();
		}
		GUI.enabled = true;

		offsetX += 70;

		if (_HeightMap == null && _DiffuseMapOriginal == null && _MetallicMap == null && _SmoothnessMap == null && _EdgeMap == null && _AOMap == null) { GUI.enabled = false; } else { GUI.enabled = true; }
		if (GUI.Button (new Rect(offsetX, offsetY, 90, 40), L.G("Adjust\r\nAlignment", "Straightens a photo taken at an angle, and corrects lens distortion."))) {
			CloseWindows();
			FixSize();
			//AlignmentGuiScript.gameObject.SetActive(true);
			AlignmentGuiScript.Initialize();
		}
		GUI.enabled = true;

		offsetX += 100;

		if (GUI.Button (new Rect(offsetX, offsetY, 120, 40), L.G("Clear All\r\nTexture Maps", "Empties every map to start again."))) {
			clearTextures = true;
		}

		if (clearTextures) {

			offsetY += 60;

			GUI.Box( new Rect (offsetX, offsetY, 120, 60), L.T("Are You Sure?") );

			if (GUI.Button (new Rect (offsetX + 10, offsetY + 30, 45, 20), L.T("Yes"))) {
				clearTextures = false;
				ClearAllTextures ();
				CloseWindows ();
				SetMaterialValues ();
				FixSizeSize( 1024.0f, 1024.0f );
			}

			if (GUI.Button (new Rect (offsetX + 65, offsetY + 30, 45, 20), L.T("No"))) {
				clearTextures = false;
			}
		}

		GUI.enabled = true;

	}

	GUIStyle smallLabel, smallToggle;

	void DrawDdsOptions ( int x, int y ) {
		if (smallLabel == null) smallLabel = new GUIStyle (GUI.skin.label) { fontSize = 10, wordWrap = true };
		if (smallToggle == null) smallToggle = new GUIStyle (GUI.skin.toggle) { fontSize = 10 };
		GUI.Box (new Rect (x, y, 160, 250), L.T("DDS Options"));
		string[] encoders = { "Microsoft texconv", "NVIDIA Texture Tools", "Custom tool" };
		for (int i = 0; i < encoders.Length; i++) {
			bool on = GUI.Toggle (new Rect (x + 8, y + 22 + i * 17, 150, 18), (int)DdsExport.Encoder == i, encoders[i], smallToggle);
			if (on && (int)DdsExport.Encoder != i) {
				DdsExport.Encoder = (DdsEncoder)i;
				DdsExport.ToolPath = "";
				SettingsGui.instance.SaveDdsSettings ();
			}
		}
		GUI.Label (new Rect (x + 8, y + 73, 150, 30), DdsExport.ToolStatus (), smallLabel);
		if (GUI.Button (new Rect (x + 8, y + 100, 144, 18), L.T("Pick the tool's .exe…"))) {
			StartCoroutine (PickDdsTool ());
		}
		GUI.Label (new Rect (x + 8, y + 120, 150, 18), L.T("Compression"));
		for (int i = 0; i < DdsExport.FormatLabels.Length; i++) {
			int col = i % 2, row = i / 2;
			bool on = GUI.Toggle (new Rect (x + 8 + col * 75, y + 138 + row * 17, 75, 18), (int)DdsExport.Format == i,
				L.G(DdsExport.FormatLabels[i], DdsExport.FormatHelp[i]), smallToggle);
			if (on && (int)DdsExport.Format != i) {
				DdsExport.Format = (DdsFormat)i;
				SettingsGui.instance.SaveDdsSettings ();
			}
		}
		bool high = GUI.Toggle (new Rect (x + 8, y + 192, 150, 18), DdsExport.HighQuality, L.T("Best quality (slower)"), smallToggle);
		if (high != DdsExport.HighQuality) {
			DdsExport.HighQuality = high;
			SettingsGui.instance.SaveDdsSettings ();
		}
		GUI.Label (new Rect (x + 8, y + 210, 150, 40), GUI.tooltip.Length > 0 ? GUI.tooltip : DdsExport.FormatHelp[(int)DdsExport.Format], smallLabel);
	}

	IEnumerator PickDdsTool () {
		yield return null;
		string path = null;
		try {
			path = NativeFileDialog.Show ("DDS tool (.exe)", "*.exe", false);
		} catch (System.Exception e) {
			Notifications.Error ("Could not open the file dialog: " + e.Message);
		}
		if (path != null) {
			DdsExport.ToolPath = path;
			SettingsGui.instance.SaveDdsSettings ();
		}
	}

	string PCM2String ( PropChannelMap pcm, string defaultName ){

		string returnString = defaultName;

		switch (pcm) {
		case PropChannelMap.Height:
			returnString = "Height";
			break;
		case PropChannelMap.Metallic:
			returnString = "Metallic";
			break;
		case PropChannelMap.Smoothness:
			returnString = "Smoothness";
			break;
		case PropChannelMap.Edge:
			returnString = "Edge";
			break;
		case PropChannelMap.Ao:
			returnString = "Ambient Occ";
			break;
		case PropChannelMap.AoEdge:
			returnString = "AO + Edge";
			break;
		}

		return returnString;

	}

	public void FlipNormalMapY(){

		if( _NormalMap != null ){
			Color pixelColor = Color.black;
			//UnityEngine.Debug.Log (_NormalMap.GetPixel (0, 0).b);
			for (int i = 0; i < _NormalMap.width; i ++) {
				for (int j = 0; j < _NormalMap.height; j ++) {
					pixelColor = _NormalMap.GetPixel (i, j);
					pixelColor.g = 1.0f - pixelColor.g;
					_NormalMap.SetPixel (i, j, pixelColor);
				}
			}
			_NormalMap.Apply ();
		}

		/*
		bool SPM = false;
		if (SampleMaterial.GetTexture ("_MainTex") == _NormalMap) {
			SPM = true;
		}

		Shader FlipNormalYShader = Shader.Find ("Hidden/Blit_FlipNormalY");
		Material FlipNormalYMaterial = new Material (FlipNormalYShader);

		RenderTexture _TempMap = RenderTexture.GetTemporary (_NormalMap.width, _NormalMap.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
		Graphics.Blit (_NormalMap, _TempMap, FlipNormalYMaterial, 0);
		RenderTexture.active = _TempMap;
		
		if (_NormalMap != null) {
			Destroy (_NormalMap);
			_NormalMap = null;
		}
		
		_NormalMap = new Texture2D (_TempMap.width, _TempMap.height);
		_NormalMap.ReadPixels (new Rect (0, 0, _TempMap.width, _TempMap.height), 0, 0);
		_NormalMap.Apply ();

		Destroy (FlipNormalYShader);
		Destroy (FlipNormalYMaterial);
		
		RenderTexture.ReleaseTemporary (_TempMap);
		_TempMap = null;

		if (MaterialGuiObject.activeSelf) {
			SetMaterialValues ();
			FullMaterial.SetTexture ("_Normal", _NormalMap);
			MaterialGuiScript.Initialize();
		}else if (SPM) {
			SetPreviewMaterial (_NormalMap);
		}
		*/

	}

	void ClearTexture( Texture2D textureToClear ){

		if (textureToClear) {
			Destroy (textureToClear);
			textureToClear = null;
		}

		Resources.UnloadUnusedAssets();
	}

	void ClearTexture( MapType mapType ){
		
		switch (mapType) {
		case MapType.height:
			if (_HeightMap) {
				Destroy (_HeightMap);
				_HeightMap = null;
			}
			if (_HDHeightMap) {
				Destroy (_HDHeightMap);
				_HDHeightMap = null;
			}
			break;
		case MapType.diffuse:
			if (_DiffuseMap) {
				Destroy (_DiffuseMap);
				_DiffuseMap = null;
			}
			if (_DiffuseMapOriginal) {
				Destroy (_DiffuseMapOriginal);
				_DiffuseMapOriginal = null;
			}
			break;
		case MapType.normal:
			if (_NormalMap) {
				Destroy (_NormalMap);
				_NormalMap = null;
			}
			break;
		case MapType.metallic:
			if (_MetallicMap) {
				Destroy (_MetallicMap);
				_MetallicMap = null;
			}
			break;
		case MapType.smoothness:
			if (_SmoothnessMap) {
				Destroy (_SmoothnessMap);
				_SmoothnessMap = null;
			}
			break;
		case MapType.edge:
			if (_EdgeMap) {
				Destroy (_EdgeMap);
				_EdgeMap = null;
			}
			break;
		case MapType.ao:
			if (_AOMap) {
				Destroy (_AOMap);
				_AOMap = null;
			}
			break;
		}

		Resources.UnloadUnusedAssets();
	}

	public void ClearAllTextures() {

		ClearTexture( MapType.height );
		ClearTexture( MapType.diffuse );
		ClearTexture( MapType.normal );
		ClearTexture( MapType.metallic );
		ClearTexture( MapType.smoothness );
		ClearTexture( MapType.edge );
		ClearTexture( MapType.ao );

	}

	public void SetFormat ( FileFormat newFormat ) {

		bmpSelected = false;
		jpgSelected = false;
		pngSelected = false;
		tgaSelected = false;
		tiffSelected = false;
		ddsSelected = false;

		switch (newFormat) {
		case FileFormat.bmp:
			bmpSelected = true;
			break;
		case FileFormat.jpg:
			jpgSelected = true;
			break;
		case FileFormat.png:
			pngSelected = true;
			break;
		case FileFormat.tga:
			tgaSelected = true;
			break;
		case FileFormat.tiff:
			tiffSelected = true;
			break;
		case FileFormat.dds:
			ddsSelected = true;
			break;
		}

		selectedFormat = newFormat;
	}

	public void SetFormat ( string newFormat ) {
		
		bmpSelected = false;
		jpgSelected = false;
		pngSelected = false;
		tgaSelected = false;
		tiffSelected = false;
		ddsSelected = false;

		switch (newFormat) {
		case "bmp":
			bmpSelected = true;
			selectedFormat = FileFormat.bmp;
			break;
		case "jpg":
			jpgSelected = true;
			selectedFormat = FileFormat.jpg;
			break;
		case "png":
			pngSelected = true;
			selectedFormat = FileFormat.png;
			break;
		case "tga":
			tgaSelected = true;
			selectedFormat = FileFormat.tga;
			break;
		case "tiff":
			tiffSelected = true;
			selectedFormat = FileFormat.tiff;
			break;
		case "dds":
			ddsSelected = true;
			selectedFormat = FileFormat.dds;
			break;
		}

	}

	public void SetLoadedTexture( MapType loadedTexture ){

		//SetMaterialValues ();

		switch( loadedTexture ){
		case MapType.height:
			SetPreviewMaterial (_HeightMap);
			break;
		case MapType.diffuse:
			SetPreviewMaterial (_DiffuseMap);
			break;
		case MapType.diffuseOriginal:
			SetPreviewMaterial (_DiffuseMapOriginal);
			break;
		case MapType.normal:
			SetPreviewMaterial (_NormalMap);
			break;
		case MapType.metallic:
			SetPreviewMaterial (_MetallicMap);
			break;
		case MapType.smoothness:
			SetPreviewMaterial (_SmoothnessMap);
			break;
		case MapType.edge:
			SetPreviewMaterial (_EdgeMap);
			break;
		case MapType.ao:
			SetPreviewMaterial (_AOMap);
			break;
		default:
			break;
		}

		FixSize ();
	}

	string SwitchFormats(  FileFormat selectedFormat ) {

		string extension = "bmp";
		switch (selectedFormat) {
		case FileFormat.bmp:
			extension = "bmp";
			break;
		case FileFormat.jpg:
			extension = "jpg";
			break;
		case FileFormat.png:
			extension = "png";
			break;
		case FileFormat.tga:
			extension = "tga";
			break;
		case FileFormat.tiff:
			extension = "tiff";
			break;
		case FileFormat.dds:
			extension = "dds";
			break;
		}

		return extension;
	}

	//==================================================//
	//					Property Map					//
	//==================================================//

	void SetPropertyTexture ( string texPrefix, Texture2D texture, Texture2D overlayTexture ){

		if (texture != null) {
			PropertyCompMaterial.SetTexture (texPrefix + "Tex", texture);
		} else {
			PropertyCompMaterial.SetTexture (texPrefix + "Tex", _TextureBlack);
		}

		PropertyCompMaterial.SetTexture (texPrefix + "OverlayTex", overlayTexture);

	}

	void SetPropertyMapChannel ( string texPrefix, PropChannelMap pcm ){

		switch (pcm) {
		case PropChannelMap.Height:
			SetPropertyTexture (texPrefix, _HeightMap, _TextureGrey);
			break;
		case PropChannelMap.Metallic:
			SetPropertyTexture (texPrefix, _MetallicMap, _TextureGrey);
			break;
		case PropChannelMap.Smoothness:
			SetPropertyTexture (texPrefix, _SmoothnessMap, _TextureGrey);
			break;
		case PropChannelMap.Edge:
			SetPropertyTexture (texPrefix, _EdgeMap, _TextureGrey);
			break;
		case PropChannelMap.Ao:
			SetPropertyTexture (texPrefix, _AOMap, _TextureGrey);
			break;
		case PropChannelMap.AoEdge:
			SetPropertyTexture (texPrefix, _AOMap, _EdgeMap);
			break;
		case PropChannelMap.None:
			SetPropertyTexture (texPrefix, _TextureBlack, _TextureGrey);
			break;
		}

	}

	GUIStyle summaryStyle;
	GUIStyle smallStyle => summaryStyle ?? (summaryStyle = new GUIStyle (GUI.skin.label) { fontSize = 10, wordWrap = false, clipping = TextClipping.Clip });

	// Name of the last packed save, without extension or suffix: Quick Save reuses it (Materialize CE).
	string packedBase;
	public bool HasQuickSavePath => packedBase != null;

	Texture2D BuildPacked () {
		string missing;
		Texture2D packed = ChannelPacker.Pack (this, out missing);
		if (missing != null) {
			Notifications.Error ("Empty maps, black in the packed texture: " + missing + ". Create or open them first.");
		}
		if (_PropertyMap != null) {
			Destroy (_PropertyMap);
		}
		_PropertyMap = packed;
		return packed;
	}

	public void PreviewPacked () {
		SetPreviewMaterial (BuildPacked ());
	}

	public void SavePacked (bool quick) {
		if (quick && packedBase != null) {
			textureToSave = BuildPacked ();
			SaveLoadProjectScript.SaveFile (packedBase + ChannelPacker.Suffix, selectedFormat, textureToSave, "");
			return;
		}
		SetFileMaskImage ();
		fileBrowser.ShowBrowser ("Save Packed Map", path => {
			string name = System.IO.Path.Combine (System.IO.Path.GetDirectoryName (path), System.IO.Path.GetFileNameWithoutExtension (path));
			if (ChannelPacker.Suffix.Length > 0 && name.EndsWith (ChannelPacker.Suffix, System.StringComparison.OrdinalIgnoreCase)) {
				name = name.Substring (0, name.Length - ChannelPacker.Suffix.Length);
			}
			packedBase = name;
			textureToSave = BuildPacked ();
			SaveLoadProjectScript.SaveFile (packedBase + ChannelPacker.Suffix, selectedFormat, textureToSave, "");
		});
	}

	public void ProcessPropertyMap () {
		BuildPacked ();
	}

	void ProcessPropertyMapOriginal () {

		SetPropertyMapChannel ("_Red", propRed);
		SetPropertyMapChannel ("_Green", propGreen);
		SetPropertyMapChannel ("_Blue", propBlue);
		SetPropertyMapChannel ("_Alpha", propAlpha);
		PropertyCompMaterial.SetFloat ("_UseAlpha", propAlpha == PropChannelMap.None ? 0f : 1f);

		Vector2 size = GetSize ();
		RenderTexture _TempMap = RenderTexture.GetTemporary ((int)size.x, (int)size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
		Graphics.Blit (_MetallicMap, _TempMap, PropertyCompMaterial, 0);
		RenderTexture.active = _TempMap;
		
		if (_PropertyMap != null) {
			Destroy (_PropertyMap);
			_PropertyMap = null;
		}

		// RGBA when an alpha map is chosen; the savers write 24-bit files when alpha is fully opaque.
		_PropertyMap = new Texture2D (_TempMap.width, _TempMap.height, propAlpha == PropChannelMap.None ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
		_PropertyMap.ReadPixels (new Rect (0, 0, _TempMap.width, _TempMap.height), 0, 0);
		_PropertyMap.Apply ();

		RenderTexture.ReleaseTemporary (_TempMap);
		_TempMap = null;

	}

	//==================================================//
	//					Project Saving					//
	//==================================================//

	void SaveProject (string pathToFile) {
		if (string.IsNullOrEmpty (pathToFile)) return;
		SaveLoadProjectScript.SaveProject (pathToFile, selectedFormat);
		RememberProject (pathToFile);
	}

	void LoadProject (string pathToFile) {
		if (string.IsNullOrEmpty (pathToFile)) return;
		SaveLoadProjectScript.LoadProject (pathToFile);
		RememberProject (pathToFile);
	}
	
	void SaveFile (string pathToFile) {
		SaveLoadProjectScript.SaveFile(pathToFile,selectedFormat,textureToSave, "" );
		// Materialize CE: Quick Save now works after a first save (it only did from command lists before).
		if (textureToSave == _HeightMap) QuicksavePathHeight = pathToFile;
		else if (textureToSave == _DiffuseMap || textureToSave == _DiffuseMapOriginal) QuicksavePathDiffuse = pathToFile;
		else if (textureToSave == _NormalMap) QuicksavePathNormal = pathToFile;
		else if (textureToSave == _MetallicMap) QuicksavePathMetallic = pathToFile;
		else if (textureToSave == _SmoothnessMap) QuicksavePathSmoothness = pathToFile;
		else if (textureToSave == _EdgeMap) QuicksavePathEdge = pathToFile;
		else if (textureToSave == _AOMap) QuicksavePathAO = pathToFile;
	}

	void CopyFile() {
		SaveLoadProjectScript.CopyFile (textureToSave);
	}

	void PasteFile() {
		ClearTexture (mapTypeToLoad);
		SaveLoadProjectScript.PasteFile (mapTypeToLoad);
	}

	void OpenFile (string pathToFile) {
		if (pathToFile == null) {
			return;
		}

		// clear the existing texture we are loading
		ClearTexture(mapTypeToLoad);

		StartCoroutine ( SaveLoadProjectScript.LoadTexture( mapTypeToLoad, pathToFile ) );
	}

	//==================================================//
	//			Fix the size of the test model			//
	//==================================================//

	
	public Vector2 GetSize() {

			Texture2D mapToUse = null;

			Vector2 size = new Vector2 (1024, 1024);
			
			if (_HeightMap != null) {
				mapToUse = _HeightMap;
			} else if (_DiffuseMap != null) {
				mapToUse = _DiffuseMap;
			} else if (_DiffuseMapOriginal != null) {
				mapToUse = _DiffuseMapOriginal;
			} else if (_NormalMap != null) {
				mapToUse = _NormalMap;
			} else if (_MetallicMap != null) {
				mapToUse = _MetallicMap;
			} else if (_SmoothnessMap != null) {
				mapToUse = _SmoothnessMap;
			} else if (_EdgeMap != null) {
				mapToUse = _EdgeMap;
			} else if (_AOMap != null) {
				mapToUse = _AOMap;
			}
			
			if (mapToUse != null) {
				size.x = mapToUse.width;
				size.y = mapToUse.height;
			} 

			return size;
	}

	public void FixSize() {
		
		Vector2 size = GetSize ();
		FixSizeSize( size.x, size.y );
		
	}

	void FixSizeMap( Texture2D mapToUse ) {
		FixSizeSize ( (float)mapToUse.width, (float)mapToUse.height );
	}

	void FixSizeMap( RenderTexture mapToUse ) {
		FixSizeSize ( (float)mapToUse.width, (float)mapToUse.height );
	}

	void FixSizeSize( float width, float height ) {
		
		Vector3 testObjectScale = new Vector3(1,1,1);
		float area = 1.0f;
		
		testObjectScale.x = width / height;
		
		float newArea = testObjectScale.x * testObjectScale.y;
		float areaScale = Mathf.Sqrt( area / newArea );

		testObjectScale.x *= areaScale;
		testObjectScale.y *= areaScale;
		
		testObject.transform.localScale = testObjectScale;
		
	}
}