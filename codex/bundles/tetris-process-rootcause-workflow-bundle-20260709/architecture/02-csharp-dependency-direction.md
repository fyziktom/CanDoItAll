# C# Dependency Direction

## Current Project References From CodeAnalytics

- `CanDoItAll.Modules.Processes` depends on process application, builder, drivers, persistence, projections, runtime, and templates.
- `CanDoItAll.Modules.Workbench` depends on MAF workflow core/runtime and process abstractions/application/persistence.
- `CanDoItAll.Processes.Application` depends on process builder, drivers abstractions, projections, runtime, and templates.
- `CanDoItAll.Processes.Runtime` depends on process abstractions, builder, contracts, core, and drivers abstractions.
- `CanDoItAll.Processes.Contracts` has no project references.
- Scoped dependency graph reported no cycles.

## Target Dependency Direction

Allowed:

- Modules/Workbench -> Processes.Application/Runtime/Contracts.
- Modules.Processes -> extracted evaluator implementations when they remain module-integration specific.
- Processes.Application -> Processes.Contracts/Abstractions/Core/Runtime as currently allowed.
- Domain provider implementations -> generic provider interfaces/contracts.
- Templates/Workbench metadata -> generic parser/route contracts.

Forbidden:

- Contracts -> Application/Runtime/Modules.
- Generic Application/Runtime -> Modules.Workbench.
- Generic Application/Runtime -> software-delivery template constants as code behavior.
- Generic Application/Runtime -> `.NET`, Blazor, or scaffold-specific tool/content constants.
- Core/Contracts -> MAF runtime implementation types.

## New Contract Projects Needed

No new project is planned in the bundle by default. Add a new project only if SB01/SB02 proves the generic rule/route contracts cannot live cleanly in existing `Contracts`/`Abstractions` without creating cycles or dragging implementation dependencies inward.

## Cycle Risk

- Moving Workbench provider interfaces into Modules.Workbench would create wrong direction if Application must call them.
- Fix: put minimal provider interface/context in `Processes.Application` or `Processes.Abstractions`; implementation lives in Workbench and is registered at composition.
- Moving route metadata parsing into Templates project may be acceptable if Application already depends on Templates; do not make Runtime depend on Workbench.

## Build/Test Proof Required

- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter <targeted filters>`
- `dotnet build CanDoItAll.slnx`
- CodeAnalytics refreshed scoped snapshot after architecture changes.
- Dependency/cycle proof from CodeAnalytics after SB05 and SB11.

## Corrective Dependency Decision

- SB12 is a local extraction and adds no project references.
- `CanDoItAll.Modules.Processes` remains the composition boundary referencing MAF and process abstractions/application/runtime.
- Extracted services must not be moved into `CanDoItAll.Processes.Runtime` because they consume MAF models and workspace services; doing so would leak integration dependencies into generic runtime.
- SB13 must prefer an internal contribution contract and module composition. If a dedicated driver project becomes necessary, repair the bundle before changing references and prove both pre/post dependency graphs.
- Forbidden direction remains `Processes.Runtime/Application -> Modules.Processes/Workbench/.NET implementation`.
- Fresh pre-change snapshot `snap-20260709195146-c1b7a73e` reports no cycle in the corrective scope.
