using UnityEngine;

/// <summary>
/// Materialize CE's look: a flat dark theme built in code (rounded panels, blue accent, round slider handles,
/// Segoe UI), replacing Unity's default embossed IMGUI skin. Metrics stay close to the default skin, so the
/// hand-placed panels keep their layout. Every OnGUI calls Apply() first; "Classic look" in Settings turns it off.
/// </summary>
public static class Theme
{
    public static bool Classic;

    public static readonly Color Accent = Hex(0x3D8BFD);
    static readonly Color Panel = Hex(0x1C1F25), PanelBorder = Hex(0x323843);
    static readonly Color Box = Hex(0x23272F), BoxBorder = Hex(0x2F3540);
    static readonly Color Button = Hex(0x323845), ButtonHover = Hex(0x3C4452), ButtonActive = Hex(0x2D6FD6);
    static readonly Color Field = Hex(0x15171C), FieldBorder = Hex(0x3A4150);
    static readonly Color Text = Hex(0xE3E7EE), TextDim = Hex(0x9AA3B2), Track = Hex(0x3A4150);

    static GUISkin skin;

    /// <summary>Colour of the frame around the preview plane, matching the panels.</summary>
    public static Color FrameColour => Hex(0x2A2F38);
    /// <summary>The window's title bar, same as the menu bar under it.</summary>
    public static Color TitleBarColour => Hex(0x23272F);

    /// <summary>The interface font: "Open Sans" (as in Quixel Mixer, included) or "Segoe UI" (Windows').</summary>
    public static string FontChoice
    {
        get => PlayerPrefs.GetString("MaterializeCE.Font", "Open Sans");
        set { PlayerPrefs.SetString("MaterializeCE.Font", value); PlayerPrefs.Save(); skin = null; }
    }

    public static void Apply()
    {
        if (Classic) return;
        if (skin == null) skin = Build();
        GUI.skin = skin;
    }

    static GUISkin Build()
    {
        var s = Object.Instantiate(GUI.skin);
        s.name = "Materialize CE";
        s.hideFlags = HideFlags.HideAndDontSave;
        // Open Sans ships with Materialize CE (SIL Open Font License, see StreamingAssets/Licenses).
        Font font = FontChoice == "Open Sans" ? Resources.Load<Font>("Fonts/OpenSans-Regular") : null;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 12);
        s.font = font;

        Style(s.label, null, Text);
        s.label.normal.background = null;

        Style(s.box, Rounded(Box, BoxBorder, 5), TextDim);
        s.box.border = new RectOffset(6, 6, 6, 6);
        s.box.fontStyle = FontStyle.Bold;

        Style(s.window, Rounded(Panel, PanelBorder, 7), Text);
        s.window.onNormal.background = s.window.normal.background;
        s.window.onNormal.textColor = Text;
        s.window.border = new RectOffset(8, 8, 8, 8);
        s.window.fontStyle = FontStyle.Bold;

        StyleButton(s.button);
        var selected = Rounded(ButtonActive, ButtonActive, 4);
        s.button.onNormal.background = selected;
        s.button.onHover.background = Rounded(Lighter(ButtonActive, 0.08f), ButtonActive, 4);
        s.button.onActive.background = selected;
        s.button.onNormal.textColor = s.button.onHover.textColor = s.button.onActive.textColor = Color.white;

        Style(s.textField, Rounded(Field, FieldBorder, 4), Text);
        s.textField.focused.background = Rounded(Field, Accent, 4);
        s.textField.focused.textColor = Color.white;
        s.textField.hover.background = Rounded(Field, Lighter(FieldBorder, 0.1f), 4);
        s.textField.border = new RectOffset(5, 5, 5, 5);
        s.textField.padding = new RectOffset(5, 5, 3, 3);
        s.textArea.normal = s.textField.normal; s.textArea.focused = s.textField.focused; s.textArea.border = s.textField.border;

        // Checkbox: rounded square, blue with a tick when checked.
        var off = CheckBox(false, Track);
        var offHover = CheckBox(false, Lighter(Track, 0.15f));
        var on = CheckBox(true, Accent);
        s.toggle.normal.background = off; s.toggle.hover.background = offHover; s.toggle.active.background = offHover;
        s.toggle.onNormal.background = on; s.toggle.onHover.background = on; s.toggle.onActive.background = on;
        s.toggle.normal.textColor = s.toggle.hover.textColor = s.toggle.active.textColor = Text;
        s.toggle.onNormal.textColor = s.toggle.onHover.textColor = s.toggle.onActive.textColor = Color.white;
        // Unity's toggle keeps its box in the top-left 14 px (border 14, 0, 14, 0): same layout here.
        s.toggle.border = new RectOffset(14, 0, 14, 0);

        // Sliders: Unity's metrics kept (the panels place them by hand); only the images change: a thin
        // rounded track centred in the default height, and a round handle of the default size.
        int trackH = Mathf.Max(8, (int)s.horizontalSlider.fixedHeight > 0 ? (int)s.horizontalSlider.fixedHeight : 12);
        s.horizontalSlider.normal.background = Track2D(16, trackH, true);
        s.horizontalSlider.border = new RectOffset(4, 4, 0, 0);
        int trackW = Mathf.Max(8, (int)s.verticalSlider.fixedWidth > 0 ? (int)s.verticalSlider.fixedWidth : 12);
        s.verticalSlider.normal.background = Track2D(trackW, 16, false);
        s.verticalSlider.border = new RectOffset(0, 0, 4, 4);
        Thumb(s.horizontalSliderThumb);
        Thumb(s.verticalSliderThumb);

