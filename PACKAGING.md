# MSIX e WACK

Este projeto agora inclui uma base para empacotamento em MSIX em `KeyboardIndicators.Package/`.

## O que foi preparado

- Projeto de empacotamento MSIX: `KeyboardIndicators.Package/KeyboardIndicators.Package.wapproj`
- Manifesto com `runFullTrust` e `startupTask`
- Placeholders de assets para o pacote
- Ajuste no app para, quando estiver empacotado, abrir `ms-settings:startupapps` em vez de tentar escrever no registro
- Script de linha de comando para WACK: `scripts/Run-Wack.ps1`

## Antes de gerar o pacote

Troque estes valores em `KeyboardIndicators.Package/Package.appxmanifest`:

- `Identity Name`
- `Publisher`
- `ProcessorArchitecture`
- `PublisherDisplayName`
- `Version`

Para publicação na Store, associe o pacote ao aplicativo reservado no Partner Center e deixe o Visual Studio reescrever identidade/publicador.

## Build do MSIX

O projeto `.wapproj` depende do Windows SDK e das ferramentas de empacotamento do Visual Studio. Nesta maquina elas nao estao instaladas, entao o pacote nao foi gerado localmente.

Para gerar um pacote de teste por linha de comando, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Create-Msix.ps1
```

Para instalar automaticamente o pacote gerado:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Create-Msix.ps1 -Install
```

Fluxo sugerido no Visual Studio 2022:

1. Instalar a workload de empacotamento MSIX / Windows application packaging.
2. Abrir `KeyboardIndicators.Packaging.slnx`.
3. Associar `KeyboardIndicators.Package` ao app da Store.
4. Gerar um pacote `x64` em `Release`.

## Rodar WACK

Depois de gerar o `.msix` ou `.msixupload`, execute:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Run-Wack.ps1 -PackagePath .\artifacts\KeyboardIndicators.msix
```

O relatorio sera salvo por padrao em `artifacts/wack-report.xml`.

## Observacoes

- Os assets atuais sao placeholders tecnicos. Para submissao real, troque por arte final.
- O item de menu "Iniciar com o Windows" continua usando registro no modo solto; no modo empacotado ele abre a tela de gerenciamento de apps de inicializacao do Windows.
- `KeyboardIndicators.slnx` foi mantido sem o `.wapproj` para continuar abrindo normalmente no Rider.
