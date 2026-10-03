using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Command-line build: Unity.exe -batchmode -quit -projectPath . -executeMethod BuildScript.BuildWindows</summary>
public static class BuildScript
{
    static void CopyAddons()
    {
        string from = System.IO.Path.GetFullPath("Addons"), to = System.IO.Path.GetFullPath("Build/Windows/Addons");
        if (!System.IO.Directory.Exists(from)) return;
        // Profiles and presets as they are now (removed ones disappear from the build). The HDRI folders are
        // left alone: HDRIs added there by hand for testing are kept.
        foreach (var addon in System.IO.Directory.GetDirectories(from))
            foreach (var sub in new[] { "Export profiles", "Render presets" })
            {
                string old = System.IO.Path.Combine(to, System.IO.Path.GetFileName(addon), sub);
                if (System.IO.Directory.Exists(old)) System.IO.Directory.Delete(old, true);
            }
        foreach (var file in System.IO.Directory.GetFiles(from, "*", System.IO.SearchOption.AllDirectories))
        {
            string target = System.IO.Path.Combine(to, file.Substring(from.Length + 1));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
            System.IO.File.Copy(file, target, true);
        }
    }

    public static void BuildWindows()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) scenes = new[] { "Assets/MainScene.unity" };
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/Windows/Materialize.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        CopyAddons();
        Debug.Log($"BUILD RESULT: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
