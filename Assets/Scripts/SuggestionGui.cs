using UnityEngine;
using System.Collections;

public class SuggestionGui : MonoBehaviour {
	
	public GameObject MainGuiObject;
	MainGui MainGuiScript;

	// The original login and suggestion server are gone; the object stays so the scene keeps its links.
	public GameObject AuthenticateObject;
	
	string SuggestionText = "";
	string stringEmail = "";

	enum SuggestionState {
		Write,
		Sending,
		Failed,
		Sent
	}

	SuggestionState suggestionState = SuggestionState.Write;

	bool suggestionSent = false;
	bool sendingSuggestion = false;
	
	Rect windowRect = new Rect (30, 300, 300, 450);
	
	// Use this for initialization
	void Start () {

		windowRect.position = new Vector2( Screen.width - 310, 50 );

	}
	
	// Update is called once per frame
	void Update () {
		
	}
	
	IEnumerator SendSuggestion () {

		suggestionState = SuggestionState.Sending;

		// Create a Web Form
		WWWForm form = new WWWForm();
		form.AddField("email", stringEmail);
		form.AddField("suggestion", SuggestionText);

		//WWW www = new WWW("http://boundingboxsoftware.com/materialize/processSuggestion.php", form);
		WWW www = new WWW("http://squirrelyjones.com/boundingbox/materialize/processSuggestion.php", form);
		yield return www;
		string returnText = www.text;
		Debug.Log ( www.text );

		if (returnText.Contains ("success")) {
			suggestionState = SuggestionState.Sent;
			SuggestionText = "";
		} else {
			suggestionState = SuggestionState.Failed;
		}

		yield return new WaitForSeconds(0.01f);
	}
	
	void DoMyWindow ( int windowID ) {
		
		int offsetX = 10;
		int offsetY = 30;

		if (suggestionState == SuggestionState.Write) {
			GUI.Label (new Rect (offsetX, offsetY, 250, 30), L.T("Got a suggestion?  Send it to us!"));
			offsetY += 30;
			SuggestionText = GUI.TextArea (new Rect (offsetX, offsetY, 280, 170), SuggestionText);
			offsetY += 180;
			if (GUI.Button (new Rect (offsetX + 150, offsetY, 130, 30), L.T("Send"))) {
				StartCoroutine (SendSuggestion ());
			}
			if (GUI.Button (new Rect (offsetX, offsetY, 130, 30), L.T("Close"))) {
				this.gameObject.SetActive (false);
			}
		} else if (suggestionState == SuggestionState.Sending) {
			GUI.Label (new Rect (offsetX, offsetY, 250, 30), L.T("Sending...."));
			offsetY += 30;
			if (GUI.Button (new Rect (offsetX, offsetY, 130, 30), L.T("Close"))) {
				suggestionState = SuggestionState.Write;
				this.gameObject.SetActive (false);
			}
		} else if (suggestionState == SuggestionState.Failed) {
			GUI.Label (new Rect (offsetX, offsetY, 250, 30), L.T("Something went wrong!"));
			offsetY += 30;
			if (GUI.Button (new Rect (offsetX, offsetY, 130, 30), L.T("Close"))) {
				suggestionState = SuggestionState.Write;
				this.gameObject.SetActive (false);
			}
			if (GUI.Button (new Rect (offsetX + 150, offsetY, 130, 30), L.T("Try Again"))) {
				suggestionState = SuggestionState.Write;
			}
		} else if (suggestionState == SuggestionState.Sent){
			GUI.Label (new Rect (offsetX, offsetY, 250, 30), L.T("Thanks!  I'll get right on that!"));
			offsetY += 30;
			if (GUI.Button (new Rect (offsetX, offsetY, 130, 30), L.T("Close"))) {
				suggestionState = SuggestionState.Write;
				this.gameObject.SetActive (false);
			}
		}
		
		GUI.DragWindow();
		
	}
	
	void OnGUI () {
		Theme.Apply ();
		
		windowRect.width = 300;
		if (suggestionState == SuggestionState.Write) {
			windowRect.height = 280;
		} else {
			windowRect.height = 100;
		}
		
		windowRect = GUI.Window(50, windowRect, DoMyWindow, L.T("Make A Suggestion!"));
		
	}
	
}
