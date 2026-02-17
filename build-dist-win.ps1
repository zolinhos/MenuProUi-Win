param(
  [Parameter(Mandatory=$false)]
  [ValidatePattern("^\d+\.\d+\.\d+$")]
  [string]$Version = "1.7.5",

  [Parameter(Mandatory=$false)]
  [string]$Runtime = "win-x64",

  [Parameter(Mandatory=$false)]
  [switch]$BuildInstaller = $true
)

$ErrorActionPreference = "Stop"

function ExecOrThrow {
  param(
    [Parameter(Mandatory=$true)][string]$Exe,
    [Parameter(Mandatory=$true)][string[]]$Args
  )

  Write-Host ">> $Exe $($Args -join ' ')"
  & $Exe @Args
  if ($LASTEXITCODE -ne 0) {
    throw "Falha executando: $Exe (ExitCode=$LASTEXITCODE)"
  }
}

$root = $PSScriptRoot
$csproj = Join-Path $root "MenuProUI.csproj"
$publishDir = Join-Path $root ("publish\" + $Runtime)
$distDir = Join-Path $root "dist"
$zipOut = Join-Path $distDir ("MenuProUI-" + $Version + "-" + $Runtime + ".zip")

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

Write-Host "==> Publicando aplicação Windows ($Runtime)..."
ExecOrThrow "dotnet" @(
  "publish", $csproj,
  "-c", "Release",
  "-r", $Runtime,
  "--self-contained", "true",
  "-o", $publishDir
)

Write-Host "==> Gerando ZIP de distribuição..."
if (Test-Path $zipOut) { Remove-Item $zipOut -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipOut -Force

if ($BuildInstaller.IsPresent) {
  Write-Host "==> Gerando instalador MSI (WiX)..."
  $installerScript = Join-Path $root "Installer\build-msi.ps1"
  if (-not (Test-Path $installerScript)) {
    throw "Script de instalador não encontrado: $installerScript"
  }

  ExecOrThrow "powershell" @(
    "-ExecutionPolicy", "Bypass",
    "-File", $installerScript,
    "-Version", $Version
  )
}

Write-Host "==> Gerando checksums (SHA256/SHA512) em dist..."
$artifacts = Get-ChildItem -Path $distDir -File | Where-Object {
  $_.Extension -in ".zip", ".msi"
} | Sort-Object Name

$sha256Lines = @()
$sha512Lines = @()

foreach ($artifact in $artifacts) {
  $h256 = Get-FileHash -Path $artifact.FullName -Algorithm SHA256
  $h512 = Get-FileHash -Path $artifact.FullName -Algorithm SHA512

  $sha256Lines += ("{0}  {1}" -f $h256.Hash.ToLowerInvariant(), $artifact.Name)
  $sha512Lines += ("{0}  {1}" -f $h512.Hash.ToLowerInvariant(), $artifact.Name)
}

Set-Content -Path (Join-Path $distDir "SHA256SUMS") -Value $sha256Lines -Encoding UTF8
Set-Content -Path (Join-Path $distDir "SHA512SUMS") -Value $sha512Lines -Encoding UTF8

Write-Host ""
Write-Host "Artefatos gerados em: $distDir"
Get-ChildItem -Path $distDir -File | Sort-Object Name | ForEach-Object {
  Write-Host ("- " + $_.Name)
}
