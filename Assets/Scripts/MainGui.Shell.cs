using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Layout shared by the panels in the Materialize CE interface.</summary>
public static class UiShell
{
    public const int MenuHeight = 0;   // the menu bar was merged into the toolbar
    public const int ToolbarHeight = 46;
    public const int ListWidth = 300;
    public const int PropsWidth = 324;
    public const int PropsHeader = 64;
    public static int Top => MenuHeight + ToolbarHeight;
    /// <summary>Width taken on the right by the Material & lighting panel (0 when closed).</summary>
    public static int RightPanel;

    /// <summary>The new interface is on unless the user chose the classic look.</summary>
    public static bool Active => !Theme.Classic;

    /// <summary>
    /// Tool windows (slot 0) sit in the properties column, next to the maps; slot 1 (post process) sits at the
    /// right of the screen.
    /// </summary>
    public static Rect Dock(Rect rect, int slot = 0)
    {
        if (!Active) return rect;
        if (slot == 1) return new Rect(Screen.width - RightPanel - rect.width - 8, Top + 8, rect.width, rect.height);
        // The window's own title is hidden (the panel shows it): its content starts right under the tabs.
        return new Rect(ListWidth + 12, Top + PropsHeader - 26, rect.width, rect.height);
    }

    static GUIStyle panelWindow;

    /// <summary>A tool window; in the new interface without title or frame, as part of the properties column.</summary>
    public static Rect Window(int id, Rect rect, GUI.WindowFunction func, string title)
    {
        if (!Active) return GUI.Window(id, rect, func, title);
        // "Hide panels": the tools hide with the rest (their settings are kept).
        if (MainGui.instance != null && MainGui.instance.hideGui) return rect;
        if (panelWindow == null)
        {
            panelWindow = new GUIStyle(GUI.skin.window);
            panelWindow.normal.background = panelWindow.onNormal.background = null;
            panelWindow.hover.background = panelWindow.onHover.background = null;
            panelWindow.focused.background = panelWindow.onFocused.background = null;
            panelWindow.active.background = panelWindow.onActive.background = null;
        }
        return GUI.Window(id, rect, func, L.T(""), panelWindow);
    }
}

/// <summary>
/// Materialize CE's main interface: menu bar and a toolbar with the everyday actions on top; on the left the list
/// of maps and, beside it, the properties of the selected one (creation settings, adjustments) or the material
/// and lighting settings. The render keeps the rest of the screen.
/// </summary>
public partial class MainGui
{
    string openMenu;
    int mapMenu = -1;
    bool confirmClearAll;
    GUIStyle bigButton, heartButton;
    GUIStyle mapName, mapInfo, mapInfoLeft, menuItem, menuBar, titleStyle, wrapStyle, iconStyle;
    Texture2D frameTexture, selectedTexture;

    // Selection: 0..6 a map, MaterialRow the material, -1 a tool. Tab 0 = create, 1 = adjust.
    const int MaterialRow = 100, EnvRow = 101;
    int selected = -1;
    // Read at the first frame: PlayerPrefs cannot be read while Unity builds the object.
    bool materialPanel = true, materialPanelRead;
    Vector2 listScroll;
    float listHeight = 800;
    int propTab;

    struct MapEntry
    {
        public string Name;
        public MapType Type;
        public Func<MainGui, Texture2D> Get;
        public Func<MainGui, GameObject> Tool;
        public string CreateLabel, CreateTip, Needs;
        public Func<MainGui, bool> CanCreate;
        public Action<MainGui> Create;
    }

    static readonly MapEntry[] mapEntries =
    {
        new MapEntry { Name = "Height", Type = MapType.height, Get = g => g._HeightMap, Tool = g => g.HeightFromDiffuseGuiObject, CreateLabel = "Create",
            CreateTip = "Create the height map from the albedo: bright = high.", Needs = "an albedo or a normal map",
            CanCreate = g => g._DiffuseMapOriginal != null || g._DiffuseMap != null || g._NormalMap != null,
            Create = g => g.OpenTool(g.HeightFromDiffuseGuiObject, () => { g.HeightFromDiffuseGuiScript.NewTexture(); g.HeightFromDiffuseGuiScript.DoStuff(); }) },
        new MapEntry { Name = "Albedo", Type = MapType.diffuseOriginal, Get = g => g._DiffuseMap != null ? g._DiffuseMap : g._DiffuseMapOriginal, Tool = g => g.EditDiffuseGuiObject, CreateLabel = "Edit",
            CreateTip = "Edit the albedo: remove baked lighting and shadows, adjust colours.", Needs = "an albedo (Open one first)",
            CanCreate = g => g._DiffuseMapOriginal != null,
            Create = g => g.OpenTool(g.EditDiffuseGuiObject, () => { g.EditDiffuseGuiScript.NewTexture(); g.EditDiffuseGuiScript.DoStuff(); }) },
        new MapEntry { Name = "Normal", Type = MapType.normal, Get = g => g._NormalMap, Tool = g => g.NormalFromHeightGuiObject, CreateLabel = "Create",
            CreateTip = "Create the normal map from the height map.", Needs = "a height map",
            CanCreate = g => g._HeightMap != null,
            Create = g => g.OpenTool(g.NormalFromHeightGuiObject, () => { g.NormalFromHeightGuiScript.NewTexture(); g.NormalFromHeightGuiScript.DoStuff(); }) },
        new MapEntry { Name = "Metallic", Type = MapType.metallic, Get = g => g._MetallicMap, Tool = g => g.MetallicGuiObject, CreateLabel = "Create",
            CreateTip = "Create the metallic mask by picking the colour of the metal.", Needs = "an albedo",
            CanCreate = g => g._DiffuseMapOriginal != null || g._DiffuseMap != null,
            Create = g => g.OpenTool(g.MetallicGuiObject, () => { g.MetallicGuiScript.NewTexture(); g.MetallicGuiScript.DoStuff(); }) },
        new MapEntry { Name = "Smoothness", Type = MapType.smoothness, Get = g => g._SmoothnessMap, Tool = g => g.SmoothnessGuiObject, CreateLabel = "Create",
            CreateTip = "Create the roughness / glossiness from the albedo.", Needs = "an albedo",
            CanCreate = g => g._DiffuseMapOriginal != null || g._DiffuseMap != null,
            Create = g => g.OpenTool(g.SmoothnessGuiObject, () => { g.SmoothnessGuiScript.NewTexture(); g.SmoothnessGuiScript.DoStuff(); }) },
        new MapEntry { Name = "Edge", Type = MapType.edge, Get = g => g._EdgeMap, Tool = g => g.EdgeFromNormalGuiObject, CreateLabel = "Create",
            CreateTip = "Create the edge map (bright ridges, dark hollows) from the normal map.", Needs = "a normal map",
            CanCreate = g => g._NormalMap != null,
            Create = g => g.OpenTool(g.EdgeFromNormalGuiObject, () => { g.EdgeFromNormalGuiScript.NewTexture(); g.EdgeFromNormalGuiScript.DoStuff(); }) },
        new MapEntry { Name = "Ambient occlusion", Type = MapType.ao, Get = g => g._AOMap, Tool = g => g.AOFromNormalGuiObject, CreateLabel = "Create",
            CreateTip = "Create the ambient occlusion from the normal and height maps.", Needs = "a normal or a height map",
            CanCreate = g => g._NormalMap != null || g._HeightMap != null,
            Create = g => g.OpenTool(g.AOFromNormalGuiObject, () => { g.AOFromNormalGuiScript.NewTexture(); g.AOFromNormalGuiScript.DoStuff(); }) },
    };

