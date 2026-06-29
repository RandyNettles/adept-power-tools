param(
    [ValidateSet('launcher', 'cli')]
    [string]$Project = 'launcher',
    [string]$Args
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

switch ($Project) {
    'launcher' {
        Write-Host "Running Launcher..." -ForegroundColor Cyan
        dotnet run --project "$root/src/AdeptTools.Launcher" @($Args -split ' ' | Where-Object { $_ })
    }
    'cli' {
        Write-Host "Running CLI..." -ForegroundColor Cyan
        dotnet run --project "$root/src/AdeptTools.Cli" -- @($Args -split ' ' | Where-Object { $_ })
    }
}
