<#
    Installs the official Open Rails starter route "Demo Model 1" into .\Content and registers it
    as a content profile, so Railz has a real route, track database, rolling stock and activity to
    render. Nothing is generated or faked: the simulator drives the route's own MSTS files.

    This is the route Open Rails offers on its own Content page, and the only free one with a stable
    direct download and a published start configuration (route "Scottish Capital Express").

    Download 260 MB / installed ~315 MB, one-off.

    Usage:
        .\Get-TestRoute.ps1              download, install, register
        .\Get-TestRoute.ps1 -Launch      ... and start the simulator in Explore mode

    Uninstall: delete .\Content and the "Demo Model 1" value under
    HKCU\SOFTWARE\OpenRails\ORTS\Folders.
#>
param(
    [string]$ContentRoot = (Join-Path $PSScriptRoot 'Content'),
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'

$name    = 'Demo Model 1'
$url     = 'https://static.openrails.org/files/DemoModel1.zip'
$profile = Join-Path $ContentRoot $name
$zip     = Join-Path $ContentRoot ($name + '.zip')

New-Item -ItemType Directory -Force -Path $ContentRoot | Out-Null

if (-not (Test-Path $zip)) {
    Write-Host "Downloading $url"
    (New-Object Net.WebClient).DownloadFile($url, $zip)
}

if (-not (Test-Path $profile)) {
    Write-Host "Extracting into $profile"
    Expand-Archive -LiteralPath $zip -DestinationPath $profile
}

# The zip holds the MSTS tree either at its root or one directory down.
$root = if (Test-Path (Join-Path $profile 'routes')) { $profile } else {
    (Get-ChildItem $profile -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'routes') } | Select-Object -First 1).FullName
}
if (-not $root) { throw "No 'routes' directory under $profile - unexpected zip layout." }

# This is the registry key the launcher and the simulator read their content profiles from.
reg add 'HKCU\SOFTWARE\OpenRails\ORTS\Folders' /v $name /t REG_SZ /d $root /f | Out-Null
Write-Host "Registered profile '$name' -> $root"

$routeDir = Get-ChildItem (Join-Path $root 'routes') -Directory | Select-Object -First 1
$pathFile = Get-ChildItem (Join-Path $routeDir.FullName 'PATHS') -Filter *.pat -ErrorAction SilentlyContinue | Select-Object -First 1
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
    # Same arguments the launcher uses for Explore mode, so this needs no launcher at all.
    $runArgs = @('-start', '-explorer', "`"$($pathFile.FullName)`"", "`"$($consist.FullName)`"", '12:00:00', '1', '0')
    Write-Host ""
    Write-Host "Launch with:"
    Write-Host "  `"$runActivity`" $($runArgs -join ' ')"
    if ($Launch) {
        Start-Process -FilePath $runActivity -ArgumentList $runArgs -WorkingDirectory (Split-Path $runActivity)
    }
}
