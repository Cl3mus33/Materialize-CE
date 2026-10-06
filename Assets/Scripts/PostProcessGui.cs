using UnityEngine;
using System.Collections;

public class PostProcessGui : MonoBehaviour {

	public GameObject MainGuiObject;
	public GameObject MainCamera;

	OpaquePostProcess oppScript;
	PostProcess ppScript;

	bool EnablePostProcess = true;
	bool FilmicTonemap = true;
	float Exposure = 1.0f;
	string ExposureText = "1";

	bool UseTAA = true;

	float BloomThreshold = 0.8f;
	string BloomThresholdText = "0.8";

	float BloomAmount = 1.0f;
	string BloomAmountText = "1.0";
	
	float LensFlareAmount = 0.5f;
	string LensFlareAmountText = "0.5";

	float LensDirtAmount = 1.0f;
	string LensDirtAmountText = "1.0";

	float VignetteAmount = 0.2f;
	string VignetteAmountText = "0.2";

	float DOFMaxBlur = 0.0f;
	string DOFMaxBlurText = "0.0";

	float DOFFocalDepth = 10.0f;
	string DOFFocalDepthText = "10.0";

	float DOFMaxDistance = 50.0f;
	string DOFMaxDistanceText = "50.0";

	bool AutoFocus = true;

	Rect windowRect = new Rect (360, 330, 300, 530);

	bool initialized = false;
	
	// Use this for initialization
	void Start () {
		Initialize ();
	}

	void Initialize(){
		if (!initialized) {
			oppScript = MainCamera.GetComponent<OpaquePostProcess> ();
			ppScript = MainCamera.GetComponent<PostProcess> ();
			initialized = true;
		}
	}

	public void PostProcessOn(){
		Initialize ();

		EnablePostProcess = true;

		oppScript.enabled = true;
		
		ppScript.enabled = true;
		ppScript.bloomThreshold = BloomThreshold;
		ppScript.bloomAmount = BloomAmount;
		
		ppScript.lensFlareAmount = LensFlareAmount;
		ppScript.lensDirtAmount = LensDirtAmount;
		ppScript.vignetteAmount = VignetteAmount;
		ppScript.filmicTonemap = FilmicTonemap;
		ppScript.exposure = Exposure;


		if (DOFMaxBlur > 12) {
			ppScript.DOFMaxBlur = 16;
		} else if (DOFMaxBlur > 6) {
			ppScript.DOFMaxBlur = 8;
		} else if (DOFMaxBlur > 3) {
			ppScript.DOFMaxBlur = 4;
		} else if (DOFMaxBlur > 1.5) {
			ppScript.DOFMaxBlur = 2;
		} else if (DOFMaxBlur > 0.5) {
			ppScript.DOFMaxBlur = 1;
		} else {
			ppScript.DOFMaxBlur = 0;
		}
		ppScript.focalDepth = DOFFocalDepth;
		ppScript.DOFMaxDistance = DOFMaxDistance;

		ppScript.AutoFocus = AutoFocus;
	}

	public void PostProcessOff(){
		Initialize ();

		EnablePostProcess = false;

		oppScript.enabled = false;			
		ppScript.enabled = false;
	}
	
	// Update is called once per frame
	void Update () {

		if (EnablePostProcess) {
			PostProcessOn();
		} else {
			PostProcessOff();
		}
	
	}

	void DoMyWindow ( int windowID ) {
		UiHelp.Panel = "PostProcess";
		
		int spacingX = 0;
		int spacingY = 50;
		int spacing2Y = 70;
		
		int offsetX = 10;
		int offsetY = 30;
		
		EnablePostProcess = GUI.Toggle (new Rect (offsetX, offsetY, 280, 30), EnablePostProcess, UiHelp.Content ("Enable Post Process"));
		offsetY += 40;


		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Bloom Threshold", BloomThreshold, BloomThresholdText, out BloomThreshold, out BloomThresholdText, 0.0f, 2.0f );
		offsetY += 40;
		
		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Bloom Amount", BloomAmount, BloomAmountText, out BloomAmount, out BloomAmountText, 0.0f, 8.0f );
		offsetY += 60;



		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Lens Flare Amount", LensFlareAmount, LensFlareAmountText, out LensFlareAmount, out LensFlareAmountText, 0.0f, 4.0f );
		offsetY += 40;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Lens Dirt Amount", LensDirtAmount, LensDirtAmountText, out LensDirtAmount, out LensDirtAmountText, 0.0f, 2.0f );
		offsetY += 40;

		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Vignette Amount", VignetteAmount, VignetteAmountText, out VignetteAmount, out VignetteAmountText, 0.0f, 1.0f );
		offsetY += 40;

		FilmicTonemap = GUI.Toggle (new Rect (offsetX, offsetY, 280, 25), FilmicTonemap, UiHelp.Content ("Filmic tone mapping (ACES)"));
		offsetY += 30;
		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "Exposure", Exposure, ExposureText, out Exposure, out ExposureText, 0.25f, 4.0f );
		offsetY += 60;

		
		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "DOF Max Blur", DOFMaxBlur, DOFMaxBlurText, out DOFMaxBlur, out DOFMaxBlurText, 0.0f, 16.0f );
		offsetY += 40;
		
		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "DOF Focal Depth", DOFFocalDepth, DOFFocalDepthText, out DOFFocalDepth, out DOFFocalDepthText, 1.0f, 50.0f );
		offsetY += 40;
		
		GuiHelper.Slider (new Rect (offsetX, offsetY, 280, 50), "DOF Max Distance", DOFMaxDistance, DOFMaxDistanceText, out DOFMaxDistance, out DOFMaxDistanceText, 5.0f, 200.0f );
		offsetY += 50;

		AutoFocus = GUI.Toggle (new Rect (offsetX, offsetY, 150, 20), AutoFocus, UiHelp.Content ("Use Auto Focus"));
		offsetY += 30;

		if (GUI.Button (new Rect (offsetX, offsetY, 130, 30), UiHelp.Content ("Reset"))) {
			EnablePostProcess = true; FilmicTonemap = true; AutoFocus = true;
			Exposure = 1.0f; ExposureText = "1";
			BloomThreshold = 0.8f; BloomThresholdText = "0.8"; BloomAmount = 1.0f; BloomAmountText = "1.0";
			LensFlareAmount = 0.5f; LensFlareAmountText = "0.5"; LensDirtAmount = 1.0f; LensDirtAmountText = "1.0";
			VignetteAmount = 0.2f; VignetteAmountText = "0.2";
			DOFMaxBlur = 0.0f; DOFMaxBlurText = "0.0"; DOFFocalDepth = 10.0f; DOFFocalDepthText = "10.0"; DOFMaxDistance = 50.0f; DOFMaxDistanceText = "50.0";
		}
		if (GUI.Button (new Rect (offsetX + 150, offsetY, 130, 30), UiHelp.Content ("Close"))) {
			this.gameObject.SetActive(false);
		}
		
		Tips.Capture (true);
		
		GUI.DragWindow();
		
	}
	
	void OnGUI () {
		Theme.Apply ();
		
		windowRect.width = 300;
		windowRect.height = 580;
		
		windowRect = UiShell.Dock (windowRect, 1);
		
		windowRect = GUI.Window(19, windowRect, DoMyWindow, L.T("Post Process"));
		
		Tips.Block (windowRect);
		
	}
}
