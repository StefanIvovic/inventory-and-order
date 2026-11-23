# Data Model: Package Pipeline

This feature has no database. Its "entities" are configuration artifacts.

## Shared package

| Field | Source | Rules |
|---|---|---|
| Id | the project's `PackageId` (defaults to the project name) | Must match `BuildingBlocks.*` or `*.Contracts` to resolve from the local feed |
| IsPackable | the project file, or the folder's `Directory.Build.props` | `false` by default at the root |
| TagPrefix | `MinVerTagPrefix` | Required when packable. Missing → build error |
| Version | computed by MinVer from git | Never written in files |

**Version states**:

| Situation | Version |
|---|---|
| A tag `<prefix>X.Y.Z` on HEAD | `X.Y.Z` (release) |
| Commits after the latest `<prefix>X.Y.Z` | `X.Y.(Z+1)-alpha.0.<height>` |
| No tag for the prefix | `0.0.0-alpha.0.<height>` |

## Tag prefix families

| Family | Prefix | Projects |
|---|---|---|
| BuildingBlocks | `building-blocks/v` | every project under `src/building-blocks/` |
| Contracts | `<service>-contracts/v` | `src/services/<service>/<Service>.Contracts` (from later features) |

## Image version entry (`deploy/versions.env`)

| Field | Rules |
|---|---|
| Key | `^[A-Z][A-Z0-9_]*_IMAGE$`, unique |
| Value | `[registry/]name:tag`, where the tag is the full release version plus variant. Not `latest`, not floating |

Format details are in [contracts/versions-env.md](contracts/versions-env.md).

## SDK pin (`global.json`)

| Field | Value |
|---|---|
| `sdk.version` | `10.0.401` |
| `sdk.rollForward` | `disable` |

Invariant: the tag of `DOTNET_SDK_IMAGE` starts with `sdk.version`.
