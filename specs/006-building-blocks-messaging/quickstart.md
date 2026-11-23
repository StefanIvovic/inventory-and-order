# Quickstart: Messaging Validation

Prerequisites: SDK 10.0.401, PowerShell 7, and Docker running.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` | All tests pass, including transactions, inbox, routing, propagation and HTTP commands |
| 2 | `pwsh scripts/check-test-cleanup.ps1` | Exit 0 |
| 3 | `pwsh scripts/pack.ps1`, then `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.Messaging` | Five packages at one version, and the consumer builds |
| 4 | `dotnet run --project tools/RepoChecks -- images` | Exit 0 |

## Rules for services

- Never call `SaveChangesAsync` in a handler. The transaction middleware does it.
- Never enable `EnableRetryOnFailure` on a DbContext that Wolverine handlers use, because Eager transactions forbid it.
- Every integration event gets `[MessageIdentity("<service>.<event>.v<N>")]`. A breaking change gets a new `vN+1` type.
- Event handlers derive from `MessageHandler<TDbContext>`, and command handlers from `CommandHandler<TDbContext>`.
