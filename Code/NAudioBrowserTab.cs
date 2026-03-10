using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Runs;

namespace TheGallery;

public partial class NAudioBrowserTab : Control
{
    // ── Layout constants ──────────────────────────────────────────────
    private const float Padding = 16f;
    private const float BottomDockHeight = 100f;
    private const float SearchBarHeight = 48f;
    private const float NowPlayingHeight = 32f;

    // ── Palette ───────────────────────────────────────────────────────
    private static readonly Color PanelBg        = new(0.1f,  0.1f,  0.15f, 1f);
    private static readonly Color PanelBorder     = new(0.18f, 0.18f, 0.25f, 1f);
    private static readonly Color Accent          = new(0.4f,  0.55f, 0.95f, 1f);
    private static readonly Color TextBright      = new(0.9f,  0.9f,  0.93f, 1f);
    private static readonly Color TextNormal      = new(0.72f, 0.72f, 0.78f, 1f);
    private static readonly Color TextDim         = new(0.45f, 0.45f, 0.52f, 1f);
    private static readonly Color FolderText      = new(0.55f, 0.65f, 0.88f, 1f);
    private static readonly Color EntryHoverBg    = new(0.15f, 0.15f, 0.22f, 1f);
    private static readonly Color EntryActiveBg   = new(0.18f, 0.22f, 0.35f, 1f);
    private static readonly Color SearchBg        = new(0.08f, 0.08f, 0.12f, 1f);
    private static readonly Color SearchBorder    = new(0.2f,  0.2f,  0.28f, 1f);
    private static readonly Color ToolbarBg       = new(0.09f, 0.09f, 0.13f, 1f);
    private static readonly Color ErrorText       = new(0.9f,  0.35f, 0.35f, 1f);
    private static readonly Color MusicTag        = new(0.45f, 0.75f, 0.45f, 1f);
    private static readonly Color SfxTag          = new(0.85f, 0.65f, 0.35f, 1f);
    private static readonly Color AmbienceTag     = new(0.5f,  0.7f,  0.85f, 1f);
    private static readonly Color OtherTag        = new(0.65f, 0.55f, 0.75f, 1f);
    private static readonly Color NowPlayingBg    = new(0.08f, 0.1f,  0.16f, 1f);
    private static readonly Color PathDimText     = new(0.35f, 0.35f, 0.42f, 1f);
    private static readonly Color SmallButtonBg   = new(0.12f, 0.12f, 0.18f, 1f);

    // ── Bank files ────────────────────────────────────────────────────
    private static readonly string[] BankFiles =
    {
        "Master.bank", "Master.strings.bank", "sfx.bank", "ambience.bank",
        "act1_a1.bank", "act1_a2.bank", "act1_b1.bank",
        "act2_a1.bank", "act2_a2.bank",
        "act3_a1.bank", "act3_a2.bank",
        "temp_sfx.bank"
    };

    private const string BankDir = "res://banks/desktop/";

    // ── UI references ─────────────────────────────────────────────────
    private VBoxContainer _leftListContainer;
    private VBoxContainer _rightListContainer;
    private Label _nowPlayingLabel;
    private Label _entryCountLabel;
    private LineEdit _searchBox;
    private HBoxContainer _parameterControls;
    private ScrollContainer _paramScroll;
    private Button _selectedEntryButton;
    private Font _fontRegular;
    private Font _fontBold;

    // ── FMOD state ────────────────────────────────────────────────────
    private GodotObject _fmodServer;
    private GodotObject _currentMusicInstance;
    private readonly List<GodotObject> _activeInstances = new();
    private string _currentLoopPath;
    private string _currentMusicPath;
    private string _musicToRestore;
    private float _lastMusicParamValue = -1f;

    // ── Event data ────────────────────────────────────────────────────
    private readonly List<string> _sfxEvents = new();
    private readonly List<string> _musicEvents = new();
    private readonly List<string> _ambienceEvents = new();
    private readonly List<string> _otherEvents = new();
    private readonly List<string> _tmpSfxEvents = new();
    private AudioStreamPlayer _tmpSfxPlayer;
    private readonly List<(Button button, string eventPath, string searchName, MarginContainer margin)> _audioEntries = new();

    // ═══════════════════════════════════════════════════════════════════
    // Initialization
    // ═══════════════════════════════════════════════════════════════════

    private GodotObject GetFmodServer()
    {
        _fmodServer ??= Engine.GetSingleton("FmodServer");
        return _fmodServer;
    }

