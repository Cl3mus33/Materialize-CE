using UnityEngine;
using System.Collections;
using System.IO;

using FreeImageAPI;
using System.Runtime.InteropServices;

using System.Xml;
using System.Xml.Serialization;

using System;
using System.Diagnostics;

public enum MapType {
	height,
	diffuse,
	diffuseOriginal,
	metallic,
	smoothness,
	normal,
	edge,
	ao,
	property,
	blank,
	// Materialize CE (added at the end: the numbers of the others are stored in command lists)
	emission,
	subsurface
}

public enum FileFormat {
	bmp,
	jpg,
	png,
	tga,
	tiff,
	dds
}

public class ProjectObject {
	
	public HeightFromDiffuseSettings HFDS;
	public string heightMapPath;

	public EditDiffuseSettings EDS;
	public string diffuseMapPath;
	public string diffuseMapOriginalPath;

	public NormalFromHeightSettings NFHS;
	public string normalMapPath;

	public MetallicSettings MS;
	public string metallicMapPath;

	public SmoothnessSettings SS;
	public string smoothnessMapPath;

	public EdgeSettings ES;
	public string edgeMapPath;

	public AOSettings AOS;
	public string aoMapPath;

	// Materialize CE: maps without a creation tool (null in older projects).
	public string emissionMapPath;
	public string subsurfaceMapPath;

	public MaterialSettings MatS;
	
}

public class SaveLoadProject : MonoBehaviour {

	public MainGui mainGui;
	public HeightFromDiffuseGui heightFromDiffuseGui;
	public EditDiffuseGui editDiffuseGui;
	public NormalFromHeightGui normalFromHeightGui;
	public MetallicGui metallicGui;
	public SmoothnessGui SmoothnessGui;
	public EdgeFromNormalGui edgeFromNormalGui;
	public AOFromNormalGui aoFromNormalGui;
	public MaterialGui materailGui;

	ProjectObject thisProject;

	char pathChar;

	public bool busy = false;

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


	
	// Use this for initialization
	void Start () {

		if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) {
			pathChar = '\\';
		} else {
			pathChar = '/';
		}

