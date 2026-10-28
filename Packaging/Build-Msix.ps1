<#
.SYNOPSIS
    Builds EyeBrowse as an MSIX bundle (x64 and ARM64), signed if a certificate is given.

.DESCRIPTION
    Needs Windows, the .NET 10 SDK and the Windows SDK (makeappx, makepri, signtool).
    Without a certificate the bundle is unsigned: fine to upload to the Microsoft Store,
    which signs it, but Windows won't install it directly. See Packaging/README.md.

.EXAMPLE
    ./Packaging/Build-Msix.ps1 -Version 0.2.0.0 -Publisher "CN=EyeBrowse Test" `
        -CertificatePath test.pfx -CertificatePassword (Read-Host -AsSecureString)
#>
param(
    [string] $Version = "0.0.1.0",
    # The version the app itself shows (About), e.g. 1.2.3-beta; by default $Version without the ".0".
    [string] $AppVersion,
    [string] $IdentityName = "EyeBrowse",
    [string] $Publisher = "CN=EyeBrowse Test",
    [string] $PublisherDisplayName = "EyeBrowse",
    [string[]] $Architectures = @("x64", "arm64"),
    [string] $OutDir = "msix",
    [string] $CertificatePath,
    [SecureString] $CertificatePassword
)
$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') {
    throw "Version must look like 1.2.3.0 (four numbers, the last 0, as the Store requires): '$Version'"
}
# The app itself (its About window) shows 1.2.3, as the other platforms' builds do.
if (-not $AppVersion) { $AppVersion = $Version -replace '\.0$', '' }

$root = Split-Path $PSScriptRoot -Parent
$OutDir = [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutDir))
$work = Join-Path $OutDir "work"
$packages = Join-Path $work "packages"
Remove-Item $OutDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $packages -ItemType Directory -Force | Out-Null

# The newest Windows SDK's tools.
$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" |
    Sort-Object { [version]($_.Directory.Parent.Name) } | Select-Object -Last 1 |
    ForEach-Object Directory
if (-not $sdkBin) { throw "Windows SDK not found (makeappx.exe)" }
function Invoke-Tool($name) {
    & (Join-Path $sdkBin $name) @args
    if ($LASTEXITCODE -ne 0) { throw "$name failed ($LASTEXITCODE)" }
}

foreach ($arch in $Architectures) {
    $layout = Join-Path $work $arch
    Write-Host "== ${arch}: publishing"
    # Not single-file: the package installs a folder, so nothing needs unpacking at start.
    dotnet publish (Join-Path $root "EyeBrowse.csproj") -c Release -r "win-$arch" --self-contained true `
        -p:PublishTrimmed=true -p:DebugType=none "-p:Version=$AppVersion" -o $layout
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $arch" }
    Get-ChildItem $layout -Filter *.pdb | Remove-Item

    Copy-Item (Join-Path $PSScriptRoot "Images") (Join-Path $layout "Images") -Recurse
    (Get-Content (Join-Path $PSScriptRoot "AppxManifest.xml") -Raw) `
        -replace '\{\{IdentityName\}\}', [Security.SecurityElement]::Escape($IdentityName) `
        -replace '\{\{Publisher\}\}', [Security.SecurityElement]::Escape($Publisher) `
        -replace '\{\{PublisherDisplayName\}\}', [Security.SecurityElement]::Escape($PublisherDisplayName) `
        -replace '\{\{Version\}\}', $Version `
        -replace '\{\{Architecture\}\}', $arch |
        Set-Content (Join-Path $layout "AppxManifest.xml") -Encoding utf8

    # resources.pri lets Windows pick the right image for each scale and size.
    Write-Host "== ${arch}: packing"
    $priConfig = Join-Path $work "priconfig-$arch.xml"
    Invoke-Tool makepri.exe createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o
    Invoke-Tool makepri.exe new /pr $layout /cf $priConfig /mn (Join-Path $layout "AppxManifest.xml") `
        /of (Join-Path $layout "resources.pri") /o
    Invoke-Tool makeappx.exe pack /d $layout /p (Join-Path $packages "EyeBrowse_${Version}_$arch.msix") /o
}

Write-Host "== bundling"
$bundle = Join-Path $OutDir "EyeBrowse_$Version.msixbundle"
Invoke-Tool makeappx.exe bundle /d $packages /p $bundle /bv $Version /o

if ($CertificatePath) {
    Write-Host "== signing"
    $password = [Net.NetworkCredential]::new("", $CertificatePassword).Password
    Invoke-Tool signtool.exe sign /fd SHA256 /f $CertificatePath /p $password $bundle
} else {
    Write-Host "== not signed (no certificate): fine for the Store, not for installing directly"
}

Remove-Item $work -Recurse -Force
Write-Host "Built $bundle"
