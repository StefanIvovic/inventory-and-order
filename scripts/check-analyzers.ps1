#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$PackageId = 'BuildingBlocks.Messaging',
    [string]$Feed = 'artifacts/packages'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$feedPath = [System.IO.Path]::GetFullPath($(if ([System.IO.Path]::IsPathRooted($Feed)) { $Feed } else { Join-Path $repoRoot $Feed }))

$prefix = "$PackageId."
$version = Get-ChildItem -Path $feedPath -Filter "$PackageId.*.nupkg" -File |
    ForEach-Object { $_.BaseName.Substring($prefix.Length) } |
    Where-Object { $_ -match '^\d+\.\d+\.\d+' } |
    Sort-Object { [System.Management.Automation.SemanticVersion]::new($_) } |
    Select-Object -Last 1
if (-not $version) {
    Write-Error "No version of $PackageId found in $feedPath." -ErrorAction Continue
    exit 1
}

$violating = @'
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Core.Permissions;
using Microsoft.EntityFrameworkCore;

namespace Consumer;

public sealed class ShopDb(DbContextOptions<ShopDb> options) : DbContext(options);

public sealed record PlaceOrder;

public sealed class PlaceOrderHandler(IPermissionCache cache)
{
    public Task Handle(PlaceOrder command, ShopDb db, CancellationToken ct) => db.SaveChangesAsync(ct);
}
'@

$clean = @'
using System;
using BuildingBlocks.Messaging;

namespace Consumer;

public sealed record AssignRole(Guid UserId);

public static class AssignRoleHandler
{
    public static PermissionAssignmentsChanged Handle(AssignRole command) => new("acme", command.UserId);
}
'@

function Invoke-ConsumerBuild([string]$source) {
    $work = Join-Path ([System.IO.Path]::GetTempPath()) ("iao-analyzers-" + [guid]::NewGuid().ToString('N'))
    $cache = Join-Path ([System.IO.Path]::GetTempPath()) ("iao-nuget-" + [guid]::NewGuid().ToString('N'))
    $previousCache = $env:NUGET_PACKAGES
    try {
        New-Item -ItemType Directory -Force -Path $work, $cache | Out-Null
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$PackageId" Version="[$version]" />
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
        Set-Content -Path (Join-Path $work 'Code.cs') -Value $source -Encoding utf8

        $env:NUGET_PACKAGES = $cache
        $output = & dotnet build $work --nologo 2>&1 | Out-String
        return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
    }
    finally {
        $env:NUGET_PACKAGES = $previousCache
        foreach ($path in @($work, $cache)) {
            if (Test-Path $path) { Remove-Item -Recurse -Force $path }
        }
    }
}

$failures = [System.Collections.Generic.List[string]]::new()

$bad = Invoke-ConsumerBuild $violating
if ($bad.ExitCode -eq 0) { $failures.Add('The violating consumer built successfully.') }
foreach ($id in 'IAO1001', 'IAO1002') {
    if ($bad.Output -notmatch "error $id") { $failures.Add("The violating consumer did not report $id.") }
}

$good = Invoke-ConsumerBuild $clean
if ($good.ExitCode -ne 0) {
    $failures.Add('The clean consumer failed to build.')
    Write-Output $good.Output
}

if (Get-ChildItem -Path ([System.IO.Path]::GetTempPath()) -Filter 'iao-analyzers-*' -Directory -ErrorAction SilentlyContinue) {
    $failures.Add('A temporary consumer folder was left behind.')
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Output "FAIL: $_" }
    exit 1
}

Write-Output "Analyzers reach consumers of $PackageId $version (IAO1001 and IAO1002 reported, clean code builds)."
exit 0
