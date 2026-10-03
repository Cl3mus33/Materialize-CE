using System.Collections.Generic;
using UnityEngine;

/// <summary>Checks that the Skyrim pack's profiles load as an add-on. -executeMethod AddonTest.Run</summary>
public static class AddonTest
{
    public static void Run()
    {
        var errors = new List<string>();
        foreach (var p in ExportProfiles.LoadAddons(errors))
            Debug.Log($"ADDONTEST {p.Label}: {p.Outputs.Count} files, _m alpha = {(p.Outputs.Find(o => o.Suffix == "_m")?.Channels[3].Source.ToString() ?? "-")}, all enabled = {p.Outputs.TrueForAll(o => o.Enabled)}");
        foreach (var e in errors) Debug.Log("ADDONTEST error " + e);
        foreach (var (addon, file) in Workspace.AddonFiles(Workspace.RenderPresetsDir, ".json")) Debug.Log("ADDONTEST preset " + addon + " " + System.IO.Path.GetFileName(file));
    }
}
