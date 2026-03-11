namespace KeyboardIndicators;

internal sealed class ShortcutSettingsForm : Form
{
    private readonly ShortcutCaptureTextBox _numLockTextBox;
    private readonly ShortcutCaptureTextBox _capsLockTextBox;
    private readonly ShortcutCaptureTextBox _scrollLockTextBox;

    public AppSettings Settings { get; private set; }

    public ShortcutSettingsForm(AppSettings currentSettings)
    {
        Settings = new AppSettings
        {
            NumLockShortcut = currentSettings.NumLockShortcut.Clone(),
            CapsLockShortcut = currentSettings.CapsLockShortcut.Clone(),
            ScrollLockShortcut = currentSettings.ScrollLockShortcut.Clone()
        };

        Text = "Configurar atalhos de lembrete";
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
            Text = "Atalhos exibidos no popup"
        };

        var descriptionLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 42),
            Size = new Size(528, 52),
            Text = "Esses atalhos servem apenas como lembrete. Clique em um campo, pressione a sequencia na ordem desejada e salve. Se deixar vazio, o popup mostra apenas a propria tecla."
        };

        _numLockTextBox = new ShortcutCaptureTextBox(Settings.NumLockShortcut) { Location = new Point(160, 108) };
        _capsLockTextBox = new ShortcutCaptureTextBox(Settings.CapsLockShortcut) { Location = new Point(160, 146) };
        _scrollLockTextBox = new ShortcutCaptureTextBox(Settings.ScrollLockShortcut) { Location = new Point(160, 184) };

        Controls.Add(titleLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(CreateRowLabel("NumLock", 112));
        Controls.Add(CreateRowLabel("CapsLock", 150));
        Controls.Add(CreateRowLabel("ScrollLock", 188));
        Controls.Add(_numLockTextBox);
        Controls.Add(_capsLockTextBox);
        Controls.Add(_scrollLockTextBox);
        Controls.Add(CreateClearButton(_numLockTextBox, 454, 107));
        Controls.Add(CreateClearButton(_capsLockTextBox, 454, 145));
        Controls.Add(CreateClearButton(_scrollLockTextBox, 454, 183));

        var saveButton = new Button
        {
            Text = "Salvar",
            DialogResult = DialogResult.OK,
            Location = new Point(388, 214),
            Size = new Size(75, 28)
        };
        saveButton.Click += SaveSettings;

        var cancelButton = new Button
        {
            Text = "Cancelar",
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

    private static Button CreateClearButton(ShortcutCaptureTextBox textBox, int x, int y)
    {
        var button = new Button
        {
            Text = "Limpar",
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
    private readonly List<Keys> _recordedKeys = [];
    private readonly HashSet<Keys> _pressedKeys = [];

    public ShortcutDefinition Shortcut => new(_recordedKeys);

    public ShortcutCaptureTextBox(ShortcutDefinition shortcut)
    {
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
            ? "Nao configurado"
            : string.Join(" + ", _recordedKeys.Select(ShortcutDefinition.FormatKey));
    }
}
