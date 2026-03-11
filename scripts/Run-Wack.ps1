param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [string]$ReportPath = (Join-Path (Get-Location) "artifacts\\wack-report.xml")
)

$appCert = Get-ChildItem "$env:ProgramFiles(x86)\Windows Kits\10\bin" -Filter appcert.exe -Recurse -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $appCert) {
    throw "appcert.exe nao foi encontrado. Instale o Windows SDK com o Windows App Certification Kit."
}

$reportDirectory = Split-Path -Parent $ReportPath
if (-not (Test-Path $reportDirectory)) {
    New-Item -ItemType Directory -Path $reportDirectory | Out-Null
}

& $appCert test -appxpackagepath $PackagePath -reportoutputpath $ReportPath
if ($LASTEXITCODE -ne 0) {
    throw "WACK retornou codigo $LASTEXITCODE."
}

Write-Host "Relatorio gerado em $ReportPath"
