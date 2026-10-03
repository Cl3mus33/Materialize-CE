using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tooltips for the whole interface (Materialize CE). IMGUI only knows the hovered control's tooltip inside the
/// OnGUI (or window) that drew it, so each one calls Tips.Capture() at its end. Windows also register their
/// area: a button hidden under a window must not show its tooltip. The tip appears once the mouse has rested
/// half a second, and goes away as soon as it moves. It can be switched off in Settings.
/// </summary>
public class Tips : MonoBehaviour
{
    public static bool Enabled = true;

    // What was hovered during the frame being built, and during the last complete one.
    static string mainText = "", windowText = "", shownText = "";
    static readonly List<Rect> windows = new List<Rect>(), lastWindows = new List<Rect>();
    static int frame = -1;

    static Vector2 restPosition;
    static float restSince;
    static Tips instance;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("Tips");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Tips>();
    }

    static void NewFrameIfNeeded()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        // The last complete frame decides what is shown: windows first, then the main panels if not covered.
        shownText = windowText.Length > 0 ? windowText : mainText;
        lastWindows.Clear();
        lastWindows.AddRange(windows);
        windows.Clear();
        mainText = windowText = "";
    }

    /// <summary>Call right after GUI.Window, with the window's rectangle: it hides what lies underneath.</summary>
    public static void Block(Rect windowRect)
    {
        NewFrameIfNeeded();
        if (Event.current.type == EventType.Repaint) windows.Add(windowRect);
    }

    /// <summary>Call at the end of an OnGUI (inWindow false) or of a window function (inWindow true).</summary>
    public static void Capture(bool inWindow = false)
    {
        NewFrameIfNeeded();
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(GUI.tooltip)) return;
        if (inWindow) windowText = GUI.tooltip;
        else if (!UnderWindow(Event.current.mousePosition)) mainText = GUI.tooltip;
    }

    static bool UnderWindow(Vector2 mouse)
    {
        foreach (var r in lastWindows) if (r.Contains(mouse)) return true;
        return false;
    }

    void OnGUI()
    {
		Theme.Apply ();
        GUI.depth = -2000;
        NewFrameIfNeeded();
        if (!Enabled || Event.current.type != EventType.Repaint) return;
        Vector2 mouse = Event.current.mousePosition;
        // Any movement hides the tip and restarts the wait, like a desktop tooltip.
        if ((mouse - restPosition).sqrMagnitude > 9f) { restPosition = mouse; restSince = Time.unscaledTime; return; }
        if (Time.unscaledTime - restSince < 0.5f || string.IsNullOrEmpty(shownText)) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = 12, padding = new RectOffset(8, 8, 6, 6) };
            style.normal.textColor = Color.white;
        }
        var content = new GUIContent(shownText);
        float width = Mathf.Min(320, style.CalcSize(content).x + 4);
        float height = style.CalcHeight(content, width);
        float x = Mathf.Min(mouse.x + 16, Screen.width - width - 4), y = mouse.y + 20;
        if (y + height > Screen.height - 4) y = mouse.y - height - 8;
        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1);
        GUI.Box(new Rect(x, y, width, height), content, style);
        GUI.Box(new Rect(x, y, width, height), content, style);   // twice: the default box is translucent
        GUI.backgroundColor = old;
    }
}
