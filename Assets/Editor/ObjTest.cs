using System.IO;
using UnityEngine;

/// <summary>ObjLoader on a small cube with quads, UVs and normals. -executeMethod ObjTest.Run</summary>
public static class ObjTest
{
    public static void Run()
    {
        string path = Path.Combine(Path.GetTempPath(), "mce-objtest.obj");
        File.WriteAllText(path, @"# cube
v -1 -1 -1
v 1 -1 -1
v 1 1 -1
v -1 1 -1
v -1 -1 1
v 1 -1 1
v 1 1 1
v -1 1 1
vt 0 0
vt 1 0
vt 1 1
vt 0 1
vn 0 0 -1
vn 0 0 1
vn -1 0 0
vn 1 0 0
vn 0 -1 0
vn 0 1 0
f 1/1/1 4/4/1 3/3/1 2/2/1
f 5/1/2 6/2/2 7/3/2 8/4/2
f 1/1/3 5/2/3 8/3/3 4/4/3
f 2/1/4 3/4/4 7/3/4 6/2/4
f 1/1/5 2/2/5 6/3/5 5/4/5
f -5/1/6 -1/4/6 -2/3/6 -6/2/6
");
        var mesh = ObjLoader.Load(path, out string error);
        if (mesh == null) { Debug.Log("OBJTEST failed: " + error); return; }
        ObjLoader.Fit(mesh, 2f);
        // The winding must agree with the file's normals: recomputed face normals point the same way.
        var fileNormals = mesh.normals;
        mesh.RecalculateNormals();
        var faceNormals = mesh.normals;
        int agree = 0;
        for (int i = 0; i < fileNormals.Length; i++) if (Vector3.Dot(fileNormals[i], faceNormals[i]) > 0.5f) agree++;
        Debug.Log($"OBJTEST {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles, size {mesh.bounds.size}, centre {mesh.bounds.center}, tangents {mesh.tangents.Length}, winding agrees on {agree}/{fileNormals.Length}");
        File.WriteAllText(path, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        var noUv = ObjLoader.Load(path, out error);
        Debug.Log("OBJTEST without UVs: " + (noUv == null ? "refused - " + error : "accepted"));
        File.Delete(path);
    }
}
