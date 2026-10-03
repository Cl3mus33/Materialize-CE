using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// The user's Materialize CE folder (Materialize CE): one place, chosen at the first launch, for projects,
/// textures, export profiles, render presets, HDRIs and add-ons. Short and easy to find, unlike AppData.
/// Add-ons (like the Skyrim pack) are folders with "Export profiles", "Render presets" and "HDRI" inside,
/// placed in the "Addons" folder of the workspace or next to Materialize.exe.
/// </summary>
public static class Workspace
{
    const string Key = "MaterializeCE.Workspace";

    public const string Projects = "Projects", Textures = "Textures", ExportProfilesDir = "Export profiles",
        RenderPresetsDir = "Render presets", Hdri = "HDRI", Addons = "Addons";

    public static string Default => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Materialize CE");

    /// <summary>Chosen yet? The welcome question is asked until it is.</summary>
    public static bool Chosen => PlayerPrefs.GetString(Key, "").Length > 0;

    public static string Root
    {
        get
        {
            string r = PlayerPrefs.GetString(Key, "");
            return r.Length > 0 ? r : Default;
        }
    }

    /// <summary>A subfolder of the workspace, created when needed.</summary>
    public static string Dir(string name)
    {
        string d = Path.Combine(Root, name);
        try { Directory.CreateDirectory(d); } catch (Exception e) { Debug.LogWarning("Workspace: " + e.Message); }
        return d;
    }

    /// <summary>Uses <paramref name="root"/> from now on; profiles and presets saved before are copied in.</summary>
    public static void Set(string root)
    {
        if (string.IsNullOrEmpty(root)) return;
        try
        {
            Directory.CreateDirectory(root);
            PlayerPrefs.SetString(Key, root);
            PlayerPrefs.Save();
            foreach (var d in new[] { Projects, Textures, ExportProfilesDir, RenderPresetsDir, Hdri, Addons }) Dir(d);
            CopyOld("ExportProfiles", ExportProfilesDir);
            CopyOld("RenderPresets", RenderPresetsDir);
            Notifications.Info(L.T("Your Materialize CE folder:") + " " + root);
        }
        catch (Exception e) { Notifications.Error(L.T("This folder cannot be used:") + " " + e.Message); }
    }

    static void CopyOld(string oldName, string newName)
    {
        string from = Path.Combine(Application.persistentDataPath, oldName);
        if (!Directory.Exists(from)) return;
        string to = Dir(newName);
        foreach (var f in Directory.GetFiles(from, "*.json"))
        {
            string target = Path.Combine(to, Path.GetFileName(f));
            if (!File.Exists(target)) File.Copy(f, target);
        }
    }

    // ---------- Add-ons ----------

    /// <summary>Every add-on folder: in the workspace's Addons and next to Materialize.exe.</summary>
    public static List<string> AddonFolders()
    {
        var list = new List<string>();
        foreach (var parent in new[] { Path.Combine(Root, Addons), Path.Combine(Path.GetDirectoryName(Application.dataPath), Addons) })
        {
            try
            {
                if (Directory.Exists(parent))
                    foreach (var d in Directory.GetDirectories(parent))
                        if (!list.Exists(x => string.Equals(Path.GetFileName(x), Path.GetFileName(d), StringComparison.OrdinalIgnoreCase))) list.Add(d);
            }
            catch (Exception e) { Debug.LogWarning("Add-ons: " + e.Message); }
        }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>Files of one kind from every add-on: (add-on name, file).</summary>
    public static List<(string Addon, string File)> AddonFiles(string subfolder, params string[] extensions)
    {
        var list = new List<(string, string)>();
        foreach (var addon in AddonFolders())
        {
            string dir = Path.Combine(addon, subfolder);
            if (!Directory.Exists(dir)) continue;
            var files = Directory.GetFiles(dir);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
                foreach (var ext in extensions)
                    if (f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { list.Add((Path.GetFileName(addon), f)); break; }
        }
        return list;
    }

    // ---------- Folder picker (Windows) ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct BROWSEINFO
    {
        public IntPtr hwndOwner, pidlRoot;
        public string pszDisplayName, lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn, lParam;
        public int iImage;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHBrowseForFolderW(ref BROWSEINFO bi);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool SHGetPathFromIDListW(IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder path);
    [DllImport("ole32.dll")] static extern void CoTaskMemFree(IntPtr pv);
    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();

    /// <summary>Windows' folder picker. Null when cancelled.</summary>
    public static string PickFolder(string title)
    {
        try
        {
            var bi = new BROWSEINFO
            {
                hwndOwner = GetActiveWindow(),
                pszDisplayName = new string('\0', 260),
                lpszTitle = title,
                ulFlags = 0x0001 | 0x0040,   // only folders, new dialog style (with "New folder")
            };
            IntPtr pidl = SHBrowseForFolderW(ref bi);
            if (pidl == IntPtr.Zero) return null;
            var sb = new System.Text.StringBuilder(1024);
            bool ok = SHGetPathFromIDListW(pidl, sb);
            CoTaskMemFree(pidl);
            return ok ? sb.ToString() : null;
        }
        catch (Exception e) { Notifications.Error(e.Message); return null; }
    }
}
