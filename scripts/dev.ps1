#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('up', 'down', 'reset', 'status')]
    [string]$Command,

    [string[]]$Profile = @('infra')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $Command) {
    Write-Output 'Usage: ./scripts/dev.ps1 <up|down|reset|status> [-Profile <profile[]>]'
    exit 1
}

try {
    & docker info --format '{{.ServerVersion}}' *> $null
    if ($LASTEXITCODE -ne 0) { throw 'unavailable' }
}
catch {
    Write-Error 'Docker is not available.' -ErrorAction Continue
    exit 2
}

$composeFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'deploy/compose.yaml'
$base = @('compose', '-f', $composeFile)
$allProfiles = @('--profile', '*')

$arguments = switch ($Command) {
    'up' {
        $selected = foreach ($name in $Profile) { '--profile'; $name }
        $base + @($selected) + @('up', '--detach', '--wait', '--wait-timeout', '120')
    }
    'down' { $base + $allProfiles + @('down', '--remove-orphans') }
    'reset' { $base + $allProfiles + @('down', '--volumes', '--remove-orphans') }
    'status' { $base + $allProfiles + @('ps', '--all') }
}

& docker @arguments
exit $LASTEXITCODE
