using System.IO;
using UnityEngine;

/// <summary>
/// Materialize CE keeps its user data under LocalLow\Materialize (the original used the author's company name,
/// "Bounding Box Software"). On first start, the export profiles and render presets saved by the earlier builds
/// are copied over, so nothing is lost. The original folder is left untouched.
/// </summary>
public static class DataMigration
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Migrate()
    {
        try
        {
            var localLow = Directory.GetParent(Directory.GetParent(Application.persistentDataPath).FullName).FullName;
            var old = Path.Combine(localLow, "Bounding Box Software", "Materialize");
            foreach (var name in new[] { "ExportProfiles", "RenderPresets" })
            {
                string from = Path.Combine(old, name), to = Path.Combine(Application.persistentDataPath, name);
                if (!Directory.Exists(from) || Directory.Exists(to)) continue;
                Directory.CreateDirectory(to);
                foreach (var file in Directory.GetFiles(from, "*.json"))
                    File.Copy(file, Path.Combine(to, Path.GetFileName(file)), false);
                Debug.Log("Copied " + name + " from " + from);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not copy the earlier user data: " + e.Message);
        }
    }
}
