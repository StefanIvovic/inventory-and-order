#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Service,
    [string]$Feed = 'artifacts/packages'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$feedPath = [System.IO.Path]::GetFullPath($(if ([System.IO.Path]::IsPathRooted($Feed)) { $Feed } else { Join-Path $repoRoot $Feed }))
$props = Join-Path $repoRoot "src/services/$Service/Directory.Packages.props"

if (-not (Test-Path $props)) {
    Write-Error "No Directory.Packages.props for service '$Service' at $props." -ErrorAction Continue
    exit 1
}

[xml]$xml = Get-Content -Raw -Path $props
$pins = @($xml.SelectNodes('//PackageVersion') |
    Where-Object { $_.Include -like 'BuildingBlocks.*' } |
    ForEach-Object { [pscustomobject]@{ Id = $_.Include; Version = $_.Version } })

if ($pins.Count -eq 0) {
    Write-Output "Service '$Service' pins no BuildingBlocks packages."
    exit 0
}

$versions = @($pins.Version | Sort-Object -Unique)
if ($versions.Count -ne 1) {
    Write-Error "Service '$Service' pins different BuildingBlocks versions: $($versions -join ', ')." -ErrorAction Continue
    exit 1
}

$version = $versions[0]
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "BuildingBlocks pin '$version' is not a tagged release (x.y.z)." -ErrorAction Continue
    exit 1
}

$missing = @($pins | Where-Object { -not (Test-Path (Join-Path $feedPath "$($_.Id).$version.nupkg")) })
if ($missing.Count -eq 0) {
    Write-Output "BuildingBlocks $version is already in the feed."
    exit 0
}

$tag = "building-blocks/v$version"
& git -C $repoRoot rev-parse -q --verify "refs/tags/$tag" *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tag '$tag' does not exist. Fetch tags or release BuildingBlocks $version first." -ErrorAction Continue
    exit 1
}

$worktree = Join-Path ([System.IO.Path]::GetTempPath()) ("iao-feed-" + [guid]::NewGuid().ToString('N'))
$exitCode = 1
try {
    & git -C $repoRoot worktree add --detach --quiet $worktree $tag
    if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for $tag" }

    & pwsh -NoProfile -File (Join-Path $worktree 'scripts/pack.ps1') -Output $feedPath
    if ($LASTEXITCODE -ne 0) { throw "Packing $tag failed" }

    $stillMissing = @($pins | Where-Object { -not (Test-Path (Join-Path $feedPath "$($_.Id).$version.nupkg")) })
    if ($stillMissing.Count -gt 0) {
        throw "Packing $tag did not produce: $(($stillMissing.Id) -join ', ')"
    }

    Write-Output "BuildingBlocks $version packed from $tag into $feedPath."
    $exitCode = 0
}
catch {
    Write-Error $_ -ErrorAction Continue
}
finally {
    if (Test-Path $worktree) {
        & git -C $repoRoot worktree remove --force $worktree *> $null
    }
    & git -C $repoRoot worktree prune *> $null
}

exit $exitCode
