using Microsoft.Win32;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Timer = System.Windows.Forms.Timer;

namespace KeyboardIndicators;

[SupportedOSPlatform("windows")]
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string AppName = "KeyboardIndicators";
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const int DefaultRefreshIntervalMs = 2000;
    private const int ImmediateRefreshIntervalMs = 1;

    private static readonly FieldInfo? NotifyIconIdField = typeof(NotifyIcon).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NotifyIconWindowField = typeof(NotifyIcon).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _launchAtStartupMenuItem;
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
        _taskbarIsLight = IsTaskbarLightTheme();
        _appsLightTheme = IsAppsLightTheme();
        _keyboardHook = new KeyboardIndicatorHook(OnKeyboardIndicatorKeyPressed);

        _launchAtStartupMenuItem = new ToolStripMenuItem("Iniciar com o Windows", null, ToggleLaunchAtStartup);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Opening += (_, _) => SuppressAndHidePopup();
        contextMenu.Closing += (_, _) => _suppressPopupUntilUtc = DateTime.UtcNow.AddMilliseconds(150);
        contextMenu.Items.Add(new ToolStripMenuItem("Atualizar agora", null, (_, _) => RefreshIcon(force: true)));
        contextMenu.Items.Add(new ToolStripMenuItem("Configurar atalhos...", null, OpenShortcutSettings));
        contextMenu.Items.Add(_launchAtStartupMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) => ExitThread()));

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

        UpdateLaunchAtStartupMenu();
        RefreshIcon(force: true);
        _refreshTimer.Start();
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
            RefreshIcon(force: true);
        }
    }

    private void OnKeyboardIndicatorKeyPressed()
    {
        _immediateRefreshTimer.Stop();
        _immediateRefreshTimer.Start();
    }

    private void ToggleLaunchAtStartup(object? sender, EventArgs e)
    {
        if (_isPackaged)
        {
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

    private void OpenShortcutSettings(object? sender, EventArgs e)
    {
        SuppressAndHidePopup();

        using var form = new ShortcutSettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settings = form.Settings;
        _settingsStore.Save(_settings);
        RefreshIcon(force: true);
    }

    private void UpdateLaunchAtStartupMenu()
    {
        if (_isPackaged)
        {
            _launchAtStartupMenuItem.Text = "Iniciar com o Windows (gerenciado pelo sistema)";
            _launchAtStartupMenuItem.Checked = false;
            _launchAtStartupMenuItem.CheckOnClick = false;
            _launchAtStartupMenuItem.Enabled = false;
            return;
        }

        _launchAtStartupMenuItem.Text = "Iniciar com o Windows";
        _launchAtStartupMenuItem.CheckOnClick = true;
        _launchAtStartupMenuItem.Enabled = true;
        _launchAtStartupMenuItem.Checked = IsLaunchAtStartupEnabled();
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

        _notifyIcon.Icon = GetOrCreateIcon(numLock, capsLock, scrollLock, _taskbarIsLight);
        _statusPopup.ApplyTheme(_appsLightTheme);
        _statusPopup.UpdateContent(numLock, capsLock, scrollLock, _settings);
    }

    private void ShowStatusPopup()
    {
        _hideDelayTimer.Stop();
        _statusPopup.UpdateContent(_lastNumLock, _lastCapsLock, _lastScrollLock, _settings);
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

    private Icon GetOrCreateIcon(bool numLock, bool capsLock, bool scrollLock, bool taskbarIsLight)
    {
        var key = new IconCacheKey(numLock, capsLock, scrollLock, taskbarIsLight);
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

        var activeColor = key.TaskbarIsLight
            ? Color.FromArgb(34, 139, 74)
            : Color.FromArgb(111, 214, 140);

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

    private readonly record struct IconCacheKey(bool NumLock, bool CapsLock, bool ScrollLock, bool TaskbarIsLight);
    private const int AppModelErrorNoPackage = 15700;
}
