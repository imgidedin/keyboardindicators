param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputPath = "artifacts\\KeyboardIndicators.msix",
    [string]$CertificatePassword = "keyboardindicators",
    [switch]$Install
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDirectory
Set-Location $projectRoot

$projectFile = Join-Path $projectRoot "KeyboardIndicators.csproj"
$manifestPath = Join-Path $projectRoot "KeyboardIndicators.Package\\Package.appxmanifest"
$assetsDirectory = Join-Path $projectRoot "KeyboardIndicators.Package\\Assets"
$publishDirectory = Join-Path $projectRoot "publish\\msix-publish"
$stageDirectory = Join-Path $projectRoot "artifacts\\msix-stage"
$resolvedOutputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
$artifactsDirectory = Split-Path -Parent $resolvedOutputPath
$certificatePath = Join-Path $artifactsDirectory "KeyboardIndicators-TestCert.pfx"

function Get-ToolPath([string]$toolName) {
    $tool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter $toolName -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $tool) {
        throw "$toolName nao foi encontrado. Instale o Windows SDK."
    }

    return $tool
}

function Get-PackageIdentityValue([xml]$manifest, [string]$attributeName) {
    return $manifest.Package.Identity.$attributeName
}

Write-Host "Encerrando instancias em execucao..."
Get-Process KeyboardIndicators -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Publicando projeto em $Configuration..."
if (Test-Path $publishDirectory) {
    Remove-Item $publishDirectory -Recurse -Force
}

dotnet publish $projectFile -c $Configuration -r $RuntimeIdentifier --self-contained false -o $publishDirectory

Write-Host "Preparando staging do MSIX..."
if (Test-Path $stageDirectory) {
    Remove-Item $stageDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stageDirectory | Out-Null
Copy-Item (Join-Path $publishDirectory "*") $stageDirectory -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $stageDirectory "Assets") | Out-Null
Copy-Item (Join-Path $assetsDirectory "*") (Join-Path $stageDirectory "Assets") -Force

[xml]$manifestXml = Get-Content $manifestPath
$manifestXml.Package.Applications.Application.Executable = "KeyboardIndicators.exe"
$startupExtension = $manifestXml.Package.Applications.Application.Extensions.Extension
if ($startupExtension) {
    $startupExtension.Executable = "KeyboardIndicators.exe"
}
$manifestXml.Save((Join-Path $stageDirectory "AppxManifest.xml"))

Write-Host "Criando pacote MSIX..."
if (-not (Test-Path $artifactsDirectory)) {
    New-Item -ItemType Directory -Force -Path $artifactsDirectory | Out-Null
}

if (Test-Path $resolvedOutputPath) {
    Remove-Item $resolvedOutputPath -Force
}

$makeAppx = Get-ToolPath "makeappx.exe"
& $makeAppx pack /d $stageDirectory /p $resolvedOutputPath /o

[xml]$manifestIdentity = Get-Content $manifestPath
$publisher = Get-PackageIdentityValue $manifestIdentity "Publisher"

Write-Host "Gerando certificado de teste..."
$password = ConvertTo-SecureString $CertificatePassword -AsPlainText -Force
$certificate = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $publisher `
    -KeyUsage DigitalSignature `
    -FriendlyName "KeyboardIndicators Test Cert" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

Export-PfxCertificate -Cert $certificate -FilePath $certificatePath -Password $password | Out-Null
Import-PfxCertificate -FilePath $certificatePath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" -Password $password | Out-Null

Write-Host "Assinando pacote..."
$signTool = Get-ToolPath "signtool.exe"
& $signTool sign /fd SHA256 /f $certificatePath /p $CertificatePassword $resolvedOutputPath

if ($Install) {
    Write-Host "Instalando pacote..."
    Add-AppxPackage $resolvedOutputPath
}

Write-Host ""
Write-Host "MSIX gerado em: $resolvedOutputPath"
Write-Host "Certificado de teste: $certificatePath"
if ($Install) {
    Write-Host "Pacote instalado com sucesso."
}
