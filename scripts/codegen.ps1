#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Service,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$serviceRoot = Join-Path $repoRoot "src/services/$Service"
$api = Get-ChildItem -Path $serviceRoot -Filter '*.Api.csproj' -Recurse -File | Select-Object -First 1
if (-not $api) {
    Write-Error "No *.Api project found under $serviceRoot." -ErrorAction Continue
    exit 1
}

$generated = Join-Path $api.DirectoryName 'Internal/Generated'
if (Test-Path $generated) {
    Remove-Item -Recurse -Force $generated
}

$placeholders = @{
    "ConnectionStrings__$((Get-Culture).TextInfo.ToTitleCase($Service))" = 'Host=127.0.0.1;Port=9;Database=codegen'
    'RabbitMq__Uri' = 'amqp://127.0.0.1:9'
    'Authentication__Authority' = 'http://127.0.0.1:9'
    'Authentication__Audience' = $Service
}
$extra = Join-Path $serviceRoot 'codegen.env'
if (Test-Path $extra) {
    foreach ($line in Get-Content $extra) {
        if ($line -match '^([A-Za-z0-9_]+)=(.*)$') {
            $placeholders[$Matches[1]] = $Matches[2]
        }
    }
}

$previous = @{}
foreach ($key in $placeholders.Keys) {
    $previous[$key] = [Environment]::GetEnvironmentVariable($key)
    [Environment]::SetEnvironmentVariable($key, $placeholders[$key])
}

try {
    & dotnet run --project $api.FullName -c Debug -- codegen write | Where-Object { $_ -match 'Wrote generated code' }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Code generation failed for $Service." -ErrorAction Continue
        exit 1
    }
}
finally {
    foreach ($key in $placeholders.Keys) {
        [Environment]::SetEnvironmentVariable($key, $previous[$key])
    }
}

if ($Check) {
    $relative = [System.IO.Path]::GetRelativePath($repoRoot, $generated).Replace('\', '/')
    $changes = & git -C $repoRoot status --porcelain -- $relative
    if ($changes) {
        Write-Output 'Generated code is out of date. Run ./scripts/codegen.ps1 and commit the result:'
        $changes | ForEach-Object { Write-Output "  $_" }
        exit 1
    }

    Write-Output "Generated code for $Service is up to date."
}

exit 0
