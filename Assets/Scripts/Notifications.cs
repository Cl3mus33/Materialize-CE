using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Short messages at the bottom of the window (Materialize CE): saves done, files that could not be read,
/// unexpected errors. The original only wrote to a hidden log, so failures looked like nothing happened.
/// Errors still go to the log too (Player.log, path shown in the message).
/// </summary>
public class Notifications : MonoBehaviour
{
    struct Message { public string Text; public bool Error; public float Until; }

    static readonly List<Message> messages = new List<Message>();
    static Notifications instance;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("Notifications");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Notifications>();
        Application.logMessageReceived += OnLog;
    }

    public static void Info(string text) => Add(text, false, 4f);
    public static void Error(string text) => Add(text, true, 10f);

    static void Add(string text, bool error, float seconds)
    {
        lock (messages)
        {
            // The same message again (an error repeated every frame) is shown once, with its count.
            int same = messages.FindIndex(m => m.Text == text || m.Text.StartsWith(text + " (×"));
            int repeats = 1;
            if (same >= 0)
            {
                string old = messages[same].Text;
                int mark = old.LastIndexOf(" (×");
                if (mark >= 0) int.TryParse(old.Substring(mark + 3).TrimEnd(')'), out repeats);
                repeats++;
                messages.RemoveAt(same);
            }
            messages.Add(new Message { Text = repeats > 1 ? text + " (×" + repeats + ")" : text, Error = error, Until = -seconds });  // time set on the main thread
            if (messages.Count > 4) messages.RemoveAt(0);
        }
    }

    /// <summary>Unity's log of this session (Player.log in the user's LocalLow folder).</summary>
    static string LogPath =>
        !string.IsNullOrEmpty(Application.consoleLogPath) ? Application.consoleLogPath : System.IO.Path.Combine(Application.persistentDataPath, "Player.log");

    // Exceptions nobody caught: tell the user instead of failing silently.
    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception)
            Error("Unexpected error: " + condition + "\nDetails in " + LogPath);
    }

    void OnGUI()
    {
		Theme.Apply ();
        GUI.depth = -1000;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, wordWrap = true, fontSize = 13, padding = new RectOffset(10, 10, 6, 6) };
            style.normal.textColor = Color.white;
        }
        float now = Time.unscaledTime, y = Screen.height - 50;
        lock (messages)
        {
            for (int i = 0; i < messages.Count; i++)
                if (messages[i].Until < 0) messages[i] = new Message { Text = messages[i].Text, Error = messages[i].Error, Until = now - messages[i].Until };
            messages.RemoveAll(m => m.Until < now);
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var m = messages[i];
                var content = new GUIContent(m.Text);
                float width = Mathf.Min(700, Screen.width - 40), height = style.CalcHeight(content, width);
                y -= height + 6;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = m.Error ? new Color(0.85f, 0.25f, 0.2f, 1) : new Color(0.2f, 0.45f, 0.75f, 1);
                GUI.Box(new Rect(20, y, width, height), content, style);
                GUI.backgroundColor = old;
            }
        }
    }
}
