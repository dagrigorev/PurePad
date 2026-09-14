#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build (and optionally test/run) the PurePad solution.
.EXAMPLE
    ./build.ps1                 # Release build of the whole solution
    ./build.ps1 -Configuration Debug
    ./build.ps1 -Test           # build, then run the test suite
    ./build.ps1 -Run            # build, then launch PurePad
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Test,
    [switch]$Run,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$solution = Join-Path $root 'PurePad.sln'

Write-Host "Building PurePad ($Configuration)..." -ForegroundColor Cyan

$restoreArg = if ($NoRestore) { '--no-restore' } else { $null }
dotnet build $solution -c $Configuration @($restoreArg | Where-Object { $_ })
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

if ($Test) {
    Write-Host 'Running tests...' -ForegroundColor Cyan
    dotnet test $solution -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests failed (exit $LASTEXITCODE)." }
}

if ($Run) {
    $exe = Join-Path $root "src/PurePad/bin/$Configuration/net8.0-windows/PurePad.exe"
    Write-Host "Launching $exe" -ForegroundColor Cyan
    & $exe
}

Write-Host 'Done.' -ForegroundColor Green
