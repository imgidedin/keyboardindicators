namespace KeyboardIndicators;

internal sealed class ShortcutSettingsForm : Form
{
    private readonly AppStrings _strings;
    private readonly ShortcutCaptureTextBox _numLockTextBox;
    private readonly ShortcutCaptureTextBox _capsLockTextBox;
    private readonly ShortcutCaptureTextBox _scrollLockTextBox;

    public AppSettings Settings { get; private set; }

    public ShortcutSettingsForm(AppSettings currentSettings, AppStrings strings)
    {
        _strings = strings;
        Settings = new AppSettings
        {
            LanguagePreference = currentSettings.LanguagePreference,
            NumLockShortcut = currentSettings.NumLockShortcut.Clone(),
            CapsLockShortcut = currentSettings.CapsLockShortcut.Clone(),
            ScrollLockShortcut = currentSettings.ScrollLockShortcut.Clone()
        };

        Text = _strings.SettingsDialogTitle;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 250);

        var titleLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 16),
            Text = _strings.SettingsTitle
        };

        var descriptionLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 42),
            Size = new Size(528, 52),
            Text = _strings.SettingsDescription
        };

        _numLockTextBox = new ShortcutCaptureTextBox(Settings.NumLockShortcut, _strings) { Location = new Point(160, 108) };
        _capsLockTextBox = new ShortcutCaptureTextBox(Settings.CapsLockShortcut, _strings) { Location = new Point(160, 146) };
        _scrollLockTextBox = new ShortcutCaptureTextBox(Settings.ScrollLockShortcut, _strings) { Location = new Point(160, 184) };

        Controls.Add(titleLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(CreateRowLabel(_strings.NumLockName, 112));
        Controls.Add(CreateRowLabel(_strings.CapsLockName, 150));
        Controls.Add(CreateRowLabel(_strings.ScrollLockName, 188));
        Controls.Add(_numLockTextBox);
        Controls.Add(_capsLockTextBox);
        Controls.Add(_scrollLockTextBox);
        Controls.Add(CreateClearButton(_numLockTextBox, _strings, 454, 107));
        Controls.Add(CreateClearButton(_capsLockTextBox, _strings, 454, 145));
        Controls.Add(CreateClearButton(_scrollLockTextBox, _strings, 454, 183));

        var saveButton = new Button
        {
            Text = _strings.Save,
            DialogResult = DialogResult.OK,
            Location = new Point(388, 214),
            Size = new Size(75, 28)
        };
        saveButton.Click += SaveSettings;

        var cancelButton = new Button
        {
            Text = _strings.Cancel,
            DialogResult = DialogResult.Cancel,
            Location = new Point(469, 214),
            Size = new Size(75, 28)
        };

        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private static Label CreateRowLabel(string text, int y)
    {
        return new Label
        {
            AutoSize = true,
            Location = new Point(16, y),
            Text = text
        };
    }

    private static Button CreateClearButton(ShortcutCaptureTextBox textBox, AppStrings strings, int x, int y)
    {
        var button = new Button
        {
            Text = strings.Clear,
            Location = new Point(x, y),
            Size = new Size(75, 28)
        };

        button.Click += (_, _) => textBox.ClearShortcut();
        return button;
    }

    private void SaveSettings(object? sender, EventArgs e)
    {
        Settings = new AppSettings
        {
            LanguagePreference = Settings.LanguagePreference,
            NumLockShortcut = _numLockTextBox.Shortcut.Clone(),
            CapsLockShortcut = _capsLockTextBox.Shortcut.Clone(),
            ScrollLockShortcut = _scrollLockTextBox.Shortcut.Clone()
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}

internal sealed class ShortcutCaptureTextBox : TextBox
{
    private readonly AppStrings _strings;
    private readonly List<Keys> _recordedKeys = [];
    private readonly HashSet<Keys> _pressedKeys = [];

    public ShortcutDefinition Shortcut => new(_recordedKeys);

    public ShortcutCaptureTextBox(ShortcutDefinition shortcut, AppStrings strings)
    {
        _strings = strings;
        _recordedKeys.AddRange(shortcut.Sequence);
        ReadOnly = true;
        ShortcutsEnabled = false;
        TabStop = true;
        Size = new Size(288, 27);
        Text = FormatShortcut();
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Select(0, 0);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var key = ShortcutDefinition.NormalizeKey(e.KeyCode);
        if (key == Keys.None)
        {
            e.SuppressKeyPress = true;
            return;
        }

        if (_pressedKeys.Count == 0)
        {
            _recordedKeys.Clear();
        }

        if (_pressedKeys.Add(key))
        {
            _recordedKeys.Add(key);
            Text = FormatShortcut();
        }

        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _pressedKeys.Remove(ShortcutDefinition.NormalizeKey(e.KeyCode));
        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return true;
    }

    public void ClearShortcut()
    {
        _recordedKeys.Clear();
        _pressedKeys.Clear();
        Text = FormatShortcut();
    }

    private string FormatShortcut()
    {
        return _recordedKeys.Count == 0
            ? _strings.NotConfigured
            : string.Join(" + ", _recordedKeys.Select(key => ShortcutDefinition.FormatKey(key, _strings)));
    }
}
