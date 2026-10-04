using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Batch export's reading of a folder of texture sets. -executeMethod BatchTest.Run</summary>
public static class BatchTest
{
    public static void Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mce-batchtest");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        foreach (var f in new[] { "stone_albedo.png", "stone_normal.png", "stone_roughness.png", "stone_ao.jpg", "stone_height.exr",
            "Wood_Planks_2K_BaseColor.jpg", "Wood_Planks_2K_Normal.jpg", "Wood_Planks_2K_Gloss.jpg", "Wood_Planks_2K_Displacement.exr",
            "mud_d.dds", "mud_n.dds", "mud_p.dds", "mud_g.dds", "readme.txt", "lonely.png", "_n.png" })
            File.WriteAllText(Path.Combine(dir, f), "");
        foreach (var s in BatchExport.Scan(dir))
            Debug.Log("BATCHTEST " + s.Name + ": " + string.Join(", ", s.Files.OrderBy(p => p.Key.ToString()).Select(p => p.Key + "=" + Path.GetFileName(p.Value))) + (s.RoughnessFile ? " [roughness inverted]" : ""));
        Directory.Delete(dir, true);
    }
}
