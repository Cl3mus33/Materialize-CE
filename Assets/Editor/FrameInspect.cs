using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;

/// <summary>-executeMethod FrameInspect.Run</summary>
public static class FrameInspect
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/MainScene.unity");
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.sharedMaterial == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            var m = mf ? mf.sharedMesh : null;
            string path = r.name; var p = r.transform.parent; while (p != null) { path = p.name + "/" + path; p = p.parent; }
            Debug.Log($"FRAME {path} mat={r.sharedMaterial.name} mesh={(m ? m.name : "-")} verts={(m ? m.vertexCount : 0)} bounds={(m ? m.bounds.ToString() : "")} scale={r.transform.localScale} pos={r.transform.localPosition} rot={r.transform.localEulerAngles}");
            if (m && r.sharedMaterial.name.StartsWith("Box") && m.vertexCount < 200)
                Debug.Log("FRAME xs " + string.Join(",", m.vertices.Select(v => v.x.ToString("0.###")).Distinct()) + " | ys " + string.Join(",", m.vertices.Select(v => v.y.ToString("0.###")).Distinct()) + " | zs " + string.Join(",", m.vertices.Select(v => v.z.ToString("0.###")).Distinct()));
        }
    }
}
