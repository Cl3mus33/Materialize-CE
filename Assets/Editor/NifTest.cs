using System.IO;
using UnityEngine;

/// <summary>NifLoader on the test planes of the Skyrim pack, and on any file given in MCE_NIF. -executeMethod NifTest.Run</summary>
public static class NifTest
{
    public static void Run()
    {
        var files = new System.Collections.Generic.List<string>(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Addons", "Skyrim", "Test meshes"), "*.nif"));
        string extra = System.Environment.GetEnvironmentVariable("MCE_NIF");
        if (!string.IsNullOrEmpty(extra)) foreach (var f in extra.Split(';')) if (File.Exists(f)) files.Add(f);
        foreach (var file in files)
        {
            var mesh = NifLoader.Load(file, out string error, out var parts);
            if (mesh == null) { Debug.Log("NIFTEST " + Path.GetFileName(file) + ": refused - " + error); continue; }
            var uv = mesh.uv; float umin = 9, umax = -9, vmin = 9, vmax = -9;
            foreach (var u in uv) { umin = Mathf.Min(umin, u.x); umax = Mathf.Max(umax, u.x); vmin = Mathf.Min(vmin, u.y); vmax = Mathf.Max(vmax, u.y); }
            // The winding must agree with the normals of the file.
            var fileNormals = mesh.normals; mesh.RecalculateNormals(); var faceNormals = mesh.normals; int agree = 0;
            for (int i = 0; i < fileNormals.Length; i++) if (Vector3.Dot(fileNormals[i], faceNormals[i]) > 0f) agree++;
            Debug.Log($"NIFTEST {Path.GetFileName(file)}: {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles, size {mesh.bounds.size}, UV {umin:0.00}..{umax:0.00} x {vmin:0.00}..{vmax:0.00}, winding agrees on {agree}/{fileNormals.Length} | parts: " + string.Join(", ", parts.ConvertAll(p => p.Name + " (" + p.Triangles.Count / 3 + ")")));
        }
        string missing = Path.Combine(Path.GetTempPath(), "not-a-nif.nif");
        File.WriteAllText(missing, "hello");
        Debug.Log("NIFTEST a file that is not a nif: " + (NifLoader.Load(missing, out string e2) == null ? "refused - " + e2 : "accepted"));
        File.Delete(missing);
    }
}
