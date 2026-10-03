using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Window or full screen, and where the window was (Materialize CE). Unity 6 starts in borderless full screen
/// and remembers the last mode in the registry, which left users stuck in full screen. Materialize now opens in
/// a window unless the user chose full screen (button or F11). On quit the window's size, position and
/// maximised state are kept (Windows' own window placement), and put back at the next launch.
/// </summary>
public class WindowMode : MonoBehaviour
{
    const string FullKey = "MaterializeCE.FullScreen";
    const string PlacementKey = "MaterializeCE.WindowPlacement";

    // A maximised window is still a window (Unity reports it as MaximizedWindow).
    public static bool IsFullScreen => Screen.fullScreenMode == FullScreenMode.FullScreenWindow || Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen;

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct WINDOWPLACEMENT
    {
        public int length, flags, showCmd;
        public POINT ptMinPosition, ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll")] static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref RECT rect, int flags);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Dark title bar in the interface's colour (Windows 11; Windows 10 gets the dark mode only).</summary>
    static void DarkTitleBar()
    {
        try
        {
            int on = 1;
            DwmSetWindowAttribute(window, 20, ref on, 4);                  // DWMWA_USE_IMMERSIVE_DARK_MODE
            Color32 bar = Theme.TitleBarColour, text = new Color32(0xE3, 0xE7, 0xEE, 255);
            int caption = bar.r | (bar.g << 8) | (bar.b << 16), textColour = text.r | (text.g << 8) | (text.b << 16);
            DwmSetWindowAttribute(window, 35, ref caption, 4);             // DWMWA_CAPTION_COLOR
            DwmSetWindowAttribute(window, 36, ref textColour, 4);          // DWMWA_TEXT_COLOR
            int border = caption;
            DwmSetWindowAttribute(window, 34, ref border, 4);              // DWMWA_BORDER_COLOR
        }
        catch (Exception e) { Debug.LogWarning("Title bar colour: " + e.Message); }
    }

    const int SW_SHOWNORMAL = 1, SW_SHOWMAXIMIZED = 3, MONITOR_DEFAULTTONULL = 0;

    static IntPtr window;
    static WindowMode self;

    static IEnumerator RestoreLater()
    {
        yield return null;
        yield return null;
        RestorePlacement();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Application.isEditor) return;
        var go = new GameObject("WindowMode");
        DontDestroyOnLoad(go);
        go.AddComponent<WindowMode>();
        Application.quitting += SavePlacement;

        bool full = PlayerPrefs.GetInt(FullKey, 0) == 1;
        if (full) Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        else if (IsFullScreen) Screen.SetResolution(1600, 1000, FullScreenMode.Windowed);
    }

    IEnumerator Start()
    {
        self = this;
        // The window handle is known once the window exists and is active.
        for (int i = 0; i < 30 && window == IntPtr.Zero; i++)
        {
            yield return null;
            window = GetActiveWindow();
        }
        if (window != IntPtr.Zero) DarkTitleBar();
        if (window != IntPtr.Zero && !IsFullScreen) RestorePlacement();
    }

    float nextSave;

    void Update()
    {
        // Kept current: at quit time the window may no longer be the active one.
        if (window == IntPtr.Zero && Application.isFocused) window = GetActiveWindow();
        // Also saved every few seconds, so a crash or a closed console keeps the last size too.
        if (Time.unscaledTime > nextSave) { nextSave = Time.unscaledTime + 3f; SavePlacement(); }
    }

    static void SavePlacement()
    {
        if (window == IntPtr.Zero || IsFullScreen) return;
        var p = new WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
        if (!GetWindowPlacement(window, ref p)) return;
        var r = p.rcNormalPosition;
        string value = r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom + "," + (p.showCmd == SW_SHOWMAXIMIZED ? 1 : 0);
        if (value == PlayerPrefs.GetString(PlacementKey, "")) return;
        PlayerPrefs.SetString(PlacementKey, value);
        PlayerPrefs.Save();
    }

    static void RestorePlacement()
    {
        var parts = PlayerPrefs.GetString(PlacementKey, "").Split(',');
        if (parts.Length != 5) return;
        int[] v = new int[5];
        for (int i = 0; i < 5; i++) if (!int.TryParse(parts[i], out v[i])) return;
        var rect = new RECT { Left = v[0], Top = v[1], Right = v[2], Bottom = v[3] };
        if (rect.Right - rect.Left < 400 || rect.Bottom - rect.Top < 300) return;
        // A screen may have been unplugged since: only restore onto a monitor that still exists.
        if (MonitorFromRect(ref rect, MONITOR_DEFAULTTONULL) == IntPtr.Zero) return;
        var p = new WINDOWPLACEMENT
        {
            length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)),
            showCmd = v[4] == 1 ? SW_SHOWMAXIMIZED : SW_SHOWNORMAL,
            rcNormalPosition = rect,
        };
        SetWindowPlacement(window, ref p);
    }

    public static void Toggle()
    {
        bool full = !IsFullScreen;
        if (full) SavePlacement();
        PlayerPrefs.SetInt(FullKey, full ? 1 : 0);
        PlayerPrefs.Save();
        if (full) Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        else
        {
            Screen.SetResolution(1600, 1000, FullScreenMode.Windowed);
            // The mode change happens at the end of the frame: the placement is put back after it.
            if (window != IntPtr.Zero && self != null) self.StartCoroutine(RestoreLater());
        }
    }
}
