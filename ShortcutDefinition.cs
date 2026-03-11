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
        return IsEmpty ? "Nao configurado" : string.Join(" + ", Sequence.Select(FormatKey));
    }

    public string ToHintText(string fallbackKeyName)
    {
        return IsEmpty ? fallbackKeyName : $"Code + {string.Join(" + ", Sequence.Select(FormatKey))}";
    }

    public static IReadOnlyList<System.Windows.Forms.Keys> Normalize(IEnumerable<System.Windows.Forms.Keys> keys)
    {
        return keys
            .Select(NormalizeKey)
            .Where(key => key != System.Windows.Forms.Keys.None)
            .Distinct()
            .ToArray();
    }

    public static string FormatKey(System.Windows.Forms.Keys key)
    {
        return NormalizeKey(key) switch
        {
            System.Windows.Forms.Keys.ShiftKey => "L/R Shift",
            System.Windows.Forms.Keys.RShiftKey => "Right Shift",
            System.Windows.Forms.Keys.LShiftKey => "Left Shift",
            System.Windows.Forms.Keys.ControlKey => "L/R Ctrl",
            System.Windows.Forms.Keys.RControlKey => "Right Ctrl",
            System.Windows.Forms.Keys.LControlKey => "Left Ctrl",
            System.Windows.Forms.Keys.Menu => "L/R Alt",
            System.Windows.Forms.Keys.RMenu => "Right Alt",
            System.Windows.Forms.Keys.LMenu => "Left Alt",
            System.Windows.Forms.Keys.Return => "Enter",
            System.Windows.Forms.Keys.Prior => "Page Up",
            System.Windows.Forms.Keys.Next => "Page Down",
            System.Windows.Forms.Keys.Capital => "Caps Lock",
            System.Windows.Forms.Keys.NumLock => "Num Lock",
            System.Windows.Forms.Keys.Scroll => "Scroll Lock",
            _ => NormalizeKey(key).ToString()
        };
    }

    public static System.Windows.Forms.Keys NormalizeKey(System.Windows.Forms.Keys key)
    {
        return key & System.Windows.Forms.Keys.KeyCode;
    }
}
