param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$output = Join-Path $root 'publish'

Write-Host "Publishing to: $output" -ForegroundColor Cyan

# Remove existing exes first so antivirus/OS file locks don't block the bundler
Get-ChildItem $output -Filter '*.exe' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# Publish CLI (adept-tool.exe)
Write-Host "`nPublishing CLI..." -ForegroundColor Yellow
dotnet publish "$root/src/AdeptTools.Cli" -o $output -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Publish Launcher (adept-tool-launcher.exe)
Write-Host "`nPublishing Launcher..." -ForegroundColor Yellow
dotnet publish "$root/src/AdeptTools.Launcher" -r win-x64 -o $output -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nPublish complete." -ForegroundColor Green
Get-ChildItem $output -Filter '*.exe' | Select-Object Name, @{N='Size (KB)';E={[math]::Round($_.Length/1KB,1)}}
