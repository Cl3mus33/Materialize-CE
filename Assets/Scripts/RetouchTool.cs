using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clone stamp on the preview plane (Materialize CE), to retouch what the automatic tiling leaves: a visible seam,
/// a repeated stone. Ctrl + click picks the source, dragging paints it at the pointer, on every map at once so
/// they stay consistent. The texture repeats: with the tiling at 3 (key T) the seams sit in the middle of the view.
/// Nothing is changed until Apply.
/// </summary>
public static class RetouchTool
{
    public static bool Active;

    sealed class Layer
    {
        public MapType Type;
        public RenderTexture Work;
        public string Slot;      // material texture, or a global when it starts with _Mce
    }

    static readonly List<Layer> layers = new List<Layer>();
    static RenderTexture workHd;
    static Material material;
    static float radius = 0.06f, hardness = 0.4f, opacity = 1f;
    static Vector2 source, offset, lastUv, cursorScreen;
    static bool hasSource, offsetPending, stroking, cursorVisible, applying, displacementWasOn;
    static float cursorPixels;
    static int dabs;
    static Rect windowRect = new Rect(0, 0, 300, 250);

    // The preview plane, read once: ray casting needs its triangles and texture coordinates.
    static Vector3[] meshVertices;
    static Vector2[] meshUvs;
    static int[] meshTriangles;
    static float worldPerUv = 10f;

    public static void Toggle(MainGui gui)
    {
        if (Active) Cancel(gui); else Begin(gui);
    }

    static void Begin(MainGui gui)
    {
        if (applying) return;
        MapAdjust.End(gui);
        gui.CloseWindows();
        layers.Clear();
        Add(gui._HeightMap, MapType.height, "_DisplacementMap");
        Add(gui._DiffuseMap != null ? gui._DiffuseMap : gui._DiffuseMapOriginal, gui._DiffuseMap != null ? MapType.diffuse : MapType.diffuseOriginal, "_DiffuseMap");
        Add(gui._NormalMap, MapType.normal, "_NormalMap");
        Add(gui._MetallicMap, MapType.metallic, "_MetallicMap");
        Add(gui._SmoothnessMap, MapType.smoothness, "_SmoothnessMap");
        Add(gui._EdgeMap, MapType.edge, "_EdgeMap");
        Add(gui._AOMap, MapType.ao, "_AOMap");
        Add(gui._EmissionMap, MapType.emission, "_MceEmissionMap");
        Add(gui._SubsurfaceMap, MapType.subsurface, "_MceSubsurfaceMap");
        if (layers.Count == 0) { Notifications.Error("Retouch: open or create a map first."); return; }
        if (gui._HDHeightMap != null)
        {
            workHd = new RenderTexture(gui._HDHeightMap.width, gui._HDHeightMap.height, 0, gui._HDHeightMap.format, RenderTextureReadWrite.Linear) { wrapMode = TextureWrapMode.Repeat };
            Graphics.Blit(gui._HDHeightMap, workHd);
        }
        if (!ReadPlane(gui)) { Release(); Notifications.Error("Retouch: the preview plane cannot be read."); return; }
        if (material == null) material = new Material(Shader.Find("Hidden/Blit_Retouch")) { hideFlags = HideFlags.HideAndDontSave };
        // Flat plane while painting: the pointer must hit where the texture is drawn.
        displacementWasOn = gui.RetouchFlatPlane(true);
        hasSource = false; stroking = false; dabs = 0;
        windowRect.x = UiShell.ListWidth + 12;
        windowRect.y = UiShell.Top + 12;
        Active = true;
        gui.SetMaterialValues();
        Show(gui);
    }

