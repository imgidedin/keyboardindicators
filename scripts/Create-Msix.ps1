param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputPath = "artifacts\\KeyboardIndicators.msix",
    [string]$CertificatePassword = "keyboardindicators",
    [switch]$Install,
    [switch]$Launch
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
$certificatePublicPath = Join-Path $artifactsDirectory "KeyboardIndicators-TestCert.cer"

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

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Import-TestCertificate([string]$certificateFilePath) {
    Import-Certificate -FilePath $certificateFilePath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
    Import-Certificate -FilePath $certificateFilePath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null

    if (Test-IsAdministrator) {
        Import-Certificate -FilePath $certificateFilePath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
        Import-Certificate -FilePath $certificateFilePath -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
    }
}

function Start-PackagedApp([string]$packageName) {
    $package = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $package) {
        throw "Nao foi possivel localizar o pacote instalado '$packageName'."
    }

    $applicationId = "App"
    Start-Process "explorer.exe" "shell:AppsFolder\$($package.PackageFamilyName)!$applicationId"
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
$packageName = Get-PackageIdentityValue $manifestIdentity "Name"
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
Export-Certificate -Cert $certificate -FilePath $certificatePublicPath | Out-Null
Import-PfxCertificate -FilePath $certificatePath -CertStoreLocation "Cert:\CurrentUser\My" -Password $password | Out-Null
Import-TestCertificate $certificatePublicPath

Write-Host "Assinando pacote..."
$signTool = Get-ToolPath "signtool.exe"
& $signTool sign /fd SHA256 /f $certificatePath /p $CertificatePassword $resolvedOutputPath

if ($Install) {
    Write-Host "Instalando pacote..."
    try {
        Add-AppxPackage $resolvedOutputPath
        $Launch = $true
    }
    catch {
        $errorText = $_.Exception.Message
        if ($errorText -match "0x800B0109|0x800B010A") {
            Write-Host ""
            Write-Host "Falha de confianca no certificado." -ForegroundColor Yellow
            if (-not (Test-IsAdministrator)) {
                Write-Host "Execute este script em um PowerShell como Administrador para importar o certificado atual na maquina." -ForegroundColor Yellow
            }
            Write-Host "Se ainda precisar fazer manualmente, execute:" -ForegroundColor Yellow
            Write-Host "Import-Certificate -FilePath `"$certificatePublicPath`" -CertStoreLocation 'Cert:\LocalMachine\Root'"
            Write-Host "Import-Certificate -FilePath `"$certificatePublicPath`" -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'"
            Write-Host "Add-AppxPackage `"$resolvedOutputPath`""
        }

        throw
    }
}

if ($Launch) {
    Write-Host "Abrindo aplicativo empacotado..."
    Start-PackagedApp $packageName
}

Write-Host ""
Write-Host "MSIX gerado em: $resolvedOutputPath"
Write-Host "Certificado de teste: $certificatePath"
Write-Host "Certificado publico: $certificatePublicPath"
if ($Install) {
    Write-Host "Pacote instalado com sucesso."
}
elseif ($Launch) {
    Write-Host "Aplicativo empacotado iniciado."
}
