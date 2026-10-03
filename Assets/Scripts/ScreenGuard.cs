using UnityEngine;

/// <summary>
/// Should use temporarily until Unity3d-team fix the bug
/// http://issuetracker.unity3d.com/issues/input-when-resizing-window-input-dot-mouseposition-will-be-clamped-by-the-original-window-size
/// </summary>
public class ScreenGuard : MonoBehaviour {

	private int prevWidth;
	private int prevHeight;
	
	private bool isInited = false;
	
	
	private void Start()
	{
		prevWidth  = Screen.width;
		prevHeight = Screen.height;
		
		isInited = true;
	}
	
	private void SetResolution()
	{
		prevWidth  = Screen.width;
		prevHeight = Screen.height;
		
		Screen.SetResolution(Screen.width, Screen.height, Screen.fullScreen);
	}
	
	// Unity fixed that bug long ago; forcing the resolution back while the user drags the window edge only
	// fights the resize, so this no longer does anything (Materialize CE). Kept so the scene keeps its component.
	public void Update()
	{
	}
}