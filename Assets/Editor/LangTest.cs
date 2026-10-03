using System.IO;
using UnityEngine;

/// <summary>Checks the French file: how many interface texts it covers. -executeMethod LangTest.Run</summary>
public static class LangTest
{
    public static void Run()
    {
        L.Language = "fr";
        var lines = File.ReadAllLines(@"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad\strings.txt");
        int done = 0; var missing = new System.Text.StringBuilder();
        foreach (var s in lines) { if (L.T(s) != s) done++; else missing.AppendLine(s); }
        Debug.Log($"LANGTEST {done}/{lines.Length} translated. Samples: '{L.T(" Show tooltips")}' '{L.T("Needs ")}' '{L.T("Contrast:")}' '{L.T("DDS: ")}'");
        File.WriteAllText(@"C:\Users\cleme\AppData\Local\Temp\claude\E--Claude-Dev\90fcc055-d8e5-4510-97a7-122983977505\scratchpad\missing.txt", missing.ToString());
        Debug.Log("LANGTEST languages: " + string.Join(", ", L.Available().ConvertAll(l => l.Code + "=" + l.Name)));
    }
}
