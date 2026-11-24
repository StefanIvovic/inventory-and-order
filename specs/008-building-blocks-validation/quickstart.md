# Quickstart: Validation

Prerequisites: SDK 10.0.401. Docker is not needed.

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test --solution src/building-blocks/BuildingBlocks.slnx` | All tests pass, including the validation tests |
| 2 | `pwsh scripts/pack.ps1`, then `pwsh scripts/check-consumer.ps1 -PackageId BuildingBlocks.Validation` | It builds |
| 3 | `dotnet run --project tools/RepoChecks -- images` | Exit 0 |

## Rules for services

- **400 vs 422**: validators check *shape* (required fields, lengths, ranges, formats, cross-field equality). Anything that needs data or state ("enough stock", "order not shipped") is a domain rule that throws `DomainException`, giving 409 or 422.
- Validators are `public`, have no constructor dependencies, and live next to their command in `Features/<UseCase>/`.
- Never put attempted values in messages.
