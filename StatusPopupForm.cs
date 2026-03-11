using System.Runtime.InteropServices;
using Timer = System.Windows.Forms.Timer;

namespace KeyboardIndicators;

internal sealed class StatusPopupForm : Form
{
    private const int CornerRadius = 8;
    private const int ShadowClassStyle = 0x00020000;
    private const double FadeStep = 0.25d;

    private readonly TableLayoutPanel _table;
    private readonly Timer _animationTimer;
    private readonly Label _keyHeader;
    private readonly Label _stateHeader;
    private readonly Label _shortcutHeader;
    private readonly Label _numKey;
    private readonly Label _numState;
    private readonly Label _numHint;
    private readonly Label _capsKey;
    private readonly Label _capsState;
    private readonly Label _capsHint;
    private readonly Label _scrollKey;
    private readonly Label _scrollState;
    private readonly Label _scrollHint;

    private Color _backgroundColor = Color.FromArgb(242, 242, 242);
    private Color _foregroundColor = Color.FromArgb(32, 32, 32);
    private Color _mutedColor = Color.FromArgb(98, 104, 112);
    private Color _activeColor = Color.FromArgb(34, 139, 74);
    private double _targetOpacity;

    public StatusPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Padding = new Padding(14, 12, 14, 12);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        DoubleBuffered = true;
        Opacity = 0d;

        _table = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 4,
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _animationTimer = new Timer
        {
            Interval = 16
        };
        _animationTimer.Tick += (_, _) => AdvanceAnimation();

        (_keyHeader, _stateHeader, _shortcutHeader) = AddHeaderRow();
        (_numKey, _numState, _numHint) = AddDataRow(1);
        (_capsKey, _capsState, _capsHint) = AddDataRow(2);
        (_scrollKey, _scrollState, _scrollHint) = AddDataRow(3);

