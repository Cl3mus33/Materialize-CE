using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public enum DdsEncoder { Texconv, Nvidia, Custom }

public enum DdsFormat { BC7, BC3, BC1, BC5, BC4, RGBA, BC7sRGB, BC1sRGB }   // new ones at the end: profiles store the number

/// <summary>
/// DDS export through the encoder the user prefers (Materialize CE):
/// Microsoft texconv (shipped in StreamingAssets, GPU BC7), NVIDIA Texture Tools (nvcompress, installed by the
/// user: NVIDIA's licence does not let us ship it), or any other command-line tool with a command template.
/// The map is written as a lossless PNG in the temp folder, then handed to the tool.
/// </summary>
public static class DdsExport
{
    public static DdsEncoder Encoder = DdsEncoder.Texconv;
    public static DdsFormat Format = DdsFormat.BC7;
    /// <summary>Slower, better compression when the tool offers it.</summary>
    public static bool HighQuality = true;
    /// <summary>Overrides where the tool is; empty to look in the usual places.</summary>
    public static string ToolPath = "";
    /// <summary>Custom tool arguments; {input}, {output}, {format} and {normal} are replaced.</summary>
    public static string CustomArguments = "\"{input}\" \"{output}\" {format}";

    public static readonly string[] FormatLabels = { "BC7 (best)", "BC3 / DXT5", "BC1 / DXT1", "BC5 (normals)", "BC4 (grey)", "RGBA (none)", "BC7 sRGB (colour)", "BC1 sRGB (colour)" };
    public static readonly string[] FormatHelp =
    {
        "Best quality for colour and normals, with alpha. Skyrim SE, Unreal, Unity.",
        "Colour with smooth alpha. Older engines, Skyrim LE.",
        "Colour without alpha, smallest file. Older engines.",
        "Two channels (X, Y) for normal maps; the engine rebuilds Z. Not for Skyrim.",
        "One grey channel: height, roughness, AO.",
        "No compression: exact pixels, 4 bytes per pixel.",
        "BC7 flagged sRGB, for colour maps in engines that read it: Skyrim True PBR (Community Shaders) albedo. Same pixels as BC7; the game converts them to linear light.",
        "BC1 flagged sRGB: colour without alpha, half the size of BC7 but lower quality. Skyrim True PBR albedo when the file size matters.",
    };

    const string NvidiaDefault = @"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools\nvcompress.exe";

    /// <summary>The tool that will run, or null when it cannot be found.</summary>
    public static string FindTool()
    {
        if (!string.IsNullOrWhiteSpace(ToolPath)) return File.Exists(ToolPath) ? ToolPath : null;
        switch (Encoder)
        {
            case DdsEncoder.Texconv:
                string bundled = Path.Combine(Application.streamingAssetsPath, "texconv.exe");
                return File.Exists(bundled) ? bundled : OnPath("texconv.exe");
            case DdsEncoder.Nvidia:
                return File.Exists(NvidiaDefault) ? NvidiaDefault : OnPath("nvcompress.exe");
            default:
                return null;   // a custom tool always needs its path
        }
    }

    static string OnPath(string exe)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    public static string ToolStatus()
    {
        string tool = FindTool();
        if (tool != null) return "Found: " + Path.GetFileName(tool);
        switch (Encoder)
        {
            case DdsEncoder.Nvidia: return "Not found: install NVIDIA Texture Tools, or pick nvcompress.exe";
            case DdsEncoder.Custom: return "Pick the tool's .exe";
            default: return "texconv.exe missing: pick it";
        }
    }

    /// <summary>Worker thread: converts a PNG to DDS. Returns null on success, else a message for the user.</summary>
    public static string Convert(string pngPath, string ddsPath, bool normalMap)
    {
        string tool = FindTool();
        if (tool == null) return "DDS export: " + ToolStatus() + " (Saving Options > DDS).";
        string arguments;
        string produced = ddsPath;
        switch (Encoder)
        {
            case DdsEncoder.Texconv:
                // texconv names its output after the input, in the -o folder.
                string[] texconvFormats = { "BC7_UNORM", "BC3_UNORM", "BC1_UNORM", "BC5_UNORM", "BC4_UNORM", "R8G8B8A8_UNORM", "BC7_UNORM_SRGB -srgb", "BC1_UNORM_SRGB -srgb" };   // -srgb: in and out both sRGB, pixels unchanged
                string outDir = Path.GetDirectoryName(ddsPath);
                produced = Path.Combine(outDir, Path.GetFileNameWithoutExtension(pngPath) + ".dds");
                arguments = "-nologo -y -m 0 -f " + texconvFormats[(int)Format] + (HighQuality ? "" : " -bc q") + " -o \"" + outDir + "\" \"" + pngPath + "\"";
                break;
            case DdsEncoder.Nvidia:
                string[] nvFormats = { "-bc7", "-bc3", "-bc1", "-bc5", "-bc4", "-rgb", "-bc7 -srgb", "-bc1 -srgb" };
                // Not -production: measured 120× slower (40 s for a 1K BC7) for a barely visible gain.
                arguments = "-silent " + (HighQuality ? "" : "-fast ") + nvFormats[(int)Format] + (normalMap ? " -normal" : "")
                            + " -mipfilter kaiser \"" + pngPath + "\" \"" + ddsPath + "\"";
                break;
            default:
                string[] names = { "bc7", "bc3", "bc1", "bc5", "bc4", "rgba", "bc7_srgb", "bc1_srgb" };
                arguments = CustomArguments.Replace("{input}", pngPath).Replace("{output}", ddsPath)
                    .Replace("{format}", names[(int)Format]).Replace("{normal}", normalMap ? "normal" : "color");
                break;
        }

        var output = new StringBuilder();
        try
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(tool, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(10 * 60 * 1000))
                {
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    return "DDS export took more than 10 minutes and was stopped: " + ddsPath;
                }
                process.WaitForExit();
                if (process.ExitCode != 0 || !File.Exists(produced))
                    return "DDS export failed (" + Path.GetFileName(tool) + ", code " + process.ExitCode + "): " + LastLines(output.ToString());
            }
            if (!string.Equals(produced, ddsPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(ddsPath)) File.Delete(ddsPath);
                File.Move(produced, ddsPath);
            }
            return null;
        }
        catch (Exception e)
        {
            return "DDS export failed: " + e.Message;
        }
    }

    static string LastLines(string text)
    {
        var lines = text.Trim().Split('\n');
        return string.Join(" ", lines, Math.Max(0, lines.Length - 3), Math.Min(3, lines.Length)).Trim();
    }
}
