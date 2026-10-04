using UnityEngine;

/// <summary>
/// "Support" button and panel (Materialize CE): donations and the project page. Links with an empty address
/// are not shown, so the panel only lists what is set here.
/// </summary>
public static class SupportLinks
{
    // ---- Fill in the addresses (https:// only) ----
    public const string PayPal = "https://www.paypal.com/donate/?business=clemus3dart%40gmail.com&no_recurring=0&currency_code=EUR";
    public const string KoFi = "https://ko-fi.com/clemus";
    public const string GitHub = "https://github.com/Cl3mus33";
    // ------------------------------------------------

    static bool open;
    static Rect windowRect;

    static bool HasAny => PayPal.Length > 0 || KoFi.Length > 0 || GitHub.Length > 0;

    /// <summary>Called from MainGui.OnGUI: the button in the top right corner and its panel.</summary>
    /// <summary>Opens the panel (the new interface calls this from Help).</summary>
    public static void Show()
    {
        open = true;
        windowRect = new Rect(Screen.width - 330, UiShell.Top + 8, 310, 0);
    }

    public static void Draw()
    {
        if (!HasAny) return;
        if (!UiShell.Active)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.6f);
            if (GUI.Button(new Rect(Screen.width - 260, 10, 140, 30), L.G("♥ Support", "Help Materialize CE: donations, and the project page.")))
            {
                if (open) open = false; else Show();
            }
            GUI.backgroundColor = old;
        }
        if (!open) return;

        int rows = (PayPal.Length > 0 ? 1 : 0) + (KoFi.Length > 0 ? 1 : 0) + (GitHub.Length > 0 ? 1 : 0);
        windowRect.height = 116 + rows * 36;
        windowRect = GUI.Window(78, windowRect, DoWindow, L.T("Support Materialize CE"));
        Tips.Block(windowRect);
    }

    static void DoWindow(int id)
    {
        GUI.Label(new Rect(12, 22, 290, 40), L.T("Materialize CE is free and open source. If it helps you, you can support its development:"));
        int y = 62;
        Link(ref y, "Donate with PayPal", PayPal, "A one-off donation through PayPal.");
        Link(ref y, "Buy me a coffee (Ko-fi)", KoFi, "A tip or regular support through Ko-fi.");
        Link(ref y, "Project page (GitHub)", GitHub, "Source code, new versions, and a place to report problems or suggest features.");
        if (GUI.Button(new Rect(windowRect.width - 92, windowRect.height - 38, 80, 26), L.T("Close"))) open = false;
        Tips.Capture(true);
        GUI.DragWindow();
    }

    static void Link(ref int y, string label, string url, string tip)
    {
        if (url.Length == 0) return;
        if (GUI.Button(new Rect(12, y, 286, 30), L.G(label, tip + "\n" + url)) && url.StartsWith("https://"))
            Application.OpenURL(url);
        y += 36;
    }
}
