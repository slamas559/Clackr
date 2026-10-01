[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '1.0.0',
    [string]$InnoSetupCompiler
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'KeySonic.UI\KeySonic.UI.csproj'
$installerPath = Join-Path $repositoryRoot 'installer\Clackr.iss'
$publishPath = Join-Path $repositoryRoot 'KeySonic.UI\bin\Release\net10.0-windows\win-x64\publish'
$artifactsPath = Join-Path $repositoryRoot 'artifacts'

if (-not $InnoSetupCompiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        $InnoSetupCompiler = $command.Source
    }
}

if (-not $InnoSetupCompiler) {
    $compilerCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $InnoSetupCompiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $InnoSetupCompiler -or -not (Test-Path $InnoSetupCompiler)) {
    throw 'Inno Setup 6 was not found. Install it or pass -InnoSetupCompiler with the path to ISCC.exe.'
}

New-Item -ItemType Directory -Path $artifactsPath -Force | Out-Null

& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    "-p:Version=$Version" `
    '-p:DebugType=None' `
    '-p:DebugSymbols=false'

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path (Join-Path $publishPath 'Clackr.exe'))) {
    throw "Published application was not found at $publishPath."
}

if (-not (Test-Path (Join-Path $publishPath 'packs'))) {
    throw 'The published app is missing its bundled packs folder.'
}

& $InnoSetupCompiler "/DAppVersion=$Version" $installerPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Installer created: $(Join-Path $artifactsPath "Clackr-Setup-$Version-win-x64.exe")"