        // Thin, quiet scrollbars.
        s.verticalScrollbar.normal.background = Rounded(Field, Field, 3, 8);
        s.verticalScrollbarThumb.normal.background = Rounded(ButtonHover, ButtonHover, 3, 8);
        s.horizontalScrollbar.normal.background = Rounded(Field, Field, 3, 8);
        s.horizontalScrollbarThumb.normal.background = Rounded(ButtonHover, ButtonHover, 3, 8);

        return s;
    }

    static void StyleButton(GUIStyle b)
    {
        b.normal.background = Rounded(Button, Lighter(Button, 0.06f), 4);
        b.hover.background = Rounded(ButtonHover, Lighter(ButtonHover, 0.08f), 4);
        b.active.background = Rounded(ButtonActive, ButtonActive, 4);
        b.focused.background = b.normal.background;
        b.normal.textColor = b.focused.textColor = Text;
        b.hover.textColor = Color.white;
        b.active.textColor = Color.white;
        b.border = new RectOffset(5, 5, 5, 5);
        b.overflow = new RectOffset(0, 0, 0, 0);
    }

    static void Style(GUIStyle st, Texture2D background, Color text)
    {
        st.normal.background = background;
        st.hover.background = background;
        st.normal.textColor = st.hover.textColor = st.active.textColor = st.focused.textColor = text;
    }

    static void Thumb(GUIStyle st)
    {
        int size = Mathf.Max(10, Mathf.RoundToInt(Mathf.Max(st.fixedWidth, st.fixedHeight)));
        var normal = Circle(size, Hex(0xDDE3EC), Accent);
        var hover = Circle(size, Color.white, Lighter(Accent, 0.1f));
        st.normal.background = normal;
        st.hover.background = hover;
        st.active.background = Circle(size, Accent, Color.white);
        st.focused.background = normal;
        st.border = new RectOffset(0, 0, 0, 0);
    }

    /// <summary>A 4 px rounded bar across the middle of a transparent texture (slider tracks).</summary>
    static Texture2D Track2D(int w, int h, bool horizontal)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        float half = 2f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = horizontal ? Mathf.Abs(y + 0.5f - h / 2f) : Mathf.Abs(x + 0.5f - w / 2f);
                float along = horizontal ? Mathf.Min(x + 0.5f, w - x - 0.5f) : Mathf.Min(y + 0.5f, h - y - 0.5f);
                float d = along < half ? Vector2.Distance(new Vector2(half - along, across), Vector2.zero) : across;
                Color c = Track;
                c.a = Mathf.Clamp01(half - d + 0.5f);
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---------- Procedural textures (9-sliced by the styles' borders) ----------

    static Texture2D Rounded(Color fill, Color border, int radius, int size = 16)
    {
        size = Mathf.Max(size, radius * 2 + 4);
        var tex = NewTexture(size);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance to the rounded rectangle's edge (negative inside).
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius;
                float edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                if (d <= 0) d = -edge;
                float alpha = Mathf.Clamp01(0.5f - d);
                Color c = d > -1.0f ? border : fill;
                c.a *= alpha;
                px[y * size + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D Circle(int size, Color fill, Color ring)
    {
        var tex = NewTexture(size);
        var px = new Color[size * size];
        float r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                Color c = d > r - 2.2f ? ring : fill;
                c.a *= Mathf.Clamp01(r - d + 0.5f);
                px[y * size + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D CheckBox(bool on, Color color)
    {
        const int size = 16, boxSize = 14;
        var small = Rounded(on ? color : Field, color, 3, boxSize);
        var box = NewTexture(size);
        var px = new Color[size * size];
        var sp = small.GetPixels();
        // Rows go bottom to top: the box fills the top 14 rows and the left 14 columns.
        for (int y = 0; y < boxSize; y++)
            for (int x = 0; x < boxSize; x++)
                px[(y + size - boxSize) * size + x] = sp[y * boxSize + x];
        Object.Destroy(small);
        if (!on) { box.SetPixels(px); box.Apply(); return box; }
        // A white tick: two strokes.
        void Line(Vector2 a, Vector2 b)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    float d = Vector2.Distance(p, a + ab * t);
                    float alpha = Mathf.Clamp01(1.6f - d);
                    if (alpha > 0) px[y * size + x] = Color.Lerp(px[y * size + x], Color.white, alpha);
                }
        }
        // Texture rows go bottom to top.
        Line(new Vector2(3.5f, 9.5f), new Vector2(6f, 6.5f));
        Line(new Vector2(6f, 6.5f), new Vector2(11f, 12.5f));
        box.SetPixels(px);
        box.Apply();
        return box;
    }

    static Texture2D NewTexture(int size) =>
        new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
    static Color Lighter(Color c, float amount) => new Color(Mathf.Min(1, c.r + amount), Mathf.Min(1, c.g + amount), Mathf.Min(1, c.b + amount), c.a);
}
