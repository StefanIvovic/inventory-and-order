#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 60,
    [int]$PollSeconds = 2
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$filters = @('label=org.testcontainers', 'label=com.docker.compose.project=e2e-test')

function Get-Leftovers {
    $found = [System.Collections.Generic.List[string]]::new()
    foreach ($filter in $filters) {
        $containers = & docker ps -a --filter $filter --format '{{.ID}} {{.Image}} {{.Status}} {{.Labels}}'
        if ($LASTEXITCODE -ne 0) { throw 'docker ps failed' }
        foreach ($line in $containers) { if ($line) { $found.Add("container $line") } }

        $volumes = & docker volume ls --filter $filter --format '{{.Name}} {{.Labels}}'
        if ($LASTEXITCODE -ne 0) { throw 'docker volume ls failed' }
        foreach ($line in $volumes) { if ($line) { $found.Add("volume $line") } }
    }
    return @($found | Sort-Object -Unique)
}

try {
    & docker info --format '{{.ServerVersion}}' *> $null
    if ($LASTEXITCODE -ne 0) { throw 'unavailable' }
}
catch {
    Write-Error 'Docker is not available.' -ErrorAction Continue
    exit 2
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    $leftovers = @(Get-Leftovers)
    if ($leftovers.Count -eq 0) {
        Write-Output 'No test containers or volumes left behind.'
        exit 0
    }
    Start-Sleep -Seconds $PollSeconds
} while ([DateTimeOffset]::UtcNow -lt $deadline)

Write-Output "Leftovers after $TimeoutSeconds s:"
$leftovers | ForEach-Object { Write-Output "  $_" }
exit 1
