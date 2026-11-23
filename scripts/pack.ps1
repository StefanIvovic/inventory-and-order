#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Output = 'artifacts/packages'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$feed = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $repoRoot $Output }
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("iao-pack-" + [guid]::NewGuid().ToString('N'))

function Get-ContentFingerprint([string]$Path) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = $zip.Entries |
            Where-Object { $_.FullName -notmatch '^(_rels/|package/|\[Content_Types\]\.xml$)' } |
            Sort-Object FullName
        $sha = [System.Security.Cryptography.SHA256]::Create()
        $lines = foreach ($entry in $entries) {
            $stream = $entry.Open()
            try { "$($entry.FullName):$([Convert]::ToHexString($sha.ComputeHash($stream)))" } finally { $stream.Dispose() }
        }
        return ($lines -join "`n")
    }
    finally {
        $zip.Dispose()
    }
}

function Get-GlobalPackagesFolder {
    $line = (& dotnet nuget locals global-packages --list) | Select-Object -First 1
    return ($line -replace '^global-packages:\s*', '').Trim()
}

function Read-PackageIdentity([string]$Path) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $nuspec = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' -and $_.FullName -notlike '*/*' } | Select-Object -First 1
        $reader = [System.IO.StreamReader]::new($nuspec.Open())
        try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return [pscustomobject]@{ Id = $xml.package.metadata.id; Version = $xml.package.metadata.version }
    }
    finally {
        $zip.Dispose()
    }
}

$projects = Get-ChildItem -Path (Join-Path $repoRoot 'src') -Filter '*.csproj' -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Where-Object { ((& dotnet msbuild $_.FullName -getProperty:IsPackable) | Select-Object -Last 1).Trim() -eq 'true' }

if (-not $projects) {
    Write-Error 'No packable projects found under src/.'
    exit 1
}

New-Item -ItemType Directory -Force -Path $feed | Out-Null
New-Item -ItemType Directory -Force -Path $staging | Out-Null

$failed = $false
try {
    foreach ($project in $projects) {
        & dotnet pack $project.FullName -c $Configuration -o $staging --nologo
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Packing failed for $($project.Name)." -ErrorAction Continue
            exit 1
        }
    }

    $globalPackages = Get-GlobalPackagesFolder

    foreach ($package in Get-ChildItem -Path $staging -Filter '*.nupkg' -File) {
        $identity = Read-PackageIdentity $package.FullName
        $target = Join-Path $feed $package.Name
        $isPrerelease = $identity.Version -match '-'

        if ((Test-Path $target) -and -not $isPrerelease) {
            if ((Get-ContentFingerprint $target) -ne (Get-ContentFingerprint $package.FullName)) {
                Write-Error "Release $($identity.Id) $($identity.Version) already exists in the feed with different content. Tag a new version instead of overwriting." -ErrorAction Continue
                $failed = $true
                continue
            }
            Write-Output "$($identity.Id) $($identity.Version) unchanged"
            continue
        }

        Copy-Item -Path $package.FullName -Destination $target -Force

        if ($isPrerelease -and $globalPackages) {
            $cached = Join-Path (Join-Path $globalPackages $identity.Id.ToLowerInvariant()) $identity.Version.ToLowerInvariant()
            if (Test-Path $cached) { Remove-Item -Recurse -Force $cached }
        }

        Write-Output "$($identity.Id) $($identity.Version)"
    }
}
finally {
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
}

if ($failed) { exit 1 }
exit 0
