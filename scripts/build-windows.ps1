param(
    [ValidateSet("Release", "Debug")][string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-arm64")][string]$Runtime = "win-x64",
    [switch]$SkipChecks,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$Repo = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Repo "Windows/HangLater.Windows/HangLater.Windows.csproj"
$Checks = Join-Path $Repo "Windows/HangLater.Windows.Checks/HangLater.Windows.Checks.csproj"
$Publish = Join-Path $Repo "build/windows/$Runtime"
$Releases = Join-Path $Repo "build/releases"

if (-not $SkipChecks) {
    dotnet run --project $Checks -c Release
}

Remove-Item $Publish -Recurse -Force -ErrorAction SilentlyContinue
New-Item $Publish -ItemType Directory -Force | Out-Null
New-Item $Releases -ItemType Directory -Force | Out-Null

dotnet publish $Project -c $Configuration -r $Runtime --self-contained true -o $Publish `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=false `
    -p:DebugType=None -p:DebugSymbols=false

$Zip = Join-Path $Releases "HangLater-Windows-$Runtime.zip"
Remove-Item $Zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$Publish/*" -DestinationPath $Zip -CompressionLevel Optimal

if ($Runtime -eq "win-x64" -and -not $SkipInstaller) {
    $Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $Iscc) {
        & $Iscc "/DSourceDir=$Publish" (Join-Path $Repo "Windows/installer.iss")
    } else {
        Write-Warning "Inno Setup 6 was not found; the portable ZIP was still created."
    }
}

Write-Host "Windows artifacts are in $Releases"
