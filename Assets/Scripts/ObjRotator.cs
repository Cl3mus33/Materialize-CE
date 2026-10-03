using UnityEngine;
using System.Collections;

public class ObjRotator : MonoBehaviour {

	Vector2 mousePos;
	Vector2 lastMousePos;
	Vector3 rotation;
	Vector3 lerpRotation;
	Vector3 startRotation;
	int lockedAxis;        // the light's drag: 0 not decided, 1 around, 2 height
	Vector2 dragTravel;

	int mouseDownCount = 0;

	public int MouseButton = 0;

	public bool AllowX = true;
	public bool InvertX = false;

	public bool AllowY = true;
	public bool InvertY = false;

	public bool holdKey = false;
	public bool noHoldKey = false;
	public KeyCode keyToHold;

	// Use this for initialization
	void Start () {

		mousePos = Input.mousePosition;
		lastMousePos = mousePos;
		
		rotation = this.transform.eulerAngles;
		lerpRotation = rotation;
		startRotation = rotation;
	
	}

	/// <summary>Turns the model smoothly to a given angle (the View menu's presets). x tilts the top away.</summary>
	public static void SetModelView (Vector3 euler) {
		foreach (var r in FindObjectsByType<ObjRotator> (FindObjectsSortMode.None))
			if (!r.holdKey) r.rotation = euler;
	}

	/// <summary>Turns the light (smoothly) so that it shines along <paramref name="forward"/> (the HDRI's sun).</summary>
	public static void SetLightDirection (Vector3 forward) {
		foreach (var r in FindObjectsByType<ObjRotator> (FindObjectsSortMode.None)) {
			if (!r.holdKey || r.keyToHold != KeyCode.L) continue;
			var light = r.GetComponentInChildren<Light> ();
			var local = light != null ? Quaternion.Inverse (r.transform.rotation) * light.transform.rotation : Quaternion.identity;
			var e = (Quaternion.LookRotation (forward) * Quaternion.Inverse (local)).eulerAngles;
			float x = Mathf.DeltaAngle (0, e.x);
			float y = r.rotation.y + Mathf.DeltaAngle (r.rotation.y, e.y);   // the short way round
			r.rotation = new Vector3 (Mathf.Clamp (x, -80, 80), y, r.rotation.z);
		}
	}

	/// <summary>The light back to where it was at launch (Materialize's original light).</summary>
	public static void ResetLight () {
		foreach (var r in FindObjectsByType<ObjRotator> (FindObjectsSortMode.None))
			if (r.holdKey && r.keyToHold == KeyCode.L)
				r.rotation = new Vector3 (r.startRotation.x, r.rotation.y + Mathf.DeltaAngle (r.rotation.y, r.startRotation.y), r.startRotation.z);
	}

	/// <summary>The model back to its starting angle (Mixer's Shift + F).</summary>
	public static void ResetModels () {
		foreach (var r in FindObjectsByType<ObjRotator> (FindObjectsSortMode.None))
			if (!r.holdKey) r.Reset ();
	}

	public void Reset(){
		rotation = new Vector3(0,0,0);
		lerpRotation = rotation;
		this.transform.eulerAngles = lerpRotation;
	}
	
	// Update is called once per frame
	void Update() {
		
		mousePos = Input.mousePosition;
		
		Vector2 mouseOffset = mousePos - lastMousePos;
		
		// Materialize CE, as in Quixel Mixer: Shift + right drag turns the light, Alt + left drag turns the model,
		// Alt + right drag zooms (CameraPanZoom). Right drag alone still turns the model, as in Materialize.
		bool shift = Input.GetKey (KeyCode.LeftShift) || Input.GetKey (KeyCode.RightShift);
		bool alt = Input.GetKey (KeyCode.LeftAlt) || Input.GetKey (KeyCode.RightAlt);
		bool isLight = holdKey && keyToHold == KeyCode.L;
		bool pressed;
		if (isLight) pressed = (Input.GetMouseButton (MouseButton) && Input.GetKey (keyToHold)) || (Input.GetMouseButton (1) && shift);
		else if (!holdKey && MouseButton == 1) pressed = (Input.GetMouseButton (1) && !shift && !alt) || (Input.GetMouseButton (0) && alt && GUIUtility.hotControl == 0);
		else pressed = Input.GetMouseButton (MouseButton) && (!holdKey || Input.GetKey (keyToHold));

		if (pressed) {
			mouseDownCount ++;
		} else {
			mouseDownCount = 0;
			lockedAxis = 0; dragTravel = Vector2.zero;
		}

		// Materialize CE: the light moves on one axis per drag, the one the drag starts along (sideways: around the
		// material, up/down: its height). Both at once made it hard to place.
		if (isLight && mouseDownCount > 1) {
			if (lockedAxis == 0) {
				dragTravel += mouseOffset;
				if (dragTravel.magnitude > 6f) lockedAxis = Mathf.Abs (dragTravel.x) >= Mathf.Abs (dragTravel.y) ? 1 : 2;
				mouseOffset = Vector2.zero;   // nothing moves until the direction is known
			}
			else if (lockedAxis == 1) mouseOffset.y = 0;
			else mouseOffset.x = 0;
		}

		// skip the first frame because we could just be regaining focus

		{

			if (mouseDownCount > 1) {
				if (AllowX) {
					if (InvertX) {
						rotation -= new Vector3 (0, 1, 0) * mouseOffset.x * 0.3f;
					} else {
						rotation += new Vector3 (0, 1, 0) * mouseOffset.x * 0.3f;
					}
				}
				if (AllowY) {
					if (InvertY) {
						rotation -= new Vector3 (1, 0, 0) * mouseOffset.y * 0.3f;
					} else {
						rotation += new Vector3 (1, 0, 0) * mouseOffset.y * 0.3f;
					}
				}
				rotation.x = Mathf.Clamp (rotation.x, -80, 80);
			}

		}
		
		lerpRotation = lerpRotation * 0.95f + rotation * 0.05f;
		this.transform.eulerAngles = lerpRotation;
		
		lastMousePos = mousePos;
		
	}
}
