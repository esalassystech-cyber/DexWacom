$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCommand) { $dotnet = $dotnetCommand.Source }
else {
    $localSdk = Join-Path $env:LOCALAPPDATA 'WacomBoardBuild\dotnet\dotnet.exe'
    if (Test-Path $localSdk) { $dotnet = $localSdk } else { throw 'Instala .NET SDK 10 x64 para publicar la aplicación.' }
}

$publishDir = Join-Path $projectRoot 'artifacts\publish'
$distDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Force -Path $publishDir, $distDir | Out-Null
$iconGenerator = Get-Command powershell.exe -ErrorAction SilentlyContinue
if (-not $iconGenerator) { throw 'Se requiere Windows PowerShell para generar el icono multirresolución.' }
& $iconGenerator.Source -STA -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot 'scripts\generate-icon.ps1')
if ($LASTEXITCODE -ne 0) { throw "generate-icon.ps1 terminó con código $LASTEXITCODE" }
& $dotnet publish (Join-Path $projectRoot 'src\Pizarra.App\Pizarra.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish terminó con código $LASTEXITCODE" }
if (-not (Test-Path (Join-Path $publishDir 'Pizarra.App.exe'))) { throw 'No se generó Pizarra.App.exe.' }

$compiler = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $compiler)) { throw 'Instala Inno Setup 6 (JRSoftware.InnoSetup) para generar el instalador.' }
& $compiler (Join-Path $projectRoot 'installer\PizarraWorkflows.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC terminó con código $LASTEXITCODE" }
Write-Host "Instalador de diagnóstico listo: $(Join-Path $distDir 'Pizarra-Workflows-Diagnostics-0.2.2.exe')"
