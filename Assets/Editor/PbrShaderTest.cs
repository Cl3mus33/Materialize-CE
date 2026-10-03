using UnityEngine;

/// <summary>Checks that Preview_PBR compiles and is supported. -executeMethod PbrShaderTest.Run</summary>
public static class PbrShaderTest
{
    public static void Run()
    {
        var s = Shader.Find(System.Environment.GetEnvironmentVariable("MCE_SHADER") ?? "Custom/Preview_PBR");
        Debug.Log("PBRTEST found=" + (s != null) + " supported=" + (s != null && s.isSupported) + " passes=" + (s == null ? 0 : new Material(s).passCount));
#if UNITY_EDITOR
        if (s != null)
        {
            var msgs = UnityEditor.ShaderUtil.GetShaderMessages(s);
            foreach (var m in msgs) Debug.Log("PBRTEST msg " + m.severity + ": " + m.message + " line " + m.line);
            Debug.Log("PBRTEST errors=" + UnityEditor.ShaderUtil.ShaderHasError(s));
        }
#endif
    }
}
