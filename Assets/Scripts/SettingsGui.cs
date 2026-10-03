using UnityEngine;
using System.Collections;
using System.IO;

using FreeImageAPI;
using System.Runtime.InteropServices;

using System.Xml;
using System.Xml.Serialization;

public class Settings {

	public bool normalMapMaxStyle;
	public bool normalMapMayaStyle;

	public bool postProcessEnabled;

	public PropChannelMap propRed;
	public PropChannelMap propGreen;
	public PropChannelMap propBlue;
	public PropChannelMap propAlpha;

	public FileFormat fileFormat;

	// Materialize CE
	public DdsEncoder ddsEncoder = DdsEncoder.Texconv;
	public DdsFormat ddsFormat = DdsFormat.BC7;
	public bool ddsHighQuality = true;
	public string ddsToolPath = "";
	public string ddsCustomArguments = "\"{input}\" \"{output}\" {format}";
	public string channelPacker = "";
	public bool showTooltips = true;
	public bool classicLook = false;
	public string language = "en";

}


public class SettingsGui : MonoBehaviour {

	public MainGui mainGui;
	public PostProcessGui postProcessGui;

	Rect windowRect = new Rect (Screen.width - 300, Screen.height - 320, 280, 600);
	bool windowOpen = false;
	public Settings settings = new Settings();

	public static SettingsGui instance;

	char pathChar;

	// Use this for initialization
	void Start () {

		instance = this;

		if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) {
			pathChar = '\\';
		} else {
			pathChar = '/';
		}

