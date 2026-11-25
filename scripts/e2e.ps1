#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Project = 'e2e-test'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path $PSScriptRoot -Parent
$deploy = Join-Path $repoRoot 'deploy'
$compose = @(
    'compose', '-p', $Project,
    '-f', (Join-Path $deploy 'compose.yaml'),
    '-f', (Join-Path $repoRoot 'tests/e2e/compose.e2e.yaml'),
    '--env-file', (Join-Path $deploy 'versions.env')
)

& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'restore-feed.ps1') -Service identity
if ($LASTEXITCODE -ne 0) {
    Write-Error 'Restoring the package feed failed.' -ErrorAction Continue
    exit 1
}

$previous = $env:COMPOSE_PROFILES
$exitCode = 1
try {
    $env:COMPOSE_PROFILES = 'infra,apps'
    & docker @compose up --build --detach --wait --wait-timeout 300
    if ($LASTEXITCODE -ne 0) { throw 'The end-to-end stack did not become healthy.' }

    $env:COMPOSE_PROFILES = 'infra,apps,e2e'
    & docker @compose run --rm playwright
    $exitCode = $LASTEXITCODE
}
catch {
    Write-Error $_ -ErrorAction Continue
    $exitCode = 1
}
finally {
    $env:COMPOSE_PROFILES = '*'
    & docker @compose down -v --remove-orphans
    $env:COMPOSE_PROFILES = $previous

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'check-test-cleanup.ps1') -Project $Project
    if ($LASTEXITCODE -ne 0 -and $exitCode -eq 0) { $exitCode = 1 }
}

exit $exitCode
