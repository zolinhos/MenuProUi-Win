param(
  [Parameter(Mandatory=$true)]
  [ValidatePattern("^\d+\.\d+\.\d+$")]
  [string]$Version
)

$ErrorActionPreference = "Stop"

function ExecOrThrow {
  param(
    [Parameter(Mandatory=$true)][string]$Exe,
    [Parameter(Mandatory=$true)][string[]]$Args
  )
  Write-Host ">> $Exe $($Args -join ' ')"
  & $Exe @Args
  if ($LASTEXITCODE -ne 0) { throw "Falha executando: $Exe (ExitCode=$LASTEXITCODE)" }
}

# --- GUID determinístico (UUID v5) ---
function Convert-GuidToNetworkBytes([Guid]$g) {
  $b = $g.ToByteArray()
  [Array]::Reverse($b, 0, 4)
  [Array]::Reverse($b, 4, 2)
  [Array]::Reverse($b, 6, 2)
  return $b
}
function Convert-NetworkBytesToGuid([byte[]]$b) {
  $x = [byte[]]::new($b.Length)
  [Array]::Copy($b, $x, $b.Length)
  [Array]::Reverse($x, 0, 4)
  [Array]::Reverse($x, 4, 2)
  [Array]::Reverse($x, 6, 2)
  return [Guid]::new($x)
}
function New-GuidV5 {
  param(
    [Parameter(Mandatory=$true)][Guid]$Namespace,
    [Parameter(Mandatory=$true)][string]$Name
  )
  $sha1 = [System.Security.Cryptography.SHA1]::Create()
  try {
    $nsBytes = Convert-GuidToNetworkBytes $Namespace
    $nameBytes = [System.Text.Encoding]::UTF8.GetBytes($Name)
    $hash = $sha1.ComputeHash($nsBytes + $nameBytes)

    $new = $hash[0..15]

    # version 5
    $new[6] = ($new[6] -band 0x0F) -bor 0x50
    # variant RFC 4122
    $new[8] = ($new[8] -band 0x3F) -bor 0x80

    return (Convert-NetworkBytesToGuid $new)
  }
  finally {
    $sha1.Dispose()
  }
}

# Namespace fixo do produto (não mudar depois de distribuir)
$upgradeCode = [Guid]"B2D85E7B-5F4A-4D88-9B9D-9B4D02D6D2A1"
# Namespace para derivar GUIDs de Component (pode ser qualquer GUID fixo seu)
$componentNamespace = [Guid]"9A6D7B6A-2D2B-4A41-9D12-3B1D5B6F1A11"

# Descobrir paths de forma robusta
$scriptPath = [string]$MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptPath)) { throw "Não consegui descobrir o caminho do script (MyInvocation vazio)." }

$installerDir = Split-Path -Parent $scriptPath
$root = Split-Path -Parent $installerDir

$pf86 = [string][Environment]::GetFolderPath("ProgramFilesX86")

# Procura WiX Toolset em locais comuns ou por executáveis no PATH
$possible = @(
  Join-Path $pf86 "WiX Toolset v3.14\bin",
  Join-Path $pf86 "WiX Toolset v3.11\bin",
  Join-Path $pf86 "WiX Toolset v3.10\bin",
  Join-Path $pf86 "WiX Toolset v3.8\bin"
)

$wix = $null
foreach ($p in $possible) {
  if (Test-Path $p) { $wix = $p; break }
}

if (-not $wix) {
  $heatCmd = Get-Command heat -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($heatCmd) { $wix = Split-Path $heatCmd.Path -Parent }
}

if (-not $wix) {
  try {
    $found = Get-ChildItem -Path $pf86 -Recurse -Filter heat.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found -and $found.FullName) { $wix = Split-Path $found.FullName -Parent }
  } catch { }
}


if (-not $wix) { throw "WiX não encontrado em locais padrão. Instale WiX Toolset ou coloque heat/candle/light no PATH." }

# garantir string única para Join-Path
$wix = [string]$wix

$heat   = Join-Path $wix "heat.exe"
$candle = Join-Path $wix "candle.exe"
$light  = Join-Path $wix "light.exe"

Write-Host "WiX bin: $wix"

$csproj     = Join-Path $root "MenuProUI.csproj"
$publishDir = Join-Path $root "publish\win-x64"
$distDir    = Join-Path $root "dist"
$objDir     = Join-Path $installerDir "obj"

$appFiles = Join-Path $installerDir "AppFiles.wxs"
$prodGen  = Join-Path $installerDir "Product.gen.wxs"

$icoPath    = Join-Path $root "Assets\menupro-ui.ico"
$readmePath = Join-Path $installerDir "README-DEPENDENCIAS.txt"

if (!(Test-Path $csproj)) { throw "csproj não encontrado: $csproj" }

# Garante pastas
New-Item -ItemType Directory -Force (Join-Path $root "Assets") | Out-Null
New-Item -ItemType Directory -Force $publishDir | Out-Null
New-Item -ItemType Directory -Force $distDir    | Out-Null
New-Item -ItemType Directory -Force $objDir     | Out-Null

