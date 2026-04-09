using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace TheGallery;

public partial class NCreatureAnimBrowserTab : Control
{
    // ── Layout constants ──────────────────────────────────────────────
    private const float Padding = 16f;
    private const float SearchBarHeight = 48f;
    private const float ToolbarHeight = 72f;
    private const float GridSpacing = 48f;
    private const float CrosshairSize = 20f;

    // ── Palette (matches VFX browser) ─────────────────────────────────
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
    private static readonly Color CrosshairCol  = new(0.3f,  0.35f, 0.5f,  0.4f);
    private static readonly Color ErrorText     = new(0.9f,  0.35f, 0.35f, 1f);
    private static readonly Color BoneMarkerCol = new(0.4f,  0.8f,  0.4f,  0.5f);
    private static readonly Color BoneDotCol    = new(0.4f,  0.8f,  0.4f,  0.6f);
    private static readonly Color BoneLabelBg   = new(0.02f, 0.02f, 0.04f, 0.75f);
    private static readonly Color SmallButtonBg = new(0.12f, 0.12f, 0.18f, 1f);

    // ── UI references ─────────────────────────────────────────────────
    private LineEdit _searchBox;
    private Label _entryCountLabel;
    private VBoxContainer _creatureListContainer;
    private ScrollContainer _listScroll;
    private Control _previewArea;
    private Node2D _previewRoot;
    private Label _currentCreatureLabel;
    private Button _selectedEntryButton;
    private Font _fontRegular;
    private Font _fontBold;

    // ── Animation controls ────────────────────────────────────────────
    private OptionButton _animDropdown;
    private HSlider _frameSlider;
    private Label _frameLabel;
    private Button _playPauseButton;
    private OptionButton _track1Dropdown;
    private readonly List<string> _availableAnims = new();
    private readonly List<string> _availableTrack1Anims = new();
    private string _currentAnimName = "idle_loop";
    private string _currentTrack1Anim = "";
    private string _appliedTrack1Anim = "";
    private bool _isPlaying;

    // ── Bone viewing ──────────────────────────────────────────────────
    private Button _showBonesButton;
    private CheckBox _showAllBoneNamesCheckbox;
    private bool _showingBones;
    private readonly List<Node2D> _boneMarkers = new();
    private readonly List<BoneInfo> _boneData = new();
    private Godot.Collections.Array _cachedBones;

    // ── Creature state ────────────────────────────────────────────────
    private NCreatureVisuals _currentVisuals;
    private MegaSprite _animController;
    private MegaAnimationState _animState;
    private MegaTrackEntry _cachedTrackEntry;
    private GodotObject _skeletonGodot;
    private Node2D _spineNode;
    private string _currentCreatureId;

    // ── Zoom / pan ────────────────────────────────────────────────────
    private float _zoom = 1f;
    private Vector2 _panOffset = Vector2.Zero;
    private bool _isPanning;
    private Vector2 _panStart;

    // ── Entry list ────────────────────────────────────────────────────
    private readonly List<(Button button, string id, string searchName, MarginContainer margin)> _creatureEntries = new();

    private struct BoneInfo
    {
        public string Name;
        public Vector2 SpineWorldPos;
    }
    
    private static readonly (string label, Color color)[] PreviewBgOptions =
    {
        ("Default", new Color(0.05f, 0.05f, 0.07f, 1f)),
        ("Green",   new Color(0f,    0.85f, 0f,    1f)),
        ("Blue",    new Color(0f,    0f,    0.85f, 1f)),
        ("Red",     new Color(0.85f, 0f,    0f,    1f)),
        ("White",   new Color(1f,    1f,    1f,    1f)),
        ("Black",   new Color(0f,    0f,    0f,    1f)),
    };
    private ColorRect _previewBgRect;
    
    private readonly List<ColorRect> _gridLines = new();
    private readonly List<ColorRect> _crosshairLines = new();

    // ═══════════════════════════════════════════════════════════════════
    // Initialization
    // ═══════════════════════════════════════════════════════════════════

