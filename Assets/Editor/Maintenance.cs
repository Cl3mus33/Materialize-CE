using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Project housekeeping run from the command line (-executeMethod Maintenance.X).</summary>
public static class Maintenance
{
    /// <summary>Text serialisation: scenes and assets become readable and diffable on GitHub.</summary>
    public static void ForceText()
    {
        EditorSettings.serializationMode = SerializationMode.ForceText;
        AssetDatabase.ForceReserializeAssets();
        AssetDatabase.SaveAssets();
        Debug.Log("MAINTENANCE: project reserialised as text");
    }

    /// <summary>Lists, then removes, components whose script no longer exists in the main scene.</summary>
    public static void RemoveMissingScripts()
    {
        var scene = EditorSceneManager.OpenScene("Assets/MainScene.unity");
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (count == 0) continue;
                Debug.Log($"MAINTENANCE: {count} missing script(s) on '{t.gameObject.name}'");
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"MAINTENANCE: removed {removed} missing script component(s)");
    }
}