# README padrão (se não existir)
if (!(Test-Path $readmePath)) {
@"
MenuProUI - Dependências (Windows)

RDP:
- mstsc.exe já vem no Windows.

SSH:
- Precisa do OpenSSH Client (ssh.exe).
- Se faltar, instale (PowerShell como Admin):
  Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0

URLs:
- Abre no navegador padrão do Windows.

Arquivos:
- Documentos\MenuProUI\clientes.csv e acessos.csv
- O app NÃO grava senhas.
"@ | Set-Content -Path $readmePath -Encoding UTF8
}

# Ícone: exige que exista (pra MSI e WinExe ficarem bonitos)
if (!(Test-Path $icoPath)) {
  throw "Ícone não encontrado: $icoPath (crie Assets\menupro-ui.ico)"
}

# Garante csproj: OutputType=WinExe + ApplicationIcon
$cs = Get-Content $csproj -Raw
$changed = $false

if ($cs -notmatch "<OutputType>WinExe</OutputType>") {
  if ($cs -match "<OutputType>") {
    $cs = [regex]::Replace($cs, "<OutputType>.*?</OutputType>", "<OutputType>WinExe</OutputType>", "IgnoreCase")
  } else {
    $cs = $cs -replace "(<PropertyGroup>\s*)", "`$1`r`n    <OutputType>WinExe</OutputType>`r`n"
  }
  $changed = $true
}

if ($cs -notmatch "<ApplicationIcon>") {
  $cs = $cs -replace "(<PropertyGroup>\s*)", "`$1`r`n    <ApplicationIcon>Assets\menupro-ui.ico</ApplicationIcon>`r`n"
  $changed = $true
}

if ($changed) {
  Set-Content -Path $csproj -Value $cs -Encoding UTF8
  Write-Host "csproj OK: OutputType=WinExe e ApplicationIcon definido."
} else {
  Write-Host "csproj OK: OutputType=WinExe e ApplicationIcon definido."
}

