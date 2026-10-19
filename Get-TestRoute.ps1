<#
    Configures and registers the "Indian Railways" content profile into .\Content
    so Railz has real Indian routes, track database, rolling stock and activity paths to
    render. Nothing is generated or faked: the simulator drives the route's own MSTS files.

    Usage:
        .\Get-TestRoute.ps1              configure and register profile
        .\Get-TestRoute.ps1 -Launch      ... and start the simulator in Explore mode
#>
param(
    [string]$ContentRoot = (Join-Path $PSScriptRoot 'Content'),
    [string]$RouteName = 'INDRAILV2',
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'

$name    = 'Indian Railways'
$profile = Join-Path $ContentRoot $name

if (-not (Test-Path $profile)) {
    throw "Indian Railways profile not found at '$profile'."
}

$root = $profile

$routesPath = Join-Path $root 'routes'
$routeDir = Get-ChildItem $routesPath -Directory | Where-Object { $_.Name -like "*$RouteName*" } | Select-Object -First 1
if (-not $routeDir) {
    $routeDir = Get-ChildItem $routesPath -Directory | Select-Object -First 1
}

$pathFile = Get-ChildItem (Join-Path $routeDir.FullName 'PATHS') -Filter *.pat -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like '*Rajdhani*' } | Select-Object -First 1
if (-not $pathFile) {
    $pathFile = Get-ChildItem (Join-Path $routeDir.FullName 'PATHS') -Filter *.pat -ErrorAction SilentlyContinue | Select-Object -First 1
}

$consist  = Get-ChildItem (Join-Path $root 'TRAINS\CONSISTS') -Filter *.con -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like '*Class 47*' } | Select-Object -First 1
if (-not $consist) {
    $consist = Get-ChildItem (Join-Path $root 'TRAINS\CONSISTS') -Filter *.con -ErrorAction SilentlyContinue | Select-Object -First 1
}

Write-Host ""
Write-Host "Route    : $($routeDir.Name)"
Write-Host "Path     : $($pathFile.Name)"
Write-Host "Consist  : $($consist.Name)"

$runActivity = Join-Path $PSScriptRoot 'Program\RunActivity.exe'
if ($pathFile -and $consist -and (Test-Path $runActivity)) {
    Write-Host ""
    Write-Host "Launch with:"
    Write-Host "  `"$runActivity`" -start -explorer `"$($pathFile.FullName)`" `"$($consist.FullName)`" 12:00:00 1 0"
    if ($Launch) {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $runActivity
        $psi.WorkingDirectory = Split-Path $runActivity
        $psi.Arguments = "-start -explorer `"$($pathFile.FullName)`" `"$($consist.FullName)`" 12:00:00 1 0"
        [System.Diagnostics.Process]::Start($psi) | Out-Null
        Write-Host "Launched RunActivity.exe successfully."
    }
}