		LoadSettings ();
	
	}

	string GetPathToFile(){

		string pathToFile = Application.dataPath;
		//string pathToFile = Application.persistentDataPath;

		if (Application.isEditor) {
			pathToFile = pathToFile + "/settings.txt";
		} else {
			pathToFile = pathToFile.Substring (0, pathToFile.Length - 16) + "settings.txt";
		}
		return pathToFile;
	}

	public void LoadSettings(){

		string pathToFile = GetPathToFile();

		Debug.Log (pathToFile);
		//if (File.Exists (pathToFile)) {
			//FileAttributes fileAttributes = File.GetAttributes(pathToFile);
			//if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) {
				//File.SetAttributes (pathToFile, FileAttributes.Normal);
			//}
		//}

		// A damaged settings file used to stop Materialize from starting; defaults are used instead (Materialize CE).
		Settings loaded = null;
		if (System.IO.File.Exists (pathToFile)) {
			try {
				var serializer = new XmlSerializer (typeof(Settings));
				using (var stream = new FileStream (pathToFile, FileMode.Open, FileAccess.Read)) {
					loaded = serializer.Deserialize (stream) as Settings;
				}
			} catch (System.Exception e) {
				Debug.LogWarning ("settings.txt could not be read, using defaults: " + e.Message);
			}
		}
		if (loaded != null) {
			settings = loaded;
		} else {
			settings.normalMapMaxStyle = true;
			settings.normalMapMayaStyle = false;
			settings.postProcessEnabled = true;
			settings.propRed = PropChannelMap.None;
			settings.propGreen = PropChannelMap.None;
			settings.propBlue = PropChannelMap.None;
			settings.fileFormat = FileFormat.png;
			SaveSettings();
		}

		SetSettings ();

	}

	void SaveSettings(){

		string pathToFile = GetPathToFile();
		
		Debug.Log (pathToFile);

		if (File.Exists (pathToFile)) {
			//FileAttributes fileAttributes = File.GetAttributes(pathToFile);
			//if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) {
				File.SetAttributes (pathToFile, FileAttributes.Normal);
			//}
		}

		try {
			var serializer = new XmlSerializer (typeof(Settings));
			using (var stream = new FileStream (pathToFile, FileMode.Create)) {
				serializer.Serialize (stream, settings);
			}
		} catch (System.Exception e) {
			// Materialize in a read-only folder: settings just are not kept.
			Debug.LogWarning ("Could not save settings.txt: " + e.Message);
		}
	}

	/// <summary>Opens or closes the preferences (Settings menu of the new interface).</summary>
	public void Toggle () {
		if (windowOpen) SaveSettings ();
		windowOpen = !windowOpen;
	}

	/// <summary>The channel packer's setup, kept for the next launches (Materialize CE).</summary>
	public void SavePackerSettings () {
		settings.channelPacker = ChannelPacker.Serialize ();
		SaveSettings ();
		Notifications.Info ("Channel packer setup kept as default: " + ChannelPacker.PresetName);
	}

	/// <summary>The maps' file format is kept as soon as it is chosen (Materialize CE).</summary>
	public void SaveFormatSetting () {
		settings.fileFormat = mainGui.selectedFormat;
		SaveSettings ();
	}

	/// <summary>DDS choices are kept as soon as they change (Materialize CE).</summary>
	public void SaveDdsSettings () {
		settings.ddsEncoder = DdsExport.Encoder;
		settings.ddsFormat = DdsExport.Format;
		settings.ddsHighQuality = DdsExport.HighQuality;
		settings.ddsToolPath = DdsExport.ToolPath;
		settings.ddsCustomArguments = DdsExport.CustomArguments;
		SaveSettings ();
	}

	void SetNormalMode(){
		int flipNormalY = 0;
		if (settings.normalMapMayaStyle) {
			flipNormalY = 1;
		}
		
		Shader.SetGlobalInt ("_FlipNormalY", flipNormalY);
	}

	public void SetSettings(){
		SetNormalMode ();

		if( settings.postProcessEnabled ){
			postProcessGui.PostProcessOn();
		}else{
			postProcessGui.PostProcessOff();
		}

		mainGui.propRed = settings.propRed;
		mainGui.propGreen = settings.propGreen;
		mainGui.propBlue = settings.propBlue;
		mainGui.propAlpha = settings.propAlpha;

		mainGui.SetFormat( settings.fileFormat );

		DdsExport.Encoder = settings.ddsEncoder;
		DdsExport.Format = settings.ddsFormat;
		DdsExport.HighQuality = settings.ddsHighQuality;
		DdsExport.ToolPath = settings.ddsToolPath ?? "";
		if (!string.IsNullOrEmpty (settings.ddsCustomArguments)) DdsExport.CustomArguments = settings.ddsCustomArguments;
		ChannelPacker.Deserialize (settings.channelPacker);
		Tips.Enabled = settings.showTooltips;
		// The classic look lacks the new tools: the new interface is always used (Materialize CE).
		Theme.Classic = false;
		L.Language = settings.language;
	}
	
	// Update is called once per frame
	void Update () {

	}

	// ---------- Window (Materialize CE: sections, everything applied and kept at once) ----------

	Vector2 scroll;
	float contentHeight = 700;
	GUIStyle sectionStyle, noteStyle;
	static readonly FileFormat[] defaultFormats = { FileFormat.png, FileFormat.tga, FileFormat.tiff, FileFormat.jpg, FileFormat.bmp };

	float Section (float y, string title) {
		GUI.Label (new Rect (12, y, 360, 20), L.T (title), sectionStyle);
		GUI.Box (new Rect (12, y + 20, 356, 1), GUIContent.none);
		return y + 28;
	}

	void DoMyWindow ( int windowID ) {
		if (sectionStyle == null) {
			sectionStyle = new GUIStyle (GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 };
			sectionStyle.normal.textColor = Theme.Accent;
			noteStyle = new GUIStyle (GUI.skin.label) { wordWrap = true, fontSize = 11 };
			noteStyle.normal.textColor = new Color (0.6f, 0.64f, 0.7f);
		}
		bool changed = false;
		scroll = GUI.BeginScrollView (new Rect (0, 24, windowRect.width, windowRect.height - 64), scroll, new Rect (0, 0, windowRect.width - 20, contentHeight));
		float y = 4;

		// ----- General -----
		y = Section (y, "GENERAL");
		GUI.Label (new Rect (12, y + 2, 100, 20), L.G ("Language", "Language of the interface. Translations are text files in StreamingAssets/Lang: add or correct one without touching the code."));
		var langs = L.Available ();
		int current = Mathf.Max (0, langs.FindIndex (l => l.Code == L.Language));
		var names = new string[langs.Count];
		for (int i = 0; i < names.Length; i++) names[i] = langs[i].Name;
		int chosen = GUI.Toolbar (new Rect (112, y, 256, 24), current, names);
		if (chosen != current) { L.Language = settings.language = langs[chosen].Code; changed = true; }
		y += 32;
		GUI.Label (new Rect (12, y + 2, 100, 20), L.G ("Font", "The interface's font: Open Sans (as in Quixel Mixer) or Segoe UI (Windows'). Applied at once."));
		int fontIndex = Theme.FontChoice == "Open Sans" ? 0 : 1;
		int newFont = GUI.Toolbar (new Rect (112, y, 256, 24), fontIndex, new[] { "Open Sans", "Segoe UI" });
		if (newFont != fontIndex) Theme.FontChoice = newFont == 0 ? "Open Sans" : "Segoe UI";
		y += 32;
		bool tips = GUI.Toggle (new Rect (12, y, 356, 22), settings.showTooltips, L.G (" Show tooltips", "Explanations that appear when the mouse rests on a button or a setting."));
		if (tips != settings.showTooltips) { settings.showTooltips = tips; Tips.Enabled = tips; changed = true; }
		y += 26;
		bool post = GUI.Toggle (new Rect (12, y, 356, 22), settings.postProcessEnabled, L.G (" Post process on at start", "Bloom, tone mapping and vignette of the preview are on when Materialize starts."));
		if (post != settings.postProcessEnabled) { settings.postProcessEnabled = post; changed = true; }
		y += 34;

		// ----- Maps -----
		y = Section (y, "MAPS");
		GUI.Label (new Rect (12, y + 2, 100, 20), L.G ("Normal maps", "The green channel convention of the normal maps Materialize makes and reads. The export profiles convert when an engine needs the other one."));
		int style = GUI.Toolbar (new Rect (112, y, 256, 24), settings.normalMapMayaStyle ? 1 : 0, new[] {
			L.G ("DirectX (Max)", "Green down: Unreal, Skyrim, 3ds Max. Materialize's original default."),
			L.G ("OpenGL (Maya)", "Green up: Unity, Blender, glTF, Maya.") });
		if ((style == 1) != settings.normalMapMayaStyle) {
			settings.normalMapMayaStyle = style == 1;
			settings.normalMapMaxStyle = style == 0;
			SetNormalMode ();
			changed = true;
		}
		y += 32;
		GUI.Label (new Rect (12, y + 2, 100, 20), L.G ("File format", "Format of each map's Save button and of the maps saved with a project."));
		int fmt = System.Array.IndexOf (defaultFormats, mainGui.selectedFormat);
		int newFmt = GUI.Toolbar (new Rect (112, y, 256, 24), fmt, new[] { "PNG", "TGA", "TIFF", "JPG", "BMP" });
		if (newFmt != fmt && newFmt >= 0) { mainGui.SetFormat (defaultFormats[newFmt]); settings.fileFormat = mainGui.selectedFormat; changed = true; }
		y += 30;
		if (GUI.Button (new Rect (12, y, 356, 24), L.G ("Keep the channel packer's setup as default", "The current channel packer setup is used at the next launches."))) {
			settings.channelPacker = ChannelPacker.Serialize ();
			changed = true;
			Notifications.Info (L.T ("Channel packer setup kept as default."));
		}
		y += 36;

		// ----- DDS -----
		y = Section (y, "DDS COMPRESSION");
		GUI.Label (new Rect (12, y + 2, 100, 20), L.G ("Tool", "The program that compresses DDS files. texconv comes with Materialize CE; NVIDIA Texture Tools is found if installed; or any tool you choose."));
		int enc = GUI.Toolbar (new Rect (112, y, 256, 24), (int)DdsExport.Encoder, new[] {
			L.G ("texconv", "Microsoft texconv (DirectXTex), included."),
			L.G ("NVIDIA", "NVIDIA Texture Tools (nvcompress), if installed."),
			L.G ("Custom", "Any command-line tool, with your own arguments.") });
		if (enc != (int)DdsExport.Encoder) { DdsExport.Encoder = (DdsEncoder)enc; DdsExport.ToolPath = ""; changed = true; }
		y += 30;
		GUI.Label (new Rect (12, y, 356, 34), DdsExport.ToolStatus (), noteStyle);
		y += 34;
		if (GUI.Button (new Rect (12, y, 176, 24), L.G ("Choose the tool's .exe…", "Point to the program if it is not found automatically."))) {
			string path = null;
			try { path = NativeFileDialog.Show (L.T ("DDS tool (.exe)"), "*.exe", false); } catch (System.Exception e) { Notifications.Error (e.Message); }
			if (!string.IsNullOrEmpty (path)) { DdsExport.ToolPath = path; changed = true; }
		}
		if (DdsExport.ToolPath.Length > 0 && GUI.Button (new Rect (196, y, 172, 24), L.G ("Find it automatically", "Forget the chosen .exe and look for the tool again."))) { DdsExport.ToolPath = ""; changed = true; }
		y += 30;
		bool high = GUI.Toggle (new Rect (12, y, 356, 22), DdsExport.HighQuality, L.G (" Best quality (slower)", "Slower but finer compression."));
		if (high != DdsExport.HighQuality) { DdsExport.HighQuality = high; changed = true; }
		y += 26;
		if (DdsExport.Encoder == DdsEncoder.Custom) {
			GUI.Label (new Rect (12, y, 356, 20), L.G ("Arguments", "{input} = the PNG to compress, {output} = the DDS to write, {format} = BC7, BC1… as chosen in the export."));
			y += 20;
			string args = GUI.TextField (new Rect (12, y, 356, 22), DdsExport.CustomArguments);
			if (args != DdsExport.CustomArguments) { DdsExport.CustomArguments = args; changed = true; }
			y += 30;
		}
		y += 6;

		// ----- Folders -----
		y = Section (y, "YOUR MATERIALIZE CE FOLDER");
		GUI.Label (new Rect (12, y, 356, 34), L.T ("Projects, saved textures, export profiles, render presets, HDRIs and add-ons go in:"), noteStyle);
		y += 32;
		GUI.Label (new Rect (12, y, 356, 22), Workspace.Root);
		y += 26;
		if (GUI.Button (new Rect (12, y, 116, 24), L.G ("Open", "Opens it in the Explorer."))) {
			Workspace.Dir (Workspace.Projects);
			Application.OpenURL ("file:///" + Workspace.Root.Replace ("\\", "/"));
		}
		if (GUI.Button (new Rect (134, y, 116, 24), L.G ("Change…", "Another folder. Your profiles and presets are copied there; projects and textures stay where they are."))) {
			string picked = Workspace.PickFolder (L.T ("Materialize CE folder"));
			if (!string.IsNullOrEmpty (picked)) Workspace.Set (picked);
		}
		if (GUI.Button (new Rect (256, y, 112, 24), L.G ("Log", "Opens the folder of Materialize's log (Player.log), useful when reporting a problem.")))
			Application.OpenURL ("file:///" + Application.persistentDataPath.Replace ("\\", "/"));
		y += 34;

		contentHeight = y + 10;
		GUI.EndScrollView ();

		if (changed) SaveAll ();
		if (GUI.Button (new Rect (windowRect.width - 100, windowRect.height - 36, 88, 28), L.T ("Close"))) windowOpen = false;
		Tips.Capture (true);
		GUI.DragWindow ();
	}

	/// <summary>Everything the window shows, kept at once.</summary>
	void SaveAll () {
		settings.ddsEncoder = DdsExport.Encoder;
		settings.ddsFormat = DdsExport.Format;
		settings.ddsHighQuality = DdsExport.HighQuality;
		settings.ddsToolPath = DdsExport.ToolPath;
		settings.ddsCustomArguments = DdsExport.CustomArguments;
		SaveSettings ();
	}

	void OnGUI () {
		Theme.Apply ();

		float h = Mathf.Min (640, Screen.height - UiShell.Top - 24);
		windowRect = UiShell.Active ? new Rect (Screen.width - 392, UiShell.Top + 8, 384, h) : new Rect (Screen.width - 400, Screen.height - h - 60, 384, h);

		if (windowOpen){
			windowRect = GUI.Window (20, windowRect, DoMyWindow, L.T("Preferences"));
			Tips.Block (windowRect);
		}

		if (!UiShell.Active && GUI.Button (new Rect(Screen.width - 280, Screen.height - 40, 80, 30), L.T("Settings"))) {
			if( windowOpen == true){
				SaveSettings();
				windowOpen = false;
			}else{
				windowOpen = true;
			}
		}

	}
}
