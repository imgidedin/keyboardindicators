namespace KeyboardIndicators;

internal sealed class ShortcutDefinition
{
    public List<System.Windows.Forms.Keys> Sequence { get; set; } = [];

    public ShortcutDefinition()
    {
    }

    public ShortcutDefinition(IEnumerable<System.Windows.Forms.Keys> keys)
    {
        Sequence = Normalize(keys).ToList();
    }

    public bool IsEmpty => Sequence.Count == 0;

    public ShortcutDefinition Clone()
    {
        return new ShortcutDefinition(Sequence);
    }

    public override string ToString()
    {
        return IsEmpty ? "Not configured" : string.Join(" + ", Sequence.Select(key => FormatKey(key, AppLocalization.Get(AppLanguage.English))));
    }

    public string ToHintText(string fallbackKeyName, AppStrings strings)
    {
        return IsEmpty ? fallbackKeyName : $"{strings.ShortcutHintPrefix} + {string.Join(" + ", Sequence.Select(key => FormatKey(key, strings)))}";
    }

    public static IReadOnlyList<System.Windows.Forms.Keys> Normalize(IEnumerable<System.Windows.Forms.Keys> keys)
    {
        return keys
            .Select(NormalizeKey)
            .Where(key => key != System.Windows.Forms.Keys.None)
            .Distinct()
            .ToArray();
    }

    public static string FormatKey(System.Windows.Forms.Keys key, AppStrings strings)
    {
        return NormalizeKey(key) switch
        {
            System.Windows.Forms.Keys.ShiftKey => strings.LeftRightShift,
            System.Windows.Forms.Keys.RShiftKey => strings.RightShift,
            System.Windows.Forms.Keys.LShiftKey => strings.LeftShift,
            System.Windows.Forms.Keys.ControlKey => strings.LeftRightCtrl,
            System.Windows.Forms.Keys.RControlKey => strings.RightCtrl,
            System.Windows.Forms.Keys.LControlKey => strings.LeftCtrl,
            System.Windows.Forms.Keys.Menu => strings.LeftRightAlt,
            System.Windows.Forms.Keys.RMenu => strings.RightAlt,
            System.Windows.Forms.Keys.LMenu => strings.LeftAlt,
            System.Windows.Forms.Keys.Return => strings.Enter,
            System.Windows.Forms.Keys.Prior => strings.PageUp,
            System.Windows.Forms.Keys.Next => strings.PageDown,
            System.Windows.Forms.Keys.Capital => strings.CapsLockName,
            System.Windows.Forms.Keys.NumLock => strings.NumLockName,
            System.Windows.Forms.Keys.Scroll => strings.ScrollLockName,
            _ => NormalizeKey(key).ToString()
        };
    }

    public static System.Windows.Forms.Keys NormalizeKey(System.Windows.Forms.Keys key)
    {
        return key & System.Windows.Forms.Keys.KeyCode;
    }
}
