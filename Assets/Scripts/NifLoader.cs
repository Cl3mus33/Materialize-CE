using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// Reads a Bethesda .nif (Skyrim LE / SE, Fallout 4) into a Unity mesh (Materialize CE), to preview the material on
/// the game's own model. The reading is done by PyNifly's NiflyDLL (BadDogSkyrim, GPL-3.0), itself built on nifly
/// (the library of Outfit Studio / BodySlide). All the shapes of the file become one mesh, at their place in the
/// file; skinned meshes come in their rest pose.
/// </summary>
public static class NifLoader
{
    const string Dll = "NiflyDLL";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr load(byte[] utf8Path);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void destroy(IntPtr nif);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int getShapes(IntPtr nif, [Out] IntPtr[] buffer, int length, int start);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int getVertsForShape(IntPtr nif, IntPtr shape, [Out] float[] buffer, int floats, int start);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int getNormalsForShape(IntPtr nif, IntPtr shape, [Out] float[] buffer, int floats, int start);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int getUVs(IntPtr nif, IntPtr shape, [Out] float[] buffer, int floats, int start);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int getTriangles(IntPtr nif, IntPtr shape, [Out] ushort[] buffer, int shorts, int start);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void getNodeTransform(IntPtr node, [Out] float[] matTransform);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr getNodeParent(IntPtr nif, IntPtr node);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int getShapeName(IntPtr shape, [Out] byte[] buffer, int length);

    /// <summary>Null and an error message when the file cannot be used.</summary>
    public static Mesh Load(string path, out string error) => Load(path, out error, out _);

    /// <summary>The same, with one part per shape of the file.</summary>
    public static Mesh Load(string path, out string error, out List<MeshPart> parts)
    {
        error = null;
        parts = new List<MeshPart>();
        IntPtr nif = IntPtr.Zero;
        try
        {
            nif = load(Encoding.UTF8.GetBytes(path + "\0"));
            if (nif == IntPtr.Zero) { error = Path.GetFileName(path) + " is not a .nif file this version can read."; return null; }

            int shapeCount = getShapes(nif, new IntPtr[1], 0, 0);
            if (shapeCount <= 0) { error = "No shape found in " + Path.GetFileName(path) + "."; return null; }
            var shapes = new IntPtr[shapeCount];
            getShapes(nif, shapes, shapeCount, 0);

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            bool allNormals = true;

            foreach (var shape in shapes)
            {
                int vertexCount = getVertsForShape(nif, shape, new float[3], 0, 0);
                int uvCount = getUVs(nif, shape, new float[2], 0, 0);
                int triCount = getTriangles(nif, shape, new ushort[3], 0, 0);
                if (vertexCount <= 0 || triCount <= 0 || uvCount < vertexCount) continue;   // no surface, or no texture coordinates

                var v = new float[vertexCount * 3];
                getVertsForShape(nif, shape, v, v.Length, 0);
                var t = new float[uvCount * 2];
                getUVs(nif, shape, t, t.Length, 0);
                var tri = new ushort[triCount * 3];
                getTriangles(nif, shape, tri, tri.Length, 0);
                int normalCount = getNormalsForShape(nif, shape, new float[3], 0, 0);
                float[] n = null;
                if (normalCount >= vertexCount) { n = new float[normalCount * 3]; getNormalsForShape(nif, shape, n, n.Length, 0); }
                else allNormals = false;

                // The shape at its place in the file: its transform, then each parent's, up to the root.
                var chain = new List<float[]>();
                IntPtr node = shape;
                for (int depth = 0; node != IntPtr.Zero && depth < 64; depth++)
                {
                    var m = new float[13];   // translation (3), rotation rows (9), scale
                    getNodeTransform(node, m);
                    chain.Add(m);
                    node = getNodeParent(nif, node);
                }

                var nameBuffer = new byte[256];
                int nameLength = Mathf.Clamp(getShapeName(shape, nameBuffer, nameBuffer.Length), 0, nameBuffer.Length - 1);
                var part = new MeshPart { Name = nameLength > 0 ? Encoding.UTF8.GetString(nameBuffer, 0, nameLength) : "shape " + (parts.Count + 1) };
                parts.Add(part);

                int first = positions.Count;
                for (int i = 0; i < vertexCount; i++)
                {
                    var p = new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]);
                    var nn = n != null ? new Vector3(n[i * 3], n[i * 3 + 1], n[i * 3 + 2]) : Vector3.forward;
                    foreach (var m in chain)
                    {
                        p = Rotate(m, p * m[12]) + new Vector3(m[0], m[1], m[2]);
                        nn = Rotate(m, nn);
                    }
                    // The game: Z up, right-handed. Unity: Y up, left-handed. Swapping Y and Z does both
                    // (and turns the triangles over, hence the reversed winding below).
                    positions.Add(new Vector3(p.x, p.z, p.y));
                    normals.Add(new Vector3(nn.x, nn.z, nn.y).normalized);
                    uvs.Add(new Vector2(t[i * 2], 1f - t[i * 2 + 1]));   // the game's V runs downwards
                }
                for (int i = 0; i < triCount; i++)
                {
                    int a = tri[i * 3], b = tri[i * 3 + 1], c = tri[i * 3 + 2];
                    if (a >= vertexCount || b >= vertexCount || c >= vertexCount) continue;
                    triangles.Add(first + a); triangles.Add(first + c); triangles.Add(first + b);
                    part.Triangles.Add(first + a); part.Triangles.Add(first + c); part.Triangles.Add(first + b);
                }
            }

            if (triangles.Count == 0) { error = Path.GetFileName(path) + " has no shape with texture coordinates."; return null; }
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            if (positions.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            if (allNormals) mesh.SetNormals(normals); else mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
        catch (DllNotFoundException) { error = "NiflyDLL.dll is missing next to the application: .nif files cannot be read."; return null; }
        catch (Exception e) { error = "Could not read the mesh: " + e.Message; return null; }
        finally
        {
            if (nif != IntPtr.Zero) { try { destroy(nif); } catch (Exception) { } }
        }
    }

    /// <summary>The rotation of a nifly transform (rows at 3..11) applied to a vector.</summary>
    static Vector3 Rotate(float[] m, Vector3 p)
    {
        return new Vector3(
            m[3] * p.x + m[4] * p.y + m[5] * p.z,
            m[6] * p.x + m[7] * p.y + m[8] * p.z,
            m[9] * p.x + m[10] * p.y + m[11] * p.z);
    }
}