    public void Initialize()
    {
        try
        {
            var screenSize = GetViewportRect().Size;
            _fontRegular = ResourceLoader.Load<Font>("res://fonts/kreon_regular.ttf");
            _fontBold = ResourceLoader.Load<Font>("res://fonts/kreon_bold.ttf");

            float availableHeight = Size.Y > 0 ? Size.Y : screenSize.Y - GlobalPosition.Y;
            float availableWidth = Size.X > 0 ? Size.X : screenSize.X;
            float listWidth = availableWidth * 0.32f;
            float previewX = Padding + listWidth + 16f;
            float previewWidth = availableWidth - previewX - Padding;

            BuildListPanel(listWidth, availableHeight);
            BuildPreviewPanel(previewX, previewWidth, availableHeight);
            PopulateCreatureList();
        }
        catch (Exception e)
        {
            GD.PrintErr("[CreatureAnimBrowser] Error in Initialize: " + e);
        }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;

        if (_showingBones && _boneData.Count > 0 && _skeletonGodot != null)
            UpdateBoneMarkerPositions();

        if (_isPlaying && _cachedTrackEntry != null && GodotObject.IsInstanceValid(_spineNode))
        {
            float duration = _cachedTrackEntry.GetAnimationEnd();
            if (duration > 0f)
            {
                float time = _cachedTrackEntry.GetTrackTime() % duration;
                _frameSlider.SetValueNoSignal(time / duration * 100.0);
                _frameLabel.Text = $"{time:F2}s / {duration:F2}s";
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || _previewArea == null) return;

        var mouse = GetGlobalMousePosition();
        if (!new Rect2(_previewArea.GlobalPosition, _previewArea.Size).HasPoint(mouse)) return;

        var localPos = mouse - _previewArea.GlobalPosition;

        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Middle && mb.Pressed)
            {
                _isPanning = true;
                _panStart = localPos - _panOffset;
                GetViewport().SetInputAsHandled();
                return;
            }
            if (mb.ButtonIndex == MouseButton.Middle && !mb.Pressed)
            {
                _isPanning = false;
                return;
            }
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomAt(localPos, 1.1f);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomAt(localPos, 1f / 1.1f);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseMotion && _isPanning)
        {
            _panOffset = localPos - _panStart;
            ApplyZoomPan();
            GetViewport().SetInputAsHandled();
        }
    }

    public void CleanupAndRestore()
    {
        ClearBoneMarkers();
        if (_currentVisuals != null && IsInstanceValid(_currentVisuals))
        {
            _currentVisuals.QueueFree();
            _currentVisuals = null;
        }
        _animController = null;
        _animState = null;
        _cachedTrackEntry = null;
        _skeletonGodot = null;
        _spineNode = null;
        _currentCreatureId = null;
        _isPlaying = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // List Panel
    // ═══════════════════════════════════════════════════════════════════

    private void BuildListPanel(float listWidth, float availableHeight)
    {
        var panelBg = CreateRoundedRect(PanelBg, PanelBorder, 8, 1);
        panelBg.Position = new Vector2(Padding, Padding);
        panelBg.Size = new Vector2(listWidth, availableHeight - Padding * 2);
        AddChild(panelBg);

        float searchY = Padding + 12;
        _searchBox = new LineEdit
        {
            PlaceholderText = "Search creatures...",
            ClearButtonEnabled = true,
            Position = new Vector2(Padding + 12, searchY),
            Size = new Vector2(listWidth - 24, 36)
        };
        ApplyFont(_searchBox, _fontRegular, 14);
        ApplySearchBoxStyle(_searchBox);
        _searchBox.TextChanged += OnSearchTextChanged;
        AddChild(_searchBox);

        _entryCountLabel = new Label
        {
            Name = "EntryCount",
            HorizontalAlignment = HorizontalAlignment.Right,
            Position = new Vector2(Padding + 12, searchY + 36 + 6),
            Size = new Vector2(listWidth - 24, 18)
        };
        ApplyFont(_entryCountLabel, _fontRegular, 11);
        _entryCountLabel.AddThemeColorOverride("font_color", TextDim);
        AddChild(_entryCountLabel);

        float sepY = searchY + 36 + 28;
        AddColorStrip(new Vector2(Padding + 12, sepY), listWidth - 24, 1, PanelBorder);

        float listTop = sepY + 8;
        float listBottom = availableHeight - Padding - 12;
        _listScroll = new ScrollContainer
        {
            Position = new Vector2(Padding + 8, listTop),
            Size = new Vector2(listWidth - 16, listBottom - listTop),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(_listScroll);

        _creatureListContainer = new VBoxContainer
        {
            Name = "CreatureList",
            CustomMinimumSize = new Vector2(listWidth - 36, 0)
        };
        _creatureListContainer.AddThemeConstantOverride("separation", 1);
        _listScroll.AddChild(_creatureListContainer);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Preview Panel
    // ═══════════════════════════════════════════════════════════════════

    private void BuildPreviewPanel(float previewX, float previewWidth, float availableHeight)
    {
        // Toolbar
        var toolbarBg = CreateRoundedRect(ToolbarBg, PanelBorder, 8, 1, true, true, false, false);
        toolbarBg.Position = new Vector2(previewX, Padding);
        toolbarBg.Size = new Vector2(previewWidth, ToolbarHeight);
        AddChild(toolbarBg);
        
        BuildBgButtons(previewX);

        _currentCreatureLabel = new Label
        {
            Text = "No creature selected",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(previewX + 16, Padding + 2),
            Size = new Vector2(200, 36)
        };
        
        ApplyFont(_currentCreatureLabel, _fontBold, 14);
        _currentCreatureLabel.AddThemeColorOverride("font_color", TextDim);
        AddChild(_currentCreatureLabel);

        BuildAnimationControls(previewX, previewWidth);
        BuildBoneControls(previewX, previewWidth);

        // Preview area background
        float previewTop = Padding + ToolbarHeight;
        float previewHeight = availableHeight - Padding * 2 - ToolbarHeight;

// Replace the CreateRoundedRect previewPanelBg block with:
        var previewPanelBorder = CreateRoundedRect(Colors.Transparent, PanelBorder, 8, 1, false, false, true, true);
        previewPanelBorder.Position = new Vector2(previewX, previewTop);
        previewPanelBorder.Size = new Vector2(previewWidth, previewHeight);
        AddChild(previewPanelBorder);

        _previewBgRect = new ColorRect
        {
            Color = PreviewBgOptions[0].color,
            Position = new Vector2(previewX + 1, previewTop + 1),
            Size = new Vector2(previewWidth - 2, previewHeight - 2)
        };
        AddChild(_previewBgRect);

        BuildPreviewGrid(previewX, previewTop, previewWidth, previewHeight);
        BuildCrosshair(previewX, previewTop, previewWidth, previewHeight);

        _previewArea = new Control
        {
            Name = "PreviewArea",
            Position = new Vector2(previewX, previewTop),
            Size = new Vector2(previewWidth, previewHeight),
            ClipContents = true
        };
        AddChild(_previewArea);

        _previewRoot = new Node2D();
        _previewArea.AddChild(_previewRoot);
    }

    private void BuildAnimationControls(float previewX, float previewWidth)
    {
        float cx = previewX + 230;
        float cy = Padding + 6;

        _animDropdown = new OptionButton
        {
            Position = new Vector2(cx, cy),
            Size = new Vector2(150, 28)
        };
        ApplyFont(_animDropdown, _fontRegular, 12);
        _animDropdown.ItemSelected += OnAnimationSelected;
        AddChild(_animDropdown);

        _frameSlider = new HSlider
        {
            Position = new Vector2(cx + 158, cy + 2),
            Size = new Vector2(160, 26),
            MinValue = 0, MaxValue = 100, Step = 0.1, Value = 0
        };
        _frameSlider.ValueChanged += OnFrameSliderChanged;
        AddChild(_frameSlider);

        _frameLabel = new Label
        {
            Text = "0.00s / 0.00s",
            Position = new Vector2(cx + 326, cy + 4),
            Size = new Vector2(110, 24)
        };
        ApplyFont(_frameLabel, _fontRegular, 12);
        _frameLabel.AddThemeColorOverride("font_color", TextDim);
        AddChild(_frameLabel);

        _playPauseButton = new Button
        {
            Text = "Play",
            Flat = true,
            Position = new Vector2(cx + 442, cy),
            Size = new Vector2(52, 28)
        };
        ApplyFont(_playPauseButton, _fontRegular, 12);
        _playPauseButton.AddThemeColorOverride("font_color", TextNormal);
        _playPauseButton.AddThemeColorOverride("font_hover_color", TextBright);
        ApplySmallButtonStyle(_playPauseButton);
        _playPauseButton.Pressed += TogglePlayPause;
        AddChild(_playPauseButton);

        // Secondary track
        var layerLabel = new Label
        {
            Text = "Layer:",
            Position = new Vector2(cx + 504, cy + 4),
            Size = new Vector2(42, 24)
        };
        ApplyFont(layerLabel, _fontRegular, 11);
        layerLabel.AddThemeColorOverride("font_color", TextDim);
        AddChild(layerLabel);

        _track1Dropdown = new OptionButton
        {
            Position = new Vector2(cx + 548, cy),
            Size = new Vector2(150, 28),
            TooltipText = "Optional secondary animation on track 1."
        };
        ApplyFont(_track1Dropdown, _fontRegular, 11);
        _track1Dropdown.ItemSelected += OnTrack1Selected;
        AddChild(_track1Dropdown);
    }
    
private void BuildBgButtons(float previewX)
    {
        float by = Padding + 42;
        float bx = previewX + 16;

        var label = new Label
        {
            Text = "BG:",
            Position = new Vector2(bx, by + 2),
            Size = new Vector2(24, 20)
        };
        ApplyFont(label, _fontRegular, 11);
        label.AddThemeColorOverride("font_color", TextDim);
        AddChild(label);

        float btnX = bx + 28;
        foreach (var (name, color) in PreviewBgOptions)
        {
            var swatch = new Button
            {
                Position = new Vector2(btnX, by),
                Size = new Vector2(24, 24),
                TooltipText = name
            };

            var swatchStyle = new StyleBoxFlat { BgColor = color, BorderColor = PanelBorder };
            SetAllCornerRadius(swatchStyle, 3);
            SetAllBorderWidth(swatchStyle, 1);
            swatch.AddThemeStyleboxOverride("normal", swatchStyle);

            var hoverStyle = new StyleBoxFlat { BgColor = color, BorderColor = Accent };
            SetAllCornerRadius(hoverStyle, 3);
            SetAllBorderWidth(hoverStyle, 1);
            swatch.AddThemeStyleboxOverride("hover", hoverStyle);
            swatch.AddThemeStyleboxOverride("pressed", hoverStyle);
            swatch.AddThemeStyleboxOverride("focus", swatchStyle);

            var col = color;
            swatch.Pressed += () => _previewBgRect.Color = col;
            AddChild(swatch);

            btnX += 32;
        }

        btnX += 8;
        var gridToggle = new CheckBox
        {
            Text = "Grid",
            ButtonPressed = true,
            Position = new Vector2(btnX, by),
            Size = new Vector2(55, 24),
            TooltipText = "Toggle grid lines"
        };
        ApplyFont(gridToggle, _fontRegular, 11);
        gridToggle.AddThemeColorOverride("font_color", TextDim);
        gridToggle.Toggled += visible =>
        {
            foreach (var line in _gridLines)
                line.Visible = visible;
            foreach (var line in _crosshairLines)
                line.Visible = visible;
        };
        AddChild(gridToggle);
    }

    private void BuildBoneControls(float previewX, float previewWidth)
    {
        float bx = previewX + previewWidth - 220;
        float by = Padding + 6;

        _showBonesButton = new Button
        {
            Text = "Show Bones",
            Flat = true,
            Position = new Vector2(bx, by),
            Size = new Vector2(95, 28),
            TooltipText = "Toggle bone markers on the preview."
        };
        ApplyFont(_showBonesButton, _fontRegular, 12);
        _showBonesButton.AddThemeColorOverride("font_color", TextNormal);
        _showBonesButton.AddThemeColorOverride("font_hover_color", TextBright);
        ApplySmallButtonStyle(_showBonesButton);
        _showBonesButton.Pressed += ToggleBoneMarkers;
        AddChild(_showBonesButton);

        _showAllBoneNamesCheckbox = new CheckBox
        {
            Text = "Names",
            Position = new Vector2(bx + 102, by),
            Size = new Vector2(65, 28),
            TooltipText = "Show bone names on the markers."
        };
        ApplyFont(_showAllBoneNamesCheckbox, _fontRegular, 11);
        _showAllBoneNamesCheckbox.AddThemeColorOverride("font_color", TextDim);
        _showAllBoneNamesCheckbox.Toggled += _ => { if (_showingBones) RebuildBoneMarkerNodes(); };
        AddChild(_showAllBoneNamesCheckbox);
    }

    private void BuildPreviewGrid(float x, float y, float width, float height)
    {
        for (float gx = GridSpacing; gx < width; gx += GridSpacing)
        {
            var line = new ColorRect
            {
                Color = GridLineColor,
                Position = new Vector2(x + gx, y),
                Size = new Vector2(1, height)
            };
            AddChild(line);
            _gridLines.Add(line);
        }
        for (float gy = GridSpacing; gy < height; gy += GridSpacing)
        {
            var line = new ColorRect
            {
                Color = GridLineColor,
                Position = new Vector2(x, y + gy),
                Size = new Vector2(width, 1)
            };
            AddChild(line);
            _gridLines.Add(line);
        }
    }

    private void BuildCrosshair(float x, float y, float width, float height)
    {
        float cx = x + width / 2;
        float cy = y + height / 2;

        var h = new ColorRect
        {
            Color = CrosshairCol,
            Position = new Vector2(cx - CrosshairSize, cy),
            Size = new Vector2(CrosshairSize * 2, 1)
        };
        AddChild(h);
        _crosshairLines.Add(h);

        var v = new ColorRect
        {
            Color = CrosshairCol,
            Position = new Vector2(cx, cy - CrosshairSize),
            Size = new Vector2(1, CrosshairSize * 2)
        };
        AddChild(v);
        _crosshairLines.Add(v);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Creature List Population
    // ═══════════════════════════════════════════════════════════════════

    private void PopulateCreatureList()
    {
        int monsterCount = 0;
        int characterCount = 0;

        // Characters
        AddSectionHeader("Characters");
        int charSectionStart = _creatureListContainer.GetChildCount();

        foreach (CharacterModel character in ModelDb.AllCharacters.OrderBy(c => c.Id.Entry))
        {
            var id = character.Id.Entry;
            var charRef = character;
            AddCreatureEntry(id, () => charRef.CreateVisuals());
            characterCount++;
        }

        int charSectionEnd = _creatureListContainer.GetChildCount();
        SetupCollapseToggle(
            _creatureListContainer.GetChild(charSectionStart - 2) as Button,
            _creatureListContainer, charSectionStart, charSectionEnd);

        // Monsters
        AddSectionHeader("Monsters");
        int monsterSectionStart = _creatureListContainer.GetChildCount();

        foreach (MonsterModel monster in ModelDb.AllAbstractModelSubtypes
                     .Where(t => t.IsSubclassOf(typeof(MonsterModel)))
                     .Select(t => ModelDb.GetByIdOrNull<MonsterModel>(ModelDb.GetId(t)))
                     .Where(m => m != null)
                     .OrderBy(m => m.Id.Entry))
        {
            var id = monster.Id.Entry;
            var monsterRef = monster;
            AddCreatureEntry(id, () => monsterRef.CreateVisuals(),
                v => monsterRef.SetupSkins(v.SpineBody, v.SpineBody.GetSkeleton()));
            monsterCount++;
        }

        int monsterSectionEnd = _creatureListContainer.GetChildCount();
        SetupCollapseToggle(
            _creatureListContainer.GetChild(monsterSectionStart - 2) as Button,
            _creatureListContainer, monsterSectionStart, monsterSectionEnd);

        _entryCountLabel.Text = $"{monsterCount + characterCount} creatures  ·  {characterCount} characters  ·  {monsterCount} monsters";
    }

    private void AddSectionHeader(string text)
    {
        var spacer = new Control { CustomMinimumSize = new Vector2(0, 6) };
        _creatureListContainer.AddChild(spacer);

        var headerButton = new Button
        {
            Text = "▼  " + text,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        ApplyFont(headerButton, _fontBold, 13);
        headerButton.AddThemeColorOverride("font_color", FolderText);
        headerButton.AddThemeColorOverride("font_hover_color", Accent);
        _creatureListContainer.AddChild(headerButton);

        var divider = new ColorRect
        {
            Color = new Color(FolderText.R, FolderText.G, FolderText.B, 0.25f),
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _creatureListContainer.AddChild(divider);
    }

    private void AddCreatureEntry(string id, Func<NCreatureVisuals> createVisuals,
        Action<NCreatureVisuals> postSetup = null)
    {
        var entryMargin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        entryMargin.AddThemeConstantOverride("margin_left", 12);
        _creatureListContainer.AddChild(entryMargin);

        var button = new Button
        {
            Text = id,
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
            SelectCreature(id, createVisuals, postSetup);
        };

        entryMargin.AddChild(button);
        _creatureEntries.Add((button, id, id.ToLowerInvariant(), entryMargin));
    }

    private static void SetupCollapseToggle(Button headerButton, VBoxContainer container,
        int startIdx, int endIdx)
    {
        if (headerButton == null) return;
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

        if (hasFilter || !hasFilter)
            ExpandAllChildren(_creatureListContainer);

        foreach (var (_, _, searchName, margin) in _creatureEntries)
            margin.Visible = !hasFilter || searchName.Contains(filter);
    }

    private static void ExpandAllChildren(Control container)
    {
        for (int i = 0; i < container.GetChildCount(); i++)
        {
            if (container.GetChild(i) is Control child)
                child.Visible = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Creature Selection
    // ═══════════════════════════════════════════════════════════════════

    private void SelectCreature(string creatureId, Func<NCreatureVisuals> createVisuals,
        Action<NCreatureVisuals> postSetup = null)
    {
        ClearBoneMarkers();
        _showingBones = false;
        _showBonesButton.Text = "Show Bones";
        _skeletonGodot = null;
        _spineNode = null;
        _animState = null;
        _cachedTrackEntry = null;
        _currentCreatureId = creatureId;
        _isPlaying = false;
        _playPauseButton.Text = "Play";
        _currentTrack1Anim = "";
        _appliedTrack1Anim = "";

        ResetZoomPan();

        if (_currentVisuals != null && IsInstanceValid(_currentVisuals))
        {
            _currentVisuals.QueueFree();
            _currentVisuals = null;
        }

        try
        {
            _currentVisuals = createVisuals();
            _previewRoot.AddChild(_currentVisuals);
            _currentVisuals.Position = new Vector2(
                _previewArea.Size.X / 2,
                _previewArea.Size.Y / 2 + _currentVisuals.Bounds.Size.Y * 0.25f
            );

            try { postSetup?.Invoke(_currentVisuals); }
            catch (Exception e) { GD.PrintErr("[CreatureAnimBrowser] SetupSkins error: " + e); }

            TryApplyCurrentSkin(creatureId);

            _availableAnims.Clear();
            _animDropdown.Clear();
            _availableTrack1Anims.Clear();
            _track1Dropdown.Clear();
            _track1Dropdown.AddItem("(none)");
            _availableTrack1Anims.Add("");

            if (_currentVisuals.HasSpineAnimation)
            {
                _animController = _currentVisuals.SpineBody;
                _animState = _animController.GetAnimationState();
                var skeleton = _animController.GetSkeleton();
                if (skeleton != null)
                {
                    _skeletonGodot = skeleton.BoundObject as GodotObject;
                    _spineNode = _animController.BoundObject as Node2D;
                }
                PopulateAnimationList();
                SetAnimationPaused("idle_loop", 0f);
            }

            SetCreatureLabel(creatureId, false);
        }
        catch (Exception e)
        {
            SetCreatureLabel($"Error loading: {e.Message}", true);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Animation
    // ═══════════════════════════════════════════════════════════════════

    private void PopulateAnimationList()
    {
        if (_animController == null) return;
        var skeleton = _animController.GetSkeleton();
        var data = skeleton?.GetData();
        var bound = data?.BoundObject as GodotObject;
        if (bound == null) return;

        var anims = bound.Call("get_animations").AsGodotArray();
        foreach (var animVariant in anims)
        {
            var anim = animVariant.AsGodotObject();
            var name = anim.Call("get_name").AsString();
            if (string.IsNullOrEmpty(name)) continue;

            if (name.Contains("_tracks/"))
            {
                _availableTrack1Anims.Add(name);
                _track1Dropdown.AddItem(name);
            }
            else
            {
                _availableAnims.Add(name);
                _animDropdown.AddItem(name);
            }
        }

        if (_availableAnims.Count > 0)
        {
            int bestIdx = _availableAnims.IndexOf("idle_loop");
            if (bestIdx < 0) bestIdx = _availableAnims.FindIndex(n =>
                n.Equals("idle", StringComparison.OrdinalIgnoreCase));
            if (bestIdx < 0) bestIdx = _availableAnims.FindIndex(n =>
                n.Contains("idle", StringComparison.OrdinalIgnoreCase));
            _animDropdown.Selected = bestIdx >= 0 ? bestIdx : 0;
        }

        _track1Dropdown.Selected = 0;
    }

    private void OnAnimationSelected(long index)
    {
        if (index < 0 || index >= _availableAnims.Count) return;
        _isPlaying = false;
        _playPauseButton.Text = "Play";
        SetAnimationPaused(_availableAnims[(int)index], 0f);
    }

    private void OnTrack1Selected(long index)
    {
        if (index < 0 || index >= _availableTrack1Anims.Count) return;
        _currentTrack1Anim = _availableTrack1Anims[(int)index];
        ApplyTrack1();
    }

    private void ApplyTrack1()
    {
        if (_animController == null || _currentTrack1Anim == _appliedTrack1Anim) return;
        _appliedTrack1Anim = _currentTrack1Anim;
        _animState ??= _animController.GetAnimationState();

        if (string.IsNullOrEmpty(_currentTrack1Anim))
        {
            try { _animState.AddEmptyAnimation(1); } catch { }
            return;
        }

        var entry = _animState.SetAnimation(_currentTrack1Anim, true, 1);
        entry?.SetTimeScale(_isPlaying ? 1f : 0f);
    }

    private void OnFrameSliderChanged(double value)
    {
        if (_isPlaying) return;
        SetAnimationPaused(_currentAnimName, (float)(value / 100.0));
    }

    private void TogglePlayPause()
    {
        if (_animController == null) return;
        _isPlaying = !_isPlaying;
        _playPauseButton.Text = _isPlaying ? "Pause" : "Play";

        if (_cachedTrackEntry != null)
        {
            if (_isPlaying)
            {
                _cachedTrackEntry.SetTimeScale(1f);
                _cachedTrackEntry.SetLoop(true);
            }
            else
            {
                _cachedTrackEntry.SetTimeScale(0f);
            }
        }

        _animState ??= _animController.GetAnimationState();
        try
        {
            var t1 = _animState.GetCurrent(1);
            t1?.SetTimeScale(_isPlaying ? 1f : 0f);
        }
        catch (NullReferenceException) { }
    }

    private void SetAnimationPaused(string animName, float normalizedTime)
    {
        if (_animController == null) return;
        _currentAnimName = animName;
        _animState ??= _animController.GetAnimationState();

        var entry = _animState.SetAnimation(animName, false);
        if (entry == null) return;

        _cachedTrackEntry = entry;
        entry.SetTimeScale(0f);
        float duration = entry.GetAnimationEnd();
        float time = normalizedTime * duration;
        entry.SetTrackTime(time);
        _animState.Update(0f);
        _animState.Apply(_animController.GetSkeleton());
        _frameLabel.Text = $"{time:F2}s / {duration:F2}s";

        if (_showingBones) RefreshBonePositions();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Bone Markers
    // ═══════════════════════════════════════════════════════════════════

    private void ToggleBoneMarkers()
    {
        if (_showingBones)
        {
            ClearBoneMarkers();
            _showBonesButton.Text = "Show Bones";
            _showingBones = false;
        }
        else
        {
            ShowBoneMarkers();
            _showBonesButton.Text = "Hide Bones";
            _showingBones = true;
        }
    }

    private void ShowBoneMarkers()
    {
        ClearBoneMarkers();
        EnsureSkeletonRefs();
        if (_skeletonGodot == null) return;
        RefreshBonePositions();
        RebuildBoneMarkerNodes();
    }

    private void EnsureSkeletonRefs()
    {
        if (_skeletonGodot == null && _animController != null)
        {
            var skeleton = _animController.GetSkeleton();
            if (skeleton != null)
            {
                _skeletonGodot = skeleton.BoundObject as GodotObject;
                _spineNode = _animController.BoundObject as Node2D;
            }
        }
    }

    private void RefreshBonePositions()
    {
        _boneData.Clear();
        _cachedBones = null;
        if (_skeletonGodot == null) return;

        _cachedBones = _skeletonGodot.Call("get_bones").AsGodotArray();
        foreach (var bv in _cachedBones)
        {
            var bone = bv.AsGodotObject();
            var data = bone.Call("get_data").AsGodotObject();
            _boneData.Add(new BoneInfo
            {
                Name = data.Call("get_bone_name").AsString(),
                SpineWorldPos = new Vector2(
                    (float)bone.Call("get_world_x"),
                    (float)bone.Call("get_world_y"))
            });
        }

        if (_boneMarkers.Count > 0) RebuildBoneMarkerNodes();
    }

    private void UpdateBoneMarkerPositions()
    {
        if (_cachedBones == null) return;

        for (int i = 0; i < _cachedBones.Count && i < _boneData.Count; i++)
        {
            var bone = _cachedBones[i].AsGodotObject();
            _boneData[i] = new BoneInfo
            {
                Name = _boneData[i].Name,
                SpineWorldPos = new Vector2(
                    (float)bone.Call("get_world_x"),
                    (float)bone.Call("get_world_y"))
            };
        }

        for (int i = 0; i < _boneData.Count && i < _boneMarkers.Count; i++)
        {
            var contentPos = SpineToPreview(_boneData[i].SpineWorldPos);
            _boneMarkers[i].Position = contentPos * _zoom + _panOffset;
        }
    }

    private void RebuildBoneMarkerNodes()
    {
        foreach (var m in _boneMarkers)
            if (IsInstanceValid(m)) m.QueueFree();
        _boneMarkers.Clear();

        bool showNames = _showAllBoneNamesCheckbox.ButtonPressed;

        foreach (var info in _boneData)
        {
            var marker = new Node2D();

            const float dotSize = 6;
            marker.AddChild(new ColorRect
            {
                Color = BoneDotCol,
                Position = new Vector2(-dotSize / 2, -dotSize / 2),
                Size = new Vector2(dotSize, dotSize)
            });

            if (showNames)
            {
                var label = new Label
                {
                    Text = info.Name,
                    Position = new Vector2(6, -8),
                    Size = new Vector2(150, 16)
                };
                ApplyFont(label, _fontRegular, 10);
                label.AddThemeColorOverride("font_color", TextBright);

                // Dark backing for readability
                var backing = new ColorRect
                {
                    Color = BoneLabelBg,
                    Position = new Vector2(4, -10),
                    Size = new Vector2(info.Name.Length * 6.5f + 6, 18)
                };
                marker.AddChild(backing);
                marker.AddChild(label);
            }

            _previewArea.AddChild(marker);
            _boneMarkers.Add(marker);
        }
    }
    
    private void TryApplyCurrentSkin(string creatureId)
    {
        if (_currentVisuals?.SpineBody == null) return;

        try
        {
            var skinManagerType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "SkinManager");
            if (skinManagerType == null) return;

            var skinName = skinManagerType
                .GetProperty("LocalSkinName", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as string;
            if (string.IsNullOrEmpty(skinName) || skinName == "Default" || skinName == "Random") return;

            bool isTint = (bool)(skinManagerType
                .GetMethod("IsTintSkin", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { skinName }) ?? false);

            if (isTint)
            {
                float hue = (float)(skinManagerType
                    .GetMethod("GetHueForSkin", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { skinName }) ?? 0f);
                if (hue != 0f)
                    _currentVisuals.SetScaleAndHue(_currentVisuals.DefaultScale, hue);
            }
            else
            {
                var texture = skinManagerType
                    .GetMethod("GetTextureForSkin", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { creatureId, skinName }) as Texture2D;
                if (texture != null)
                    ApplyTextureSkin(_currentVisuals.SpineBody, texture);
            }
        }
        catch { }
    }

    private static void ApplyTextureSkin(MegaSprite spineBody, Texture2D texture)
    {
        var shader = new Shader();
        shader.Code = @"
shader_type canvas_item;
uniform sampler2D skin_texture;
varying vec4 modulate_color;
void vertex() { modulate_color = COLOR; }
void fragment() { COLOR = texture(skin_texture, UV) * modulate_color; }
";
        var mat = new ShaderMaterial();
        mat.Shader = shader;
        mat.SetShaderParameter("skin_texture", texture);
        spineBody.SetNormalMaterial(mat);

        var addMat = new ShaderMaterial();
        addMat.Shader = shader;
        addMat.SetShaderParameter("skin_texture", texture);
        spineBody.BoundObject.Call("set_additive_material", addMat);
    }

    private void ClearBoneMarkers()
    {
        foreach (var m in _boneMarkers)
            if (IsInstanceValid(m)) m.QueueFree();
        _boneMarkers.Clear();
        _boneData.Clear();
        _cachedBones = null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Coordinate Transforms / Zoom / Pan
    // ═══════════════════════════════════════════════════════════════════

    private Vector2 SpineToPreview(Vector2 spinePos)
    {
        if (_currentVisuals == null || _spineNode == null) return spinePos;
        return _currentVisuals.Position + _spineNode.Position + spinePos * _spineNode.Scale;
    }

    private void ResetZoomPan()
    {
        _zoom = 1f;
        _panOffset = Vector2.Zero;
        ApplyZoomPan();
    }

    private void ApplyZoomPan()
    {
        if (_previewRoot == null) return;
        _previewRoot.Scale = Vector2.One * _zoom;
        _previewRoot.Position = _panOffset;
    }

    private void ZoomAt(Vector2 localPos, float factor)
    {
        float old = _zoom;
        _zoom = Mathf.Clamp(_zoom * factor, 0.2f, 10f);
        _panOffset = localPos - (_zoom / old) * (localPos - _panOffset);
        ApplyZoomPan();
    }

    // ═══════════════════════════════════════════════════════════════════
    // UI Helpers
    // ═══════════════════════════════════════════════════════════════════

    private void SetCreatureLabel(string text, bool isError)
    {
        _currentCreatureLabel.Text = text;
        _currentCreatureLabel.AddThemeColorOverride("font_color",
            isError ? ErrorText : TextBright);
    }

    private void AddColorStrip(Vector2 position, float width, float height, Color color)
    {
        AddChild(new ColorRect
        {
            Color = color,
            Position = position,
            Size = new Vector2(width, height)
        });
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

    private static void ApplySmallButtonStyle(Button btn)
    {
        btn.AddThemeStyleboxOverride("normal",  MakeSmallButtonStyle(SmallButtonBg, PanelBorder));
        btn.AddThemeStyleboxOverride("hover",   MakeSmallButtonStyle(EntryHoverBg, Accent));
        btn.AddThemeStyleboxOverride("pressed", MakeSmallButtonStyle(EntryHoverBg, Accent));
        btn.AddThemeStyleboxOverride("focus",   MakeSmallButtonStyle(SmallButtonBg, PanelBorder));
    }

    private static void ApplyFont(Control control, Font font, int size)
    {
        if (font != null)
            control.AddThemeFontOverride("font", font);
        control.AddThemeFontSizeOverride("font_size", size);
    }

    // ═══════════════════════════════════════════════════════════════════
    // StyleBox Factories
    // ═══════════════════════════════════════════════════════════════════

    private static StyleBoxFlat MakeFlatStyle(Color bg, int cornerRadius, int leftMargin = 0)
    {
        var style = new StyleBoxFlat { BgColor = bg, ContentMarginLeft = leftMargin };
        SetAllCornerRadius(style, cornerRadius);
        return style;
    }

    private static StyleBoxFlat MakeSmallButtonStyle(Color bg, Color border)
    {
        var style = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        SetAllCornerRadius(style, 4);
        SetAllBorderWidth(style, 1);
        style.ContentMarginLeft = 6;
        style.ContentMarginRight = 6;
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

    private static PanelContainer CreateRoundedRect(Color bgColor, Color borderColor, int radius,
        int borderWidth, bool topLeft = true, bool topRight = true,
        bool bottomLeft = true, bool bottomRight = true)
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