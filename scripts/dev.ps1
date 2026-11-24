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

$profiles = if ($Command -eq 'up') { $Profile -join ',' } else { '*' }
$arguments = switch ($Command) {
    'up' { $base + @('up', '--build', '--detach', '--wait', '--wait-timeout', '120') }
    'down' { $base + @('down', '--remove-orphans') }
    'reset' { $base + @('down', '--volumes', '--remove-orphans') }
    'status' { $base + @('ps', '--all') }
}

$previous = $env:COMPOSE_PROFILES
try {
    $env:COMPOSE_PROFILES = $profiles
    & docker @arguments
    $code = $LASTEXITCODE
}
finally {
    $env:COMPOSE_PROFILES = $previous
}
exit $code
