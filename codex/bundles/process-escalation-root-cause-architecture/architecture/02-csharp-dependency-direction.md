# C# Dependency Direction

## Current Project References

- `CanDoItAll.Processes.Application` references runtime, projections, templates, builder, contracts, and driver abstractions.
- `CanDoItAll.Processes.Runtime` references abstractions, core, builder, contracts, and driver abstractions.
- `CanDoItAll.Processes.Drivers.Standard` references driver abstractions and process abstractions.
- `CanDoItAll.Modules.Processes` references MAF core/models and many process projects; this is the correct adapter/composition-heavy boundary.
- `CanDoItAll.AgentFramework.Maf` references agent framework models/core and owns runtime tool/capability composition.
- CodeAnalytics snapshot `snap-20260707190811-633e2e0e` found no project cycles in the inspected scope.

## Target Project References

- `Processes.Contracts` remains dependency-light and serializable.
- `Processes.Runtime` may depend on `Processes.Contracts` but must not depend on MAF, module projects, templates, or domain driver implementations.
- `Processes.Application` may depend on driver abstractions and runtime stores, but should depend on domain drivers only through abstractions/catalog registration.
- `Processes.Drivers.Standard` stays generic and must not depend on .NET/software-delivery templates.
- `Modules.Processes` remains the composition root that connects process application services to MAF execution.
- Any .NET delivery driver project must depend inward on driver abstractions/contracts, not outward into application internals.

## Forbidden References

- No `Processes.Runtime` reference to `CanDoItAll.AgentFramework.*`.
- No `Processes.Runtime` reference to `Modules.Processes`.
- No `Processes.Runtime` or `Processes.Application` reference to Blazor, .NET delivery templates, Calculator, Tetris, or screenshot-specific packages.
- No generic MAF workspace runtime code should reference process definitions or software-delivery templates.
- No domain driver should reference UI components or Workbench pages.

## Cycle Risk

- Capability readiness touches processes, agents, tools, MCPs, and skills. Avoid cycles by using contracts in `Processes.Contracts` and adapter implementations in `Modules.Processes` or MAF.
- Recovery classification touches runtime diagnostics and domain drivers. Keep generic failure categories in contracts/runtime and driver-specific policy behind `Processes.Drivers.Abstractions`.
- Projection enrichment can accidentally depend on runtime application services. Keep read model enrichers behind store interfaces, not dispatch services.

## New Contract Projects Needed

- No new contract project is required at bundle-preparation time.
- A new `.NET delivery driver` project is allowed only if SB04 proves that existing `Processes.Drivers.Standard` or module composition would otherwise mix domain and generic behavior.
- If added, it must be wired through dependency injection and tested with architecture reference checks.

## Build/Test Proof Required

- `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore --configuration Debug`
- Unit tests for process contracts/runtime/application slices touched by each subbundle.
- Integration tests for process launch/matching/readiness and projection readback.
- Architecture test proving forbidden references and forbidden domain strings do not enter generic layers.
