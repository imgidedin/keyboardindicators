namespace KeyboardIndicators;

internal sealed class AppSettings
{
    public ShortcutDefinition NumLockShortcut { get; set; } = new();
    public ShortcutDefinition CapsLockShortcut { get; set; } = new();
    public ShortcutDefinition ScrollLockShortcut { get; set; } = new();
}
