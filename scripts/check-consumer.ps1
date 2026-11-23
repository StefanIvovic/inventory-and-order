#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageId,
    [string]$Version,
    [string]$Feed = 'artifacts/packages'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$feedPath = [System.IO.Path]::GetFullPath($(if ([System.IO.Path]::IsPathRooted($Feed)) { $Feed } else { Join-Path $repoRoot $Feed }))

if (-not $Version) {
    $prefix = "$PackageId."
    $candidates = Get-ChildItem -Path $feedPath -Filter "$PackageId.*.nupkg" -File |
        ForEach-Object { $_.BaseName.Substring($prefix.Length) } |
        Where-Object { $_ -match '^\d+\.\d+\.\d+' }
    if (-not $candidates) {
        Write-Error "No version of $PackageId found in $feedPath." -ErrorAction Continue
        exit 1
    }
    $Version = $candidates |
        Sort-Object { [System.Management.Automation.SemanticVersion]::new($_) } |
        Select-Object -Last 1
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("iao-consumer-" + [guid]::NewGuid().ToString('N'))
$cache = Join-Path ([System.IO.Path]::GetTempPath()) ("iao-nuget-" + [guid]::NewGuid().ToString('N'))
$previousCache = $env:NUGET_PACKAGES
$exitCode = 1

try {
    New-Item -ItemType Directory -Force -Path $work, $cache | Out-Null

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$PackageId" Version="[$Version]" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $work 'Consumer.csproj') -Encoding utf8

    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feedPath" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="BuildingBlocks.*" />
      <package pattern="*.Contracts" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -Path (Join-Path $work 'nuget.config') -Encoding utf8

    Copy-Item -Path (Join-Path $repoRoot 'global.json') -Destination $work

    $env:NUGET_PACKAGES = $cache
    Push-Location $work
    try {
        & dotnet build --nologo
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Consumer could not restore or build $PackageId $Version from the local feed (package version not found or build failed)." -ErrorAction Continue
        }
        else {
            Write-Output "Consumer built against $PackageId $Version"
            $exitCode = 0
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:NUGET_PACKAGES = $previousCache
    foreach ($path in @($work, $cache)) {
        if (Test-Path $path) { Remove-Item -Recurse -Force $path }
    }
    foreach ($path in @($work, $cache)) {
        if (Test-Path $path) {
            Write-Error "Temporary folder was not removed: $path" -ErrorAction Continue
            $exitCode = 1
        }
    }
}

exit $exitCode
