using System.Globalization;

namespace KeyboardIndicators;

internal sealed class ShortcutSettingsForm : Form
{
    private readonly ComboBox _prefixModeComboBox;
    private readonly TextBox _customPrefixTextBox;
    private readonly Label _customPrefixLabel;
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
            ShortcutPrefixMode = currentSettings.ShortcutPrefixMode,
            CustomShortcutPrefix = currentSettings.CustomShortcutPrefix,
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
        ClientSize = new Size(560, 332);

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
            Size = new Size(528, 44),
            Text = _strings.SettingsDescription
        };

        var prefixModeLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 96),
            Text = _strings.SettingsPrefixModeLabel
        };

        _prefixModeComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(160, 92),
            Size = new Size(170, 27)
        };
        _prefixModeComboBox.Items.AddRange(
        [
            new PrefixModeOption(ShortcutPrefixMode.None, _strings.PrefixNone),
            new PrefixModeOption(ShortcutPrefixMode.Code, _strings.PrefixCode),
            new PrefixModeOption(ShortcutPrefixMode.Fn, _strings.PrefixFn),
            new PrefixModeOption(ShortcutPrefixMode.Custom, _strings.PrefixCustom)
        ]);
        _prefixModeComboBox.SelectedItem = _prefixModeComboBox.Items
            .Cast<PrefixModeOption>()
            .First(option => option.Mode == Settings.ShortcutPrefixMode);
        _prefixModeComboBox.SelectedIndexChanged += (_, _) => UpdateCustomPrefixVisibility();

        _customPrefixLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 130),
            Text = _strings.SettingsCustomPrefixLabel
        };

        _customPrefixTextBox = new TextBox
        {
            Location = new Point(160, 126),
            Size = new Size(170, 27),
            Text = Settings.CustomShortcutPrefix
        };
        _customPrefixTextBox.TextChanged += (_, _) => TrimCustomPrefix();

        _numLockTextBox = new ShortcutCaptureTextBox(Settings.NumLockShortcut, _strings) { Location = new Point(160, 176) };
        _capsLockTextBox = new ShortcutCaptureTextBox(Settings.CapsLockShortcut, _strings) { Location = new Point(160, 214) };
        _scrollLockTextBox = new ShortcutCaptureTextBox(Settings.ScrollLockShortcut, _strings) { Location = new Point(160, 252) };

        Controls.Add(titleLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(prefixModeLabel);
        Controls.Add(_prefixModeComboBox);
        Controls.Add(_customPrefixLabel);
        Controls.Add(_customPrefixTextBox);
        Controls.Add(CreateRowLabel(_strings.NumLockName, 180));
        Controls.Add(CreateRowLabel(_strings.CapsLockName, 218));
        Controls.Add(CreateRowLabel(_strings.ScrollLockName, 256));
        Controls.Add(_numLockTextBox);
        Controls.Add(_capsLockTextBox);
        Controls.Add(_scrollLockTextBox);
        Controls.Add(CreateClearButton(_numLockTextBox, _strings, 454, 175));
        Controls.Add(CreateClearButton(_capsLockTextBox, _strings, 454, 213));
        Controls.Add(CreateClearButton(_scrollLockTextBox, _strings, 454, 251));

        var saveButton = new Button
        {
            Text = _strings.Save,
            DialogResult = DialogResult.OK,
            Location = new Point(388, 292),
            Size = new Size(75, 28)
        };
        saveButton.Click += SaveSettings;

        var cancelButton = new Button
        {
            Text = _strings.Cancel,
            DialogResult = DialogResult.Cancel,
            Location = new Point(469, 292),
            Size = new Size(75, 28)
        };

        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        UpdateCustomPrefixVisibility();
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
        var prefixMode = SelectedPrefixMode;
        var customPrefix = _customPrefixTextBox.Text.Trim();
        if (prefixMode == ShortcutPrefixMode.Custom && string.IsNullOrWhiteSpace(customPrefix))
        {
            MessageBox.Show(this, _strings.CustomPrefixValidationMessage, _strings.ValidationErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Settings = new AppSettings
        {
            LanguagePreference = Settings.LanguagePreference,
            ShortcutPrefixMode = prefixMode,
            CustomShortcutPrefix = customPrefix,
            NumLockShortcut = _numLockTextBox.Shortcut.Clone(),
            CapsLockShortcut = _capsLockTextBox.Shortcut.Clone(),
            ScrollLockShortcut = _scrollLockTextBox.Shortcut.Clone()
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private ShortcutPrefixMode SelectedPrefixMode =>
        (_prefixModeComboBox.SelectedItem as PrefixModeOption)?.Mode ?? ShortcutPrefixMode.Code;

    private void UpdateCustomPrefixVisibility()
    {
        var showCustomPrefix = SelectedPrefixMode == ShortcutPrefixMode.Custom;
        _customPrefixLabel.Visible = showCustomPrefix;
        _customPrefixTextBox.Visible = showCustomPrefix;
        _customPrefixTextBox.Enabled = showCustomPrefix;
    }

    private void TrimCustomPrefix()
    {
        var text = _customPrefixTextBox.Text;
        var indexes = StringInfo.ParseCombiningCharacters(text);
        if (indexes.Length <= 10)
        {
            return;
        }

        var trimmed = new StringInfo(text).SubstringByTextElements(0, 10);
        var selectionStart = _customPrefixTextBox.SelectionStart;
        _customPrefixTextBox.Text = trimmed;
        _customPrefixTextBox.SelectionStart = Math.Min(selectionStart, trimmed.Length);
    }

    private sealed record PrefixModeOption(ShortcutPrefixMode Mode, string Text)
    {
        public override string ToString() => Text;
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
