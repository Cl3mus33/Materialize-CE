using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class TilingTextureMakerGui : MonoBehaviour {
	
	MainGui MGS;

	Material thisMaterial;

	public GameObject testObject;
	
	RenderTexture _HDHeightMapTemp;
	RenderTexture _HeightMapTemp;
	RenderTexture _DiffuseMapTemp;
	RenderTexture _DiffuseMapOriginalTemp;
	RenderTexture _MetallicMapTemp;
	RenderTexture _SmoothnessMapTemp;
	RenderTexture _NormalMapTemp;
	RenderTexture _EdgeMapTemp;
	RenderTexture _EmissionMapTemp;
	RenderTexture _SubsurfaceMapTemp;
	RenderTexture _AOMapTemp;

	RenderTexture _TileTemp;
	RenderTexture _SplatTemp;
	RenderTexture _SplatTempAlt;

	float Falloff = 0.1f;
	float OverlapX = 0.2f;
	float OverlapY = 0.2f;

	float LastFalloff = 0.1f;
	float LastOverlapX = 0.2f;
	float LastOverlapY = 0.2f;

	float TexTiling = 1.0f;
	float TexOffsetX = 0.0f;
	float TexOffsetY = 0.0f;

	string FalloffText = "0.1";
	string OverlapXText = "0.2";
	string OverlapYText = "0.2";
	
	string TexTilingText = "1.0";
	string TexOffsetXText = "0.0";
	string TexOffsetYText = "0.0";

	float SplatRotation = 0.0f;
	string SplatRotationText = "0.0";

    float SplatRotationRandom = 0.25f;
    string SplatRotationRandomText = "0.25";

	float SplatScale = 1.0f;
	string SplatScaleText = "1.0";

	float SplatWobble = 0.2f;
	string SplatWobbleText = "0.2";

    float SplatWobbleRandom = 0.2f;
    string SplatWobbleRandomText = "0.2";

    float SplatRandomize = 0.0f;
    string SplatRandomizeText = "0.0";

	GUIContent[] TexSizes;

	int NewTexSelectionX = 2;
	int NewTexSelectionY = 2;

	int LastNewTexSelectionX = 2;
	int LastNewTexSelectionY = 2;

	int NewTexSizeX = 1024;
	int NewTexSizeY = 1024;

    Vector2 targetAR;

	Material blitMaterial;

	Rect windowRect = new Rect (30, 300, 300, 530);

	bool doStuff = false;

	bool techniqueOverlap = true;
	bool techniqueSplat = false;

	Vector4[] splatKernel;

	Vector2[] offsetKernel;

	Vector3 objectScale = Vector3.one;

	enum TileTechnique {
		Overlap,
		Splat
	}

		TileTechnique tileTech = TileTechnique.Overlap;
	TileTechnique lastTileTech = TileTechnique.Overlap;

	// ---------- Materialize CE: fluid preview ----------
	// While a slider moves, the maps are tiled at PreviewMax at most; the full size follows once the user
	// stops for RefineDelay seconds (and always before Set Maps). GPU buffers come from Unity's pool instead
	// of being created and destroyed on every change, and the source maps are copied once per session.
	const int PreviewMax = 1024;
	const float RefineDelay = 0.35f;
	int workX = 1024, workY = 1024;
	bool pendingRefine;
	float lastChange;
	readonly Dictionary<Texture, RenderTexture> sourceCache = new Dictionary<Texture, RenderTexture> ();
	// The splat pattern's randomness is seeded: it no longer reshuffles while a slider moves.
	int patternSeed = 1;


	
	void Start () {

		blitMaterial = new Material (Shader.Find ("Hidden/Blit_Seamless_Texture_Maker"));

		TexSizes = new GUIContent[4];
		TexSizes [0] = L.G("512" );
		TexSizes [1] = L.G("1024" );
		TexSizes [2] = L.G("2048" );
		TexSizes [3] = L.G("4096" );

        //offsetKernel = new Vector2[4];
        //offsetKernel [0] = new Vector2 (-0.5f, -0.5f);
        //offsetKernel [1] = new Vector2 (-0.5f, 0.5f);
        //offsetKernel [2] = new Vector2 (0.5f, -0.5f);
        //offsetKernel [3] = new Vector2 (0.5f, 0.5f);

		offsetKernel = new Vector2[9];
		offsetKernel [0] = new Vector2 (-1, -1);
		offsetKernel [1] = new Vector2 (-1, 0);
		offsetKernel [2] = new Vector2 (-1, 1);
		offsetKernel [3] = new Vector2 (0, -1);
		offsetKernel [4] = new Vector2 (0, 0);
		offsetKernel [5] = new Vector2 (0, 1);
		offsetKernel [6] = new Vector2 (1, -1);
		offsetKernel [7] = new Vector2 (1, 0);
		offsetKernel [8] = new Vector2 (1, 1);

	}

	public void Initialize(){
		MGS = MainGui.instance;
		thisMaterial = MGS.FullMaterial;

		testObject.GetComponent<Renderer>().material = thisMaterial;
		doStuff = true;
	}

	void Update() {

		thisMaterial.SetVector ("_Tiling", new Vector4 ( TexTiling, TexTiling, TexOffsetX, TexOffsetY ));

		if (LastOverlapX != OverlapX) {
			LastOverlapX = OverlapX;
			doStuff = true;
		}

		if (LastOverlapY != OverlapY) {
			LastOverlapY = OverlapY;
			doStuff = true;
		}

		if (LastFalloff != Falloff) {
			LastFalloff = Falloff;
			doStuff = true;
		}

		if (NewTexSelectionX != LastNewTexSelectionX) {
			LastNewTexSelectionX = NewTexSelectionX;
			doStuff = true;
		}

		if (NewTexSelectionY != LastNewTexSelectionY) {
			LastNewTexSelectionY = NewTexSelectionY;
			doStuff = true;
		}

		if (tileTech != lastTileTech) {
			lastTileTech = tileTech;
			doStuff = true;
		}

        if (doStuff) {
            doStuff = false;

    		switch (NewTexSelectionX) {
    		case 0:
    			NewTexSizeX = 512;
    			break;
    		case 1:
    			NewTexSizeX = 1024;
    			break;
    		case 2:
    			NewTexSizeX = 2048;
    			break;
    		case 3:
    			NewTexSizeX = 4096;
    			break;
    		default:
    			NewTexSizeX = 1024;
    			break;
    		}
    		
    		switch (NewTexSelectionY) {
    		case 0:
    			NewTexSizeY = 512;
    			break;
    		case 1:
    			NewTexSizeY = 1024;
    			break;
    		case 2:
    			NewTexSizeY = 2048;
    			break;
    		case 3:
    			NewTexSizeY = 4096;
    			break;
    		default:
    			NewTexSizeY = 1024;
    			break;
    		}



    			    		float aspect = (float)NewTexSizeX / (float)NewTexSizeY;

				var randomState = Random.state;
				Random.InitState (patternSeed);


            if (Mathf.Approximately(aspect, 8.0f)) { 
    			SKRectWide3 ();
            } else if (Mathf.Approximately(aspect, 4.0f)) { 
    			SKRectWide2 ();
            } else if (Mathf.Approximately(aspect, 2.0f)) { 
    			SKRectWide ();
            } else if (Mathf.Approximately(aspect, 1.0f)) { 
    			SKSquare ();
            } else if (Mathf.Approximately(aspect, 0.5f)) { 
    			SKRectTall ();
            } else if (Mathf.Approximately(aspect, 0.25f)) { 
    			SKRectTall2 ();
                        } else if (Mathf.Approximately(aspect, 0.125f)) {
    			SKRectTall3 ();
    		}
				Random.state = randomState;

    		

    		float area = 1.0f;
    		objectScale = Vector3.one;
    		objectScale.x = aspect;
    		float newArea = objectScale.x * objectScale.y;
    		float areaScale = Mathf.Sqrt ( area / newArea );
    		
    		objectScale.x *= areaScale;
    		objectScale.y *= areaScale;
    		
    			    		testObject.transform.localScale = objectScale;

				float scale = Mathf.Min (1f, (float)PreviewMax / Mathf.Max (NewTexSizeX, NewTexSizeY));
				workX = Mathf.Max (64, Mathf.RoundToInt (NewTexSizeX * scale));
				workY = Mathf.Max (64, Mathf.RoundToInt (NewTexSizeY * scale));
				TileTextures ();
				pendingRefine = scale < 1f;
				lastChange = Time.unscaledTime;
			} else if (pendingRefine && Time.unscaledTime - lastChange > RefineDelay) {
				pendingRefine = false;
				workX = NewTexSizeX;
				workY = NewTexSizeY;
				TileTextures ();
			}
		}

	void SKSquare(){
        splatKernel = new Vector4[4];
		splatKernel [0] = new Vector4 (0.0f, 0.25f, 0.8f, Random.value);
        splatKernel [1] = new Vector4 (0.5f, 0.25f, 0.8f, Random.value);
        splatKernel [2] = new Vector4 (0.25f, 0.75f, 0.8f, Random.value);
        splatKernel [3] = new Vector4 (0.75f, 0.75f, 0.8f, Random.value);
	}

	void SKRectWide(){
        splatKernel = new Vector4[6];
		splatKernel [0] = new Vector4 (0.0f, 0.25f, 0.5f, Random.value);
		splatKernel [1] = new Vector4 (0.333f, 0.25f, 0.5f, Random.value);
		splatKernel [2] = new Vector4 (0.666f, 0.25f, 0.5f, Random.value);

		splatKernel [3] = new Vector4 (0.166f, 0.75f, 0.5f, Random.value);
		splatKernel [4] = new Vector4 (0.5f, 0.75f, 0.5f, Random.value);
		splatKernel [5] = new Vector4 (0.833f, 0.75f, 0.5f, Random.value);
	}

	void SKRectWide2(){
		splatKernel = new Vector4[4];
        splatKernel [0] = new Vector4 (0.0f, 0.375f, 0.4f, Random.value);
        splatKernel [1] = new Vector4 (0.25f, 0.625f, 0.4f, Random.value);
        splatKernel [2] = new Vector4 (0.5f, 0.375f, 0.4f, Random.value);
        splatKernel [3] = new Vector4 (0.75f, 0.625f, 0.4f, Random.value);
	}

	void SKRectWide3(){
		splatKernel = new Vector4[8];
        splatKernel [0] = new Vector4 (0.0f, 0.375f, 0.25f, Random.value);
        splatKernel [1] = new Vector4 (0.125f, 0.625f, 0.25f, Random.value);
        splatKernel [2] = new Vector4 (0.25f, 0.375f, 0.25f, Random.value);
        splatKernel [3] = new Vector4 (0.375f, 0.625f, 0.25f, Random.value);
        splatKernel [4] = new Vector4 (0.5f, 0.375f, 0.25f, Random.value);
        splatKernel [5] = new Vector4 (0.625f, 0.625f, 0.25f, Random.value);
        splatKernel [6] = new Vector4 (0.75f, 0.375f, 0.25f, Random.value);
        splatKernel [7] = new Vector4 (0.875f, 0.625f, 0.25f, Random.value);
	}

	void SKRectTall(){
		splatKernel = new Vector4[6];
		splatKernel [0] = new Vector4 (0.25f, 0.0f, 0.5f, Random.value);
		splatKernel [1] = new Vector4 (0.25f, 0.333f, 0.5f, Random.value);
		splatKernel [2] = new Vector4 (0.25f, 0.666f, 0.5f, Random.value);
		
		splatKernel [3] = new Vector4 (0.75f, 0.166f, 0.5f, Random.value);
		splatKernel [4] = new Vector4 (0.75f, 0.5f, 0.5f, Random.value);
		splatKernel [5] = new Vector4 (0.75f, 0.833f, 0.5f, Random.value);
	}
	
	void SKRectTall2(){
		splatKernel = new Vector4[4];
        splatKernel [0] = new Vector4 (0.375f, 0.0f, 0.4f, Random.value);
        splatKernel [1] = new Vector4 (0.625f, 0.25f, 0.4f, Random.value);
        splatKernel [2] = new Vector4 (0.375f, 0.5f, 0.4f, Random.value);
        splatKernel [3] = new Vector4 (0.625f, 0.75f, 0.4f, Random.value);
	}
	
	void SKRectTall3(){
        splatKernel = new Vector4[8];
        splatKernel [0] = new Vector4 ( 0.375f, 0.0f, 0.25f, Random.value);
        splatKernel [1] = new Vector4 ( 0.625f, 0.125f, 0.25f, Random.value);
        splatKernel [2] = new Vector4 ( 0.375f, 0.25f, 0.25f, Random.value);
        splatKernel [3] = new Vector4 ( 0.625f, 0.375f, 0.25f, Random.value);
        splatKernel [4] = new Vector4 ( 0.375f, 0.5f, 0.25f, Random.value);
        splatKernel [5] = new Vector4 ( 0.625f, 0.625f, 0.25f, Random.value);
        splatKernel [6] = new Vector4 ( 0.375f, 0.75f, 0.25f, Random.value);
        splatKernel [7] = new Vector4 ( 0.625f, 0.875f, 0.25f, Random.value);
	}
	
	void DoMyWindow ( int windowID ) {
		UiHelp.Panel = "Tiling";
		
		int spacingX = 0;
		int spacingY = 50;
		int spacing2Y = 70;
		
		int offsetX = 10;
		int offsetY = 30;

		techniqueOverlap = GUI.Toggle (new Rect(offsetX, offsetY, 130, 30), techniqueOverlap, UiHelp.Content ("Technique Overlap"));
		if (techniqueOverlap) {
			techniqueSplat = false;
			tileTech = TileTechnique.Overlap;
		} else if ( !techniqueSplat ) {
			techniqueOverlap = true;
			tileTech = TileTechnique.Overlap;
		}

		techniqueSplat = GUI.Toggle (new Rect (offsetX + 150, offsetY, 130, 30), techniqueSplat, UiHelp.Content ("Technique Splat"));
		if (techniqueSplat) {
			techniqueOverlap = false;
			tileTech = TileTechnique.Splat;
		} else if (!techniqueOverlap) {
			techniqueSplat = true;
			tileTech = TileTechnique.Splat;
		}

		offsetY += 40;
		
		GUI.Label (new Rect (offsetX, offsetY, 150, 20), UiHelp.Content ("New Texture Size X"));
		NewTexSelectionX = GUI.SelectionGrid( new Rect (offsetX, offsetY + 30, 120, 50), NewTexSelectionX, TexSizes, 2 );
		
		GUI.Label (new Rect (offsetX + 150, offsetY, 150, 20), UiHelp.Content ("New Texture Size Y"));
		NewTexSelectionY = GUI.SelectionGrid( new Rect (offsetX + 150, offsetY + 30, 120, 50), NewTexSelectionY, TexSizes, 2 );
		
		offsetY += 100;
		
		if (GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Edge Falloff", Falloff, FalloffText, out Falloff, out FalloffText, 0.01f, 1.0f)) {
			doStuff = true;
		}
		offsetY += 40;

		if (techniqueOverlap) {

			if (GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Overlap X", OverlapX, OverlapXText, out OverlapX, out OverlapXText, 0.00f, 1.0f)) {
				doStuff = true;
			}
			offsetY += 40;
		
			if (GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Overlap Y", OverlapY, OverlapYText, out OverlapY, out OverlapYText, 0.00f, 1.0f)) {
				doStuff = true;
			}
			offsetY += 50;

		}

		if (techniqueSplat) {

			if (GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Splat Rotation", SplatRotation, SplatRotationText, out SplatRotation, out SplatRotationText, 0.0f, 1.0f)) {
				doStuff = true;
			}
			offsetY += 40;

            if (GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Splat Random Rotation", SplatRotationRandom, SplatRotationRandomText, out SplatRotationRandom, out SplatRotationRandomText, 0.0f, 1.0f)) {
                doStuff = true;
            }
            offsetY += 40;

			if (GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Splat Scale", SplatScale, SplatScaleText, out SplatScale, out SplatScaleText, 0.5f, 2.0f)) {
				doStuff = true;
			}
			offsetY += 40;

			if (GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Splat Wooble Amount", SplatWobble, SplatWobbleText, out SplatWobble, out SplatWobbleText, 0.0f, 1.0f)) {
				doStuff = true;
			}
            offsetY += 40;

                        if (GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Splat Randomize", SplatRandomize, SplatRandomizeText, out SplatRandomize, out SplatRandomizeText, 0.0f, 1.0f)) {
                doStuff = true;
            }
            offsetY += 40;

			if (GUI.Button (new Rect (offsetX, offsetY, 130, 25), UiHelp.Content ("New Pattern"))) {
				patternSeed++;
				doStuff = true;
			}
			GUI.Label (new Rect (offsetX + 140, offsetY + 3, 150, 22), pendingRefine ? "Preview (refining…)" : "Full resolution");
            offsetY += 40;

		}

		GUI.Label (new Rect (offsetX, offsetY, 150, 30), UiHelp.Content ("Tiling Test Variables"));
		offsetY += 30;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Texture Tiling", TexTiling, TexTilingText, out TexTiling, out TexTilingText, 0.1f, 5.0f);
		offsetY += 40;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Texture Offset X", TexOffsetX, TexOffsetXText, out TexOffsetX, out TexOffsetXText, -1.0f, 1.0f);
		offsetY += 40;
		
		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Texture Offset Y", TexOffsetY, TexOffsetYText, out TexOffsetY, out TexOffsetYText, -1.0f, 1.0f);
		offsetY += 40;
		
		if( GUI.Button (new Rect (offsetX + 150, offsetY, 130, 30), UiHelp.Content ("Set Maps")) ){
			StartCoroutine( SetMaps ( ) );
		}

		
		Tips.Capture (true);

		
		GUI.DragWindow();
		
	}

	void OnGUI () {
		Theme.Apply ();
		
		windowRect.width = 300;
				if (techniqueSplat) {
			windowRect.height = 650;
		} else {
			windowRect.height = 490;
		}
		
		windowRect = UiShell.Dock (windowRect);
		
		windowRect = UiShell.Window(18, windowRect, DoMyWindow, L.T("Tiling Texture Maker"));
		
		Tips.Block (windowRect);
		
	}

	Texture2D SetMap ( Texture2D textureTarget, RenderTexture textureToSet ) {
		RenderTexture.active = textureToSet;
		textureTarget = new Texture2D (NewTexSizeX, NewTexSizeY, TextureFormat.ARGB32, true, true);
		textureTarget.ReadPixels (new Rect (0, 0, NewTexSizeX, NewTexSizeY), 0, 0);
		textureTarget.Apply ();
		return textureTarget;
	}

	RenderTexture SetMapRT ( RenderTexture textureTarget, RenderTexture textureToSet ) {
		textureTarget = new RenderTexture (NewTexSizeX, NewTexSizeY, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
		textureTarget.wrapMode = TextureWrapMode.Repeat;
		Graphics.Blit (textureToSet, textureTarget);
		return textureTarget;
	}

		IEnumerator SetMaps () {

		if (MGS._HeightMap == null) {
			yield break;
		}

		// The preview may be at reduced size: the maps are always set at the chosen size.
		if (workX != NewTexSizeX || workY != NewTexSizeY) {
			workX = NewTexSizeX;
			workY = NewTexSizeY;
			pendingRefine = false;
			TileTextures ();
		}


		if (MGS._DiffuseMap != null) { 
			Debug.Log ("Setting Diffuse");
			Destroy (MGS._DiffuseMap);
			MGS._DiffuseMap = null;
			MGS._DiffuseMap = SetMap( MGS._DiffuseMap, _DiffuseMapTemp ); 
		}

		yield return new WaitForSeconds(0.1f);

		if (MGS._DiffuseMapOriginal != null) { 
			Debug.Log ("Setting Original Diffuse");
			Destroy (MGS._DiffuseMapOriginal);
			MGS._DiffuseMapOriginal = null;
			MGS._DiffuseMapOriginal = SetMap( MGS._DiffuseMapOriginal, _DiffuseMapOriginalTemp ); 
		}

		yield return new WaitForSeconds(0.1f);

		if (MGS._MetallicMap != null) { 
			Debug.Log ("Setting Specular");
			Destroy (MGS._MetallicMap);
			MGS._MetallicMap = null;
			MGS._MetallicMap = SetMap( MGS._MetallicMap, _MetallicMapTemp ); 
		}

		yield return new WaitForSeconds(0.1f);

		if (MGS._SmoothnessMap != null) { 
			Debug.Log ("Setting Roughness");
			Destroy (MGS._SmoothnessMap);
			MGS._SmoothnessMap = null;
			MGS._SmoothnessMap = SetMap( MGS._SmoothnessMap, _SmoothnessMapTemp ); 
		}

		yield return new WaitForSeconds(0.1f);

		if (MGS._NormalMap != null) { 
			Debug.Log ("Setting Normal");
			Destroy (MGS._NormalMap);
			MGS._NormalMap = null;
			MGS._NormalMap = SetMap( MGS._NormalMap, _NormalMapTemp );
		}

		yield return new WaitForSeconds(0.1f);

		if (MGS._EdgeMap != null) { 
			Debug.Log ("Setting Edge");
			Destroy (MGS._EdgeMap);
			MGS._EdgeMap = null;
			MGS._EdgeMap = SetMap( MGS._EdgeMap, _EdgeMapTemp );
		}

		yield return new WaitForSeconds(0.1f);

		if (MGS._AOMap != null) { 
			Debug.Log ("Setting AO");
			Destroy (MGS._AOMap);
			MGS._AOMap = null;
			MGS._AOMap = SetMap( MGS._AOMap, _AOMapTemp );
		}

		if (MGS._EmissionMap != null) {
			Destroy (MGS._EmissionMap);
			MGS._EmissionMap = null;
			MGS._EmissionMap = SetMap( MGS._EmissionMap, _EmissionMapTemp );
		}

		if (MGS._SubsurfaceMap != null) {
			Destroy (MGS._SubsurfaceMap);
			MGS._SubsurfaceMap = null;
			MGS._SubsurfaceMap = SetMap( MGS._SubsurfaceMap, _SubsurfaceMapTemp );
		}

		yield return new WaitForSeconds(0.1f);

		if (MGS._HeightMap != null) { 
			Debug.Log ("Setting Height");
			Destroy (MGS._HeightMap);
			MGS._HeightMap = null;
			MGS._HeightMap = SetMap (MGS._HeightMap, _HeightMapTemp);
		}

		yield return new WaitForSeconds(0.1f);


		if (MGS._HDHeightMap != null) { 
			Debug.Log ("Setting Height");
			MGS._HDHeightMap.Release ();
			MGS._HDHeightMap = null;
			MGS._HDHeightMap = SetMapRT (MGS._HDHeightMap, _HDHeightMapTemp);
		}

		yield return new WaitForSeconds(0.1f);

		// The maps were replaced: their cached copies are stale.
		foreach (var rt in sourceCache.Values) {
			if (rt != null) {
				rt.Release ();
				Destroy (rt);
			}
		}
		sourceCache.Clear ();

		Resources.UnloadUnusedAssets ();
		Notifications.Info ("Tiled maps set at " + NewTexSizeX + " × " + NewTexSizeY + ".");

	}

	public void Close(){
		CleanupTextures ();
		this.gameObject.SetActive (false);
	}
	
		// All working buffers come from RenderTexture.GetTemporary (Materialize CE).
	void CleanupTexture( RenderTexture _Texture ) {
		if (_Texture != null) {
			RenderTexture.ReleaseTemporary (_Texture);
		}
	}

	RenderTexture Rent (int width, int height, RenderTextureFormat format) {
		var rt = RenderTexture.GetTemporary (width, height, 0, format, RenderTextureReadWrite.Linear);
		rt.wrapMode = TextureWrapMode.Repeat;
		rt.filterMode = FilterMode.Bilinear;
		return rt;
	}

	void CleanupTexture( Texture2D _Texture ) {		
		if (_Texture != null) {
			Destroy( _Texture );
			_Texture = null;
		}
	}
	
	void CleanupTextures() {
		
		Debug.Log ("Cleaning Up Textures");

		CleanupTexture( _HDHeightMapTemp );
		CleanupTexture( _HeightMapTemp );
		CleanupTexture( _DiffuseMapTemp );
		CleanupTexture( _DiffuseMapOriginalTemp );
		CleanupTexture( _MetallicMapTemp );
		CleanupTexture( _SmoothnessMapTemp );
		CleanupTexture( _NormalMapTemp );
		CleanupTexture( _EdgeMapTemp );
		CleanupTexture( _EmissionMapTemp );
		CleanupTexture( _SubsurfaceMapTemp );
		CleanupTexture( _AOMapTemp );

				_HDHeightMapTemp = _HeightMapTemp = _DiffuseMapTemp = _DiffuseMapOriginalTemp = null;
		_MetallicMapTemp = _SmoothnessMapTemp = _NormalMapTemp = _EdgeMapTemp = _AOMapTemp = _EmissionMapTemp = _SubsurfaceMapTemp = null;

		foreach (var rt in sourceCache.Values) {
			if (rt != null) {
				rt.Release ();
				Destroy (rt);
			}
		}
		sourceCache.Clear ();
	}

	// need an overload to turn Texture2D into RenderTexture;
	RenderTexture TileTexture ( Texture2D textureToTile, RenderTexture textureTarget, string TexName ) {
		
				RenderTexture source;
		if (!sourceCache.TryGetValue (textureToTile, out source) || source == null) {
			source = new RenderTexture (textureToTile.width, textureToTile.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
			source.wrapMode = TextureWrapMode.Repeat;
			Graphics.Blit (textureToTile, source);
			sourceCache[textureToTile] = source;
		}
		return TileTexture (source, textureTarget, TexName);

	}

	RenderTexture TileTexture ( RenderTexture textureToTile, RenderTexture textureTarget, string TexName ) {

		switch(tileTech) {
		case TileTechnique.Overlap:
			return TileTextureOverlap (textureToTile, textureTarget, TexName);
			break;
		case TileTechnique.Splat:
			return TileTextureSplat (textureToTile, textureTarget, TexName);
			break;
		default:
			return TileTextureOverlap (textureToTile, textureTarget, TexName);
			break;
		}

	}

	RenderTexture TileTextureSplat ( RenderTexture textureToTile, RenderTexture textureTarget, string TexName ) {

				CleanupTexture (textureTarget);
		// The splat buffers keep the depth of each piece in alpha: half floats (ARGB32 lost precision there).
		RenderTextureFormat format = TexName == "_HDDisplacementMap" ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
		_SplatTemp = Rent (workX, workY, RenderTextureFormat.ARGBHalf);
		_SplatTempAlt = Rent (workX, workY, RenderTextureFormat.ARGBHalf);
		textureTarget = Rent (workX, workY, format);

		blitMaterial.SetTexture ("_MainTex", textureToTile);
		blitMaterial.SetTexture ("_HeightTex", MGS._HeightMap);
		blitMaterial.SetVector ("_ObjectScale", objectScale);

		if (SettingsGui.instance.settings.normalMapMayaStyle) {
			blitMaterial.SetFloat ("_FlipY", 1.0f); 
		} else {
			blitMaterial.SetFloat ("_FlipY", 0.0f);
		}

		// Clear the ping pong buffers
		Graphics.Blit (Texture2D.blackTexture, _SplatTemp, blitMaterial, 2);
		Graphics.Blit (Texture2D.blackTexture, _SplatTempAlt, blitMaterial, 2);

		float texARWidth = (float)MGS._HeightMap.width / (float)MGS._HeightMap.height;
		float texARHeight = (float)MGS._HeightMap.height / (float)MGS._HeightMap.width;
		Vector2 texAR = Vector2.one;
		if (texARWidth < texARHeight) {
			texAR.x = texARWidth;
		} else {
			texAR.y = texARHeight;
		}
            
        		float targetARWidth = (float)workX / (float)workY;
        float targetARHeight = (float)workY / (float)workX;
        targetAR = Vector2.one;
        if (targetARWidth < targetARHeight) {
            targetAR.x = targetARWidth;
        } else {
            targetAR.y = targetARHeight;
        }

        blitMaterial.SetFloat("_SplatScale", SplatScale );
        blitMaterial.SetVector("_AspectRatio", texAR );
        blitMaterial.SetVector ("_TargetAspectRatio", targetAR);

        blitMaterial.SetFloat("_SplatRotation", SplatRotation );
        blitMaterial.SetFloat("_SplatRotationRandom", SplatRotationRandom );

		bool isEven = true;
		for (int i = 0; i < splatKernel.Length; i++) {

            blitMaterial.SetVector("_SplatKernel", splatKernel[i] );

            float offsetX = Mathf.Sin ( ( SplatRandomize + 1.0f + (float)i ) * 128.352f );
            float offsetY = Mathf.Cos ( ( SplatRandomize + 1.0f + (float)i ) * 243.767f );
            blitMaterial.SetVector("_Wobble", new Vector3( offsetX, offsetY, SplatWobble ) );

            blitMaterial.SetFloat ("_SplatRandomize", Mathf.Sin ( ( SplatRandomize + 1.0f + (float)i ) * 472.361f ));

			if( isEven ){
				blitMaterial.SetTexture ("_TargetTex", _SplatTempAlt);
				Graphics.Blit (textureToTile, _SplatTemp, blitMaterial, 1);
				isEven = false;
			}else{
				blitMaterial.SetTexture ("_TargetTex", _SplatTemp);
				Graphics.Blit (textureToTile, _SplatTempAlt, blitMaterial, 1);
				isEven = true;
			}
		}

		//GameObject.Destroy(transHelper.gameObject);

		if (isEven) {
			Graphics.Blit (_SplatTempAlt, textureTarget, blitMaterial, 3);
		} else {
			Graphics.Blit (_SplatTemp, textureTarget, blitMaterial, 3);
		}

		thisMaterial.SetTexture ( TexName, textureTarget );

				CleanupTexture( _SplatTemp );
		CleanupTexture( _SplatTempAlt );
		_SplatTemp = _SplatTempAlt = null;

		return textureTarget;

	}


	RenderTexture TileTextureOverlap ( RenderTexture textureToTile, RenderTexture textureTarget, string TexName ) {
		
				CleanupTexture (textureTarget);
		textureTarget = Rent (workX, workY, TexName == "_HDDisplacementMap" ? RenderTextureFormat.RHalf : RenderTextureFormat.ARGB32);

		blitMaterial.SetTexture ("_MainTex", textureToTile);

		Graphics.Blit( textureToTile, textureTarget, blitMaterial, 0 );
		
		thisMaterial.SetTexture ( TexName, textureTarget );

		return textureTarget;
		
	}

		void TileTextures () {

		blitMaterial.SetFloat ("_Falloff", 1.0f);
		blitMaterial.SetFloat ("_Falloff", Falloff);
		blitMaterial.SetFloat ("_OverlapX", OverlapX);
		blitMaterial.SetFloat ("_OverlapY", OverlapY);

				if (MGS._HeightMap == null) {
				return;
		} else {
			blitMaterial.SetTexture ("_HeightTex", MGS._HeightMap);
			blitMaterial.SetFloat ( "_IsHeight", 1.0f );
			_HeightMapTemp = TileTexture(MGS._HeightMap, _HeightMapTemp, "_DisplacementMap");
		}


		if (MGS._HDHeightMap != null) {
			blitMaterial.SetFloat ( "_IsHeight", 1.0f );
			_HDHeightMapTemp = TileTexture(MGS._HDHeightMap, _HDHeightMapTemp, "_HDDisplacementMap");
		}


		blitMaterial.SetFloat ( "_IsHeight", 0.0f );

		if (MGS._DiffuseMapOriginal != null) { 
			_DiffuseMapOriginalTemp = TileTexture(MGS._DiffuseMapOriginal, _DiffuseMapOriginalTemp, "_DiffuseMapOriginal"); 
			thisMaterial.SetTexture ("_DiffuseMap", _DiffuseMapOriginalTemp);
		}

		if (MGS._DiffuseMap != null) { 
			_DiffuseMapTemp = TileTexture(MGS._DiffuseMap, _DiffuseMapTemp, "_DiffuseMap"); 
			thisMaterial.SetTexture ("_DiffuseMap", _DiffuseMapTemp);
		}

		if (MGS._MetallicMap != null) {
			_MetallicMapTemp = TileTexture(MGS._MetallicMap, _MetallicMapTemp, "_MetallicMap");
			thisMaterial.SetTexture ("_MetallicMap", _MetallicMapTemp);
		}

		if (MGS._SmoothnessMap != null) { 
			_SmoothnessMapTemp = TileTexture(MGS._SmoothnessMap, _SmoothnessMapTemp, "_SmoothnessMap"); 
			thisMaterial.SetTexture ("_SmoothnessMap", _SmoothnessMapTemp);
		}

		if (MGS._NormalMap != null) { 
			blitMaterial.SetFloat ( "_IsNormal", 1.0f );
			_NormalMapTemp = TileTexture(MGS._NormalMap, _NormalMapTemp, "_NormalMap"); 
			thisMaterial.SetTexture ("_NormalMap", _NormalMapTemp);
		}

		blitMaterial.SetFloat ( "_IsNormal", 0.0f );

		if (MGS._EdgeMap != null) { 
			_EdgeMapTemp = TileTexture(MGS._EdgeMap, _EdgeMapTemp, "_EdgeMap");
			thisMaterial.SetTexture ("_EdgeMap", _EdgeMapTemp);
		}

		if (MGS._AOMap != null) { 
			_AOMapTemp = TileTexture(MGS._AOMap, _AOMapTemp, "_AOMap");
			thisMaterial.SetTexture ("_AOMap", _AOMapTemp);
		}

		if (MGS._EmissionMap != null) {
			_EmissionMapTemp = TileTexture(MGS._EmissionMap, _EmissionMapTemp, "_MceEmissionMap");
			Shader.SetGlobalTexture ("_MceEmissionMap", _EmissionMapTemp);
		}

		if (MGS._SubsurfaceMap != null) {
			_SubsurfaceMapTemp = TileTexture(MGS._SubsurfaceMap, _SubsurfaceMapTemp, "_MceSubsurfaceMap");
			Shader.SetGlobalTexture ("_MceSubsurfaceMap", _SubsurfaceMapTemp);
		}

			}
}