Push-Location $root
try {
  Write-Host "== Publish (self-contained) =="
  ExecOrThrow "dotnet" @(
    "publish", $csproj,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-o", $publishDir
  )

  Write-Host "== Harvest publish folder (heat) =="
  ExecOrThrow $heat @(
    "dir", $publishDir,
    "-gg",
    "-cg", "AppFiles",
    "-dr", "INSTALLFOLDER",
    "-srd","-scom","-sreg",
    "-var", "var.SourceDir",
    "-out", $appFiles
  )

  Write-Host "== Patch AppFiles.wxs (ICE38: HKCU KeyPath + GUID estável) =="

  $nsUri = "http://schemas.microsoft.com/wix/2006/wi"
  [xml]$doc = Get-Content -Path $appFiles

  $nsmgr = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
  $nsmgr.AddNamespace("w", $nsUri)

  $components = $doc.SelectNodes("//w:Component", $nsmgr)
  foreach ($cmp in $components) {
    $cmpId = $cmp.GetAttribute("Id")
    if ([string]::IsNullOrWhiteSpace($cmpId)) { continue }

    # 1) Remove KeyPath de File (se existir)
    $files = $cmp.SelectNodes("./w:File", $nsmgr)
    foreach ($f in $files) {
      if ($f.HasAttribute("KeyPath")) { $f.RemoveAttribute("KeyPath") }
    }

    # 2) RegistryValue HKCU como KeyPath (se não existir)
    $hasRegKeyPath = $false
    $regs = $cmp.SelectNodes("./w:RegistryValue", $nsmgr)
    foreach ($r in $regs) {
      if ($r.GetAttribute("Root") -eq "HKCU" -and $r.GetAttribute("KeyPath") -eq "yes") {
        $hasRegKeyPath = $true
        break
      }
    }

    if (-not $hasRegKeyPath) {
      $reg = $doc.CreateElement("RegistryValue", $nsUri)
      $reg.SetAttribute("Root","HKCU")
      $reg.SetAttribute("Key","Software\MenuProUI\Components")
      $reg.SetAttribute("Name",$cmpId)
      $reg.SetAttribute("Type","integer")
      $reg.SetAttribute("Value","1")
      $reg.SetAttribute("KeyPath","yes")
      [void]$cmp.PrependChild($reg)
    }

    # 3) GUID determinístico (troca Guid="*" por Guid fixo)
    $g = New-GuidV5 -Namespace $componentNamespace -Name ("AppFiles:" + $cmpId)
    $cmp.SetAttribute("Guid", ("{" + $g.ToString() + "}"))
  }

  $doc.Save($appFiles)

  Write-Host "== Generate Product.gen.wxs =="

  # GUIDs determinísticos pros seus components "fixos"
  $guidStartMenu = (New-GuidV5 $componentNamespace "Product:StartMenuShortcuts").ToString()
  $guidDesktop   = (New-GuidV5 $componentNamespace "Product:DesktopShortcut").ToString()
  $guidDocs      = (New-GuidV5 $componentNamespace "Product:Docs").ToString()
  $guidCleanup   = (New-GuidV5 $componentNamespace "Product:Cleanup").ToString()

  $gen = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="$nsUri">
  <Product Id="*" Name="MenuProUI" Language="1046" Version="$Version"
           Manufacturer="Solutions" UpgradeCode="$($upgradeCode.ToString())">

    <Package InstallerVersion="500" Compressed="yes" InstallScope="perUser" />
    <MajorUpgrade DowngradeErrorMessage="Uma versão mais nova do MenuProUI já está instalada." />
    <MediaTemplate EmbedCab="yes" />

    <Property Id="ARPPRODUCTICON" Value="MenuProUIIcon" />
    <Icon Id="MenuProUIIcon" SourceFile="$icoPath" />

    <Directory Id="TARGETDIR" Name="SourceDir">

      <!-- Necessário pra DirectoryRef funcionar -->
      <Directory Id="DesktopFolder" />

      <!-- App em LocalAppData (per-user) -->
      <Directory Id="LocalAppDataFolder">
        <Directory Id="INSTALLFOLDER" Name="MenuProUI" />
      </Directory>

      <!-- Start Menu (per-user) -->
      <Directory Id="ProgramMenuFolder">
        <Directory Id="ApplicationProgramsFolder" Name="MenuProUI" />
      </Directory>
    </Directory>

    <Feature Id="MainFeature" Title="MenuProUI" Level="1">
      <ComponentGroupRef Id="AppFiles" />
      <ComponentRef Id="StartMenuShortcuts" />
      <ComponentRef Id="DesktopShortcut" />
      <ComponentRef Id="Docs" />
      <ComponentRef Id="Cleanup" />
    </Feature>

    <!-- Start Menu -->
    <DirectoryRef Id="ApplicationProgramsFolder">
      <Component Id="StartMenuShortcuts" Guid="{$guidStartMenu}">
        <Shortcut Id="StartMenuShortcut"
                  Name="MenuProUI"
                  Description="Gerenciador de acessos (SSH/RDP/URL)"
                  Target="[INSTALLFOLDER]MenuProUI.exe"
                  WorkingDirectory="INSTALLFOLDER"
                  Icon="MenuProUIIcon" />
        <RemoveFolder Id="RemoveStartMenuFolder" Directory="ApplicationProgramsFolder" On="uninstall" />
        <RegistryValue Root="HKCU" Key="Software\MenuProUI" Name="StartMenu" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </DirectoryRef>

    <!-- Desktop -->
    <DirectoryRef Id="DesktopFolder">
      <Component Id="DesktopShortcut" Guid="{$guidDesktop}">
        <Shortcut Id="DesktopShortcutFile"
                  Name="MenuProUI"
                  Target="[INSTALLFOLDER]MenuProUI.exe"
                  WorkingDirectory="INSTALLFOLDER"
                  Icon="MenuProUIIcon" />
        <RegistryValue Root="HKCU" Key="Software\MenuProUI" Name="Desktop" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </DirectoryRef>

    <!-- README + Cleanup -->
    <DirectoryRef Id="INSTALLFOLDER">
      <Component Id="Docs" Guid="{$guidDocs}">
        <File Id="ReadmeDeps" Name="README-DEPENDENCIAS.txt" Source="$readmePath" />
        <RegistryValue Root="HKCU" Key="Software\MenuProUI" Name="Docs" Type="integer" Value="1" KeyPath="yes" />
      </Component>

      <Component Id="Cleanup" Guid="{$guidCleanup}">
        <RemoveFolder Id="RemoveInstallFolder" Directory="INSTALLFOLDER" On="uninstall" />
        <RegistryValue Root="HKCU" Key="Software\MenuProUI" Name="Cleanup" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </DirectoryRef>

    <UIRef Id="WixUI_Minimal" />
    <UIRef Id="WixUI_ErrorProgressText" />
  </Product>
</Wix>
"@

  Set-Content -Path $prodGen -Value $gen -Encoding UTF8

  Write-Host "== Candle =="
  ExecOrThrow $candle @(
    "-nologo",
    "-dSourceDir=$publishDir",
    "-out", ($objDir + "\"),
    $prodGen,
    $appFiles
  )

  Write-Host "== Light (gera MSI) =="
  $msiOut = Join-Path $distDir ("MenuProUI-" + $Version + "-x64.msi")
  ExecOrThrow $light @(
    "-nologo",
    "-ext", "WixUIExtension",
    "-ext", "WixUtilExtension",
    "-out", $msiOut,
    (Join-Path $objDir "Product.gen.wixobj"),
    (Join-Path $objDir "AppFiles.wixobj")
  )

  Write-Host "OK -> $msiOut"
}
finally {
  Pop-Location
}
