namespace KeyboardIndicators;

internal enum ShortcutPrefixMode
{
    None,
    Code,
    Fn,
    Custom
}

internal sealed class AppSettings
{
    public AppLanguagePreference? LanguagePreference { get; set; }
    public ShortcutPrefixMode ShortcutPrefixMode { get; set; } = ShortcutPrefixMode.Code;
    public string CustomShortcutPrefix { get; set; } = string.Empty;
    public ShortcutDefinition NumLockShortcut { get; set; } = new();
    public ShortcutDefinition CapsLockShortcut { get; set; } = new();
    public ShortcutDefinition ScrollLockShortcut { get; set; } = new();

    public string? GetShortcutPrefix(AppStrings strings)
    {
        return ShortcutPrefixMode switch
        {
            ShortcutPrefixMode.None => null,
            ShortcutPrefixMode.Code => "Code",
            ShortcutPrefixMode.Fn => "Fn",
            ShortcutPrefixMode.Custom => string.IsNullOrWhiteSpace(CustomShortcutPrefix) ? null : CustomShortcutPrefix.Trim(),
            _ => "Code"
        };
    }
}
