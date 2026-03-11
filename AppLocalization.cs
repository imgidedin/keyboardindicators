using System.Globalization;

namespace KeyboardIndicators;

internal enum AppLanguage
{
    English,
    PortugueseBrazil
}

internal enum AppLanguagePreference
{
    English,
    PortugueseBrazil
}

internal sealed class AppStrings
{
    public required string LanguageMenu { get; init; }
    public required string UseSystemLanguage { get; init; }
    public required string EnglishLabel { get; init; }
    public required string PortugueseBrazilLabel { get; init; }
    public required string RefreshNow { get; init; }
    public required string ConfigureShortcuts { get; init; }
    public required string LaunchAtStartup { get; init; }
    public required string LaunchAtStartupManagedBySystem { get; init; }
    public required string Exit { get; init; }
    public required string SettingsDialogTitle { get; init; }
    public required string SettingsTitle { get; init; }
    public required string SettingsDescription { get; init; }
    public required string Save { get; init; }
    public required string Cancel { get; init; }
    public required string Clear { get; init; }
    public required string PopupKeyHeader { get; init; }
    public required string PopupStateHeader { get; init; }
    public required string PopupShortcutHeader { get; init; }
    public required string On { get; init; }
    public required string Off { get; init; }
    public required string NotConfigured { get; init; }
    public required string ShortcutHintPrefix { get; init; }
    public required string NumLockName { get; init; }
    public required string CapsLockName { get; init; }
    public required string ScrollLockName { get; init; }
    public required string LeftRightShift { get; init; }
    public required string RightShift { get; init; }
    public required string LeftShift { get; init; }
    public required string LeftRightCtrl { get; init; }
    public required string RightCtrl { get; init; }
    public required string LeftCtrl { get; init; }
    public required string LeftRightAlt { get; init; }
    public required string RightAlt { get; init; }
    public required string LeftAlt { get; init; }
    public required string Enter { get; init; }
    public required string PageUp { get; init; }
    public required string PageDown { get; init; }
}

internal static class AppLocalization
{
    private static readonly AppStrings English = new()
    {
        LanguageMenu = "Language",
        UseSystemLanguage = "Use system language",
        EnglishLabel = "English",
        PortugueseBrazilLabel = "Portuguese (Brazil)",
        RefreshNow = "Refresh now",
        ConfigureShortcuts = "Configure shortcuts...",
        LaunchAtStartup = "Launch at startup",
        LaunchAtStartupManagedBySystem = "Launch at startup (managed by the system)",
        Exit = "Exit",
        SettingsDialogTitle = "Configure shortcut reminders",
        SettingsTitle = "Shortcuts shown in the popup",
        SettingsDescription = "These shortcuts are only shown as reminders. Click a field, press the sequence in the desired order, and save. If left empty, the popup shows only the key itself.",
        Save = "Save",
        Cancel = "Cancel",
        Clear = "Clear",
        PopupKeyHeader = "Key",
        PopupStateHeader = "State",
        PopupShortcutHeader = "Shortcut",
        On = "On",
        Off = "Off",
        NotConfigured = "Not configured",
        ShortcutHintPrefix = "Shortcut",
        NumLockName = "Num Lock",
        CapsLockName = "Caps Lock",
        ScrollLockName = "Scroll Lock",
        LeftRightShift = "L/R Shift",
        RightShift = "Right Shift",
        LeftShift = "Left Shift",
        LeftRightCtrl = "L/R Ctrl",
        RightCtrl = "Right Ctrl",
        LeftCtrl = "Left Ctrl",
        LeftRightAlt = "L/R Alt",
        RightAlt = "Right Alt",
        LeftAlt = "Left Alt",
        Enter = "Enter",
        PageUp = "Page Up",
        PageDown = "Page Down"
    };

    private static readonly AppStrings PortugueseBrazil = new()
    {
        LanguageMenu = "Idioma",
        UseSystemLanguage = "Usar idioma do sistema",
        EnglishLabel = "English",
        PortugueseBrazilLabel = "Português (Brasil)",
        RefreshNow = "Atualizar agora",
        ConfigureShortcuts = "Configurar atalhos...",
        LaunchAtStartup = "Iniciar com o Windows",
        LaunchAtStartupManagedBySystem = "Iniciar com o Windows (gerenciado pelo sistema)",
        Exit = "Sair",
        SettingsDialogTitle = "Configurar atalhos de lembrete",
        SettingsTitle = "Atalhos exibidos no popup",
        SettingsDescription = "Esses atalhos servem apenas como lembrete. Clique em um campo, pressione a sequência na ordem desejada e salve. Se deixar vazio, o popup mostra apenas a própria tecla.",
        Save = "Salvar",
        Cancel = "Cancelar",
        Clear = "Limpar",
        PopupKeyHeader = "Tecla",
        PopupStateHeader = "Estado",
        PopupShortcutHeader = "Atalho",
        On = "Ligado",
        Off = "Desligado",
        NotConfigured = "Não configurado",
        ShortcutHintPrefix = "Atalho",
        NumLockName = "Num Lock",
        CapsLockName = "Caps Lock",
        ScrollLockName = "Scroll Lock",
        LeftRightShift = "Shift E/D",
        RightShift = "Shift direito",
        LeftShift = "Shift esquerdo",
        LeftRightCtrl = "Ctrl E/D",
        RightCtrl = "Ctrl direito",
        LeftCtrl = "Ctrl esquerdo",
        LeftRightAlt = "Alt E/D",
        RightAlt = "Alt direito",
        LeftAlt = "Alt esquerdo",
        Enter = "Enter",
        PageUp = "Page Up",
        PageDown = "Page Down"
    };

    public static AppLanguage Resolve(AppLanguagePreference? preference)
    {
        if (preference is not null)
        {
            return preference.Value == AppLanguagePreference.PortugueseBrazil
                ? AppLanguage.PortugueseBrazil
                : AppLanguage.English;
        }

        return CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.PortugueseBrazil
            : AppLanguage.English;
    }

    public static AppStrings Get(AppLanguage language)
    {
        return language == AppLanguage.PortugueseBrazil ? PortugueseBrazil : English;
    }
}
