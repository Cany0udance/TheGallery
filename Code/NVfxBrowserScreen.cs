using System.Reflection;
using Godot;
using Godot.Collections;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace TheGallery;

public partial class NVfxBrowserScreen : NSubmenu
{
    // ── Layout constants ──────────────────────────────────────────────
    private const float Padding = 16f;
    private const float TabBarHeight = 44f;
    private const float TabWidth = 110f;
    private const float TabStartX = 24f;
    private const float IndicatorHeight = 3f;
    private const float SearchHeight = 36f;
    private const float ToolbarHeight = 40f;
    private const float GridSpacing = 48f;
    private const float CrosshairSize = 20f;
    private const string VfxRootDir = "res://scenes/vfx";

    // ── Palette ───────────────────────────────────────────────────────
    private static readonly Color BgColor       = new(0.07f, 0.07f, 0.1f,  1f);
    private static readonly Color PanelBg       = new(0.1f,  0.1f,  0.15f, 1f);
    private static readonly Color PanelBorder   = new(0.18f, 0.18f, 0.25f, 1f);
    private static readonly Color PreviewBg     = new(0.05f, 0.05f, 0.07f, 1f);
    private static readonly Color Accent        = new(0.4f,  0.55f, 0.95f, 1f);
    private static readonly Color TextBright    = new(0.9f,  0.9f,  0.93f, 1f);
    private static readonly Color TextNormal    = new(0.72f, 0.72f, 0.78f, 1f);
    private static readonly Color TextDim       = new(0.45f, 0.45f, 0.52f, 1f);
    private static readonly Color FolderText    = new(0.55f, 0.65f, 0.88f, 1f);
    private static readonly Color EntryHoverBg  = new(0.15f, 0.15f, 0.22f, 1f);
    private static readonly Color EntryActiveBg = new(0.18f, 0.22f, 0.35f, 1f);
    private static readonly Color SearchBg      = new(0.08f, 0.08f, 0.12f, 1f);
    private static readonly Color SearchBorder  = new(0.2f,  0.2f,  0.28f, 1f);
    private static readonly Color ToolbarBg     = new(0.09f, 0.09f, 0.13f, 1f);
    private static readonly Color GridLineColor = new(0.08f, 0.08f, 0.11f, 1f);
    private static readonly Color ErrorText     = new(0.9f,  0.35f, 0.35f, 1f);
    private static readonly Color CrosshairCol  = new(0.3f,  0.35f, 0.5f,  0.4f);

    private static readonly string[] PositionFieldNames =
    {
        "_sourcePosition", "_startPosition", "_position",
        "_destinationPosition", "_targetPosition", "_endPosition"
    };

    // ── UI references ─────────────────────────────────────────────────
    private Control _vfxTab;
    private Control _audioTab;
    private Button _vfxTabButton;
    private Button _audioTabButton;
    private ColorRect _vfxTabIndicator;
    private ColorRect _audioTabIndicator;
    private Label _currentVfxLabel;
    private LineEdit _searchBox;
    private VBoxContainer _vfxListContainer;
    private Control _previewArea;
    private Button _selectedEntryButton;
    private Font _fontRegular;
    private Font _fontBold;

    // ── VFX state ─────────────────────────────────────────────────────
    private Node _currentVfxInstance;
    private readonly List<(Button button, string path, string name)> _vfxEntries = new();

    protected override Control InitialFocusedControl => null;

    // ═══════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        try
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            var screenSize = GetViewportRect().Size;
            _fontRegular = ResourceLoader.Load<Font>("res://fonts/kreon_regular.ttf");
            _fontBold = ResourceLoader.Load<Font>("res://fonts/kreon_bold.ttf");

            AddFullRectChild(new ColorRect { Color = BgColor });
            BuildTabBar(screenSize.X);
            AddColorStrip(new Vector2(0, TabBarHeight), screenSize.X, 1, PanelBorder);

            float contentTop = TabBarHeight + 1;
            float availableHeight = screenSize.Y - contentTop;

            BuildVfxTab(screenSize, contentTop, availableHeight);
            BuildAudioTab(screenSize, contentTop, availableHeight);
            BuildBackButton();

