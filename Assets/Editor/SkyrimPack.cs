using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Writes the Skyrim export profiles and render presets as the external Skyrim pack. -executeMethod SkyrimPack.Run</summary>
public static class SkyrimPack
{
    public static void Run()
    {
        string root = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Addons", "Skyrim");
        string ep = Path.Combine(root, "Export profiles"), rp = Path.Combine(root, "Render presets");
        Directory.CreateDirectory(ep); Directory.CreateDirectory(rp);
        foreach (var p in ExportProfiles.BuiltIn().Where(p => p.Name.StartsWith("Skyrim")))
        {
            File.WriteAllText(Path.Combine(ep, p.Name + ".json"), JsonUtility.ToJson(p, true));
            Debug.Log("SKYPACK profile " + p.Name);
        }
        foreach (var p in RenderPresets.BuiltIn().Where(p => p.Name.StartsWith("Skyrim")))
        {
            File.WriteAllText(Path.Combine(rp, p.Name + ".json"), JsonUtility.ToJson(p, true));
            Debug.Log("SKYPACK preset " + p.Name);
        }
    }
}