    static void Add(Texture2D map, MapType type, string slot)
    {
        if (map == null) return;
        var rt = new RenderTexture(map.width, map.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        Graphics.Blit(map, rt);
        layers.Add(new Layer { Type = type, Work = rt, Slot = slot });
    }

    static void Show(MainGui gui)
    {
        foreach (var l in layers)
        {
            if (l.Slot.StartsWith("_Mce")) Shader.SetGlobalTexture(l.Slot, l.Work);
            else gui.FullMaterial.SetTexture(l.Slot, l.Work);
        }
    }

    static void Release()
    {
        foreach (var l in layers) if (l.Work != null) { l.Work.Release(); UnityEngine.Object.Destroy(l.Work); }
        layers.Clear();
        if (workHd != null) { workHd.Release(); UnityEngine.Object.Destroy(workHd); workHd = null; }
    }

    static void Cancel(MainGui gui)
    {
        if (applying) return;
        Release();
        End(gui);
    }

    static void End(MainGui gui)
    {
        Active = false;
        cursorVisible = false;
        if (displacementWasOn) gui.RetouchFlatPlane(false);
        gui.SetMaterialValues();
    }

    static IEnumerator Apply(MainGui gui)
    {
        applying = true;
        yield return null;
        foreach (var l in layers)
        {
            Texture2D result = null;
            yield return gui.StartCoroutine(GpuReadback.Into(l.Work, t => result = t));
            if (result == null) continue;
            Texture2D old = null;
            switch (l.Type)
            {
                case MapType.height: old = gui._HeightMap; gui._HeightMap = result; break;
                case MapType.diffuse: old = gui._DiffuseMap; gui._DiffuseMap = result; break;
                case MapType.diffuseOriginal: old = gui._DiffuseMapOriginal; gui._DiffuseMapOriginal = result; break;
                case MapType.normal: old = gui._NormalMap; gui._NormalMap = result; break;
                case MapType.metallic: old = gui._MetallicMap; gui._MetallicMap = result; break;
                case MapType.smoothness: old = gui._SmoothnessMap; gui._SmoothnessMap = result; break;
                case MapType.edge: old = gui._EdgeMap; gui._EdgeMap = result; break;
                case MapType.ao: old = gui._AOMap; gui._AOMap = result; break;
                case MapType.emission: old = gui._EmissionMap; gui._EmissionMap = result; break;
                case MapType.subsurface: old = gui._SubsurfaceMap; gui._SubsurfaceMap = result; break;
            }
            if (old != null) UnityEngine.Object.Destroy(old);
        }
        if (workHd != null)
        {
            // The painted full-precision height becomes the one the tools read.
            if (gui._HDHeightMap != null) gui._HDHeightMap.Release();
            gui._HDHeightMap = workHd;
            workHd = null;
        }
        int count = layers.Count;
        Release();
        applying = false;
        End(gui);
        Notifications.Info(string.Format(L.T("Retouch applied to {0} maps."), count));
    }

    // ---------- Painting ----------

    /// <summary>One dab on one map (also used by the editor test).</summary>
    public static void DabInto(RenderTexture work, Material mat, Vector2 dest, Vector2 sourceOffset, float brushRadius, float brushHardness, float brushOpacity)
    {
        float larger = Mathf.Max(work.width, work.height);
        mat.SetVector("_Dest", dest);
        mat.SetVector("_Offset", sourceOffset);
        mat.SetVector("_Aspect", new Vector2(work.width / larger, work.height / larger));
        mat.SetFloat("_Radius", brushRadius);
        mat.SetFloat("_Hardness", brushHardness);
        mat.SetFloat("_Opacity", brushOpacity);
        var temp = RenderTexture.GetTemporary(work.width, work.height, 0, work.format, RenderTextureReadWrite.Linear);
        Graphics.Blit(work, temp, mat, 0);
        Graphics.CopyTexture(temp, 0, 0, work, 0, 0);
        RenderTexture.ReleaseTemporary(temp);
    }

    static void Dab(Vector2 uv)
    {
        foreach (var l in layers) DabInto(l.Work, material, uv, offset, radius, hardness, opacity);
        if (workHd != null) DabInto(workHd, material, uv, offset, radius, hardness, opacity);
        dabs++;
    }

    /// <summary>Called from MainGui.Update.</summary>
    public static void Tick(MainGui gui)
    {
        if (!Active || applying) return;
        Show(gui);   // other code resets the material's textures: the working copies stay on screen
        cursorVisible = false;
        if (Input.GetMouseButtonUp(0)) stroking = false;

        Vector2 m = Input.mousePosition;
        float guiY = Screen.height - m.y;
        bool overPanels = guiY < UiShell.Top || m.x < UiShell.ListWidth || m.x > Screen.width - UiShell.RightPanel || windowRect.Contains(new Vector2(m.x, guiY));
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (overPanels || alt) { stroking = false; return; }

        if (!Raycast(gui, m, out Vector2 uv, out Vector3 world)) { stroking = false; return; }

        // The brush outline at the pointer.
        var cam = Camera.main;
        float tiling = Mathf.Max(0.01f, gui.FullMaterial.GetVector("_Tiling").x);
        float worldRadius = radius * worldPerUv / tiling;
        Vector3 a = cam.WorldToScreenPoint(world), b = cam.WorldToScreenPoint(world + cam.transform.right * worldRadius);
        cursorScreen = new Vector2(a.x, Screen.height - a.y);
        cursorPixels = Mathf.Abs(b.x - a.x);
        cursorVisible = true;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl)
        {
            if (Input.GetMouseButtonDown(0)) { source = uv; hasSource = true; offsetPending = true; }
            stroking = false;
            return;
        }
        if (!Input.GetMouseButton(0) || !hasSource) return;

        if (!stroking)
        {
            stroking = true;
            // The first stroke after a pick fixes the distance to the source; later strokes keep it (aligned stamp).
            if (offsetPending) { offset = source - uv; offsetPending = false; }
            lastUv = uv;
            Dab(uv);
            return;
        }
        // Dabs along the move, the short way round on the repeating texture.
        Vector2 delta = uv - lastUv;
        delta.x -= Mathf.Round(delta.x); delta.y -= Mathf.Round(delta.y);
        float length = delta.magnitude;
        if (length < radius * 0.15f) return;
        int steps = Mathf.Clamp(Mathf.CeilToInt(length / (radius * 0.3f)), 1, 24);
        for (int i = 1; i <= steps; i++)
        {
            Vector2 p = lastUv + delta * (i / (float)steps);
            Dab(new Vector2(Mathf.Repeat(p.x, 1f), Mathf.Repeat(p.y, 1f)));
        }
        lastUv = uv;
    }

