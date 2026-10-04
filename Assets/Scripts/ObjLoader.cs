using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>A named part of an imported mesh (a shape of a .nif, an object or group of an .obj) and its triangles.</summary>
public sealed class MeshPart
{
    public string Name;
    public readonly List<int> Triangles = new List<int>();
}

/// <summary>
/// Reads a Wavefront .obj file into a Unity mesh (Materialize CE), to preview the material on your own model:
/// positions, texture coordinates, normals, any polygon (fan-triangulated), negative indices. Materials and
/// groups are ignored: the whole file becomes one mesh.
/// </summary>
public static class ObjLoader
{
    /// <summary>Null and an error message when the file cannot be used.</summary>
    public static Mesh Load(string path, out string error) => Load(path, out error, out _);

    /// <summary>The same, with the parts of the file (its o / g lines): one part when it has none.</summary>
    public static Mesh Load(string path, out string error, out List<MeshPart> parts)
    {
        error = null;
        parts = new List<MeshPart>();
        MeshPart part = null;
        var positions = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var outPos = new List<Vector3>();
        var outUv = new List<Vector2>();
        var outNormal = new List<Vector3>();
        var triangles = new List<int>();
        var seen = new Dictionary<(int, int, int), int>();
        bool hasNormals = true, hasUvs = true;
        var inv = CultureInfo.InvariantCulture;
        var corner = new List<int>();

        try
        {
            foreach (var raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length < 2 || line[0] == '#') continue;
                var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (tokens[0])
                {
                    case "v":
                        // .obj is right-handed, Unity left-handed: x is mirrored (and the winding reversed below).
                        if (tokens.Length >= 4) positions.Add(new Vector3(-float.Parse(tokens[1], inv), float.Parse(tokens[2], inv), float.Parse(tokens[3], inv)));
                        break;
                    case "vt":
                        if (tokens.Length >= 3) uvs.Add(new Vector2(float.Parse(tokens[1], inv), float.Parse(tokens[2], inv)));
                        break;
                    case "vn":
                        if (tokens.Length >= 4) normals.Add(new Vector3(-float.Parse(tokens[1], inv), float.Parse(tokens[2], inv), float.Parse(tokens[3], inv)));
                        break;
                    case "o":
                    case "g":
                        part = new MeshPart { Name = parts2Name(parts, line.Length > 2 ? line.Substring(2).Trim() : "") };
                        parts.Add(part);
                        break;
                    case "f":
                        if (part == null) { part = new MeshPart { Name = Path.GetFileNameWithoutExtension(path) }; parts.Add(part); }
                        corner.Clear();
                        for (int i = 1; i < tokens.Length; i++)
                        {
                            var idx = tokens[i].Split('/');
                            int p = Index(idx[0], positions.Count);
                            int t = idx.Length > 1 && idx[1].Length > 0 ? Index(idx[1], uvs.Count) : -1;
                            int n = idx.Length > 2 && idx[2].Length > 0 ? Index(idx[2], normals.Count) : -1;
                            if (p < 0 || p >= positions.Count) continue;
                            if (t < 0 || t >= uvs.Count) { t = -1; hasUvs = false; }
                            if (n < 0 || n >= normals.Count) { n = -1; hasNormals = false; }
                            if (!seen.TryGetValue((p, t, n), out int vertex))
                            {
                                vertex = outPos.Count;
                                seen[(p, t, n)] = vertex;
                                outPos.Add(positions[p]);
                                outUv.Add(t >= 0 ? uvs[t] : Vector2.zero);
                                outNormal.Add(n >= 0 ? normals[n] : Vector3.up);
                            }
                            corner.Add(vertex);
                        }
                        for (int i = 1; i + 1 < corner.Count; i++)
                        {
                            triangles.Add(corner[0]); triangles.Add(corner[i + 1]); triangles.Add(corner[i]);   // reversed winding
                            part.Triangles.Add(corner[0]); part.Triangles.Add(corner[i + 1]); part.Triangles.Add(corner[i]);
                        }
                        break;
                }
            }
        }
        catch (Exception e) { error = "Could not read the mesh: " + e.Message; return null; }

        parts.RemoveAll(p => p.Triangles.Count == 0);
        if (triangles.Count == 0) { error = "No faces found in " + Path.GetFileName(path) + "."; return null; }
        if (!hasUvs) { error = Path.GetFileName(path) + " has no texture coordinates (UVs): the material cannot be shown on it."; return null; }

        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        if (outPos.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(outPos);
        mesh.SetUVs(0, outUv);
        mesh.SetTriangles(triangles, 0);
        if (hasNormals) mesh.SetNormals(outNormal); else mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static string parts2Name(List<MeshPart> parts, string name) => name.Length > 0 ? name : "part " + (parts.Count + 1);

    /// <summary>1-based, or negative = counted from the end; -1 when unreadable.</summary>
    static int Index(string text, int count)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return -1;
        return i > 0 ? i - 1 : i < 0 ? count + i : -1;
    }

    /// <summary>Moves the mesh to its centre and scales it so its largest side is <paramref name="size"/>.</summary>
    public static void Fit(Mesh mesh, float size)
    {
        var b = mesh.bounds;
        float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (largest < 1e-6f) return;
        float scale = size / largest;
        var v = mesh.vertices;
        for (int i = 0; i < v.Length; i++) v[i] = (v[i] - b.center) * scale;
        mesh.vertices = v;
        mesh.RecalculateBounds();
    }
}
