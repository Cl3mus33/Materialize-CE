using UnityEngine;
using System.Collections;

public class ControlsGui : MonoBehaviour {

	static ControlsGui self;
	void Awake () { self = this; }
	/// <summary>Opens or closes the controls panel (Help menu of the new interface).</summary>
	public static void Toggle () {
		if (self == null) self = FindAnyObjectByType<ControlsGui> (FindObjectsInactive.Include);
		if (self != null) self.windowOpen = !self.windowOpen;
	}


	Rect windowRect = new Rect (Screen.width - 520, Screen.height - 320, 300, 600);
	bool windowOpen = false;
	public Settings settings = new Settings();

	public static SettingsGui instance;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	static readonly string[,] rows = {
		{ "#", "MOUSE" },
		{ "Rotate Model", "Right Mouse Button (or Alt + Left)" },
		{ "Move Model", "Middle Mouse Button" },
		{ "Zoom In/Out", "Mouse Scroll Wheel (or Alt + Right, up / down)" },
		{ "Focus / reset the angle", "F / Shift + F" },
		{ "Rotate Light", "Shift + Right Button (or Middle + L)" },
		{ "Rotate Background", "Middle Mouse Button + B" },
		{ "#", "KEYBOARD" },
		{ "Hide / show the panels", "Space" },
		{ "Quick export", "Ctrl + Shift + E" },
		{ "Export details (channels)", "Ctrl + Shift + C" },
		{ "Material & lighting panel", "Ctrl + Shift + M" },
		{ "New project", "Ctrl + N" },
		{ "Open project", "Ctrl + O" },
		{ "Save project", "Ctrl + S" },
		{ "Save project as", "Ctrl + Shift + S" },
		{ "Preferences", "Ctrl + K" },
		{ "Full screen", "F11" },
		{ "#", "VIEWS" },
		{ "Next preview shape", "M" },
		{ "Displacement on / off", "D" },
		{ "Grid at level 0", "G" },
		{ "Tiling preview (3 × 3)", "T" },
		{ "Previous / next environment", "Arrow Up / Down" },
		{ "Full material", "1" },
		{ "Albedo", "2" },
		{ "Metalness / specular", "3" },
		{ "Roughness / glossiness", "4" },
		{ "Normal", "5" },
		{ "Displacement (height)", "6" },
		{ "Ambient occlusion", "7" },
		{ "Curvature", "8" },
	};
	GUIStyle keyStyle, headStyle, nameStyle;

	void DoMyWindow ( int windowID ) {
		if (keyStyle == null) {
			nameStyle = new GUIStyle (GUI.skin.label) { fontSize = 12, padding = new RectOffset (0, 0, 0, 0), wordWrap = false };
			nameStyle.normal.textColor = new Color (0.62f, 0.66f, 0.72f);
			keyStyle = new GUIStyle (nameStyle);
			keyStyle.normal.textColor = new Color (0.92f, 0.94f, 0.97f);
			headStyle = new GUIStyle (nameStyle) { fontStyle = FontStyle.Bold, fontSize = 11 };
			headStyle.normal.textColor = Theme.Accent;
		}
		float y = 30;
		for (int i = 0; i < rows.GetLength (0); i++) {
			if (rows[i, 0] == "#") {
				y += i == 0 ? 0 : 8;
				GUI.Label (new Rect (14, y, 380, 18), L.T (rows[i, 1]), headStyle);
				y += 20;
				continue;
			}
			GUI.Label (new Rect (14, y, 210, 18), L.T (rows[i, 0]), nameStyle);
			GUI.Label (new Rect (226, y, 180, 18), L.T (rows[i, 1]), keyStyle);
			y += 19;
		}
		y += 10;
		if (GUI.Button (new Rect (windowRect.width - 110, y, 96, 28), L.T("Close"))) {
			windowOpen = false;
		}
		contentHeight = y + 40;
		GUI.DragWindow ();
	}

	float contentHeight = 620;

	void OnGUI () {
		Theme.Apply ();

		windowRect = new Rect (Screen.width / 2 - 210, UiShell.Top + 20, 420, contentHeight);

		if (windowOpen){
			windowRect = GUI.Window (22, windowRect, DoMyWindow, L.T("Controls"));
			Tips.Block (windowRect);
		}

		if (!UiShell.Active && GUI.Button (new Rect(Screen.width - 370, Screen.height - 40, 80, 30), L.T("Controls"))) {
			if( windowOpen == true){
				windowOpen = false;
			}else{
				windowOpen = true;
			}
		}

	}
}
