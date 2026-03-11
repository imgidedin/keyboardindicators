<details open>
<summary>English</summary>

# Keyboard Indicators

Small Windows utility that lives in the system tray and shows the current state of `Num Lock`, `Caps Lock`, and `Scroll Lock`.

## Overview

The goal here is simple:

- lightweight tray icon
- immediate updates when the keys change
- a discreet popup with the current state
- local shortcut settings shown as reminders

## Requirements

- Windows 10 or Windows 11
- A .NET Desktop Runtime compatible with the project's target framework

## Running Locally

```powershell
dotnet build -c Release
dotnet run -c Release
```

If the app is running and you need to rebuild it, close it first so the tested binary is the latest one and no file stays locked.

## Project Structure

- `Program.cs`: application bootstrap
- `TrayApplicationContext.cs`: tray logic, popup handling, and Windows integration
- `StatusPopupForm.cs`: popup window
- `ShortcutSettingsForm.cs`: settings UI
- `AppSettingsStore.cs`: settings persistence
- `KeyboardIndicators.Package/`: MSIX packaging base

## Packaging

There is already an MSIX packaging base in the project. Details are in [PACKAGING.md](PACKAGING.md).

## License

This project is licensed under the `GNU Affero General Public License v3.0`.

If you plan to contribute, read [CONTRIBUTING.md](CONTRIBUTING.md) first.

</details>

<details>
<summary>Português (Brasil)</summary>

# Keyboard Indicators

Pequeno utilitário para Windows que fica na bandeja e mostra o estado de `Num Lock`, `Caps Lock` e `Scroll Lock`.

## Visão Geral

O foco aqui é simples:

- ícone leve na tray
- atualização imediata quando as teclas mudam
- popup discreto com o estado atual
- configuração local de atalhos exibidos como lembrete

## Requisitos

- Windows 10 ou Windows 11
- .NET Desktop Runtime compatível com o alvo do projeto

## Execução Local

```powershell
dotnet build -c Release
dotnet run -c Release
```

Se for rebuildar e a aplicação estiver aberta, feche antes para evitar arquivo travado e garantir que o binário testado é o mais recente.

## Estrutura do Projeto

- `Program.cs`: bootstrap da aplicação
- `TrayApplicationContext.cs`: lógica principal da tray, popup e integrações com Windows
- `StatusPopupForm.cs`: janela do popup
- `ShortcutSettingsForm.cs`: tela de configuração
- `AppSettingsStore.cs`: persistência das configurações
- `KeyboardIndicators.Package/`: base para empacotamento MSIX

## Empacotamento

Existe uma base de empacotamento em MSIX no projeto. Os detalhes estão em [PACKAGING.md](PACKAGING.md).

## Licença

Este projeto está sob a `GNU Affero General Public License v3.0`.

Se você pretende contribuir, leia antes [CONTRIBUTING.md](CONTRIBUTING.md).

</details>