    public void Initialize()
    {
        _musicToRestore = TrackCurrentMusicPatch.LastMusicPath;

        try
        {
            var screenSize = GetViewportRect().Size;
            _fontRegular = ResourceLoader.Load<Font>("res://fonts/kreon_regular.ttf");
            _fontBold = ResourceLoader.Load<Font>("res://fonts/kreon_bold.ttf");

            float availableHeight = Size.Y > 0 ? Size.Y : screenSize.Y - GlobalPosition.Y;
            float availableWidth = Size.X > 0 ? Size.X : screenSize.X;

            BuildSearchStrip(availableWidth);
            BuildBottomDock(availableWidth, availableHeight);
            BuildEventColumns(availableWidth, availableHeight);
            PopulateEventList();
        }
        catch (Exception e)
        {
            GD.PrintErr("[AudioBrowser] Error in Initialize: " + e);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // UI Construction
    // ═══════════════════════════════════════════════════════════════════

    private void BuildSearchStrip(float width)
    {
        AddColorStrip(Vector2.Zero, width, SearchBarHeight, PanelBg);
        AddColorStrip(new Vector2(0, SearchBarHeight), width, 1, PanelBorder);

        _searchBox = new LineEdit
        {
            PlaceholderText = "Search audio events...",
            ClearButtonEnabled = true,
            Position = new Vector2(Padding, 8),
            Size = new Vector2(360, 34)
        };
        ApplyFont(_searchBox, _fontRegular, 14);
        ApplySearchBoxStyle(_searchBox);
        _searchBox.TextChanged += OnSearchTextChanged;
        AddChild(_searchBox);

        _entryCountLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(width - Padding - 300, 8),
            Size = new Vector2(300, 34)
        };
        ApplyFont(_entryCountLabel, _fontRegular, 12);
        _entryCountLabel.AddThemeColorOverride("font_color", TextDim);
        AddChild(_entryCountLabel);
    }

    private void BuildBottomDock(float width, float height)
    {
        float dockY = height - BottomDockHeight;

        AddColorStrip(new Vector2(0, dockY), width, BottomDockHeight, ToolbarBg);
        AddColorStrip(new Vector2(0, dockY), width, 1, PanelBorder);

        // Now-playing bar
        float npY = dockY + 10;
        var npBg = CreateRoundedRect(NowPlayingBg, PanelBorder, 6, 1);
        npBg.Position = new Vector2(Padding, npY);
        npBg.Size = new Vector2(width - Padding * 2, NowPlayingHeight);
        AddChild(npBg);

        _nowPlayingLabel = new Label
        {
            Text = "Nothing playing",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(Padding + 12, npY),
            Size = new Vector2(width - Padding * 2 - 260, NowPlayingHeight)
        };
        ApplyFont(_nowPlayingLabel, _fontBold, 13);
        _nowPlayingLabel.AddThemeColorOverride("font_color", TextDim);
        AddChild(_nowPlayingLabel);

        // Transport buttons (right-aligned)
        float tbY = npY + 4;
        float tbH = NowPlayingHeight - 8;
        float tbRight = width - Padding - 8;
        AddTransportButton("Stop All",   tbRight - 58,                     tbY, 52, tbH, StopAll);
        AddTransportButton("Stop Loops", tbRight - 58 - 4 - 68,           tbY, 64, tbH, StopCurrentLoops);
        AddTransportButton("Stop Music", tbRight - 58 - 4 - 68 - 4 - 72, tbY, 68, tbH, StopCurrentMusic);

        // Parameter controls row
        float paramY = npY + NowPlayingHeight + 6;
        float paramHeight = BottomDockHeight - NowPlayingHeight - 24;

        _paramScroll = new ScrollContainer
        {
            Position = new Vector2(Padding, paramY),
            Size = new Vector2(width - Padding * 2, paramHeight),
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            Visible = false
        };
        AddChild(_paramScroll);

        _parameterControls = new HBoxContainer();
        _parameterControls.AddThemeConstantOverride("separation", 24);
        _paramScroll.AddChild(_parameterControls);
    }

    private void BuildEventColumns(float width, float height)
    {
        float listTop = SearchBarHeight + 1;
        float dockY = height - BottomDockHeight;

        var listScroll = new ScrollContainer
        {
            Position = new Vector2(0, listTop),
            Size = new Vector2(width, dockY - listTop),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(listScroll);

        var columnsHBox = new HBoxContainer();
        columnsHBox.CustomMinimumSize = new Vector2(width, 0);
        columnsHBox.AddThemeConstantOverride("separation", 0);
        listScroll.AddChild(columnsHBox);

        float colWidth = (width - Padding * 2 - 16) / 2f;

        columnsHBox.AddChild(CreateFixedSpacer(Padding));
        _leftListContainer = CreateEventColumn(colWidth);
        columnsHBox.AddChild(_leftListContainer);
        columnsHBox.AddChild(CreateFixedSpacer(16));
        _rightListContainer = CreateEventColumn(colWidth);
        columnsHBox.AddChild(_rightListContainer);
        columnsHBox.AddChild(CreateFixedSpacer(Padding));
    }

    private void AddTransportButton(string text, float x, float y, float w, float h, Action onPressed)
    {
        var btn = new Button
        {
            Text = text,
            Flat = true,
            Position = new Vector2(x, y),
            Size = new Vector2(w, h)
        };
        ApplyFont(btn, _fontRegular, 11);
        btn.AddThemeColorOverride("font_color", TextNormal);
        btn.AddThemeColorOverride("font_hover_color", TextBright);

        btn.AddThemeStyleboxOverride("normal",  MakeSmallButtonStyle(SmallButtonBg, PanelBorder));
        btn.AddThemeStyleboxOverride("hover",   MakeSmallButtonStyle(EntryHoverBg, Accent));
        btn.AddThemeStyleboxOverride("pressed", MakeSmallButtonStyle(EntryHoverBg, Accent));
        btn.AddThemeStyleboxOverride("focus",   MakeSmallButtonStyle(SmallButtonBg, PanelBorder));

        btn.Pressed += onPressed;
        AddChild(btn);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Event List Population
    // ═══════════════════════════════════════════════════════════════════

    private void PopulateEventList()
{
    try
    {
        var events = ExtractAllEventPaths();
        foreach (var path in events)
        {
            bool isLoop = path.Contains("_loop") || path.EndsWith("/loop");
            if (path.Contains("/music/"))
                _musicEvents.Add(path);
            else if (path.Contains("/ambience/") || path.Contains("/amb/"))
                _ambienceEvents.Add(path);
            else if (isLoop)
                _ambienceEvents.Add(path);
            else if (path.Contains("/sfx/"))
                _sfxEvents.Add(path);
            else
                _otherEvents.Add(path);
        }

        // Discover TmpSfx entries via reflection
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var tmpSfxType = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "TmpSfx");
                if (tmpSfxType == null) continue;

                foreach (var field in tmpSfxType.GetFields(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                {
                    if (field.FieldType != typeof(string) || !field.IsLiteral) continue;
                    if (field.GetValue(null) is string val
                        && (val.EndsWith(".mp3") || val.EndsWith(".tres")))
                    {
                        var fullPath = "res://debug_audio/" + val;
                        if (!_tmpSfxEvents.Contains(fullPath))
                            _tmpSfxEvents.Add(fullPath);
                    }
                }

                var assetPathsField = tmpSfxType.GetField("assetPaths",
                    BindingFlags.Public | BindingFlags.Static);
                if (assetPathsField?.GetValue(null) is IReadOnlyList<string> paths)
                {
                    foreach (var p in paths)
                    {
                        if (!_tmpSfxEvents.Contains(p))
                            _tmpSfxEvents.Add(p);
                    }
                }
                break;
            }
            catch { }
        }

        _musicEvents.Sort();
        _sfxEvents.Sort();
        _ambienceEvents.Sort();
        _otherEvents.Sort();
        _tmpSfxEvents.Sort();

        int total = _musicEvents.Count + _sfxEvents.Count + _ambienceEvents.Count
                  + _otherEvents.Count + _tmpSfxEvents.Count;
        _entryCountLabel.Text = $"{total} events  ·  {_musicEvents.Count} music  ·  "
                              + $"{_sfxEvents.Count} sfx  ·  {_ambienceEvents.Count} amb  ·  "
                              + $"{_tmpSfxEvents.Count} tmp";

        AddCategorySection(_leftListContainer,  "Music",    _musicEvents,    PlayAsMusic,   MusicTag);
        AddCategorySection(_leftListContainer,  "Ambience", _ambienceEvents, PlayAsLoop,    AmbienceTag);
        AddCategorySection(_rightListContainer, "SFX",      _sfxEvents,      PlayAsOneShot, SfxTag);
        if (_tmpSfxEvents.Count > 0)
            AddCategorySection(_rightListContainer, "Temp SFX", _tmpSfxEvents, PlayAsTmpSfx, SfxTag);
        if (_otherEvents.Count > 0)
            AddCategorySection(_rightListContainer, "Other", _otherEvents, PlayAsOneShot, OtherTag);
    }
    catch (Exception e)
    {
        _entryCountLabel.Text = "Error loading events";
        _entryCountLabel.AddThemeColorOverride("font_color", ErrorText);
    }
}

    private List<string> ExtractAllEventPaths()
    {
        var results = new HashSet<string>();

        try
        {
            var server = GetFmodServer();
            LoadAllBanks(server);

            var descriptions = server.Call("get_all_event_descriptions").AsGodotArray();
            foreach (var descVariant in descriptions)
            {
                try
                {
                    var path = descVariant.AsGodotObject().Call("get_path").AsString();
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("event:/"))
                        results.Add(path);
                }
                catch { }
            }
        }
        catch { }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                foreach (var field in type.GetFields(
                             BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.Static | BindingFlags.FlattenHierarchy))
                {
                    try
                    {
                        if (field.FieldType != typeof(string)) continue;
                        if (field.GetValue(null) is string value && value.StartsWith("event:/"))
                            results.Add(value);
                    }
                    catch { }
                }
            }
            catch { }
        }

