using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// The Windows open / save dialog (Materialize CE). The built-in browser made a full-size decode, a temporary
/// PNG and a memory sweep for every thumbnail, one file after another, so big texture folders took ages to
/// show. Windows' own dialog lists folders instantly, has its thumbnails, favourites and search, and handles
/// any path (accents, network drives). The last folder is remembered per kind of file.
/// </summary>
public static class NativeFileDialog
{
    public static bool Available => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    const int OFN_OVERWRITEPROMPT = 0x2, OFN_HIDEREADONLY = 0x4, OFN_NOCHANGEDIR = 0x8, OFN_PATHMUSTEXIST = 0x800,
              OFN_FILEMUSTEXIST = 0x1000, OFN_EXPLORER = 0x80000;
    const int MaxPath = 32768;

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool GetOpenFileNameW(ref OpenFileName ofn);
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool GetSaveFileNameW(ref OpenFileName ofn);
    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();

    /// <param name="masks">"*.png;*.jpg" as the built-in browser used them.</param>
    /// <returns>The chosen path, or null when cancelled.</returns>
    public static string Show(string title, string masks, bool save)
    {
        var patterns = (masks ?? "*.*").Replace(',', ';').Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim()).ToArray();
        string kind = patterns.Contains("*.mtz") ? "Projects" : "Images";
        string description = kind == "Projects" ? "Materialize projects" : "Images";
        string filter = description + " (" + string.Join(", ", patterns) + ")\0" + string.Join(";", patterns) + "\0All files (*.*)\0*.*\0\0";
        string key = "MaterializeCE.LastFolder." + kind;
        string initial = PlayerPrefs.GetString(key, "");
        if (!Directory.Exists(initial)) initial = Workspace.Dir(kind == "Projects" ? Workspace.Projects : Workspace.Textures);

        IntPtr buffer = Marshal.AllocHGlobal(MaxPath * 2);
        try
        {
            // Start with an empty name.
            Marshal.Copy(new byte[MaxPath * 2], 0, buffer, MaxPath * 2);
            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
                hwndOwner = GetActiveWindow(),
                lpstrFilter = filter,
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = MaxPath,
                lpstrInitialDir = initial,
                lpstrTitle = title,
                lpstrDefExt = save ? patterns.FirstOrDefault()?.TrimStart('*', '.') : null,
                Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_HIDEREADONLY | OFN_PATHMUSTEXIST | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST),
            };
            bool ok = save ? GetSaveFileNameW(ref ofn) : GetOpenFileNameW(ref ofn);
            if (!ok) return null;
            string path = Marshal.PtrToStringUni(buffer);
            if (string.IsNullOrEmpty(path)) return null;
            PlayerPrefs.SetString(key, Path.GetDirectoryName(path));
            PlayerPrefs.Save();
            return path;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Windows file dialog failed, using the built-in browser: " + e.Message);
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