        Controls.Add(_table);
        ApplyTheme(isLightTheme: true);
        ApplyStrings(AppLocalization.Get(AppLanguage.English));
    }

    public void ApplyTheme(bool isLightTheme)
    {
        _backgroundColor = isLightTheme ? Color.FromArgb(250, 250, 250) : Color.FromArgb(43, 43, 46);
        _foregroundColor = isLightTheme ? Color.FromArgb(26, 26, 28) : Color.FromArgb(244, 244, 245);
        _mutedColor = isLightTheme ? Color.FromArgb(109, 109, 116) : Color.FromArgb(174, 174, 178);
        _activeColor = isLightTheme ? Color.FromArgb(34, 139, 74) : Color.FromArgb(111, 214, 140);

        BackColor = _backgroundColor;
        ForeColor = _foregroundColor;
        UpdateTextColors();
        Invalidate();
    }

    public void ApplyStrings(AppStrings strings)
    {
        _keyHeader.Text = strings.PopupKeyHeader;
        _stateHeader.Text = strings.PopupStateHeader;
        _shortcutHeader.Text = strings.PopupShortcutHeader;
        _numKey.Text = strings.NumLockName;
        _capsKey.Text = strings.CapsLockName;
        _scrollKey.Text = strings.ScrollLockName;
    }

    public void UpdateContent(bool numLock, bool capsLock, bool scrollLock, AppSettings settings, AppStrings strings)
    {
        _numState.Text = numLock ? strings.On : strings.Off;
        _numState.ForeColor = numLock ? _activeColor : _mutedColor;
        _numHint.Text = settings.NumLockShortcut.ToHintText(strings.NumLockName, strings);

        _capsState.Text = capsLock ? strings.On : strings.Off;
        _capsState.ForeColor = capsLock ? _activeColor : _mutedColor;
        _capsHint.Text = settings.CapsLockShortcut.ToHintText(strings.CapsLockName, strings);

        _scrollState.Text = scrollLock ? strings.On : strings.Off;
        _scrollState.ForeColor = scrollLock ? _activeColor : _mutedColor;
        _scrollHint.Text = settings.ScrollLockShortcut.ToHintText(strings.ScrollLockName, strings);
    }

    public Rectangle BoundsOnScreen => new(Location, Size);

    public void ShowAnimated()
    {
        _targetOpacity = 1d;

        if (!Visible)
        {
            Opacity = 0d;
            Show();
        }

        StartAnimation();
    }

    public void HideAnimated(bool animated)
    {
        _targetOpacity = 0d;

        if (!Visible)
        {
            Opacity = 0d;
            return;
        }

        if (!animated)
        {
            _animationTimer.Stop();
            Opacity = 0d;
            Hide();
            return;
        }

        StartAnimation();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            const int wsExNoActivate = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= wsExToolWindow | wsExNoActivate;
            cp.ClassStyle |= ShadowClassStyle;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateRoundedRegion();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRoundedRegion();
        CenterTable();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var backgroundBrush = new SolidBrush(_backgroundColor);
        using var path = CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);

        e.Graphics.FillPath(backgroundBrush, path);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        CenterTable();
    }

    private void UpdateTextColors()
    {
        foreach (Control control in _table.Controls)
        {
            if (control.Tag as string == "header")
            {
                control.ForeColor = _foregroundColor;
                continue;
            }

            if (control != _numState && control != _capsState && control != _scrollState)
            {
                control.ForeColor = _foregroundColor;
            }
        }
    }

    private (Label keyHeader, Label stateHeader, Label shortcutHeader) AddHeaderRow()
    {
        var keyHeader = CreateCell(string.Empty, true, ContentAlignment.MiddleLeft);
        var stateHeader = CreateCell(string.Empty, true, ContentAlignment.MiddleCenter);
        var shortcutHeader = CreateCell(string.Empty, true, ContentAlignment.MiddleCenter);

        _table.Controls.Add(keyHeader, 0, 0);
        _table.Controls.Add(stateHeader, 1, 0);
        _table.Controls.Add(shortcutHeader, 2, 0);

        return (keyHeader, stateHeader, shortcutHeader);
    }

    private (Label key, Label state, Label hint) AddDataRow(int row)
    {
        var keyLabel = CreateCell(string.Empty, false, ContentAlignment.MiddleLeft);
        var stateLabel = CreateCell(string.Empty, false, ContentAlignment.MiddleCenter);
        var hintLabel = CreateCell(string.Empty, false, ContentAlignment.MiddleCenter);

        _table.Controls.Add(keyLabel, 0, row);
        _table.Controls.Add(stateLabel, 1, row);
        _table.Controls.Add(hintLabel, 2, row);

        return (keyLabel, stateLabel, hintLabel);
    }

    private Label CreateCell(string text, bool header, ContentAlignment textAlign)
    {
        return new Label
        {
            AutoSize = true,
            MinimumSize = new Size(0, 22),
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(0, 1, 0, 1),
            Font = header ? new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold) : SystemFonts.MessageBoxFont,
            Text = text,
            TextAlign = textAlign,
            BackColor = Color.Transparent,
            ForeColor = _foregroundColor,
            Tag = header ? "header" : null,
            AutoEllipsis = false,
            Anchor = AnchorStyles.None
        };
    }

    private void StartAnimation()
    {
        if (!_animationTimer.Enabled)
        {
            _animationTimer.Start();
        }
    }

    private void CenterTable()
    {
        if (_table is null)
        {
            return;
        }

        if (_table.Width <= 0 || _table.Height <= 0)
        {
            return;
        }

        var availableWidth = ClientSize.Width - Padding.Horizontal;
        var availableHeight = ClientSize.Height - Padding.Vertical;
        var x = Padding.Left + Math.Max(0, (availableWidth - _table.Width) / 2);
        var y = Padding.Top + Math.Max(0, (availableHeight - _table.Height) / 2);
        _table.Location = new Point(x, y);
    }

    private void AdvanceAnimation()
    {
        if (_targetOpacity > Opacity)
        {
            Opacity = Math.Min(_targetOpacity, Opacity + FadeStep);
        }
        else if (_targetOpacity < Opacity)
        {
            Opacity = Math.Max(_targetOpacity, Opacity - FadeStep);
        }

        if (Math.Abs(Opacity - _targetOpacity) > double.Epsilon)
        {
            return;
        }

        _animationTimer.Stop();
        if (_targetOpacity <= 0d && Visible)
        {
            Hide();
        }
    }

    private void UpdateRoundedRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