    static bool ReadPlane(MainGui gui)
    {
        var filter = gui.testObject != null ? gui.testObject.GetComponent<MeshFilter>() : null;
        return filter != null && ReadMesh(filter.sharedMesh, gui.testObject.transform);
    }

    /// <summary>Keeps the triangles and texture coordinates of the mesh the pointer is cast against.</summary>
    public static bool ReadMesh(Mesh mesh, Transform t)
    {
        if (mesh == null || !mesh.isReadable) return false;
        meshVertices = mesh.vertices;
        meshUvs = mesh.uv;
        meshTriangles = mesh.triangles;
        if (meshUvs.Length != meshVertices.Length || meshTriangles.Length < 3) return false;
        // World size of one texture repeat, from the first triangle with some area in texture space.
        for (int i = 0; i + 2 < meshTriangles.Length; i += 3)
        {
            int a = meshTriangles[i], b = meshTriangles[i + 1];
            float uvLength = (meshUvs[b] - meshUvs[a]).magnitude;
            if (uvLength < 1e-5f) continue;
            worldPerUv = t.TransformVector(meshVertices[b] - meshVertices[a]).magnitude / uvLength;
            break;
        }
        return true;
    }

    /// <summary>The texture coordinate under the pointer (with the material's tiling), and the point in the world.</summary>
    static bool Raycast(MainGui gui, Vector2 screen, out Vector2 uv, out Vector3 world)
    {
        uv = Vector2.zero; world = Vector3.zero;
        var cam = Camera.main;
        if (cam == null || gui.testObject == null || !gui.testObject.activeInHierarchy) return false;
        return RaycastMesh(gui.testObject.transform, cam, screen, gui.FullMaterial.GetVector("_Tiling"), out uv, out world);
    }

