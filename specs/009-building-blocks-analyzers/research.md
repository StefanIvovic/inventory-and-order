# Research: BuildingBlocks Analyzers

## R1. Roslyn version and target

- **Decision**: `BuildingBlocks.Analyzers` targets `netstandard2.0` and references Microsoft.CodeAnalysis.CSharp **4.14.0**, with `PrivateAssets=all` and `EnforceExtendedAnalyzerRules=true`.
- **Rationale**: Analyzers are loaded by the host compiler and must not reference a newer Roslyn than that compiler. SDK 10.0.401 ships compiler 5.9.0 (`csc -version`). 4.14.0 leaves headroom for IDEs with older compilers (FR-005). Roslyn requires `netstandard2.0`.

## R2. Distribution: separate analyzer package, flowing transitively

- **Original decision (superseded, see below)**: `BuildingBlocks.Analyzers` is its own package. The analyzer DLL goes in `analyzers/dotnet/cs`, with `IncludeBuildOutput=false` and no lib folder.
  - AspNetCore, EntityFrameworkCore, Messaging and Validation reference it with `OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"` and `PrivateAssets="none"`, so their packed dependency carries **no** `exclude="Analyzers"`, and the analyzers flow to services transitively.
  - BuildingBlocks.Core does not reference it, so its zero dependencies are kept.
- **Rejected**: embedding the DLL into each package. A service referencing several packages would load the same analyzer several times, with a risk of duplicate diagnostics.
- **Verified during implementation: the original design does not work.** `dotnet pack` **drops** project references with `ReferenceOutputAssembly=false`, so no package depended on `BuildingBlocks.Analyzers` (nuspec inspection).
- **Implemented fallback**:
  - the analyzer DLL is packed into **BuildingBlocks.Core** under `analyzers/dotnet/cs`. Core keeps 0 dependencies, because analyzers are not dependencies.
  - **`PrivateAssets="none"` is not needed and was removed.** A mutation check showed that Core's analyzers reach a consumer of `BuildingBlocks.Messaging` even when every packed dependency carries `exclude="Build,Analyzers"`, because NuGet's exclude on a dependency does not stop analyzers that arrive indirectly. Rather than keep settings with no effect, the references stay plain, and `check-analyzers.ps1` in CI guards the behaviour
  - there is exactly one analyzer copy, so there are no duplicate diagnostics when a service references several packages
  - `BuildingBlocks.Analyzers` itself is not packable. The four packages keep an `OutputItemType="Analyzer"` reference, so the repository's own code is analyzed at build time (FR-007)
- **Proof**: `scripts/check-analyzers.ps1`. A consumer referencing only `BuildingBlocks.Messaging` fails with IAO1001 and IAO1002, and clean code builds.

## R3. IAO1001 design

- `CompilationStartAction`: skip when `AssemblyName` starts with `BuildingBlocks.`. Resolve `BuildingBlocks.Core.Permissions.IPermissionCache` and skip if it is absent.
- `SyntaxNodeAction` on `IdentifierName` and `GenericName`: report when `SymbolInfo.Symbol` (or a candidate) is that type. This covers parameters, fields, locals, generic arguments, `typeof` and casts.
- Generated code is excluded.

## R4. IAO1002 design

- `OperationAction(OperationKind.Invocation)`: the target method is named `SaveChanges` or `SaveChangesAsync`, and its `ContainingType` derives from `Microsoft.EntityFrameworkCore.DbContext`.
- **Scope**: `OperationAnalysisContext.ContainingSymbol` is already the enclosing member, even for operations inside lambdas and local functions. A mutation check showed that an explicit walk outwards was dead code, so it was removed. The lambda and local-function tests stay as regression tests. The member must be an ordinary method. It must be public, named Handle, Handles, Consume or Consumes, on a type that:
  - derives from `BuildingBlocks.Core.Handlers.CommandHandler<>` or `MessageHandler<>`
  - or has a name ending in `Handler` or `Consumer`
  - or has `Wolverine.Attributes.WolverineHandlerAttribute` or implements `Wolverine.IWolverineHandler`
- These are the docs' discovery conventions.
- **Generated code (G1)**: Wolverine's generated handlers call `SaveChangesAsync` inside `HandleAsync`. That name is not in the list, and generated code is excluded through `ConfigureGeneratedCodeAnalysis(None)`, so Static code generation (feature 011) produces no IAO1002. A test pins this.

## R5. Tests

- **Decision**: an in-repo harness instead of `Microsoft.CodeAnalysis.Testing`. `CSharpCompilation` over the running runtime's trusted platform assemblies plus EF Core, BuildingBlocks.Core and the Wolverine assembly, then `CompilationWithAnalyzers`, then assertions on ids and lines.
- **Rationale**: Offline and deterministic. The official testing package resolves reference assemblies from NuGet at test time, and its .NET 10 reference-assembly sets lag behind. This is a deviation from the earlier plan, recorded here.
- **End-to-end**: `scripts/check-analyzers.ps1` creates a temporary consumer outside the repository, referencing **only** `BuildingBlocks.Messaging` from the feed. A violating build must fail with both ids, and a clean build must succeed. It always cleans up. It runs in CI after pack.