    /// <summary>The name a map row shows in the current workflow.</summary>
    static string DisplayName(MapEntry e)
    {
        if (e.Type == MapType.metallic) return L.T(Workflow.MetalName);
        if (e.Type == MapType.smoothness) return L.T(Workflow.SurfaceName);
        return L.T(e.Name);
    }

    void OpenTool(GameObject tool, Action start)
    {
        MapAdjust.End(this);
        CloseWindows();
        FixSize();
        tool.SetActive(true);
        start();
    }

    void EnsureShellStyles()
    {
        if (mapName != null) return;
        // No padding and no clipping: the default label padding cut the names in half.
        mapName = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13, padding = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.UpperLeft, clipping = TextClipping.Overflow };
        mapInfo = new GUIStyle(GUI.skin.label) { fontSize = 11, padding = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.UpperRight, clipping = TextClipping.Overflow };
        mapInfo.normal.textColor = new Color(0.6f, 0.64f, 0.7f);
        mapInfoLeft = new GUIStyle(mapInfo) { alignment = TextAnchor.UpperLeft };
        titleStyle = new GUIStyle(mapName) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        iconStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
        wrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
        wrapStyle.normal.textColor = new Color(0.75f, 0.78f, 0.84f);
        menuItem = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 8, 3, 3) };
        menuBar = new GUIStyle(GUI.skin.button) { padding = new RectOffset(10, 10, 3, 3) };
        menuBar.normal.background = null;
        selectedTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        selectedTexture.SetPixel(0, 0, new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.22f));
        selectedTexture.Apply();
    }

    /// <summary>The new interface, drawn instead of the old floating blocks.</summary>
    void DrawShell()
    {
        EnsureShellStyles();
        ApplyFrameColour();
        FollowOpenTool();
        bool closeMenus = Event.current.type == EventType.MouseDown;

        // ---------- Menu bar ----------

        bool f11 = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F11;
        if (f11) { Fullscreen(); Event.current.Use(); }
        HandleShortcuts();

        if (!materialPanelRead) { materialPanelRead = true; materialPanel = PlayerPrefs.GetInt("MaterializeCE.MaterialPanel", 1) == 1; }
        UiShell.RightPanel = materialPanel && !hideGui ? UiShell.PropsWidth : 0;
        DrawToolbar();
        if (!hideGui)
        {
            DrawMapsList();
            bool tool = TilingTextureMakerGuiObject.activeSelf || AlignmentGuiScript.gameObject.activeSelf;
            if ((selected >= 0 && selected < mapEntries.Length) || tool) DrawProperties();
            if (materialPanel) DrawMaterialPanel();
        }
        if (!Workspace.Chosen) DrawWorkspaceWelcome();
        DrawViewLabel();
        if (viewMenuOpen) DrawViewMenu();
        if (confirmClearAll) DrawClearAllConfirm();
        DrawSaveProjectPanel();
        if (closeMenus && Event.current.type == EventType.MouseDown && openMenu != null) { openMenu = null; }
    }

    // ---------- Keyboard shortcuts (as in Quixel Mixer) ----------

    void HandleShortcuts()
    {
        var e = Event.current;
        // Never while typing in a field.
        if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0) return;
        bool ctrl = e.control || e.command, shift = e.shift, alt = e.alt;
        bool used = true;
        if (ctrl && shift)
        {
            switch (e.keyCode)
            {
                case KeyCode.E: if (!ExportWindow.Busy) ExportWindow.Run(this); break;          // quick export
                case KeyCode.S: RunMenu("File", "Save project…"); break;                          // save as
                case KeyCode.C: ExportWindow.Toggle(); break;                                      // export channels
                case KeyCode.M: SetMaterialPanel(!materialPanel); break;                           // material editor
                default: used = false; break;
            }
        }
        else if (ctrl)
        {
            switch (e.keyCode)
            {
                case KeyCode.N: confirmClearAll = true; break;
                case KeyCode.O: RunMenu("File", "Open project…"); break;
                case KeyCode.S: if (currentProject.Length > 0) SaveProject(currentProject); else RunMenu("File", "Save project…"); break;
                case KeyCode.K: SettingsGui.instance.Toggle(); break;
                default: used = false; break;
            }
        }
        else if (!alt)
        {
            int view = -1;
            switch (e.keyCode)
            {
                case KeyCode.Space: RunMenu("View", hideGui ? "Show panels" : "Hide panels"); break;
                case KeyCode.Alpha1: case KeyCode.Keypad1: RunMenu("View", "Full material"); break;
                case KeyCode.Alpha2: case KeyCode.Keypad2: view = 1; break;   // albedo
                case KeyCode.Alpha3: case KeyCode.Keypad3: view = 3; break;   // metalness / specular
                case KeyCode.Alpha4: case KeyCode.Keypad4: view = 4; break;   // roughness / glossiness
                case KeyCode.Alpha5: case KeyCode.Keypad5: view = 2; break;   // normal
                case KeyCode.Alpha6: case KeyCode.Keypad6: view = 0; break;   // displacement (height)
                case KeyCode.Alpha7: case KeyCode.Keypad7: view = 6; break;   // ambient occlusion
                case KeyCode.Alpha8: case KeyCode.Keypad8: view = 5; break;   // edge
                case KeyCode.M: MaterialGuiScript.NextShape(); break;             // preview mesh
                case KeyCode.D: MaterialGuiScript.ToggleDisplacement(); break;    // displacement preview
                case KeyCode.T: MaterialGuiScript.ToggleTiling(); break;          // tiling preview
                case KeyCode.UpArrow: CycleEnvironment(-1); break;                // cycle HDRI
                case KeyCode.DownArrow: CycleEnvironment(1); break;
                case KeyCode.G: ToggleGrid(); break;                           // grid at level 0
                case KeyCode.Keypad0: SetView(viewPresets[0].Angle); break;   // front
                case KeyCode.Period: case KeyCode.KeypadPeriod: SetView(viewPresets[1].Angle); break;   // three-quarter
                case KeyCode.F:
                    if (CameraPanZoom.main != null) CameraPanZoom.main.Focus();
                    if (shift) ObjRotator.ResetModels();                            // + reset angle
                    break;
                default: used = false; break;
            }
            if (view >= 0)
            {
                var entry = mapEntries[view];
                var tex = ShownTexture(entry);
                if (tex != null) { SetPreviewMaterial(tex); aloneLabel = DisplayName(entry); aloneKey = (view == 0 ? 6 : view == 1 ? 2 : view == 2 ? 5 : view == 3 ? 3 : view == 4 ? 4 : view == 5 ? 8 : 7).ToString(); }
                else Notifications.Info(DisplayName(entry) + ": " + L.T("empty"));
            }
        }
        else used = false;
        if (used) e.Use();
    }

    // ---------- Toolbar ----------

    void DrawToolbar()
    {
        var bar = new Rect(-2, UiShell.MenuHeight - 1, Screen.width + 4, UiShell.ToolbarHeight + 1);
        GUI.Box(bar, GUIContent.none);
        float x = 8, y = UiShell.MenuHeight + 8, h = 30;
        bool Tool(string label, string tip)
        {
            float w = GUI.skin.button.CalcSize(L.G(label)).x + 14;
            bool clicked = GUI.Button(new Rect(x, y, w, h), L.G(label, tip));
            x += w + 4;
            return clicked;
        }
        void Gap() { x += 10; GUI.Box(new Rect(x - 7, y + 3, 1, h - 6), GUIContent.none); }

        if (Tool("Tile maps", "Make every map seamless (tiling).")) RunMenu("Tools", "Tile maps (seamless)");
        if (Tool("Align / perspective", "Straighten a photo taken at an angle.")) RunMenu("Tools", "Adjust alignment / perspective");
        Gap();
        if (Tool("Full material", "Show every map together on the preview.")) RunMenu("View", "Full material");
        float viewX = x;
        if (Tool("View ▾", "Preset angles: front, three-quarter from above, grazing light on the relief; and reset the camera.")) viewMenuOpen = !viewMenuOpen;
        viewMenuRect = new Rect(viewX, y + h + 4, 210, 0);
        var oldTint = GUI.backgroundColor;
        if (materialPanel) GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
        if (Tool("Material & lighting", "Shows or hides the Material & lighting panel on the right: workflow, render mode, presets, light, tiling.")) SetMaterialPanel(!materialPanel);
        GUI.backgroundColor = oldTint;
        if (Tool("Post process", "Bloom, tone mapping, exposure, vignette… of the preview only.")) RunMenu("View", "Post process");
        if (Tool(hideGui ? "Show panels" : "Hide panels", "Hide the panels to look at the render alone.")) RunMenu("View", hideGui ? "Show panels" : "Hide panels");
        if (Tool(WindowMode.IsFullScreen ? "Windowed" : "Full screen", "Full screen or window (F11).")) Fullscreen();

        // On the right: settings, help, support, and Export, bigger and always at hand.
        if (bigButton == null)
        {
            bigButton = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            heartButton = new GUIStyle(GUI.skin.button) { fontSize = 14, richText = true };
        }
        float bh = 34, by = y - 2, rx = Screen.width - 8;
        rx -= 150;
        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
        if (GUI.Button(new Rect(rx, by, 150, bh), L.G("Export…", "Write all the files for your engine in one go: Skyrim, Unreal, Unity, glTF… with your own profiles."), bigButton))
            ExportWindow.Toggle();
        GUI.backgroundColor = old;
        float supportW = heartButton.CalcSize(new GUIContent("♥  " + L.T("Support"))).x + 24;
        rx -= supportW + 6;
        if (GUI.Button(new Rect(rx, by, supportW, bh), new GUIContent("<color=#FF6B7A>♥</color>  " + L.T("Support"), L.T("Support Materialize CE: PayPal, Ko-fi, GitHub.")), heartButton)) SupportLinks.Show();
        rx -= 16;
        float hw = GUI.skin.button.CalcSize(L.G("Help")).x + 18;
        rx -= hw;
        if (GUI.Button(new Rect(rx, y, hw, h), L.G("Help", "Mouse and keyboard controls."))) ControlsGui.Toggle();
        float sw = GUI.skin.button.CalcSize(L.G("Settings")).x + 18;
        rx -= sw + 4;
        if (GUI.Button(new Rect(rx, y, sw, h), L.G("Settings", "Preferences: normal map style, file formats, DDS tool, tooltips, look…"))) SettingsGui.instance.Toggle();
        Tips.Capture(true);
        Tips.Block(bar);
    }

    // ---------- Menus ----------

    Rect menuRect;
    string[] menuItems;

    float Menu(float x, string title, string[] items)
    {
        float w = GUI.skin.button.CalcSize(L.G(title)).x + 8;
        var r = new Rect(x, 2, w, UiShell.MenuHeight - 4);
        bool isOpen = openMenu == title;
        // Hovering another title while a menu is open switches to it, as in desktop menus.
        if (openMenu != null && !isOpen && r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint) { openMenu = title; isOpen = true; }
        if (GUI.Toggle(r, isOpen, title, menuBar) != isOpen) openMenu = isOpen ? null : title;
        if (openMenu == title) { menuRect = new Rect(x, UiShell.MenuHeight, 230, 0); menuItems = items; }
        return x + w + 2;
    }

    void DrawOpenMenu()
    {
        if (openMenu == null || menuItems == null) return;
        float y = menuRect.y;
        float h = 0;
        foreach (var item in menuItems) h += item == "-" ? 8 : 26;
        GUI.Box(new Rect(menuRect.x - 2, y - 2, menuRect.width + 4, h + 6), GUIContent.none);
        foreach (var item in menuItems)
        {
            if (item == "-") { y += 8; continue; }
            if (GUI.Button(new Rect(menuRect.x, y, menuRect.width, 24), item, menuItem))
            {
                string menu = openMenu;
                openMenu = null;
                RunMenu(menu, item);
                Event.current.Use();
                return;
            }
            y += 26;
        }
        Tips.Block(new Rect(menuRect.x - 2, menuRect.y - 2, menuRect.width + 4, h + 6));
    }

    void RunMenu(string menu, string item)
    {
        switch (item)
        {
            case "Open project…": SetFileMaskProject(); fileBrowser.ShowBrowser("Load Project", LoadProject); break;
            case "Save project…": saveProjectOpen = true; break;
            case "Export…": ExportWindow.Toggle(); break;
            case "Quit": Application.Quit(); break;
            case "Clear all maps…": confirmClearAll = true; break;
            case "Tile maps (seamless)":
                if (_HeightMap == null) { Notifications.Error("Tiling needs a height map first."); break; }
                MapAdjust.End(this); CloseWindows(); FixSize(); TilingTextureMakerGuiObject.SetActive(true); TilingTextureMakerGuiScript.Initialize(); break;
            case "Adjust alignment / perspective": MapAdjust.End(this); CloseWindows(); FixSize(); AlignmentGuiScript.Initialize(); break;
            case "Full material": MapAdjust.End(this); CloseWindows(); FixSize(); MaterialGuiObject.SetActive(true); MaterialGuiScript.Initialize(); break;
            case "Post process": PostProcessGuiObject.SetActive(!PostProcessGuiObject.activeSelf); break;
            case "Next environment":
                envSelected = "builtin:" + ((envSelected != null && envSelected.StartsWith("builtin:") ? selectedCubemap + 1 : 0) % CubeMaps.Length);
                SaveEnvironment();
                ApplyEnvironment(true);
                break;
            case "Hide panels": hideGui = true; HideWindows(); break;
            case "Show panels":
                hideGui = false;
                for (int i = 0; i < objectsToUnhide.Count; i++) objectsToUnhide[i].SetActive(true);
                break;
            case "Full screen (F11)": case "Windowed (F11)": Fullscreen(); break;
            case "Preferences…": SettingsGui.instance.Toggle(); break;
            case "Mouse and keys": ControlsGui.Toggle(); break;
            case "Support Materialize CE ♥": SupportLinks.Show(); break;
        }
    }

    void DrawClearAllConfirm()
    {
        var r = new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 100);
        GUI.Window(80, r, id =>
        {
            GUI.Label(new Rect(12, 24, 276, 36), L.T("Empty every map? Unsaved work will be lost."));
            if (GUI.Button(new Rect(12, 64, 130, 26), L.T("Clear everything")))
            {
                confirmClearAll = false;
                MapAdjust.End(this);
                ClearAllTextures();
                CloseWindows();
                SetMaterialValues();
                FixSizeSize(1024.0f, 1024.0f);
            }
            if (GUI.Button(new Rect(158, 64, 130, 26), L.T("Cancel"))) confirmClearAll = false;
        }, "Clear all maps");
        Tips.Block(r);
    }

    // ---------- Maps list ----------

    /// <summary>A creation tool opened (from its button, a menu or a shortcut): its map becomes the selection.</summary>
    void FollowOpenTool()
    {
        if (Event.current.type != EventType.Layout) return;
        for (int i = 0; i < mapEntries.Length; i++)
            if (mapEntries[i].Tool(this).activeSelf)
            {
                if (selected != i || propTab != 0) { MapAdjust.End(this); selected = i; propTab = 0; }
                return;
            }
        if (TilingTextureMakerGuiObject.activeSelf || AlignmentGuiScript.gameObject.activeSelf)
        {
            if (selected != -1) { MapAdjust.End(this); selected = -1; }
        }
    }

    // ---------- What the preview shows (a single map) ----------

    string aloneLabel = "", aloneKey = "";
    GUIStyle viewLabelStyle;

    /// <summary>When the preview shows one map alone (number keys, Preview alone): its name on top of the view.</summary>
    void DrawViewLabel()
    {
        if (hideGui || aloneLabel.Length == 0 || testObject == null) return;
        var rend = testObject.GetComponent<Renderer>();
        if (rend == null || rend.sharedMaterial != SampleMaterial) { aloneLabel = ""; return; }
        if (viewLabelStyle == null)
            viewLabelStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(14, 14, 6, 6), richText = true };
        string text = "<b>" + aloneLabel + "</b>" + (aloneKey.Length > 0 ? "  (" + aloneKey + ")" : "") + "   ·   " + L.T("1 = full material");
        var content = new GUIContent(text);
        var size = viewLabelStyle.CalcSize(content);
        float left = UiShell.ListWidth + ((selected >= 0 && selected < mapEntries.Length) ? UiShell.PropsWidth : 0);
        float right = Screen.width - UiShell.RightPanel;
        GUI.Box(new Rect(left + (right - left - size.x) / 2, UiShell.Top + 10, size.x, size.y), content, viewLabelStyle);
    }

    // ---------- View presets ----------

    bool viewMenuOpen;
    Rect viewMenuRect;

    static readonly (string Name, string Tip, Vector3 Angle)[] viewPresets =
    {
        ("Front", "The material flat, facing you: the texture as it is.", new Vector3(0, 0, 0)),
        ("Three-quarter from above", "Seen from above at an angle, as on the ground or a wall in a game.", new Vector3(55, -25, 0)),
        ("From above", "Looking down on it, slightly tilted.", new Vector3(70, 0, 0)),
        ("Grazing (relief)", "Almost edge-on: shows the depth of the relief and the parallax.", new Vector3(78, -35, 0)),
    };

    // ---------- Grid at level 0 ----------

    GameObject grid;
    bool GridVisible => grid != null && grid.activeSelf;

    void ToggleGrid()
    {
        if (grid == null)
        {
            var shader = Shader.Find("Hidden/Preview_Grid");
            if (shader == null || testObject == null) return;
            grid = GameObject.CreatePrimitive(PrimitiveType.Quad);
            grid.name = "Level 0 grid";
            var col = grid.GetComponent<Collider>();
            if (col != null) Destroy(col);
            grid.GetComponent<Renderer>().sharedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            grid.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // On the plane, at its zero level (height = the displacement offset): the plane lies in its local XY.
            grid.transform.SetParent(testObject.transform, false);
            grid.transform.localPosition = Vector3.zero;
            grid.transform.localRotation = Quaternion.identity;
            grid.transform.localScale = new Vector3(20, 20, 1);
            grid.SetActive(false);
        }
        grid.SetActive(!grid.activeSelf);
        PlayerPrefs.SetInt("MaterializeCE.Grid", grid.activeSelf ? 1 : 0);
    }

    void SetView(Vector3 angle)
    {
        ObjRotator.SetModelView(angle);
        if (CameraPanZoom.main != null) CameraPanZoom.main.Focus();
    }

    void DrawViewMenu()
    {
        var r = viewMenuRect;
        r.height = (viewPresets.Length + 2) * 26 + 12;
        GUI.Window(86, r, id =>
        {
            float iy = 4;
            foreach (var v in viewPresets)
            {
                if (GUI.Button(new Rect(4, iy, r.width - 8, 24), L.G(v.Name, v.Tip), menuItem)) { SetView(v.Angle); viewMenuOpen = false; }
                iy += 26;
            }
            iy += 4;
            if (GUI.Button(new Rect(4, iy, r.width - 8, 24), L.G((GridVisible ? "● " : "") + "Grid at level 0 (G)", "A reference grid at the plane's zero level: what the displacement raises covers it, what sinks shows under it."), menuItem)) { ToggleGrid(); viewMenuOpen = false; }
            iy += 26;
            if (GUI.Button(new Rect(4, iy, r.width - 8, 24), L.G("Reset the camera (F)", "Back to the starting framing, keeping the angle."), menuItem)) { if (CameraPanZoom.main != null) CameraPanZoom.main.Focus(); viewMenuOpen = false; }
            Tips.Capture(true);
        }, "");
        GUI.BringWindowToFront(86);
        Tips.Block(r);
        if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition) && !new Rect(r.x, r.y - 40, 90, 40).Contains(Event.current.mousePosition)) viewMenuOpen = false;
    }

    void SetMaterialPanel(bool open)
    {
        materialPanel = open;
        PlayerPrefs.SetInt("MaterializeCE.MaterialPanel", open ? 1 : 0);
        PlayerPrefs.Save();
    }

    void Select(int i)
    {
        MapAdjust.End(this);
        // A second click on the selected map closes its properties.
        if (selected == i) { selected = -1; CloseWindows(); SetMaterialValues(); return; }
        CloseWindows();
        selected = i;
        var e = mapEntries[i];
        if (e.Get(this) != null && propTab == 1) MapAdjust.Begin(this, e.Type, DisplayName(e));
        SetMaterialValues();
    }

    /// <summary>A click on a row that no button of the row took selects it.</summary>
    void RowClick(Rect row, int index)
    {
        var ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && row.Contains(ev.mousePosition)) { Select(index); ev.Use(); }
    }

    void DrawMapsList()
    {
        int w = UiShell.ListWidth;
        var side = new Rect(0, UiShell.Top, w, Screen.height - UiShell.Top);
        GUI.Box(side, GUIContent.none);
        float block = ProjectBlockHeight();
        var view = new Rect(0, side.y + 4, w, side.height - 4 - block);
        listScroll = GUI.BeginScrollView(view, listScroll, new Rect(0, 0, w - 16, listHeight));
        float cw = listHeight > view.height ? w - 16 : w;
        GUI.Label(new Rect(12, 6, cw - 24, 16), L.T("MAPS"), mapInfoLeft);
        float y = 26;
        for (int i = 0; i < mapEntries.Length; i++) y = DrawMapRow(i, y, cw);

        // The environment, under the maps.
        y += 10;
        GUI.Box(new Rect(12, y, cw - 24, 1), GUIContent.none);
        y += 10;
        y = DrawEnvironmentPanel(12, y, cw - 24);
        listHeight = y + 12;
        GUI.EndScrollView();

        DrawProjectBlock(new Rect(0, side.yMax - block, w, block));
        if (mapMenu >= 0) DrawMapMenu(mapEntries[mapMenu], view.y + mapMenuY - listScroll.y);
        if (profileListOpen) DrawProfileList();
        Tips.Capture(true);
        Tips.Block(side);
    }

    // ---------- Project & export (bottom of the left column) ----------

    const string RecentKey = "MaterializeCE.RecentProjects";
    string currentProject = "";
    bool profileListOpen;
    Rect profileButton;
    GUIStyle exportButton, compactButton, fieldLabel;

    List<string> RecentProjects()
    {
        var list = new List<string>();
        foreach (var p in PlayerPrefs.GetString(RecentKey, "").Split('|')) if (p.Length > 0 && System.IO.File.Exists(p)) list.Add(p);
        return list;
    }

    /// <summary>The project just opened or saved: current, and first of the recent ones.</summary>
    void RememberProject(string path)
    {
        currentProject = path;
        var list = RecentProjects();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > 5) list.RemoveRange(5, list.Count - 5);
        PlayerPrefs.SetString(RecentKey, string.Join("|", list.ToArray()));
        PlayerPrefs.Save();
        // The export takes the project's name.
        ExportWindow.BaseName = System.IO.Path.GetFileNameWithoutExtension(path);
    }

    float ProjectBlockHeight()
    {
        int recent = Mathf.Min(3, RecentProjects().FindAll(p => !string.Equals(p, currentProject, StringComparison.OrdinalIgnoreCase)).Count);
        return 118 + (recent > 0 ? 20 + recent * 23 : 0) + 200;
    }

    void DrawProjectBlock(Rect r)
    {
        GUI.Box(new Rect(r.x, r.y, r.width, 1), GUIContent.none);
        float x = r.x + 12, w = r.width - 24, y = r.y + 10;

        // ----- Project -----
        GUI.Label(new Rect(x, y, w, 16), L.T("PROJECT"), mapInfoLeft);
        y += 20;
        string name = currentProject.Length > 0 ? System.IO.Path.GetFileNameWithoutExtension(currentProject) : L.T("Unsaved project");
        GUI.Label(new Rect(x, y, w, 20), new GUIContent(name, currentProject), mapName);
        y += 26;
        float bw = (w - 9) / 4f;
        if (compactButton == null) compactButton = new GUIStyle(GUI.skin.button) { padding = new RectOffset(2, 2, 3, 3), clipping = TextClipping.Clip };
        if (GUI.Button(new Rect(x, y, bw, 26), L.G("New", "Empty every map to start a new material."), compactButton)) confirmClearAll = true;
        if (GUI.Button(new Rect(x + bw + 3, y, bw, 26), L.G("Open…", "Open a Materialize project (.mtz) with all its maps."), compactButton)) RunMenu("File", "Open project…");
        if (GUI.Button(new Rect(x + 2 * (bw + 3), y, bw, 26), L.G("Save", "Save the project again where it is (or choose where, the first time)."), compactButton))
        {
            if (currentProject.Length > 0) SaveProject(currentProject);
            else RunMenu("File", "Save project…");
        }
        if (GUI.Button(new Rect(x + 3 * (bw + 3), y, bw, 26), L.G("Save as…", "Save the project under another name or in another folder, and choose the maps' file format."), compactButton)) RunMenu("File", "Save project…");
        y += 34;
        var recent = RecentProjects().FindAll(p => !string.Equals(p, currentProject, StringComparison.OrdinalIgnoreCase));
        if (recent.Count > 0)
        {
            GUI.Label(new Rect(x, y, w, 16), L.T("Recent"), mapInfoLeft);
            y += 20;
            for (int i = 0; i < Mathf.Min(3, recent.Count); i++)
            {
                if (GUI.Button(new Rect(x, y, w, 21), new GUIContent("  " + System.IO.Path.GetFileNameWithoutExtension(recent[i]), recent[i]), menuItem))
                    LoadProject(recent[i]);
                y += 23;
            }
        }
        y += 8;

        // ----- Export -----
        GUI.Box(new Rect(x, y, w, 1), GUIContent.none);
        y += 10;
        GUI.Label(new Rect(x, y, w, 16), L.T("EXPORT"), mapInfoLeft);
        y += 20;
        var profiles = ExportWindow.Profiles;
        profileButton = new Rect(x, y, w, 26);
        if (GUI.Button(profileButton, L.G(profiles[ExportWindow.Selected].Label + "  ▾", "Which engine or game the files are for (export profile).")))
            profileListOpen = !profileListOpen;
        y += 30;
        if (fieldLabel == null) fieldLabel = new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
        GUI.Label(new Rect(x, y, 56, 22), L.G("Name", "Base name of the files; each file adds its suffix: stone + _n = stone_n."), fieldLabel);
        ExportWindow.BaseName = GUI.TextField(new Rect(x + 58, y, w - 58, 22), ExportWindow.BaseName ?? "");
        y += 28;
        GUI.Label(new Rect(x, y, 56, 22), L.G("Folder", "Where the files are written."), fieldLabel);
        string folderName = string.IsNullOrEmpty(ExportWindow.Folder) ? "…" : System.IO.Path.GetFileName(ExportWindow.Folder.TrimEnd('\\', '/'));
        if (GUI.Button(new Rect(x + 58, y, w - 58, 22), new GUIContent(folderName + "  …", ExportWindow.Folder + "\n" + L.T("Click to choose another folder.")))) ExportWindow.PickFolder(this);
        y += 30;
        if (exportButton == null) exportButton = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.45f, 0.72f, 1f);
        GUI.enabled = !ExportWindow.Busy;
        if (GUI.Button(new Rect(x, y, w - 42, 40), L.G(ExportWindow.Busy ? "Exporting…" : "Export", "Writes every file of the profile, with this name, in this folder."), exportButton))
            ExportWindow.Run(this);
        GUI.enabled = true;
        GUI.backgroundColor = old;
        if (GUI.Button(new Rect(x + w - 38, y, 38, 40), L.G("•••", "Details: the files of the profile, their channels, formats and compression; edit and save profiles."))) ExportWindow.Toggle();
        y += 46;
        if (ExportWindow.Busy)
        {
            // Progress bar while the files are written.
            GUI.DrawTexture(new Rect(x, y, w, 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(1, 1, 1, 0.08f), 0, 3);
            GUI.DrawTexture(new Rect(x, y, Mathf.Max(6, w * Mathf.Clamp01(ExportWindow.Progress)), 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, Theme.Accent, 0, 3);
            y += 10;
        }
        if (ExportWindow.Status.Length > 0) GUI.Label(new Rect(x, y, w, 18), new GUIContent(ExportWindow.Status, ExportWindow.Status), mapInfoLeft);
    }

    /// <summary>The profiles, as a list opening upwards from the profile button.</summary>
    void DrawProfileList()
    {
        var profiles = ExportWindow.Profiles;
        float h = profiles.Count * 24 + 8;
        var r = new Rect(profileButton.x, UiShell.Top + profileButton.y - h - 2 + 0, profileButton.width, h);
        // profileButton is in the column's coordinates, which are the screen's (no scroll there).
        r.y = profileButton.y - h - 2;
        // A window: it takes the clicks before the buttons underneath.
        GUI.Window(84, r, id =>
        {
            float y = 4;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (GUI.Button(new Rect(4, y, r.width - 8, 22), (i == ExportWindow.Selected ? "● " : "") + profiles[i].Label, menuItem))
                { ExportWindow.Selected = i; profileListOpen = false; }
                y += 24;
            }
        }, "");
        GUI.BringWindowToFront(84);
        if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition) && !profileButton.Contains(Event.current.mousePosition)) profileListOpen = false;
    }

    float mapMenuY;

    float DrawMapRow(int i, float y, float w)
    {
        var e = mapEntries[i];
        Texture2D tex = e.Get(this);
        var row = new Rect(4, y, w - 8, 54);
        if (selected == i) GUI.DrawTexture(row, selectedTexture);

        var thumb = new Rect(12, y + 5, 44, 44);
        GUI.Box(thumb, GUIContent.none);
        Texture shown = Workflow.IsDerived(e.Type) ? Workflow.ThumbOf(e.Type) : tex;
        if (tex != null && shown != null) GUI.DrawTexture(new Rect(thumb.x + 2, thumb.y + 2, 40, 40), shown, ScaleMode.ScaleToFit);
        string name = DisplayName(e) + (MapAdjust.HasPending(e.Type) ? "  ●" : "");
        GUI.Label(new Rect(64, y + 6, w - 150, 18), name, mapName);
        GUI.Label(new Rect(w - 96, y + 7, 84, 16), tex != null ? tex.width + " × " + tex.height : L.T("empty"), mapInfo);

        float bx = 64, by = y + 27;
        bool derivedSpec = e.Type == MapType.metallic && Workflow.Specular;
        GUI.enabled = e.CanCreate(this);
        if (GUI.Button(new Rect(bx, by, 60, 22), L.G(e.CreateLabel, derivedSpec ? "The specular colour is made from the albedo and a metallic mask: create the mask by picking the metal." : e.CreateTip)))
        {
            e.Create(this);
            selected = i; propTab = 0;
        }
        GUI.enabled = !derivedSpec;
        if (GUI.Button(new Rect(bx + 64, by, 52, 22), L.G("Open", derivedSpec ? "The specular map is computed: open a metallic mask in the Metallic workflow (Material & lighting)." :
            e.Type == MapType.smoothness && !Workflow.Gloss ? "Open a roughness map (white = rough)." : "Load an image file into this map (PNG, JPG, TGA, TIFF, EXR, DDS…).")))
        { OpenMap(e); Select(i); }
        GUI.enabled = tex != null;
        if (GUI.Button(new Rect(bx + 120, by, 50, 22), L.G("Save", "Save this map to a file (" + selectedFormat.ToString().ToUpperInvariant() + ")."))) SaveMap(e, false);
        GUI.enabled = true;
        if (GUI.Button(new Rect(bx + 174, by, 30, 22), L.G("⋯", "More: paste, copy, quick save, preview alone, clear."))) { mapMenu = mapMenu == i ? -1 : i; mapMenuY = y + 50; }

        RowClick(row, i);
        return y + 58;
    }

    string RenderModeName()
    {
        switch (MaterialGuiScript != null ? MaterialGuiScript.RenderModeIndex : 0)
        {
            case 1: return "Skyrim SE";
            case 2: return "Skyrim CM";
            default: return "PBR";
        }
    }

    void DrawMapMenu(MapEntry e, float y)
    {
        Texture2D tex = e.Get(this);
        bool roughOpen = e.Type == MapType.smoothness && !Workflow.Gloss;
        var items = new List<(string Label, string Tip, bool Enabled, Action Run)>
        {
            ("Paste", "Put the image from the clipboard into this map.", !(e.Type == MapType.metallic && Workflow.Specular),
                () => { mapTypeToLoad = e.Type; Workflow.InvertNextSmoothnessLoad = roughOpen; PasteFile(); }),
            ("Copy", "Put this map on the clipboard.", tex != null, () => { textureToSave = ShownTexture(e); CopyFile(); }),
            ("Quick save", "Save again to the last file this map was saved to.", tex != null && QuickPath(e.Type) != "", () => SaveMap(e, true)),
            ("Preview alone", "Show only this map on the preview.", tex != null, () => { SetPreviewMaterial(ShownTexture(e)); aloneLabel = DisplayName(e); aloneKey = ""; }),
            ("Clear", "Empty this map.", tex != null, () => { MapAdjust.End(this); ClearTexture(e.Type == MapType.diffuseOriginal ? MapType.diffuse : e.Type); CloseWindows(); SetMaterialValues(); FixSize(); }),
        };
        var r = new Rect(UiShell.ListWidth - 180, y, 170, items.Count * 26 + 8);
        Tips.Block(r);
        // A window: it takes the clicks before the rows underneath.
        GUI.Window(85, r, id =>
        {
            float iy = 4;
            foreach (var it in items)
            {
                GUI.enabled = it.Enabled;
                if (GUI.Button(new Rect(4, iy, r.width - 8, 24), L.G(it.Label, it.Tip), menuItem)) { mapMenu = -1; it.Run(); }
                iy += 26;
            }
            GUI.enabled = true;
            Tips.Capture(true);
        }, "");
        GUI.BringWindowToFront(85);
        if (Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition)) mapMenu = -1;
    }

    /// <summary>The map as the row shows it: roughness and specular are built from smoothness and metallic.</summary>
    Texture2D ShownTexture(MapEntry e)
    {
        var tex = e.Get(this);
        if (tex == null || !Workflow.IsDerived(e.Type)) return tex;
        var packed = ChannelPacker.Pack(this, Workflow.Channels(e.Type), out string missing);
        if (missing != null) Notifications.Error("Missing for " + DisplayName(e) + ": " + missing);
        return packed;
    }

    void OpenMap(MapEntry e)
    {
        mapTypeToLoad = e.Type;
        Workflow.InvertNextSmoothnessLoad = e.Type == MapType.smoothness && !Workflow.Gloss;
        SetFileMaskImage();
        fileBrowser.ShowBrowser("Open " + DisplayName(e) + " Map", OpenFile);
    }

    string QuickPath(MapType type)
    {
        switch (type)
        {
            case MapType.height: return QuicksavePathHeight;
            case MapType.diffuseOriginal: return QuicksavePathDiffuse;
            case MapType.normal: return QuicksavePathNormal;
            case MapType.metallic: return QuicksavePathMetallic;
            case MapType.smoothness: return QuicksavePathSmoothness;
            case MapType.edge: return QuicksavePathEdge;
            case MapType.ao: return QuicksavePathAO;
        }
        return "";
    }

    void SaveMap(MapEntry e, bool quick)
    {
        textureToSave = ShownTexture(e);
        if (textureToSave == null) return;
        if (quick) { SaveFile(QuickPath(e.Type)); return; }
        SetFileMaskImage();
        fileBrowser.ShowBrowser("Save " + DisplayName(e) + " Map", SaveFile);
    }

    // ---------- Properties ----------

    Vector2 matScroll, envScroll;
    float matHeight = 900;

    void DrawProperties()
    {
        var col = new Rect(UiShell.ListWidth, UiShell.Top, UiShell.PropsWidth, Screen.height - UiShell.Top);
        GUI.Box(col, GUIContent.none);
        float x = col.x + 12, w = col.width - 24, y = col.y + 10;

        if (selected >= 0 && selected < mapEntries.Length)
        {
            var e = mapEntries[selected];
            var tex = e.Get(this);
            GUI.Label(new Rect(x, y, w, 24), DisplayName(e), titleStyle);
            y += 30;
            int tab = GUI.Toolbar(new Rect(x, y, w, 24), propTab, new[] {
                L.G(e.CreateLabel, "The settings that generate this map from the others."),
                L.G("Adjust", "Levels, contrast, strength… of the map as it is, imported or created.") });
            if (tab != propTab)
            {
                propTab = tab;
                if (propTab == 1) { CloseWindows(); if (tex != null) MapAdjust.Begin(this, e.Type, DisplayName(e)); SetMaterialValues(); }
                else MapAdjust.End(this);
            }
            y = col.y + UiShell.PropsHeader;

            if (propTab == 0 && !e.Tool(this).activeSelf)
            {
                // The tool window docks here when open; until then, what it does and how to start it.
                bool derivedSpec = e.Type == MapType.metallic && Workflow.Specular;
                GUI.Label(new Rect(x, y, w, 60), L.T(derivedSpec ? "The specular colour is made from the albedo and a metallic mask (white = metal). Create the mask by picking the colour of the metal." : e.CreateTip), wrapStyle);
                y += 60;
                bool can = e.CanCreate(this);
                GUI.enabled = can;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
                string what = derivedSpec ? L.T("Create the metallic mask") : string.Format(L.T(e.CreateLabel == "Edit" ? "Edit the {0} map" : "Create the {0} map"), DisplayName(e).ToLowerInvariant());
                if (GUI.Button(new Rect(x, y, w, 32), what)) e.Create(this);
                GUI.backgroundColor = old;
                GUI.enabled = true;
                if (!can) GUI.Label(new Rect(x, y + 38, w, 40), L.T("Needs ") + L.T(e.Needs) + ".", wrapStyle);
            }
            else if (propTab == 1)
            {
                if (tex == null)
                    GUI.Label(new Rect(x, y, w, 40), L.T("Open or create this map first."), wrapStyle);
                else
                {
                    if (!MapAdjust.IsActive(e.Type)) MapAdjust.Begin(this, e.Type, DisplayName(e));
                    GUI.BeginGroup(new Rect(x, y, w, col.yMax - y));
                    MapAdjust.DrawInline(this, 0, w);
                    GUI.EndGroup();
                }
            }
        }
        else
        {
            GUI.Label(new Rect(x, y, w, 24), L.T(TilingTextureMakerGuiObject.activeSelf ? "Tile maps (seamless)" : "Align / perspective"), titleStyle);
        }
        Tips.Capture(true);
        Tips.Block(col);
    }

    /// <summary>Material & lighting, on the right of the screen: workflow, render mode, presets, light, tiling, shape.</summary>
    void DrawMaterialPanel()
    {
        int pw = UiShell.PropsWidth;
        var col = new Rect(Screen.width - pw, UiShell.Top, pw, Screen.height - UiShell.Top);
        GUI.Box(col, GUIContent.none);
        float x = col.x + 12, w = col.width - 24, y = col.y + 10;
        GUI.Label(new Rect(x, y, w - 40, 24), L.T("Material & lighting"), titleStyle);
        if (GUI.Button(new Rect(col.xMax - 36, y, 26, 24), L.G("×", "Hide this panel (the Material & lighting button brings it back)."))) SetMaterialPanel(false);
        y += 30;
        if (!MaterialGuiObject.activeSelf)
        {
            if (GUI.Button(new Rect(x, y, w, 26), L.G("Show the full material", "Every map together on the preview."))) ShowFullMaterial();
            y += 32;
        }
        matScroll = GUI.BeginScrollView(new Rect(x, y, w + 12, col.yMax - y - 6), matScroll, new Rect(0, 0, w - 6, matHeight));
        matHeight = MaterialGuiScript.DrawInline(0) + 10;
        GUI.EndScrollView();
        Tips.Capture(true);
        Tips.Block(col);
    }

    /// <summary>First launch: where the user's work goes (projects, textures, presets, HDRIs, add-ons).</summary>
    void DrawWorkspaceWelcome()
    {
        var r = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 130, 500, 260);
        GUI.Window(83, r, id =>
        {
            GUI.Label(new Rect(16, 30, 468, 60), L.T("Choose the folder where Materialize CE keeps your work: projects, saved textures, export profiles, render presets, HDRIs and add-ons. Pick a short, easy to find place."), wrapStyle);
            GUI.Label(new Rect(16, 96, 468, 20), L.T("Suggested:"), mapInfoLeft);
            GUI.Label(new Rect(16, 116, 468, 22), Workspace.Default, mapName);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            if (GUI.Button(new Rect(16, 156, 228, 34), L.T("Use this folder"))) Workspace.Set(Workspace.Default);
            GUI.backgroundColor = old;
            if (GUI.Button(new Rect(256, 156, 228, 34), L.T("Choose another folder…")))
            {
                string picked = Workspace.PickFolder(L.T("Materialize CE folder"));
                if (!string.IsNullOrEmpty(picked)) Workspace.Set(System.IO.Path.GetFileName(picked).StartsWith("Materialize") ? picked : System.IO.Path.Combine(picked, "Materialize CE"));
            }
            GUI.Label(new Rect(16, 204, 468, 40), L.T("Subfolders are created inside: Projects, Textures, Export profiles, Render presets, HDRI, Addons. You can change it later in Settings."), mapInfoLeft);
        }, L.T("Welcome to Materialize CE"));
        Tips.Block(r);
    }

    /// <summary>
    /// A thinner frame: its outer edge (at ±5.354, the plane being ±5) comes in to ±5.08 and its rim, which stood
    /// above the surface, drops to just over it. Done on a copy of the mesh, the scene is untouched.
    /// </summary>
    static void ThinFrame(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount > 500) return;
        var mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
        var v = mesh.vertices;
        for (int i = 0; i < v.Length; i++)
        {
            if (Mathf.Abs(v[i].x) > 5.01f) v[i].x = Mathf.Sign(v[i].x) * 5.08f;
            if (Mathf.Abs(v[i].y) > 5.01f) v[i].y = Mathf.Sign(v[i].y) * 5.08f;
            if (v[i].z > 0.01f) v[i].z = 0.03f;
        }
        mesh.vertices = v;
        mesh.RecalculateBounds();
        mf.mesh = mesh;
    }

    /// <summary>The frame around the preview plane takes the interface's colour.</summary>
    void ApplyFrameColour()
    {
        if (frameTexture != null) return;
        frameTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        frameTexture.SetPixel(0, 0, Theme.FrameColour);
        frameTexture.Apply();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Box_Material"))
            {
                r.material.SetTexture("_DiffuseMap", frameTexture);
                ThinFrame(r);
                r.enabled = false;   // the frame is hidden: only the material shows
                // Matte: a glossy frame mirrors the sky and looks pale blue instead of the interface's colour.
                r.material.SetTexture("_SmoothnessMap", Texture2D.blackTexture);
                r.material.SetTexture("_MetallicMap", Texture2D.blackTexture);
                r.material.SetFloat("_Smoothness", 0f);
            }
    }
}
