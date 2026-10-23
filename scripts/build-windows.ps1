[CmdletBinding()]
param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repositoryRoot
try {
    $project = 'Windows/HangLater/HangLater.csproj'
    & dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
    if (-not $SkipTests) {
        foreach ($check in Get-ChildItem 'Windows/Tests' -Directory | Sort-Object Name) {
            & dotnet run --project (Join-Path $check.FullName ($check.Name + '.csproj')) -c Release
            if ($LASTEXITCODE -ne 0) { throw ($check.Name + ' failed.') }
        }
    }
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o 'artifacts/windows' --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    $executable = Join-Path $repositoryRoot 'artifacts/windows/HangLater.exe'
    if ((Get-Item -LiteralPath $executable).VersionInfo.FileVersion -ne '0.10.1.0') { throw 'Unexpected executable version.' }
    $hash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath 'artifacts/windows/HangLater.exe.sha256' -Value ($hash + '  HangLater.exe') -Encoding ascii
    Write-Output ('Portable release: ' + $executable)
} finally { Pop-Location }