        return results.ToList();
    }

    private void AddCategorySection(VBoxContainer column, string categoryName, List<string> events,
        Action<string> playAction, Color tagColor)
    {
        // Category header
        var headerMargin = new MarginContainer();
        headerMargin.AddThemeConstantOverride("margin_top", 8);
        headerMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.AddChild(headerMargin);

        var headerButton = new Button
        {
            Text = $"▼  {categoryName}  ({events.Count})",
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 30)
        };
        ApplyFont(headerButton, _fontBold, 14);
        headerButton.AddThemeColorOverride("font_color", tagColor);
        headerButton.AddThemeColorOverride("font_hover_color", Accent);
        headerMargin.AddChild(headerButton);

        var headerLine = new ColorRect
        {
            Color = new Color(tagColor.R, tagColor.G, tagColor.B, 0.25f),
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        column.AddChild(headerLine);

        int childStartIndex = column.GetChildCount();
        string lastGroup = "";

        foreach (var eventPath in events)
        {
            var parts = eventPath.Replace("event:/", "").Split('/');
            string group = parts.Length > 2
                ? string.Join("/", parts.Take(parts.Length - 1))
                : (parts.Length > 1 ? parts[0] : "");

            if (group != lastGroup)
            {
                AddGroupHeader(column, group);
                lastGroup = group;
            }

            AddEventEntry(column, eventPath, parts, playAction);
        }

        int childEndIndex = column.GetChildCount();
        SetupCollapseToggle(headerButton, column, childStartIndex, childEndIndex);
    }

    private void AddGroupHeader(VBoxContainer column, string group)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.AddChild(margin);

        var label = new Label { Text = group };
        ApplyFont(label, _fontRegular, 11);
        label.AddThemeColorOverride("font_color", FolderText);
        margin.AddChild(label);
    }

    private void AddEventEntry(VBoxContainer column, string eventPath, string[] parts, Action<string> playAction)
    {
        string displayName = parts.Last();
        string shortPath = parts.Length > 1 ? string.Join("/", parts.Take(parts.Length - 1)) : "";

        var entryMargin = new MarginContainer();
        entryMargin.AddThemeConstantOverride("margin_left", 10);
        entryMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.AddChild(entryMargin);

        var entryRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        entryRow.AddThemeConstantOverride("separation", 8);
        entryMargin.AddChild(entryRow);

        var button = new Button
        {
            Text = displayName,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 26)
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
            playAction(eventPath);
        };
        entryRow.AddChild(button);

        if (!string.IsNullOrEmpty(shortPath))
        {
            var pathLabel = new Label
            {
                Text = shortPath,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                ClipText = true
            };
            ApplyFont(pathLabel, _fontRegular, 10);
            pathLabel.AddThemeColorOverride("font_color", PathDimText);
            entryRow.AddChild(pathLabel);
        }

        _audioEntries.Add((button, eventPath, displayName.ToLowerInvariant(), entryMargin));
    }

    private static void SetupCollapseToggle(Button headerButton, VBoxContainer column, int startIdx, int endIdx)
    {
        headerButton.Pressed += () =>
        {
            bool collapsing = true;
            for (int i = startIdx; i < endIdx; i++)
            {
                if (column.GetChild(i) is not Control child) continue;
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
        {
            ExpandAllChildren(_leftListContainer);
            ExpandAllChildren(_rightListContainer);
        }

        foreach (var (_, path, searchName, margin) in _audioEntries)
            margin.Visible = !hasFilter || searchName.Contains(filter) || path.ToLowerInvariant().Contains(filter);

        if (!hasFilter)
        {
            ExpandAllChildren(_leftListContainer);
            ExpandAllChildren(_rightListContainer);
        }
    }

    private static void ExpandAllChildren(VBoxContainer container)
    {
        for (int i = 0; i < container.GetChildCount(); i++)
        {
            if (container.GetChild(i) is Control child)
                child.Visible = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Playback
    // ═══════════════════════════════════════════════════════════════════

    private void PlayAsMusic(string eventPath)
    {
        try
        {
            StopAll();
            var server = GetFmodServer();

            if (!EnsureEventExists(server, eventPath))
            {
                UpdateNowPlaying(null, "Event not found: " + eventPath, true);
                return;
            }

            var instance = server.Call("create_event_instance", eventPath).AsGodotObject();
            instance.Call("start");
            _currentMusicInstance = instance;
            _activeInstances.Add(instance);
            _currentMusicPath = eventPath;
            UpdateNowPlaying("Music", eventPath);
            BuildParameterControls(server, eventPath, instance);
        }
        catch (Exception e)
        {
            UpdateNowPlaying(null, "Error: " + e.Message, true);
        }
    }

    private void PlayAsOneShot(string eventPath)
    {
        try
        {
            var server = GetFmodServer();
            if (!server.Call("check_event_path", eventPath).AsBool())
            {
                UpdateNowPlaying(null, "Not found: " + eventPath, true);
                return;
            }
            server.Call("play_one_shot", eventPath);
            UpdateNowPlaying("SFX", eventPath);
        }
        catch (Exception e)
        {
            UpdateNowPlaying(null, "Error: " + e.Message, true);
        }
    }

    private void PlayAsLoop(string eventPath)
    {
        try
        {
            var server = GetFmodServer();
            if (!server.Call("check_event_path", eventPath).AsBool())
            {
                UpdateNowPlaying(null, "Not found: " + eventPath, true);
                return;
            }
            if (_currentLoopPath != null)
                NAudioManager.Instance?.StopLoop(_currentLoopPath);

            NAudioManager.Instance?.PlayLoop(eventPath, false);
            _currentLoopPath = eventPath;
            UpdateNowPlaying("Loop", eventPath);
        }
        catch (Exception e)
        {
            UpdateNowPlaying(null, "Error: " + e.Message, true);
        }
    }
    
    private void PlayAsTmpSfx(string resourcePath)
    {
        try
        {
            var stream = ResourceLoader.Load<AudioStream>(resourcePath);
            if (stream == null)
            {
                UpdateNowPlaying(null, "Not found: " + resourcePath, true);
                return;
            }

            _tmpSfxPlayer ??= new AudioStreamPlayer();
            if (_tmpSfxPlayer.GetParent() == null)
                AddChild(_tmpSfxPlayer);

            _tmpSfxPlayer.Stream = stream;
            _tmpSfxPlayer.Play();
            UpdateNowPlaying("TmpSFX", resourcePath);
        }
        catch (Exception e)
        {
            UpdateNowPlaying(null, "Error: " + e.Message, true);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Parameters
    // ═══════════════════════════════════════════════════════════════════

    private void SetParameter(string paramName, float value, string eventPath, string displayValue)
    {
        try { GetFmodServer().Call("set_global_parameter_by_name", paramName, value); }
        catch { }

        if (_currentMusicInstance != null && GodotObject.IsInstanceValid(_currentMusicInstance))
        {
            try { _currentMusicInstance.Call("set_parameter_by_name", paramName, value); }
            catch { }
        }

        UpdateNowPlayingWithParam(eventPath, paramName, displayValue);
    }

    private void SetLabeledMusicParameter(string paramName, float value, string eventPath, string displayValue)
    {
        try
        {
            var server = GetFmodServer();
            try { server.Call("set_global_parameter_by_name", paramName, value); }
            catch { }

            RecreateCurrentMusicInstance(server, eventPath);
        }
        catch { }

        UpdateNowPlayingWithParam(eventPath, paramName, displayValue);
    }

    private void SetMusicParameter(string paramName, float value, string eventPath,
        string displayValue, bool isGlobal)
    {
        try
        {
            var server = GetFmodServer();

            bool needsRecreate = _currentMusicPath != eventPath
                                 || _currentMusicInstance == null
                                 || !GodotObject.IsInstanceValid(_currentMusicInstance);

            if (needsRecreate)
                RecreateCurrentMusicInstance(server, eventPath);

            TrySetParameter(_currentMusicInstance, server, paramName, value, isGlobal);
            _lastMusicParamValue = value;
        }
        catch { }

        UpdateNowPlayingWithParam(eventPath, paramName, displayValue);
    }

    private void TrySetParameter(GodotObject instance, GodotObject server, string paramName,
        float value, bool isGlobal)
    {
        if (isGlobal)
        {
            try { server.Call("set_global_parameter_by_name", paramName, value); }
            catch { }
            return;
        }

        try
        {
            instance.Call("set_parameter_by_name", paramName, value);
            return;
        }
        catch { }

        try { server.Call("set_global_parameter_by_name", paramName, value); }
        catch { }
    }

    private void ResetMusicInstance(string eventPath)
    {
        try
        {
            RecreateCurrentMusicInstance(GetFmodServer(), eventPath);
            _lastMusicParamValue = 0f;
        }
        catch { }
    }

    private void RecreateCurrentMusicInstance(GodotObject server, string eventPath)
    {
        ReleaseCurrentMusicInstance();
        EnsureBanksLoaded(server, eventPath);

        var newInstance = server.Call("create_event_instance", eventPath).AsGodotObject();
        newInstance.Call("start");
        _currentMusicInstance = newInstance;
        _activeInstances.Add(newInstance);
    }

    private void ReleaseCurrentMusicInstance()
    {
        if (_currentMusicInstance == null || !GodotObject.IsInstanceValid(_currentMusicInstance))
            return;

        _currentMusicInstance.Call("stop", 0);
        _currentMusicInstance.Call("release");
        _activeInstances.Remove(_currentMusicInstance);
        _currentMusicInstance = null;
    }

    private void EnsureBanksLoaded(GodotObject server, string eventPath)
    {
        if (server.Call("check_event_path", eventPath).AsBool())
            return;
        LoadAllBanks(server);
    }

    private bool EnsureEventExists(GodotObject server, string eventPath)
    {
        if (server.Call("check_event_path", eventPath).AsBool())
            return true;
        LoadAllBanks(server);
        return server.Call("check_event_path", eventPath).AsBool();
    }

    private static void LoadAllBanks(GodotObject server)
    {
        foreach (var bankFile in BankFiles)
        {
            try { server.Call("load_bank", BankDir + bankFile, 0); }
            catch { }
        }
        server.Call("wait_for_all_loads");
    }

    private async void WalkParameterToTarget(string paramName, float target, string eventPath,
        string displayValue, bool isGlobal)
    {
        try
        {
            for (float v = 1f; v <= target; v += 1f)
            {
                if (_currentMusicInstance == null || !GodotObject.IsInstanceValid(_currentMusicInstance))
                    return;
                await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
                _currentMusicInstance.Call("set_parameter_by_name", paramName, v);
                _lastMusicParamValue = v;
            }
        }
        catch { }

        UpdateNowPlayingWithParam(eventPath, paramName, displayValue);
    }

    private void BuildParameterControls(GodotObject server, string eventPath, GodotObject instance)
    {
        ClearParameterControls();

        try
        {
            var desc = server.Call("get_event", eventPath).AsGodotObject();
            var paramCount = desc.Call("get_parameter_count").AsInt32();
            if (paramCount == 0) return;

            _paramScroll.Visible = true;
            bool isMusic = eventPath == _currentMusicPath;

            for (int i = 0; i < paramCount; i++)
            {
                var param = desc.Call("get_parameter_by_index", i).AsGodotObject();
                var paramName = param.Call("get_name").AsString();
                var paramMin = (float)param.Call("get_minimum").AsDouble();
                var paramMax = (float)param.Call("get_maximum").AsDouble();

                var labelList = ExtractParameterLabels(desc, i);

                if (i > 0)
                    AddParameterSeparator();

                var paramGroup = new VBoxContainer();
                paramGroup.AddThemeConstantOverride("separation", 3);

                AddParameterNameLabel(paramGroup, paramName);

                if (labelList.Count > 0)
                    AddLabeledParameterButtons(paramGroup, paramName, labelList, eventPath, isMusic);
                else if (isMusic)
                    AddMusicStepperControls(paramGroup, paramName, paramMin, paramMax, eventPath);
                else
                    AddSliderControl(paramGroup, paramName, paramMin, paramMax, eventPath);

                _parameterControls.AddChild(paramGroup);
            }
        }
        catch { }
    }

    private List<string> ExtractParameterLabels(GodotObject desc, int index)
    {
        var labels = new List<string>();
        try
        {
            var arr = desc.Call("get_parameter_labels_by_index", index).AsGodotArray();
            foreach (var label in arr)
                labels.Add(label.AsString());
        }
        catch { }
        return labels;
    }

    private void AddParameterSeparator()
    {
        var sep = new ColorRect
        {
            Color = PanelBorder,
            CustomMinimumSize = new Vector2(1, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _parameterControls.AddChild(sep);
    }

    private void AddParameterNameLabel(VBoxContainer group, string name)
    {
        var label = new Label { Text = name };
        ApplyFont(label, _fontBold, 11);
        label.AddThemeColorOverride("font_color", Accent);
        group.AddChild(label);
    }

    private void AddLabeledParameterButtons(VBoxContainer group, string paramName,
        List<string> labels, string eventPath, bool isMusic)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 3);

        for (int v = 0; v < labels.Count; v++)
        {
            var btn = new Button
            {
                Text = labels[v],
                CustomMinimumSize = new Vector2(0, 24)
            };
            ApplyFont(btn, _fontRegular, 11);
            btn.AddThemeColorOverride("font_color", TextNormal);
            btn.AddThemeColorOverride("font_hover_color", TextBright);
            btn.AddThemeStyleboxOverride("normal", MakeSmallButtonStyle(SmallButtonBg, PanelBorder));

            float value = v;
            string label = labels[v];
            btn.Pressed += () =>
            {
                if (isMusic)
                    SetLabeledMusicParameter(paramName, value, eventPath, label);
                else
                    SetParameter(paramName, value, eventPath, label);
            };
            row.AddChild(btn);
        }
        group.AddChild(row);
    }

    private void AddMusicStepperControls(VBoxContainer group, string paramName,
        float paramMin, float paramMax, string eventPath)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        float currentVal = paramMin;

        var valueLabel = new Label
        {
            Text = paramMin.ToString("F0"),
            CustomMinimumSize = new Vector2(28, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        ApplyFont(valueLabel, _fontRegular, 12);
        valueLabel.AddThemeColorOverride("font_color", TextBright);

        var downBtn = CreateStepperButton("⟲");
        var upBtn = CreateStepperButton("+");

        downBtn.Pressed += () =>
        {
            if (currentVal <= paramMin) return;
            currentVal = paramMin;
            valueLabel.Text = currentVal.ToString("F0");
            ResetMusicInstance(eventPath);
            _nowPlayingLabel.Text = $"Music:  {eventPath}  [Reset to 0]";
            _nowPlayingLabel.AddThemeColorOverride("font_color", TextBright);
        };

        upBtn.Pressed += () =>
        {
            if (currentVal >= paramMax) return;
            currentVal += 1f;
            valueLabel.Text = currentVal.ToString("F0");
            SetMusicParameter(paramName, currentVal, eventPath, currentVal.ToString("F0"), false);
        };

        row.AddChild(downBtn);
        row.AddChild(valueLabel);
        row.AddChild(upBtn);
        group.AddChild(row);
    }

    private void AddSliderControl(VBoxContainer group, string paramName,
        float paramMin, float paramMax, string eventPath)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var slider = new HSlider
        {
            MinValue = paramMin,
            MaxValue = paramMax,
            Step = 1,
            Value = paramMin,
            CustomMinimumSize = new Vector2(160, 18)
        };

        var valueLabel = new Label
        {
            Text = paramMin.ToString("F0"),
            CustomMinimumSize = new Vector2(28, 0)
        };
        ApplyFont(valueLabel, _fontRegular, 12);
        valueLabel.AddThemeColorOverride("font_color", TextBright);

        slider.ValueChanged += val =>
        {
            valueLabel.Text = val.ToString("F0");
            SetParameter(paramName, (float)val, eventPath, val.ToString("F0"));
        };

        row.AddChild(slider);
        row.AddChild(valueLabel);
        group.AddChild(row);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stop / Cleanup
    // ═══════════════════════════════════════════════════════════════════

    private void StopCurrentMusic()
    {
        StopAllInstances();
        NAudioManager.Instance?.StopMusic();
        _currentMusicPath = null;

        if (_currentLoopPath != null)
            UpdateNowPlaying("Loop", _currentLoopPath);
        else
            ResetNowPlaying();
    }

    private void StopCurrentLoops()
    {
        NAudioManager.Instance?.StopAllLoops();
        _currentLoopPath = null;

        if (_currentMusicPath != null)
            UpdateNowPlaying("Music", _currentMusicPath);
        else
            ResetNowPlaying();
    }

    private void StopAll()
    {
        StopAllInstances();
        NAudioManager.Instance?.StopMusic();
        NAudioManager.Instance?.StopAllLoops();
        _currentMusicPath = null;
        _currentLoopPath = null;

        if (_tmpSfxPlayer != null && _tmpSfxPlayer.Playing)
            _tmpSfxPlayer.Stop();

        ResetNowPlaying();
        ClearParameterControls();
        if (_selectedEntryButton != null)
        {
            ApplyEntryButtonStyle(_selectedEntryButton);
            _selectedEntryButton = null;
        }
    }

    private void StopAllInstances()
    {
        foreach (var instance in _activeInstances)
        {
            try
            {
                if (instance != null && GodotObject.IsInstanceValid(instance))
                {
                    instance.Call("stop", 1);
                    instance.Call("release");
                }
            }
            catch { }
        }
        _activeInstances.Clear();
        _currentMusicInstance = null;
    }

    public void CleanupAndRestore()
    {
        StopAll();

        if (!string.IsNullOrEmpty(_musicToRestore)
            && RunManager.Instance?.IsInProgress != true)
            NAudioManager.Instance?.PlayMusic(_musicToRestore);
    }

    // ═══════════════════════════════════════════════════════════════════
    // UI Helpers
    // ═══════════════════════════════════════════════════════════════════

    private void UpdateNowPlaying(string prefix, string text, bool isError = false)
    {
        _nowPlayingLabel.Text = prefix != null ? $"{prefix}:  {text}" : text;
        _nowPlayingLabel.AddThemeColorOverride("font_color", isError ? ErrorText : TextBright);
    }

    private void UpdateNowPlayingWithParam(string eventPath, string paramName, string displayValue)
    {
        _nowPlayingLabel.Text = $"Music:  {eventPath}  [{paramName}={displayValue}]";
        _nowPlayingLabel.AddThemeColorOverride("font_color", TextBright);
    }

    private void ResetNowPlaying()
    {
        _nowPlayingLabel.Text = "Nothing playing";
        _nowPlayingLabel.AddThemeColorOverride("font_color", TextDim);
    }

    private void ClearParameterControls()
    {
        if (_parameterControls == null) return;
        foreach (Node child in _parameterControls.GetChildren())
            child.QueueFree();
        _paramScroll.Visible = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Style Factories
    // ═══════════════════════════════════════════════════════════════════

    private void ApplyEntryButtonStyle(Button btn)
    {
        var normal = MakeFlatStyle(Colors.Transparent, 4, 8);
        var hover  = MakeFlatStyle(EntryHoverBg, 4, 8);

        btn.AddThemeStyleboxOverride("normal",  normal);
        btn.AddThemeStyleboxOverride("hover",   hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus",   normal);
        btn.AddThemeColorOverride("font_color", TextNormal);
    }

    private void ApplySelectedEntryStyle(Button btn)
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

    private void ApplySearchBoxStyle(LineEdit lineEdit)
    {
        var normal = MakeSearchStyle(SearchBorder);
        var focus  = MakeSearchStyle(Accent);

        lineEdit.AddThemeStyleboxOverride("normal", normal);
        lineEdit.AddThemeStyleboxOverride("focus", focus);
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

    private Button CreateStepperButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(32, 24)
        };
        ApplyFont(btn, _fontBold, 14);
        btn.AddThemeColorOverride("font_color", TextNormal);
        btn.AddThemeColorOverride("font_hover_color", TextBright);

        var style = MakeSmallButtonStyle(SmallButtonBg, PanelBorder);
        btn.AddThemeStyleboxOverride("normal",  style);
        btn.AddThemeStyleboxOverride("hover",   (StyleBoxFlat)style.Duplicate());
        btn.AddThemeStyleboxOverride("pressed", (StyleBoxFlat)style.Duplicate());
        return btn;
    }

    private void AddColorStrip(Vector2 position, float width, float height, Color color)
    {
        var rect = new ColorRect
        {
            Color = color,
            Position = position,
            Size = new Vector2(width, height)
        };
        AddChild(rect);
    }

    private static Control CreateFixedSpacer(float width)
    {
        return new Control { CustomMinimumSize = new Vector2(width, 0) };
    }

    private static VBoxContainer CreateEventColumn(float width)
    {
        var col = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        col.AddThemeConstantOverride("separation", 1);
        return col;
    }

    // ═══════════════════════════════════════════════════════════════════
    // StyleBox Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static StyleBoxFlat MakeFlatStyle(Color bg, int cornerRadius, int leftMargin = 0)
    {
        var style = new StyleBoxFlat { BgColor = bg };
        SetAllCornerRadius(style, cornerRadius);
        style.ContentMarginLeft = leftMargin;
        return style;
    }

    private static StyleBoxFlat MakeSmallButtonStyle(Color bg, Color border)
    {
        var style = new StyleBoxFlat { BgColor = bg };
        SetAllCornerRadius(style, 4);
        SetAllBorderWidth(style, 1);
        style.BorderColor = border;
        style.ContentMarginLeft = 6;
        style.ContentMarginRight = 6;
        return style;
    }

    private static StyleBoxFlat MakeSearchStyle(Color border)
    {
        var style = new StyleBoxFlat { BgColor = SearchBg };
        SetAllCornerRadius(style, 6);
        SetAllBorderWidth(style, 1);
        style.BorderColor = border;
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