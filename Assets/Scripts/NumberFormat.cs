using System.Globalization;
using UnityEngine;

/// <summary>
/// Materialize CE: numbers are written with a decimal point whatever the language of Windows (1.65, not 1,65),
/// so the numeric keypad can be used. The value fields still accept a comma (see GuiHelper.ParseFloat).
/// </summary>
public static class NumberFormat
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }
}
