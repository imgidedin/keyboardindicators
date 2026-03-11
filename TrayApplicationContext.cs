using Microsoft.Win32;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Timer = System.Windows.Forms.Timer;
using Windows.ApplicationModel;

namespace KeyboardIndicators;

[SupportedOSPlatform("windows10.0.17763.0")]
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string AppName = "KeyboardIndicators";
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const int DefaultRefreshIntervalMs = 2000;
    private const int ImmediateRefreshIntervalMs = 1;

    private static readonly FieldInfo? NotifyIconIdField = typeof(NotifyIcon).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NotifyIconWindowField = typeof(NotifyIcon).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _refreshNowMenuItem;
    private readonly ToolStripMenuItem _configureShortcutsMenuItem;
    private readonly ToolStripMenuItem _languageMenuItem;
    private readonly ToolStripMenuItem _systemLanguageMenuItem;
    private readonly ToolStripMenuItem _englishLanguageMenuItem;
    private readonly ToolStripMenuItem _portugueseBrazilLanguageMenuItem;
    private readonly ToolStripMenuItem _launchAtStartupMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;
    private readonly Timer _refreshTimer;
    private readonly Timer _immediateRefreshTimer;
    private readonly Timer _hoverMonitorTimer;
    private readonly Timer _showDelayTimer;
    private readonly Timer _hideDelayTimer;
    private readonly AppSettingsStore _settingsStore;
    private readonly StatusPopupForm _statusPopup;
    private readonly KeyboardIndicatorHook _keyboardHook;
    private readonly Dictionary<IconCacheKey, Icon> _iconCache = [];

    private AppSettings _settings;
    private readonly bool _isPackaged;
    private AppLanguage _effectiveLanguage;
    private AppStrings _strings;
    private StartupTaskState? _packagedStartupState;
    private bool _lastNumLock;
    private bool _lastCapsLock;
    private bool _lastScrollLock;
    private bool _lastAppliedTaskbarIsLight;
    private bool _lastAppliedAppsLightTheme;
    private bool _taskbarIsLight;
    private bool _appsLightTheme;
    private Rectangle _lastTrayIconBounds = Rectangle.Empty;
    private DateTime _suppressPopupUntilUtc = DateTime.MinValue;
    private bool _hoveringTrayIcon;

    public TrayApplicationContext()
    {
        _settingsStore = new AppSettingsStore();
        _settings = _settingsStore.Load();
        _statusPopup = new StatusPopupForm();
        _isPackaged = IsRunningPackaged();
        _effectiveLanguage = AppLocalization.Resolve(_settings.LanguagePreference);
        _strings = AppLocalization.Get(_effectiveLanguage);
        _taskbarIsLight = IsTaskbarLightTheme();
        _appsLightTheme = IsAppsLightTheme();
        _keyboardHook = new KeyboardIndicatorHook(OnKeyboardIndicatorKeyPressed);

        _refreshNowMenuItem = new ToolStripMenuItem(string.Empty, null, (_, _) => RefreshIcon(force: true));
        _configureShortcutsMenuItem = new ToolStripMenuItem(string.Empty, null, OpenShortcutSettings);
        _systemLanguageMenuItem = new ToolStripMenuItem(string.Empty, null, (_, _) => SetLanguagePreference(null));
        _englishLanguageMenuItem = new ToolStripMenuItem(string.Empty, null, (_, _) => SetLanguagePreference(AppLanguagePreference.English));
        _portugueseBrazilLanguageMenuItem = new ToolStripMenuItem(string.Empty, null, (_, _) => SetLanguagePreference(AppLanguagePreference.PortugueseBrazil));
        _languageMenuItem = new ToolStripMenuItem(string.Empty, null,
            _systemLanguageMenuItem,
            _englishLanguageMenuItem,
            _portugueseBrazilLanguageMenuItem);
        _launchAtStartupMenuItem = new ToolStripMenuItem(string.Empty, null, ToggleLaunchAtStartup);
        _exitMenuItem = new ToolStripMenuItem(string.Empty, null, (_, _) => ExitThread());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Opening += (_, _) => SuppressAndHidePopup();
        contextMenu.Closing += (_, _) => _suppressPopupUntilUtc = DateTime.UtcNow.AddMilliseconds(150);
        contextMenu.Items.Add(_refreshNowMenuItem);
        contextMenu.Items.Add(_configureShortcutsMenuItem);
        contextMenu.Items.Add(_languageMenuItem);
        contextMenu.Items.Add(_launchAtStartupMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Text = string.Empty,
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        _notifyIcon.MouseMove += (_, e) => OnNotifyIconMouseMove(e);
        _notifyIcon.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                SuppressAndHidePopup();
            }
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                SuppressAndHidePopup();
            }
        };

        _refreshTimer = new Timer
        {
            Interval = DefaultRefreshIntervalMs
        };
        _refreshTimer.Tick += (_, _) => RefreshIcon(force: false);

        _immediateRefreshTimer = new Timer
        {
            Interval = ImmediateRefreshIntervalMs
        };
        _immediateRefreshTimer.Tick += (_, _) =>
        {
            _immediateRefreshTimer.Stop();
            RefreshIcon(force: false);
        };

        _hoverMonitorTimer = new Timer
        {
            Interval = 50
        };
        _hoverMonitorTimer.Tick += (_, _) => MonitorHoverState();

        _showDelayTimer = new Timer
        {
            Interval = Math.Max(SystemInformation.MouseHoverTime, 400)
        };
        _showDelayTimer.Tick += (_, _) =>
        {
            _showDelayTimer.Stop();
            if (_hoveringTrayIcon && DateTime.UtcNow >= _suppressPopupUntilUtc)
            {
                ShowStatusPopup();
            }
        };

        _hideDelayTimer = new Timer
        {
            Interval = 180
        };
        _hideDelayTimer.Tick += (_, _) =>
        {
            _hideDelayTimer.Stop();
            if (!IsCursorInsideActiveHoverZone())
            {
                HideStatusPopup(animated: true);
            }
        };

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        ApplyLanguage();
        UpdateLaunchAtStartupMenu();
        RefreshIcon(force: true);
        _refreshTimer.Start();
        _ = RefreshPackagedStartupStateAsync();
    }

    protected override void ExitThreadCore()
    {
        _immediateRefreshTimer.Stop();
        _hoverMonitorTimer.Stop();
        _showDelayTimer.Stop();
        _hideDelayTimer.Stop();
        _refreshTimer.Stop();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        HideStatusPopup(animated: false);
        _keyboardHook.Dispose();
        _statusPopup.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        foreach (var icon in _iconCache.Values)
        {
            icon.Dispose();
        }

        base.ExitThreadCore();
    }

    private void OnNotifyIconMouseMove(MouseEventArgs e)
    {
        if (DateTime.UtcNow < _suppressPopupUntilUtc)
        {
            return;
        }

        _hoveringTrayIcon = true;
        _lastTrayIconBounds = TryGetTrayIconBounds() ?? EstimateTrayIconBoundsFromCursor();
        _hideDelayTimer.Stop();

        if (_statusPopup.Visible)
        {
            return;
        }

        if (!_showDelayTimer.Enabled)
        {
            _showDelayTimer.Start();
        }

        if (!_hoverMonitorTimer.Enabled)
        {
            _hoverMonitorTimer.Start();
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            _taskbarIsLight = IsTaskbarLightTheme();
            _appsLightTheme = IsAppsLightTheme();
            if (_settings.LanguagePreference is null)
            {
                ApplyLanguage();
            }
            RefreshIcon(force: true);
        }
    }

    private void OnKeyboardIndicatorKeyPressed()
    {
        _immediateRefreshTimer.Stop();
        _immediateRefreshTimer.Start();
    }

    private async void ToggleLaunchAtStartup(object? sender, EventArgs e)
    {
        if (_isPackaged)
        {
            await TogglePackagedLaunchAtStartupAsync();
            return;
        }

        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunRegistryPath);

        if (runKey is null)
        {
            return;
        }

        if (IsLaunchAtStartupEnabled())
        {
            runKey.DeleteValue(AppName, throwOnMissingValue: false);
        }
        else
        {
            runKey.SetValue(AppName, $"\"{Application.ExecutablePath}\"");
        }

        UpdateLaunchAtStartupMenu();
    }

    [SupportedOSPlatform("windows10.0.17763.0")]
    private async Task TogglePackagedLaunchAtStartupAsync()
    {
        _launchAtStartupMenuItem.Enabled = false;
        var shouldEnable = _packagedStartupState is not StartupTaskState.Enabled;
        _packagedStartupState = await StartupTaskManager.SetEnabledAsync(shouldEnable);
        UpdateLaunchAtStartupMenu();
    }

    private void OpenShortcutSettings(object? sender, EventArgs e)
    {
        SuppressAndHidePopup();

        using var form = new ShortcutSettingsForm(_settings, _strings);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settings = form.Settings;
        _settingsStore.Save(_settings);
        ApplyLanguage();
        RefreshIcon(force: true);
    }

    [SupportedOSPlatform("windows10.0.17763.0")]
    private void UpdateLaunchAtStartupMenu()
    {
        if (_isPackaged)
        {
            _launchAtStartupMenuItem.Text = _strings.LaunchAtStartup;
            _launchAtStartupMenuItem.CheckOnClick = false;
            _launchAtStartupMenuItem.Checked = _packagedStartupState == StartupTaskState.Enabled;
            _launchAtStartupMenuItem.Enabled = _packagedStartupState is not StartupTaskState.DisabledByPolicy;
            return;
        }

        _launchAtStartupMenuItem.Text = _strings.LaunchAtStartup;
        _launchAtStartupMenuItem.CheckOnClick = true;
        _launchAtStartupMenuItem.Enabled = true;
        _launchAtStartupMenuItem.Checked = IsLaunchAtStartupEnabled();
    }

    [SupportedOSPlatform("windows10.0.17763.0")]
    private async Task RefreshPackagedStartupStateAsync()
    {
        if (!_isPackaged)
        {
            return;
        }

        _packagedStartupState = await StartupTaskManager.GetStateAsync();
        if (_notifyIcon.Container is null && _notifyIcon.Icon is null)
        {
            return;
        }

        if (Application.MessageLoop)
        {
            _notifyIcon.ContextMenuStrip?.BeginInvoke(UpdateLaunchAtStartupMenu);
        }
        else
        {
            _launchAtStartupMenuItem.CheckOnClick = false;
            UpdateLaunchAtStartupMenu();
        }
    }

    private void SetLanguagePreference(AppLanguagePreference? preference)
    {
        if (_settings.LanguagePreference == preference)
        {
            return;
        }

        _settings.LanguagePreference = preference;
        _settingsStore.Save(_settings);
        ApplyLanguage();
        RefreshIcon(force: true);
    }

    private void ApplyLanguage()
    {
        _effectiveLanguage = AppLocalization.Resolve(_settings.LanguagePreference);
        _strings = AppLocalization.Get(_effectiveLanguage);
        _statusPopup.ApplyStrings(_strings);
        UpdateMenuTexts();
        UpdateLaunchAtStartupMenu();
    }

    private void UpdateMenuTexts()
    {
        _refreshNowMenuItem.Text = _strings.RefreshNow;
        _configureShortcutsMenuItem.Text = _strings.SettingsMenu;
        _languageMenuItem.Text = _strings.LanguageMenu;
        _systemLanguageMenuItem.Text = _strings.UseSystemLanguage;
        _englishLanguageMenuItem.Text = _strings.EnglishLabel;
        _portugueseBrazilLanguageMenuItem.Text = _strings.PortugueseBrazilLabel;
        _exitMenuItem.Text = _strings.Exit;

        _systemLanguageMenuItem.Checked = _settings.LanguagePreference is null;
        _englishLanguageMenuItem.Checked = _settings.LanguagePreference == AppLanguagePreference.English;
        _portugueseBrazilLanguageMenuItem.Checked = _settings.LanguagePreference == AppLanguagePreference.PortugueseBrazil;
    }

    private static bool IsLaunchAtStartupEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
        var value = runKey?.GetValue(AppName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    private void RefreshIcon(bool force)
    {
        var numLock = Control.IsKeyLocked(Keys.NumLock);
        var capsLock = Control.IsKeyLocked(Keys.CapsLock);
        var scrollLock = Control.IsKeyLocked(Keys.Scroll);

        if (!force &&
            numLock == _lastNumLock &&
            capsLock == _lastCapsLock &&
            scrollLock == _lastScrollLock &&
            _taskbarIsLight == _lastAppliedTaskbarIsLight &&
            _appsLightTheme == _lastAppliedAppsLightTheme)
        {
            return;
        }

        _lastNumLock = numLock;
        _lastCapsLock = capsLock;
        _lastScrollLock = scrollLock;
        _lastAppliedTaskbarIsLight = _taskbarIsLight;
        _lastAppliedAppsLightTheme = _appsLightTheme;

        var activeColor = _settings.GetActiveIndicatorColor();
        _notifyIcon.Icon = GetOrCreateIcon(numLock, capsLock, scrollLock, _taskbarIsLight, activeColor);
        _statusPopup.ApplyTheme(_appsLightTheme);
        _statusPopup.ApplyActiveColor(activeColor);
        _statusPopup.UpdateContent(numLock, capsLock, scrollLock, _settings, _strings);
    }

    private void ShowStatusPopup()
    {
        _hideDelayTimer.Stop();
        _statusPopup.UpdateContent(_lastNumLock, _lastCapsLock, _lastScrollLock, _settings, _strings);
        _lastTrayIconBounds = TryGetTrayIconBounds() ?? (_lastTrayIconBounds.IsEmpty ? EstimateTrayIconBoundsFromCursor() : _lastTrayIconBounds);
        _statusPopup.Location = CalculatePopupLocation(_lastTrayIconBounds, _statusPopup.Size);
        _statusPopup.ShowAnimated();

        if (!_hoverMonitorTimer.Enabled)
        {
            _hoverMonitorTimer.Start();
        }
    }

    private void MonitorHoverState()
    {
        _hoveringTrayIcon = IsCursorWithinTrayBounds();

        if (!_statusPopup.Visible && !_showDelayTimer.Enabled)
        {
            _hoverMonitorTimer.Stop();
            return;
        }

        if (!_hoveringTrayIcon && !_statusPopup.Visible)
        {
            _showDelayTimer.Stop();
            _hoverMonitorTimer.Stop();
            return;
        }

        if (IsCursorInsideActiveHoverZone())
        {
            _hideDelayTimer.Stop();
            return;
        }

        if (!_hideDelayTimer.Enabled)
        {
            _hideDelayTimer.Start();
        }
    }

    private void SuppressAndHidePopup()
    {
        _hoveringTrayIcon = false;
        _showDelayTimer.Stop();
        _hideDelayTimer.Stop();
        _suppressPopupUntilUtc = DateTime.UtcNow.AddMilliseconds(350);
        HideStatusPopup(animated: false);
    }

    private void HideStatusPopup(bool animated)
    {
        _hoverMonitorTimer.Stop();
        _showDelayTimer.Stop();
        _hideDelayTimer.Stop();
        _statusPopup.HideAnimated(animated);
    }

    private static Rectangle EstimateTrayIconBoundsFromCursor()
    {
        var cursor = Cursor.Position;
        return new Rectangle(cursor.X - 8, cursor.Y - 8, 16, 16);
    }

    private static Point CalculatePopupLocation(Rectangle trayBounds, Size popupSize)
    {
        var screen = Screen.FromPoint(trayBounds.Location).WorkingArea;
        var x = trayBounds.Left + ((trayBounds.Width - popupSize.Width) / 2);
        var y = trayBounds.Top - popupSize.Height - 8;

        if (y < screen.Top)
        {
            y = trayBounds.Bottom + 8;
        }

        if (x < screen.Left)
        {
            x = screen.Left + 6;
        }
        else if (x + popupSize.Width > screen.Right)
        {
            x = screen.Right - popupSize.Width - 6;
        }

        if (y + popupSize.Height > screen.Bottom)
        {
            y = Math.Max(screen.Top + 6, screen.Bottom - popupSize.Height - 6);
        }

        return new Point(x, y);
    }

    private bool IsCursorInsideActiveHoverZone()
    {
        var cursor = Cursor.Position;
        var trayBounds = _lastTrayIconBounds.IsEmpty ? EstimateTrayIconBoundsFromCursor() : _lastTrayIconBounds;
        var paddedTrayBounds = Rectangle.Inflate(trayBounds, 8, 8);

        if (paddedTrayBounds.Contains(cursor))
        {
            return true;
        }

        if (!_statusPopup.Visible)
        {
            return false;
        }

        var paddedPopupBounds = Rectangle.Inflate(_statusPopup.BoundsOnScreen, 8, 8);
        if (paddedPopupBounds.Contains(cursor))
        {
            return true;
        }

        return CreateHoverBridgeBounds(paddedTrayBounds, paddedPopupBounds).Contains(cursor);
    }

    private bool IsCursorWithinTrayBounds()
    {
        var trayBounds = TryGetTrayIconBounds() ?? (_lastTrayIconBounds.IsEmpty ? EstimateTrayIconBoundsFromCursor() : _lastTrayIconBounds);
        _lastTrayIconBounds = trayBounds;
        return Rectangle.Inflate(trayBounds, 6, 6).Contains(Cursor.Position);
    }

    private static Rectangle CreateHoverBridgeBounds(Rectangle trayBounds, Rectangle popupBounds)
    {
        var centerX = trayBounds.Left + (trayBounds.Width / 2);
        var bridgeHalfWidth = Math.Max(trayBounds.Width / 2, 10);
        var left = Math.Max(Math.Min(trayBounds.Left, popupBounds.Left), centerX - bridgeHalfWidth);
        var right = Math.Min(Math.Max(trayBounds.Right, popupBounds.Right), centerX + bridgeHalfWidth);
        var top = Math.Min(trayBounds.Top, popupBounds.Top);
        var bottom = Math.Max(trayBounds.Bottom, popupBounds.Bottom);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private Rectangle? TryGetTrayIconBounds()
    {
        try
        {
            var windowObject = NotifyIconWindowField?.GetValue(_notifyIcon);
            var handleProperty = windowObject?.GetType().GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (NotifyIconIdField?.GetValue(_notifyIcon) is not int id || handleProperty?.GetValue(windowObject) is not IntPtr handle || handle == IntPtr.Zero)
            {
                return null;
            }

            var identifier = new NotifyIconIdentifier
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
                hWnd = handle,
                uID = (uint)id
            };

            var result = Shell_NotifyIconGetRect(ref identifier, out var rect);
            if (result != 0)
            {
                return null;
            }

            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTaskbarLightTheme()
    {
        return ReadThemeRegistryValue("SystemUsesLightTheme", defaultValue: true);
    }

    private static bool IsAppsLightTheme()
    {
        return ReadThemeRegistryValue("AppsUseLightTheme", defaultValue: true);
    }

    private static bool ReadThemeRegistryValue(string valueName, bool defaultValue)
    {
        using var personalizeKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            writable: false);

        var value = personalizeKey?.GetValue(valueName);
        return value is int intValue ? intValue != 0 : defaultValue;
    }

    private static bool IsRunningPackaged()
    {
        var length = 0;
        var result = GetCurrentPackageFullName(ref length, null);
        return result != AppModelErrorNoPackage;
    }

    private Icon GetOrCreateIcon(bool numLock, bool capsLock, bool scrollLock, bool taskbarIsLight, Color activeColor)
    {
        var key = new IconCacheKey(numLock, capsLock, scrollLock, taskbarIsLight, activeColor.ToArgb());
        if (_iconCache.TryGetValue(key, out var icon))
        {
            return icon;
        }

        icon = CreateIcon(key);
        _iconCache[key] = icon;
        return icon;
    }

    private static Icon CreateIcon(IconCacheKey key)
    {
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var inactiveColor = key.TaskbarIsLight
            ? Color.FromArgb(88, 96, 105)
            : Color.FromArgb(172, 180, 189);

        var activeColor = Color.FromArgb(key.ActiveColorArgb);

        DrawBar(graphics, 1, key.NumLock ? activeColor : inactiveColor);
        DrawBar(graphics, 6, key.CapsLock ? activeColor : inactiveColor);
        DrawBar(graphics, 11, key.ScrollLock ? activeColor : inactiveColor);

        var handle = bitmap.GetHicon();
        try
        {
            using var temporaryIcon = Icon.FromHandle(handle);
            return (Icon)temporaryIcon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void DrawBar(Graphics graphics, int x, Color color)
    {
        using var brush = new SolidBrush(color);
        using var path = RoundedRect(new Rectangle(x, 2, 4, 12), 2);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeRect iconLocation);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, System.Text.StringBuilder? packageFullName);

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public Guid guidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private readonly record struct IconCacheKey(bool NumLock, bool CapsLock, bool ScrollLock, bool TaskbarIsLight, int ActiveColorArgb);
    private const int AppModelErrorNoPackage = 15700;
}
