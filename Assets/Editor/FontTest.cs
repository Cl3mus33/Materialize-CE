using UnityEngine;
/// <summary>-executeMethod FontTest.Run</summary>
public static class FontTest
{
    public static void Run()
    {
        var f = Resources.Load<Font>("Fonts/OpenSans-Regular");
        Debug.Log("FONTTEST " + (f == null ? "NOT FOUND" : f.name + " size=" + f.fontSize + " dynamic=" + f.dynamic + " names=" + string.Join(",", f.fontNames)));
    }
}
