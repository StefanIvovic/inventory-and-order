# Data Model: BuildingBlocks Analyzers

| Rule | Severity | Title | Message |
|---|---|---|---|
| IAO1001 | Error | Do not use IPermissionCache outside BuildingBlocks | `IPermissionCache` must not be used here: return `PermissionAssignmentsChanged` from the handler that changed the assignments, so every instance invalidates after commit |
| IAO1002 | Error | Do not save inside a Wolverine handler | `{0}` must not be called in a Wolverine handler: the transaction middleware saves and commits after the handler returns |

Category: `BuildingBlocks.Correctness`. Help link: `specs/009-building-blocks-analyzers/quickstart.md`.

## Handler method (IAO1002 scope)

| Condition | Rule |
|---|---|
| Method | Ordinary, public, named `Handle`, `Handles`, `Consume` or `Consumes` |
| Type | Derives from `CommandHandler<>` or `MessageHandler<>`, OR the name ends in `Handler` or `Consumer`, OR `[WolverineHandler]`, OR implements `IWolverineHandler` |
| Call site | The method body, or any lambda or local function nested in it |
| Receiver | The invoked method's containing type derives from `Microsoft.EntityFrameworkCore.DbContext` |
