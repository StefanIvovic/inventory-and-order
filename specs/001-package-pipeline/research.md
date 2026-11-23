# Research: Package Pipeline

All versions below were verified against their registries on 2026-11-23.

## R1. Package versioning tool

- **Decision**: MinVer 8.0.0, configured per package family through `MinVerTagPrefix`
  (`building-blocks/v` for every `BuildingBlocks.*` project, `<service>-contracts/v` for each
  contracts package).
- **Rationale**: Versions come purely from git tags with no files to maintain. Untagged commits
  get `{next-patch}-alpha.0.{height}` automatically. Tag prefixes isolate package families in a
  monorepo.
- **Alternatives considered**: Nerdbank.GitVersioning (needs a `version.json` per package, so more
  configuration). GitVersion (heavier, and its branch-strategy configuration isn't needed).
  Manual `<Version>` (forbidden by SC-002).

## R2. Failing on a missing tag prefix (FR-005)

- **Decision**: The root `Directory.Build.targets` adds a target that runs before MinVer. When
  `IsPackable == true` and `MinVerTagPrefix` is empty, it raises an error naming the project.
  `IsPackable` defaults to `false` at the root, so only explicitly packable projects are
  affected.
- **Rationale**: Without a prefix, MinVer would read every tag in the repository and produce a
  wrong version silently.

## R3. Shallow clones

- **Decision**: CI checks out with `fetch-depth: 0`. MinVer reports shallow clones with a
  warning, and with `TreatWarningsAsErrors` that warning fails the build, which satisfies the
  shallow-clone edge case.
- **Alternatives considered**: `MinVerIgnoreHeight`. Rejected because it hides the problem.

## R4. Republishing the same prerelease version and NuGet caches

- **Problem**: Two packs of uncommitted changes produce the same prerelease version. NuGet's
  global packages folder caches the first copy, so consumers would silently restore stale content.
- **Decision**:
  - `scripts/pack.ps1` removes the matching `<id>/<version>` folders from the global packages
    folder for every package it produces, but only for prerelease versions.
  - For release (tagged) versions it refuses to overwrite an existing `.nupkg` in the feed whose
    content hash differs, and fails with a clear message. This covers the stale-package edge case.
  - The consumer check always uses an isolated, temporary `NUGET_PACKAGES` folder.

## R5. Package source mapping (FR-002)

- **Decision**: The root `nuget.config` clears all sources and declares `local`
  (`artifacts/packages`) and `nuget.org`. Its `packageSourceMapping` sends `BuildingBlocks.*`
  and `*.Contracts` to `local` only, and `*` to `nuget.org`.
- **Feed folder in git (found during implementation)**: NuGet fails restore with NU1301 when a
  local source folder doesn't exist. `artifacts/packages/.gitkeep` is therefore committed, and
  `.gitignore` ignores everything else under `artifacts/`.
- **Rationale**: A package with the same name published on nuget.org can never be picked up
  instead of ours (dependency confusion), and a missing local package fails restore clearly.

## R6. No-comments check (FR-009) — REMOVED

**Owner decision (2026-11-23)**: there is no comment-detection tooling. "No comments" is an
authoring discipline under constitution IX and is checked in review. The research below is kept
only as a record of the option that was rejected.

- **Decision**: A small .NET console tool, `tools/RepoChecks`, with a `comments` command.
  - **C#**: parsed with Roslyn (Microsoft.CodeAnalysis.CSharp 5.12.0). The syntax tree reports
    single-line, multi-line and XML documentation comment trivia. Preprocessor directives
    (`#nullable`, `#pragma`) are not comments.
  - **Other types**: a small lexer per file type that tracks string state:
    - TypeScript and SCSS: `//` and `/* */`, skipping single, double and template strings.
    - HTML: `<!-- -->`.
    - YAML, Dockerfile, PowerShell, shell and `.env`: `#` outside quotes.
    - PowerShell also has `<# #>` block comments.
  - **Allowed exceptions**: a shebang on line 1 of a shell script, and Dockerfile parser
    directives on the leading lines (`# syntax=`, `# escape=`). These are directives, not
    comments.
  - **Excluded folders**: `bin/`, `obj/`, `node_modules/`, `.git/`, `.specify/`, `.claude/`,
    `artifacts/`, `specs/`. Markdown is not source.
- **Rationale**: Regex-only detection on C# gives false positives on `"https://..."` and verbatim
  or raw strings. Roslyn is exact. For the other types, a string-aware lexer is enough for our
  own sources.
- **Known limit**: TypeScript regex literals that contain `//` could be misreported. Angular
  arrives in a later phase. If it causes trouble, the TS lexer can be swapped for the TypeScript
  compiler API.
- **Alternatives considered**: A Roslyn analyzer (C# only). StyleCop (enforces documentation
  rather than banning comments). `grep` (too many false positives).

## R7. Image-pin check (FR-013)

- **Decision**: The `RepoChecks` tool has a single `images` command.
  - **Duplicates (IP008)**: one key per image repository (registry and name, without the tag).
  - **`deploy/versions.env`**: only `KEY=VALUE` lines. Keys end in `_IMAGE`. Each value is a full
    reference `registry/name:tag`. The tag must be the full release version plus variant, and is
    checked against `^\d+\.\d+(\.\d+)?(-[a-z0-9.]+)?$`. It must not be `latest`, and for
    components that publish patches it must not be major-only or major.minor. The latter rule
    comes from a small known list: dotnet and rabbitmq require a patch, postgres is
    major.minor by design.
  - **Compose files**: each `image:` value must be exactly `${KEY}`, and `KEY` must exist in
    `versions.env`. Compose YAML is parsed with YamlDotNet 18.1.0.
  - **Dockerfiles**: every `FROM` must use `${ARG}` or `$ARG`, declared with `ARG NAME` and **no
    default** before the first `FROM`, and the ARG name must exist in `versions.env`. Stage
    references (`FROM build AS final`) are allowed.
  - **Test sources** (`*.Tests` projects): any string literal that contains a repository name
    from `versions.env` followed by `:` fails, because fixtures must read the tag from the
    versions file.
  - **SDK consistency**: the tag of `DOTNET_SDK_IMAGE` must start with the `global.json` SDK
    version.
- **Rationale**: Full references (rather than tag-only variables) keep the registry and the tag
  together, and make the check a single exact-match rule.

## R8. SDK pin (FR-014)

- **Decision**: `global.json` sets `{"sdk": {"version": "10.0.401", "rollForward": "disable"}}`,
  and CI uses `actions/setup-dotnet` with `global-json-file`.
- **Rationale**: 10.0.401 is installed on the developer machine and exists as an image
  (`mcr.microsoft.com/dotnet/sdk:10.0.401-alpine3.24`). `disable` means one exact SDK everywhere.
- **Owner decision (2026-11-23)**: stay on 10.0.401 and on PostgreSQL 18.7. Moving to the newer
  SDK patch (10.0.404) or to PostgreSQL 19 would each be a deliberate change of its own.

## R9. Seeded image versions (FR-012)

| Key | Value | Notes |
|---|---|---|
| `DOTNET_SDK_IMAGE` | `mcr.microsoft.com/dotnet/sdk:10.0.401-alpine3.24` | Matches `global.json` |
| `DOTNET_ASPNET_IMAGE` | `mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24` | Newest 10.0 runtime patch, so services get security fixes |
| `POSTGRES_IMAGE` | `postgres:18.7-alpine3.24` | Newest 18.x. PostgreSQL 19.0 is out, but stays a deliberate later upgrade until Npgsql and EF Core support is confirmed |
| `RABBITMQ_IMAGE` | `rabbitmq:4.4.1-management-alpine` | Newest 4.4 release, with the management UI for dev |

## R10. CI workflow (FR-010)

- **Decision**: `.github/workflows/building-blocks.yml` with these steps:
  - Trigger: `pull_request` and `push` to `master`, with `paths` set to `src/building-blocks/**`,
    `tools/**`, `scripts/**`, `deploy/versions.env`, `global.json`, `nuget.config`,
    `Directory.Build.*` and the workflow file itself.
  - Runner: `ubuntu-latest`.
  - `actions/checkout@v7.0.1` with `fetch-depth: 0`.
  - `actions/setup-dotnet@v6.1.0` with `global-json-file: global.json`.
  - Run, in order: the RepoChecks tests, `images`, `pack.ps1`, then `check-consumer.ps1`.
- **Note**: Actions are pinned to exact release tags, in the same spirit as the image pins.

## R11. Test framework for the tool

- **Decision (revised during implementation)**: `tools/RepoChecks.Tests` uses xUnit v3 4.0.3 on
  Microsoft.Testing.Platform. `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`,
  and tests run with `dotnet test --solution <slnx>`.
- **Why the change**: xUnit v3 4.x no longer supports VSTest under the .NET 10 SDK. The build
  failed with "Testing with VSTest target is no longer supported". The packages
  Microsoft.NET.Test.Sdk and xunit.runner.visualstudio are therefore not used. Seeded fixture files cover every forbidden image form (SC-007). Fixtures are written to a per-test temporary folder and deleted in
  `Dispose`, so the tests touch no Docker and no dev state.
