namespace KeyboardIndicators;

internal sealed class AppSettings
{
    public AppLanguagePreference? LanguagePreference { get; set; }
    public ShortcutDefinition NumLockShortcut { get; set; } = new();
    public ShortcutDefinition CapsLockShortcut { get; set; } = new();
    public ShortcutDefinition ScrollLockShortcut { get; set; } = new();
}