		thisProject = new ProjectObject ();
	
	}
	
	// Update is called once per frame
	void Update () {
	
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

	public void LoadProject ( string pathToFile ) {
		UnityEngine.Debug.Log ("Loading Project: " + pathToFile);

		// A damaged or foreign file used to throw here and leave Materialize half-reset (Materialize CE).
		ProjectObject loaded = null;
		try {
			var serializer = new XmlSerializer(typeof(ProjectObject));
			using (var stream = new FileStream(pathToFile, FileMode.Open, FileAccess.Read)) {
				loaded = serializer.Deserialize(stream) as ProjectObject;
			}
		} catch (Exception e) {
			Notifications.Error ("Could not open the project " + Path.GetFileName (pathToFile) + ": " + (e.InnerException ?? e).Message);
			return;
		}
		if (loaded == null) {
			Notifications.Error ("Not a Materialize project: " + Path.GetFileName (pathToFile));
			return;
		}
		thisProject = loaded;

		heightFromDiffuseGui.SetValues(thisProject);
		editDiffuseGui.SetValues(thisProject);
		normalFromHeightGui.SetValues (thisProject);
		metallicGui.SetValues (thisProject);
		SmoothnessGui.SetValues (thisProject);
		edgeFromNormalGui.SetValues (thisProject);
		aoFromNormalGui.SetValues (thisProject);
		materailGui.SetValues (thisProject);

		mainGui.ClearAllTextures ();

		StartCoroutine ( LoadAllTextures( pathToFile ) );
	}

	public void SaveProject ( string pathToFile,  FileFormat selectedFormat ) {
		UnityEngine.Debug.Log ("Saving Project: " + pathToFile);

		// A project's maps must open again: FreeImage cannot read BC7, so DDS projects keep PNG maps (Materialize CE).
		if (selectedFormat == FileFormat.dds) {
			selectedFormat = FileFormat.png;
			Notifications.Info ("Project maps are saved as PNG: DDS is for exporting.");
		}
		string extension = SwitchFormats(selectedFormat);

		if ( pathToFile.Contains (".") ) {
			pathToFile = pathToFile.Substring (0, pathToFile.LastIndexOf ("."));
		}

		int fileIndex = pathToFile.LastIndexOf (pathChar);
		string projectName = pathToFile.Substring (fileIndex+1, pathToFile.Length-fileIndex-1);

		heightFromDiffuseGui.GetValues(thisProject);
		if (mainGui._HeightMap != null) {
			thisProject.heightMapPath = projectName + "_height." + extension;
		} else {
			thisProject.heightMapPath = "null";
		}

		editDiffuseGui.GetValues(thisProject);
		if (mainGui._DiffuseMap != null) {
			thisProject.diffuseMapPath = projectName + "_diffuse." + extension;
		} else {
			thisProject.diffuseMapPath = "null";
		}

		if (mainGui._DiffuseMapOriginal != null) {
			thisProject.diffuseMapOriginalPath = projectName + "_diffuseOriginal." + extension;
		} else {
			thisProject.diffuseMapOriginalPath = "null";
		}

		normalFromHeightGui.GetValues(thisProject);
		if (mainGui._NormalMap != null) {
			thisProject.normalMapPath = projectName + "_normal." + extension;
		} else {
			thisProject.normalMapPath = "null";
		}

		metallicGui.GetValues(thisProject);
		if (mainGui._MetallicMap != null) {
			thisProject.metallicMapPath = projectName + "_metallic." + extension;
		} else {
			thisProject.metallicMapPath = "null";
		}

		SmoothnessGui.GetValues(thisProject);
		if (mainGui._SmoothnessMap != null) {
			thisProject.smoothnessMapPath = projectName + "_smoothness." + extension;
		} else {
			thisProject.smoothnessMapPath = "null";
		}

		edgeFromNormalGui.GetValues(thisProject);
		if (mainGui._EdgeMap != null) {
			thisProject.edgeMapPath = projectName + "_edge." + extension;
		} else {
			thisProject.edgeMapPath = "null";
		}

		aoFromNormalGui.GetValues(thisProject);
		if (mainGui._AOMap != null) {
			thisProject.aoMapPath = projectName + "_ao." + extension;
		} else {
			thisProject.aoMapPath = "null";
		}

		thisProject.emissionMapPath = mainGui._EmissionMap != null ? projectName + "_emission." + extension : "null";
		thisProject.subsurfaceMapPath = mainGui._SubsurfaceMap != null ? projectName + "_subsurface." + extension : "null";

		materailGui.GetValues (thisProject);

		try {
			var serializer = new XmlSerializer(typeof(ProjectObject));
			using (var stream = new FileStream(pathToFile + ".mtz", FileMode.Create)) {
				serializer.Serialize(stream, thisProject);
			}
		} catch (Exception e) {
			Notifications.Error ("Could not save the project: " + e.Message);
			return;
		}

		SaveAllFiles (pathToFile, selectedFormat);
	}

	public void SaveAllFiles (string pathToFile, FileFormat selectedFormat) {
		//int fileIndex = pathToFile.LastIndexOf (pathChar);
		//UnityEngine.Debug.Log = "You're saving all files: " + pathToFile.Substring (fileIndex+1, pathToFile.Length-fileIndex-1);
		string extension = SwitchFormats(selectedFormat);
		if (pathToFile.Contains (".")) {
			pathToFile = pathToFile.Substring (0, pathToFile.LastIndexOf ("."));
		}
		mainGui.StartCoroutine ( SaveAllTextures( extension, pathToFile ) );
	}

	public void SaveFile ( string pathToFile, FileFormat selectedFormat, Texture2D textureToSave, string mapType ) {
		//int fileIndex = pathToFile.LastIndexOf (pathChar);
		//UnityEngine.Debug.Log = "You're saving file: " + pathToFile.Substring (fileIndex+1, pathToFile.Length-fileIndex-1);
		if (pathToFile.Contains (".")) {
			pathToFile = pathToFile.Substring (0, pathToFile.LastIndexOf ("."));
		}
		string extension = SwitchFormats(selectedFormat);
		StartCoroutine ( SaveTexture( extension, textureToSave, pathToFile + mapType ) );
	}

	public void PasteFile( MapType mapTypeToLoad ){

		string tempImagePath = Path.Combine (Application.temporaryCachePath, "paste.png");
		UnityEngine.Debug.Log (tempImagePath);

		try{
			Process myProcess = new Process ();
			myProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			myProcess.StartInfo.CreateNoWindow = true;
			myProcess.StartInfo.UseShellExecute = false;
			myProcess.StartInfo.FileName = Application.streamingAssetsPath.Replace ("/", "\\") + "\\c2i.exe";
			myProcess.StartInfo.Arguments = "\"" + tempImagePath.Replace ("/", "\\") + "\"";
			myProcess.EnableRaisingEvents = true;
			myProcess.Start();
			myProcess.WaitForExit ();

			StartCoroutine ( LoadTexture (mapTypeToLoad, tempImagePath) );
		} catch (Exception e ){
			UnityEngine.Debug.Log (e);
		}

	}

	public void CopyFile( Texture2D textureToSave ){

		// Written right now (the clipboard helper reads it immediately), in Windows' temp folder: the
		// Materialize folder may be read-only (Materialize CE).
		string tempImage = Path.Combine (Application.temporaryCachePath, "clipboard");
		string error = FastImageSaver.Write (FastImageSaver.Prepare (textureToSave, tempImage, "png"));
		if (error != null) {
			Notifications.Error (error);
			return;
		}

		try{
			Process myProcess = new Process ();
			myProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			myProcess.StartInfo.CreateNoWindow = true;
			myProcess.StartInfo.UseShellExecute = false;
			myProcess.StartInfo.FileName = Application.streamingAssetsPath.Replace ("/", "\\") + "\\i2c.exe";
			myProcess.StartInfo.Arguments = "\"" + (tempImage + ".png").Replace ("/", "\\") + "\"";
			myProcess.EnableRaisingEvents = true;
			myProcess.Start();
			myProcess.WaitForExit ();
		} catch (Exception e ){
			UnityEngine.Debug.Log (e);
		}
	}

	//==============================================//
	//			Texture Saving Coroutines			//
	//==============================================//


	// Materialize CE: each map is saved after the previous one finishes (no shared "busy" flag, which loading
	// also uses), and the user is told what was written.
	public IEnumerator SaveAllTextures( string extension, string pathToFile ) {

		var maps = new System.Collections.Generic.List<(Texture2D Texture, string Suffix)> {
			(mainGui._HeightMap, "_height"), (mainGui._DiffuseMap, "_diffuse"), (mainGui._DiffuseMapOriginal, "_diffuseOriginal"),
			(mainGui._NormalMap, "_normal"), (mainGui._MetallicMap, "_metallic"), (mainGui._SmoothnessMap, "_smoothness"),
			(mainGui._EdgeMap, "_edge"), (mainGui._AOMap, "_ao"),
			(mainGui._EmissionMap, "_emission"), (mainGui._SubsurfaceMap, "_subsurface"),
		};
		int count = 0;
		foreach (var m in maps) if (m.Texture != null) count++;
		Notifications.Info ("Saving the project: " + count + " maps…");
		UnityEngine.Debug.Log ("Saving " + count + " project maps to " + pathToFile + "_*." + extension);

		int written = 0;
		var failed = new System.Collections.Generic.List<string> ();
		foreach (var m in maps) {
			if (m.Texture == null) continue;
			string path = pathToFile + m.Suffix + "." + extension;
			yield return mainGui.StartCoroutine (SaveTexture (extension, m.Texture, pathToFile + m.Suffix));
			if (File.Exists (path)) written++;
			else failed.Add (Path.GetFileName (path));
		}
		if (failed.Count == 0) {
			Notifications.Info ("Project saved: " + written + " maps next to " + Path.GetFileName (pathToFile) + ".mtz");
		} else {
			Notifications.Error ("Project saved, but these maps could not be written: " + string.Join (", ", failed.ToArray ()));
		}
	}

	public IEnumerator SaveTexture( string extension, Texture2D textureToSave, string pathToFile ) {
		busy = true;

		if (textureToSave != null) {
			// Materialize CE: pixels read here, encoding and writing on a worker thread (see FastImageSaver).
			FastImageSaver.Job job = null;
			string error = null;
			try {
				job = FastImageSaver.Prepare (textureToSave, pathToFile, extension);
				// The height keeps its full precision in 16-bit PNG / TIFF when a high-precision version exists.
				string ext = extension.ToLowerInvariant ();
				if (textureToSave == mainGui._HeightMap && mainGui._HDHeightMap != null && (ext == "png" || ext == "tiff")
					&& mainGui._HDHeightMap.width == textureToSave.width && mainGui._HDHeightMap.height == textureToSave.height) {
					job.Grey = FastImageSaver.ReadHeight (mainGui._HDHeightMap);
				}
			} catch (Exception e) {
				error = "Could not read the map to save it: " + e.Message;
			}
			if (job != null) {
				var writing = System.Threading.Tasks.Task.Run (() => FastImageSaver.Write (job));
				while (!writing.IsCompleted) {
					yield return null;
				}
				error = writing.Result;
			}
			if (error != null) {
				UnityEngine.Debug.LogWarning (error);
				Notifications.Error (error);
			} else {
				Notifications.Info ("Saved " + job.Path);
			}
		}
		yield return new WaitForSeconds (0.01f);
		busy = false;

	}

	//==============================================//
	//			Texture Loading Coroutines			//
	//==============================================//

	public IEnumerator LoadAllTextures( string pathToFile ) {
		pathToFile = pathToFile.Substring (0, pathToFile.LastIndexOf (pathChar));
		pathToFile += pathChar;

		if (thisProject.heightMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.height, pathToFile + thisProject.heightMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (thisProject.diffuseMapOriginalPath != "null") {
			StartCoroutine (LoadTexture (MapType.diffuseOriginal, pathToFile + thisProject.diffuseMapOriginalPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (thisProject.diffuseMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.diffuse, pathToFile + thisProject.diffuseMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (thisProject.normalMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.normal, pathToFile + thisProject.normalMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (thisProject.metallicMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.metallic, pathToFile + thisProject.metallicMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (thisProject.smoothnessMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.smoothness, pathToFile + thisProject.smoothnessMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (thisProject.edgeMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.edge, pathToFile + thisProject.edgeMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (thisProject.aoMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.ao, pathToFile + thisProject.aoMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (!string.IsNullOrEmpty (thisProject.emissionMapPath) && thisProject.emissionMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.emission, pathToFile + thisProject.emissionMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		if (!string.IsNullOrEmpty (thisProject.subsurfaceMapPath) && thisProject.subsurfaceMapPath != "null") {
			StartCoroutine (LoadTexture (MapType.subsurface, pathToFile + thisProject.subsurfaceMapPath));
		}
		while( busy ){ yield return new WaitForSeconds( 0.01f ); }

		yield return new WaitForSeconds( 0.01f );
	}

	public IEnumerator LoadTexture( MapType textureToLoad, string pathToFile ) {
		busy = true;

		// Decoded on a worker thread; the interface keeps running meanwhile (Materialize CE).
		UnityEngine.Debug.Log ("Loading Image: " + pathToFile );
		var decoding = System.Threading.Tasks.Task.Run (() => FastImageLoader.Decode (pathToFile));
		while (!decoding.IsCompleted) {
			yield return null;
		}
		FastImageLoader.Pixels pixels = decoding.Result;
		if (pixels.Error != null) {
			UnityEngine.Debug.LogWarning (pixels.Error);
			Notifications.Error (pixels.Error);
		}

		if (pixels.Error == null) {
			// A roughness file opened in the Roughness row: Materialize keeps smoothness, its inverse (Materialize CE).
			if (textureToLoad == MapType.smoothness && Workflow.InvertNextSmoothnessLoad) {
				for (int i = 0; i < pixels.Bgra.Length; i += 4) {
					pixels.Bgra[i] = (byte)(255 - pixels.Bgra[i]);
					pixels.Bgra[i + 1] = (byte)(255 - pixels.Bgra[i + 1]);
					pixels.Bgra[i + 2] = (byte)(255 - pixels.Bgra[i + 2]);
				}
			}
			Workflow.InvertNextSmoothnessLoad = false;
			Texture2D newTexture = FastImageLoader.ToTexture (pixels);

			switch( textureToLoad ){
			case MapType.height:
				mainGui._HeightMap = newTexture;
				// 16-bit / EXR heights: the tools read this full-precision copy (Materialize CE).
				if (mainGui._HDHeightMap != null) {
					mainGui._HDHeightMap.Release ();
					mainGui._HDHeightMap = null;
				}
				mainGui._HDHeightMap = FastImageLoader.ToHeightTexture (pixels);
				if (mainGui._HDHeightMap != null) {
					Notifications.Info ("High-precision height loaded: normals and AO will use its full precision.");
				}
				break;
			case MapType.diffuse:
				mainGui._DiffuseMap = newTexture;
				break;
			case MapType.diffuseOriginal:
				mainGui._DiffuseMapOriginal = newTexture;
				break;
			case MapType.normal:
				mainGui._NormalMap = newTexture;
				break;
			case MapType.metallic:
				mainGui._MetallicMap = newTexture;
				break;
			case MapType.smoothness:
				mainGui._SmoothnessMap = newTexture;
				break;
			case MapType.edge:
				mainGui._EdgeMap = newTexture;
				break;
			case MapType.ao:
				mainGui._AOMap = newTexture;
				break;
			case MapType.emission:
				mainGui._EmissionMap = newTexture;
				break;
			case MapType.subsurface:
				mainGui._SubsurfaceMap = newTexture;
				break;
			default:
				break;
			}

			mainGui.SetLoadedTexture (textureToLoad);

			Resources.UnloadUnusedAssets();

		}

		yield return new WaitForSeconds (0.01f);

		busy = false;

	}

}