    /// <summary>The same, on the mesh given to ReadMesh, for any camera (the editor test uses it).</summary>
    public static bool RaycastMesh(Transform t, Camera cam, Vector2 screen, Vector4 tile, out Vector2 uv, out Vector3 world)
    {
        uv = Vector2.zero; world = Vector3.zero;
        Ray ray = cam.ScreenPointToRay(screen);
        Vector3 origin = t.InverseTransformPoint(ray.origin), dir = t.InverseTransformDirection(ray.direction);
        Vector3 scale = t.lossyScale;
        dir = new Vector3(dir.x / Mathf.Max(1e-6f, scale.x), dir.y / Mathf.Max(1e-6f, scale.y), dir.z / Mathf.Max(1e-6f, scale.z));
        float best = float.MaxValue; bool hit = false; Vector2 hitUv = Vector2.zero;
        for (int i = 0; i + 2 < meshTriangles.Length; i += 3)
        {
            Vector3 v0 = meshVertices[meshTriangles[i]], v1 = meshVertices[meshTriangles[i + 1]], v2 = meshVertices[meshTriangles[i + 2]];
            Vector3 e1 = v1 - v0, e2 = v2 - v0, p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) continue;
            float inv = 1f / det;
            Vector3 s = origin - v0;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) continue;
            Vector3 q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < 0f || u + v > 1f) continue;
            float dist = Vector3.Dot(e2, q) * inv;
            if (dist <= 0f || dist >= best) continue;
            best = dist; hit = true;
            hitUv = meshUvs[meshTriangles[i]] * (1f - u - v) + meshUvs[meshTriangles[i + 1]] * u + meshUvs[meshTriangles[i + 2]] * v;
            world = t.TransformPoint(origin + dir * dist);
        }
        if (!hit) return false;
        uv = new Vector2(Mathf.Repeat(hitUv.x * tile.x + tile.z, 1f), Mathf.Repeat(hitUv.y * tile.y + tile.w, 1f));
        return true;
    }

    // ---------- Window ----------

    static Texture2D ring;
    static GUIStyle wrap;

    public static void Draw(MainGui gui)
    {
        if (!Active) return;
        if (cursorVisible && cursorPixels > 2f)
        {
            if (ring == null) ring = MakeRing();
            var old = GUI.color;
            GUI.color = hasSource ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.8f, 0.3f, 0.9f);
            GUI.DrawTexture(new Rect(cursorScreen.x - cursorPixels, cursorScreen.y - cursorPixels, cursorPixels * 2, cursorPixels * 2), ring);
            GUI.color = old;
        }
        windowRect = GUI.Window(87, windowRect, id => DoWindow(gui), L.T("Retouch (clone stamp)"));
        Tips.Block(windowRect);
    }

    static void DoWindow(MainGui gui)
    {
        float x = 12, y = 26, w = windowRect.width - 24;
        if (wrap == null) wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
        GUI.Label(new Rect(x, y, w, 54), L.T(hasSource ? "Drag on the plane to paint the source there. Ctrl + click picks another source." : "Ctrl + click on the plane to pick the source (a clean area)."), wrap);
        y += 58;
        Slider(ref y, x, w, "Brush size", ref radius, 0.01f, 0.3f);
        Slider(ref y, x, w, "Hardness", ref hardness, 0f, 1f);
        Slider(ref y, x, w, "Opacity", ref opacity, 0.05f, 1f);
        GUI.Label(new Rect(x, y, w, 20), L.T("T: tiling ×3, to see the seams."));
        y += 26;
        GUI.enabled = !applying;
        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
        if (GUI.Button(new Rect(x, y, (w - 6) / 2, 26), L.G("Apply", "Keeps the retouch on every map."))) gui.StartCoroutine(Apply(gui));
        GUI.backgroundColor = old;
        if (GUI.Button(new Rect(x + (w + 6) / 2, y, (w - 6) / 2, 26), L.G("Cancel", "Leaves the maps as they were."))) Cancel(gui);
        GUI.enabled = true;
        windowRect.height = y + 38;
        GUI.DragWindow();
    }

    static void Slider(ref float y, float x, float w, string label, ref float value, float min, float max)
    {
        GUI.Label(new Rect(x, y, 110, 20), L.T(label));
        value = GUI.HorizontalSlider(new Rect(x + 112, y + 6, w - 112, 12), value, min, max);
        y += 26;
    }

    static Texture2D MakeRing()
    {
        const int S = 128;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        for (int j = 0; j < S; j++)
            for (int i = 0; i < S; i++)
            {
                float d = Mathf.Sqrt((i - S / 2f + 0.5f) * (i - S / 2f + 0.5f) + (j - S / 2f + 0.5f) * (j - S / 2f + 0.5f)) / (S / 2f);
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(d - 0.96f) / 0.03f);
                px[j * S + i] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }
}