            SwitchTab(true);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[VfxBrowserScreen] Error in _Ready: {e}");
        }
    }

    protected override void ConnectSignals() => base.ConnectSignals();

    public override void OnSubmenuClosed()
    {
        base.OnSubmenuClosed();
        ClearCurrentVfx();

        _audioTab?.GetChildren()
            .OfType<NAudioBrowserTab>()
            .FirstOrDefault()
            ?.CleanupAndRestore();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Tab Bar
    // ═══════════════════════════════════════════════════════════════════

    private void BuildTabBar(float screenWidth)
    {
        var tabBarBg = new ColorRect
        {
            Color = PanelBg,
            Position = Vector2.Zero,
            Size = new Vector2(screenWidth, TabBarHeight)
        };
        AddChild(tabBarBg);

        float audioTabX = TabStartX + TabWidth + 8;

        _vfxTabButton = CreateTabButton("VFX", TabStartX, () => SwitchTab(true));
        AddChild(_vfxTabButton);
        _vfxTabIndicator = CreateTabIndicator(TabStartX);
        AddChild(_vfxTabIndicator);

        _audioTabButton = CreateTabButton("Audio", audioTabX, () => SwitchTab(false));
        AddChild(_audioTabButton);
        _audioTabIndicator = CreateTabIndicator(audioTabX);
        _audioTabIndicator.Visible = false;
        AddChild(_audioTabIndicator);
    }

    private Button CreateTabButton(string text, float x, Action onPressed)
    {
        var btn = new Button
        {
            Text = text,
            Flat = true,
            Position = new Vector2(x, 0),
            Size = new Vector2(TabWidth, TabBarHeight)
        };
        ApplyFont(btn, _fontBold, 16);
        btn.Pressed += onPressed;
        return btn;
    }

    private static ColorRect CreateTabIndicator(float x) => new()
    {
        Color = Accent,
        Position = new Vector2(x, TabBarHeight - IndicatorHeight),
        Size = new Vector2(TabWidth, IndicatorHeight)
    };

    private void SwitchTab(bool showVfx)
    {
        _vfxTab.Visible = showVfx;
        _audioTab.Visible = !showVfx;

        SetTabButtonColors(_vfxTabButton, showVfx);
        SetTabButtonColors(_audioTabButton, !showVfx);

        _vfxTabIndicator.Visible = showVfx;
        _audioTabIndicator.Visible = !showVfx;
    }

    private static void SetTabButtonColors(Button btn, bool active)
    {
        btn.AddThemeColorOverride("font_color", active ? TextBright : TextDim);
        btn.AddThemeColorOverride("font_hover_color", active ? TextBright : TextNormal);
    }

    // ═══════════════════════════════════════════════════════════════════
    // VFX Tab
    // ═══════════════════════════════════════════════════════════════════

    private void BuildVfxTab(Vector2 screenSize, float contentTop, float availableHeight)
    {
        _vfxTab = CreateTabPanel("VfxTab", screenSize.X, contentTop, availableHeight);
        AddChild(_vfxTab);

        float listWidth = screenSize.X * 0.32f;
        float previewX = Padding + listWidth + 16f;
        float previewWidth = screenSize.X - previewX - Padding;

        BuildVfxListPanel(_vfxTab, listWidth, availableHeight);
        BuildPreviewPanel(_vfxTab, previewX, previewWidth, availableHeight);
    }

    private void BuildVfxListPanel(Control parent, float listWidth, float availableHeight)
    {
        // Panel background
        var panelBg = CreateRoundedRect(PanelBg, PanelBorder, 8, 1);
        panelBg.Position = new Vector2(Padding, Padding);
        panelBg.Size = new Vector2(listWidth, availableHeight - Padding * 2);
        parent.AddChild(panelBg);

        // Search box
        float searchY = Padding + 12;
        _searchBox = new LineEdit
        {
            PlaceholderText = "Search effects...",
            ClearButtonEnabled = true,
            Position = new Vector2(Padding + 12, searchY),
            Size = new Vector2(listWidth - 24, SearchHeight)
        };
        ApplyFont(_searchBox, _fontRegular, 14);
        ApplySearchBoxStyle(_searchBox);
        _searchBox.TextChanged += OnSearchTextChanged;
        parent.AddChild(_searchBox);

        // Entry count
        var countLabel = new Label
        {
            Name = "EntryCount",
            HorizontalAlignment = HorizontalAlignment.Right,
            Position = new Vector2(Padding + 12, searchY + SearchHeight + 6),
            Size = new Vector2(listWidth - 24, 18)
        };
        ApplyFont(countLabel, _fontRegular, 11);
        countLabel.AddThemeColorOverride("font_color", TextDim);
        parent.AddChild(countLabel);

        // Separator
        float sepY = searchY + SearchHeight + 28;
        AddColorStrip(parent, new Vector2(Padding + 12, sepY), listWidth - 24, 1, PanelBorder);

        // Scrollable list
        float listTop = sepY + 8;
        float listBottom = availableHeight - Padding - 12;

        var listScroll = new ScrollContainer
        {
            Position = new Vector2(Padding + 8, listTop),
            Size = new Vector2(listWidth - 16, listBottom - listTop),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        parent.AddChild(listScroll);

        _vfxListContainer = new VBoxContainer
        {
            Name = "VfxList",
            CustomMinimumSize = new Vector2(listWidth - 36, 0)
        };
        _vfxListContainer.AddThemeConstantOverride("separation", 1);
        listScroll.AddChild(_vfxListContainer);

        ScanVfxDirectory(_vfxListContainer);
        countLabel.Text = $"{_vfxEntries.Count} effects";
    }

    private void BuildPreviewPanel(Control parent, float previewX, float previewWidth, float availableHeight)
    {
        // Toolbar
        var toolbarBg = CreateRoundedRect(ToolbarBg, PanelBorder, 8, 1, true, true, false, false);
        toolbarBg.Position = new Vector2(previewX, Padding);
        toolbarBg.Size = new Vector2(previewWidth, ToolbarHeight);
        parent.AddChild(toolbarBg);

        _currentVfxLabel = new Label
        {
            Text = "No effect selected",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(previewX + 16, Padding + 2),
            Size = new Vector2(previewWidth - 110, ToolbarHeight - 4)
        };
        ApplyFont(_currentVfxLabel, _fontBold, 14);
        _currentVfxLabel.AddThemeColorOverride("font_color", TextDim);
        parent.AddChild(_currentVfxLabel);

        var clearButton = new Button
        {
            Text = "Clear",
            Flat = true,
            Position = new Vector2(previewX + previewWidth - 80, Padding + 6),
            Size = new Vector2(68, ToolbarHeight - 12)
        };
        ApplyFont(clearButton, _fontRegular, 13);
        clearButton.AddThemeColorOverride("font_color", TextNormal);
        clearButton.AddThemeColorOverride("font_hover_color", TextBright);
        clearButton.Pressed += ClearCurrentVfx;
        parent.AddChild(clearButton);

        // Preview area
        float previewTop = Padding + ToolbarHeight;
        float previewHeight = availableHeight - Padding * 2 - ToolbarHeight;

        var previewPanelBg = CreateRoundedRect(PreviewBg, PanelBorder, 8, 1, false, false, true, true);
        previewPanelBg.Position = new Vector2(previewX, previewTop);
        previewPanelBg.Size = new Vector2(previewWidth, previewHeight);
        parent.AddChild(previewPanelBg);

        BuildPreviewGrid(parent, previewX, previewTop, previewWidth, previewHeight);
        BuildCrosshair(parent, previewX, previewTop, previewWidth, previewHeight);

        _previewArea = new Control
        {
            Name = "PreviewArea",
            Position = new Vector2(previewX, previewTop),
            Size = new Vector2(previewWidth, previewHeight),
            ClipContents = true
        };
        parent.AddChild(_previewArea);
    }

    private static void BuildPreviewGrid(Control parent, float x, float y, float width, float height)
    {
        for (float gx = GridSpacing; gx < width; gx += GridSpacing)
            AddColorStrip(parent, new Vector2(x + gx, y), 1, height, GridLineColor);

        for (float gy = GridSpacing; gy < height; gy += GridSpacing)
            AddColorStrip(parent, new Vector2(x, y + gy), width, 1, GridLineColor);
    }

    private static void BuildCrosshair(Control parent, float x, float y, float width, float height)
    {
        float cx = x + width / 2;
        float cy = y + height / 2;

        AddColorStrip(parent, new Vector2(cx - CrosshairSize, cy), CrosshairSize * 2, 1, CrosshairCol);
        AddColorStrip(parent, new Vector2(cx, cy - CrosshairSize), 1, CrosshairSize * 2, CrosshairCol);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Audio Tab
    // ═══════════════════════════════════════════════════════════════════

    private void BuildAudioTab(Vector2 screenSize, float contentTop, float availableHeight)
    {
        _audioTab = CreateTabPanel("AudioTab", screenSize.X, contentTop, availableHeight);
        _audioTab.Visible = false;
        AddChild(_audioTab);

        var audioBrowser = new NAudioBrowserTab();
        audioBrowser.SetAnchorsPreset(LayoutPreset.FullRect);
        _audioTab.AddChild(audioBrowser);
        audioBrowser.Initialize();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Back Button
    // ═══════════════════════════════════════════════════════════════════

    private void BuildBackButton()
    {
        var backButtonScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/back_button.tscn");
        var backButton = backButtonScene.Instantiate<NBackButton>();
        backButton.Name = "BackButton";
        AddChild(backButton);

        ConnectSignals();

        var backButtonField = typeof(NSubmenu).GetField("_backButton",
            BindingFlags.NonPublic | BindingFlags.Instance);
        (backButtonField?.GetValue(this) as NBackButton)?.Enable();
    }

    // ═══════════════════════════════════════════════════════════════════
    // VFX Directory Scanning
    // ═══════════════════════════════════════════════════════════════════

    private void ScanVfxDirectory(VBoxContainer listContainer, string dirPath = VfxRootDir, int depth = 0)
    {
        var dir = DirAccess.Open(dirPath);
        if (dir == null) return;

        dir.ListDirBegin();
        var subdirs = new List<string>();
        var scenes = new List<string>();

        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (dir.CurrentIsDir() && !fileName.StartsWith("."))
                subdirs.Add(fileName);
            else if (fileName.EndsWith(".tscn"))
                scenes.Add(fileName);
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        subdirs.Sort();
        scenes.Sort();

        foreach (var subdir in subdirs)
            AddFolderEntry(listContainer, dirPath, subdir, depth);

        foreach (var scene in scenes)
            AddSceneEntry(listContainer, dirPath + "/" + scene, scene, depth);
    }

    private void AddFolderEntry(VBoxContainer listContainer, string parentPath, string folderName, int depth)
    {
        var folderMargin = CreateIndentedMargin(depth * 18);
        listContainer.AddChild(folderMargin);

        var folderButton = new Button
        {
            Text = "▼  " + folderName,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        ApplyFont(folderButton, _fontBold, 13);
        folderButton.AddThemeColorOverride("font_color", FolderText);
        folderButton.AddThemeColorOverride("font_hover_color", Accent);
        folderMargin.AddChild(folderButton);

        int childStartIndex = listContainer.GetChildCount();
        ScanVfxDirectory(listContainer, parentPath + "/" + folderName, depth + 1);
        int childEndIndex = listContainer.GetChildCount();

        SetupCollapseToggle(folderButton, listContainer, childStartIndex, childEndIndex);
    }

    private void AddSceneEntry(VBoxContainer listContainer, string fullPath, string fileName, int depth)
    {
        string displayName = fileName.Replace(".tscn", "");

        var entryMargin = CreateIndentedMargin(depth * 18 + 12);
        listContainer.AddChild(entryMargin);

        var button = new Button
        {
            Text = displayName,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        ApplyFont(button, _fontRegular, 13);
        button.AddThemeColorOverride("font_color", TextNormal);
        button.AddThemeColorOverride("font_hover_color", TextBright);
        ApplyEntryButtonStyle(button);

        button.Pressed += () =>
        {
            if (_selectedEntryButton != null)
                ApplyEntryButtonStyle(_selectedEntryButton);
            _selectedEntryButton = button;
            ApplySelectedEntryStyle(button);
            PlayVfx(fullPath);
        };

        entryMargin.AddChild(button);
        _vfxEntries.Add((button, fullPath, displayName.ToLowerInvariant()));
    }

    private static void SetupCollapseToggle(Button headerButton, VBoxContainer container, int startIdx, int endIdx)
    {
        headerButton.Pressed += () =>
        {
            bool collapsing = true;
            for (int i = startIdx; i < endIdx; i++)
            {
                if (container.GetChild(i) is not Control child) continue;
                if (i == startIdx) collapsing = child.Visible;
                child.Visible = !collapsing;
            }
            headerButton.Text = collapsing
                ? headerButton.Text.Replace("▼", "▶")
                : headerButton.Text.Replace("▶", "▼");
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Search
    // ═══════════════════════════════════════════════════════════════════

    private void OnSearchTextChanged(string newText)
    {
        string filter = newText.Trim().ToLowerInvariant();
        bool hasFilter = !string.IsNullOrEmpty(filter);

        if (hasFilter)
            SetAllChildrenVisible(_vfxListContainer);

        foreach (var (_, _, name) in _vfxEntries)
        {
            bool match = !hasFilter || name.Contains(filter);
            var parentMargin = _vfxEntries
                .First(e => e.name == name).button.GetParent() as Control;
            if (parentMargin != null)
                parentMargin.Visible = match;
        }

        if (!hasFilter)
            SetAllChildrenVisible(_vfxListContainer);
    }

    private static void SetAllChildrenVisible(Control container)
    {
        for (int i = 0; i < container.GetChildCount(); i++)
        {
            if (container.GetChild(i) is Control child)
                child.Visible = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // VFX Playback
    // ═══════════════════════════════════════════════════════════════════

    private void PlayVfx(string scenePath)
    {
        ClearCurrentVfx();

        try
        {
            var scene = ResourceLoader.Load<PackedScene>(scenePath);
            if (scene == null)
            {
                SetVfxLabel($"Failed to load: {scenePath}", true);
                return;
            }

            _currentVfxInstance = scene.Instantiate();
            PositionVfxInstance(_currentVfxInstance);
            PreInitializeVfx(_currentVfxInstance);
            _previewArea.AddChild(_currentVfxInstance);
            RestartParticles(_currentVfxInstance);

            string displayName = scenePath.Split('/').Last().Replace(".tscn", "");
            SetVfxLabel(displayName, false);
        }
        catch (Exception e)
        {
            SetVfxLabel($"Error: {e.Message}", true);
        }
    }

    private void PositionVfxInstance(Node instance)
    {
        var center = _previewArea.Size / 2;

        if (instance is Node2D node2D)
            node2D.Position = center;
        else if (instance is Control control)
        {
            control.AnchorLeft = 0;
            control.AnchorTop = 0;
            control.AnchorRight = 0;
            control.AnchorBottom = 0;
            control.Position = center;
        }
    }

    private void PreInitializeVfx(Node instance)
    {
        var type = instance.GetType();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        Vector2 globalCenter = _previewArea.GlobalPosition + _previewArea.Size / 2;
        Vector2 sourcePos = globalCenter + new Vector2(-150, 50);
        Vector2 destPos = globalCenter + new Vector2(150, 50);

        // Map field names to appropriate positions
        var positionMap = new System.Collections.Generic.Dictionary<string, Vector2>
        {
            ["_sourcePosition"] = sourcePos,
            ["_startPosition"] = sourcePos,
            ["_destinationPosition"] = destPos,
            ["_endPosition"] = destPos,
            ["_targetPosition"] = globalCenter,
            ["_position"] = globalCenter
        };

        foreach (var (fieldName, value) in positionMap)
            SetFieldIfExists(type, instance, fieldName, value, flags);

        TryCallInitialize(type, instance, sourcePos, destPos, globalCenter);
    }

    private static void TryCallInitialize(Type type, object instance, Vector2 source, Vector2 dest, Vector2 center)
    {
        var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            if (method.Name != "Initialize") continue;
            var parameters = method.GetParameters();

            try
            {
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(Vector2) &&
                    parameters[1].ParameterType == typeof(Vector2))
                {
                    method.Invoke(instance, new object[] { source, dest });
                    return;
                }
                if (parameters.Length == 1 &&
                    parameters[0].ParameterType == typeof(Vector2))
                {
                    method.Invoke(instance, new object[] { center });
                    return;
                }
                if (parameters.Length == 0)
                {
                    method.Invoke(instance, null);
                    return;
                }
            }
            catch { }
        }
    }

    private static void SetFieldIfExists(Type type, object instance, string fieldName, Vector2 value, BindingFlags flags)
    {
        var field = type.GetField(fieldName, flags);
        if (field is { FieldType.FullName: "Godot.Vector2" })
            field.SetValue(instance, value);
    }

    private static void RestartParticles(Node root)
    {
        switch (root)
        {
            case GpuParticles2D gpu:
                gpu.Emitting = false;
                gpu.Emitting = true;
                break;
            case CpuParticles2D cpu:
                cpu.Emitting = false;
                cpu.Emitting = true;
                break;
        }

        foreach (var child in root.GetChildren())
            RestartParticles(child);
    }

    private void ClearCurrentVfx()
    {
        if (_currentVfxInstance != null && IsInstanceValid(_currentVfxInstance))
        {
            _currentVfxInstance.QueueFree();
            _currentVfxInstance = null;
        }

        if (_selectedEntryButton != null)
        {
            ApplyEntryButtonStyle(_selectedEntryButton);
            _selectedEntryButton = null;
        }

        SetVfxLabel("No effect selected", false, TextDim);
    }

    private void SetVfxLabel(string text, bool isError, Color? overrideColor = null)
    {
        _currentVfxLabel.Text = text;
        _currentVfxLabel.AddThemeColorOverride("font_color",
            overrideColor ?? (isError ? ErrorText : TextBright));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Style Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static void ApplyEntryButtonStyle(Button btn)
    {
        var normal = MakeFlatStyle(Colors.Transparent, 4, 8);
        var hover  = MakeFlatStyle(EntryHoverBg, 4, 8);

        btn.AddThemeStyleboxOverride("normal",  normal);
        btn.AddThemeStyleboxOverride("hover",   hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus",   normal);
        btn.AddThemeColorOverride("font_color", TextNormal);
    }

    private static void ApplySelectedEntryStyle(Button btn)
    {
        var style = MakeFlatStyle(EntryActiveBg, 4, 8);
        style.BorderWidthLeft = 3;
        style.BorderColor = Accent;

        btn.AddThemeStyleboxOverride("normal",  style);
        btn.AddThemeStyleboxOverride("hover",   style);
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeStyleboxOverride("focus",   style);
        btn.AddThemeColorOverride("font_color", TextBright);
    }

    private static void ApplySearchBoxStyle(LineEdit lineEdit)
    {
        lineEdit.AddThemeStyleboxOverride("normal", MakeSearchStyle(SearchBorder));
        lineEdit.AddThemeStyleboxOverride("focus",  MakeSearchStyle(Accent));
        lineEdit.AddThemeColorOverride("font_color", TextBright);
        lineEdit.AddThemeColorOverride("font_placeholder_color", TextDim);
        lineEdit.AddThemeColorOverride("caret_color", Accent);
    }

    private static void ApplyFont(Control control, Font font, int size)
    {
        if (font != null)
            control.AddThemeFontOverride("font", font);
        control.AddThemeFontSizeOverride("font_size", size);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Factory Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static Control CreateTabPanel(string name, float width, float top, float height) => new()
    {
        Name = name,
        Position = new Vector2(0, top),
        Size = new Vector2(width, height)
    };

    private static MarginContainer CreateIndentedMargin(int left)
    {
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", left);
        return margin;
    }

    private void AddColorStrip(Vector2 position, float width, float height, Color color)
    {
        AddColorStrip(this, position, width, height, color);
    }

    private static void AddColorStrip(Control parent, Vector2 position, float width, float height, Color color)
    {
        parent.AddChild(new ColorRect
        {
            Color = color,
            Position = position,
            Size = new Vector2(width, height)
        });
    }

    private static void AddFullRectChild(Control child)
    {
    }

    private static StyleBoxFlat MakeFlatStyle(Color bg, int cornerRadius, int leftMargin = 0)
    {
        var style = new StyleBoxFlat { BgColor = bg, ContentMarginLeft = leftMargin };
        SetAllCornerRadius(style, cornerRadius);
        return style;
    }

    private static StyleBoxFlat MakeSearchStyle(Color border)
    {
        var style = new StyleBoxFlat { BgColor = SearchBg, BorderColor = border };
        SetAllCornerRadius(style, 6);
        SetAllBorderWidth(style, 1);
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        return style;
    }

    private static void SetAllCornerRadius(StyleBoxFlat style, int radius)
    {
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
    }

    private static void SetAllBorderWidth(StyleBoxFlat style, int width)
    {
        style.BorderWidthTop = width;
        style.BorderWidthBottom = width;
        style.BorderWidthLeft = width;
        style.BorderWidthRight = width;
    }

    private static PanelContainer CreateRoundedRect(Color bgColor, Color borderColor, int radius, int borderWidth,
        bool topLeft = true, bool topRight = true, bool bottomLeft = true, bool bottomRight = true)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = bgColor,
            CornerRadiusTopLeft = topLeft ? radius : 0,
            CornerRadiusTopRight = topRight ? radius : 0,
            CornerRadiusBottomLeft = bottomLeft ? radius : 0,
            CornerRadiusBottomRight = bottomRight ? radius : 0,
            BorderColor = borderColor
        };
        SetAllBorderWidth(style, borderWidth);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }
}