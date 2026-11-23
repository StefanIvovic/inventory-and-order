# Research: BuildingBlocks Permissions

Versions verified on 2026-11-23: Microsoft.Extensions.TimeProvider.Testing 10.11.1. That is the
10.x line, chosen over 11.0.x to stay aligned with .NET 10.

## R1. Where things live

- **Decision**:
  - The abstractions (`IPermissionManager`, `IPermissionProvider`, `IPermissionCache`,
    `PermissionDeniedException`, `IRequestContext`, `CommandHandler<TDbContext>`) go in
    `BuildingBlocks.Core`. They use only BCL types: `TimeProvider`, `IReadOnlySet`.
  - The implementations (`PermissionManager`, `PermissionCache`, `RequestContext`) and the
    registration go in `BuildingBlocks.AspNetCore`.
- **Rationale**: Core stays dependency-free. `CommandHandler<TDbContext>` constrains `TDbContext`
  only to `class`, so Core needs no EF reference. The implementations need `IMemoryCache` and
  options, which come with the ASP.NET shared framework.

## R2. Cache design

- **Decision**: `PermissionCache` (singleton) stores, in `IMemoryCache` under the key
  `perm:{tenantId ?? ""}:{userId:N}`, an entry `(IReadOnlySet<string> Granted, DateTimeOffset ExpiresAt)`.
  - `ExpiresAt` comes from `TimeProvider`. On read, an entry whose `ExpiresAt <= now` counts as a
    miss.
  - The `IMemoryCache` entry also gets an absolute expiration so memory is reclaimed.
  - `Invalidate` removes the key.
  - Only successful provider results are stored (FR-004).
- **Rationale**: Expiry decisions follow `TimeProvider`, so tests use `FakeTimeProvider` and
  never wait (FR-008). `IMemoryCache` still evicts real entries.
- **Accepted trade-off**: Two concurrent cache misses for the same key may both call the
  provider. That is harmless, and simpler than per-key locking.

## R3. Per-request resolution

- **Decision**: `PermissionManager` is scoped. It memoizes the granted-set task for the request,
  so all checks in one request share one lookup (US3 scenario 1).
- **Unauthenticated callers**: an empty set, without calling the cache or the provider (FR-003).

## R4. Denied mapping

- **Decision**: Extend `BuildingBlocksExceptionHandler`:
  - `PermissionDeniedException` → 403
  - `code: permission-denied`
  - `missingPermissions`: string array
  - `detail`: "You do not have the permissions required for this operation."
  - log level Warning, with the user id, the missing permissions and the trace id
- **Rationale**: A denied check is security-relevant, but it is not a server fault.

## R5. Registration

- **Decision**: `services.AddBuildingBlocksPermissions<TProvider>(Action<PermissionOptions>? configure = null)`.
  - It registers `TProvider` as scoped `IPermissionProvider` (so it can use the scoped
    DbContext), `PermissionCache` as singleton (`IPermissionCache`), `PermissionManager` and
    `RequestContext` as scoped, and `TimeProvider.System` through `TryAddSingleton`.
  - `PermissionOptions.CacheDuration` defaults to 60 seconds and must be positive.
  - It calls `AddMemoryCache()`.

## R6. Command handler base

- **Decision**: `public abstract class CommandHandler<TDbContext>(IRequestContext context, TDbContext db) where TDbContext : class`.
  It exposes protected `Context`, `Db`, `User`, `Permissions`, `Tenant` and `Clock`.
- **Deferred**: Wolverine's discovery of `Handle` methods on derived classes is verified in
  feature 005, where Wolverine is introduced (constitution note B20).
- **Tests**: Here a handler is resolved directly from DI and invoked from a test endpoint.
