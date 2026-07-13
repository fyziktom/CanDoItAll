# C# Dependency Direction

## Current Scoped Direction

The CodeAnalytics snapshot reported no cycles in the scoped process graph. Current direction is broadly:

- Modules depend on process application/runtime/template services.
- Application depends on builder/projections/runtime/templates.
- Runtime depends on abstractions/builder/contracts/core.
- Templates depend on abstractions/contracts/core.
- Contracts has no project references.

## Required Direction

- `Processes.Contracts` remains leaf-like and must not reference runtime, application, modules, or templates.
- `Processes.Runtime` must not reference `CanDoItAll.Modules.Processes` or Workbench.
- `Processes.Templates` may define and validate template contracts, but runtime behavior must consume normalized typed metadata rather than markdown prose.
- `Modules.Processes` may adapt AgentFramework results into process runtime concepts.
- `Modules.Workbench` may contribute launch variables, but central resolver behavior must not live only there.

## Forbidden Couplings

- No UI/projection dependency in recovery classifier or completion gate evaluation.
- No direct file-system artifact acceptance in parent bridge without ledger/slot evidence.
- No template markdown parsing in runtime recovery logic to infer hard requirements.
- No stringly typed recovery decisions when enums/records already exist or should be introduced.

## Architecture Check

Each architecture-heavy subbundle must run the C# architecture gate in `reviews/csharp-architecture-gate.md` before dependent phases continue.

## SB10 Check

- CodeAnalytics snapshot `snap-20260708203629-184e6305` reported `cycles: []` for `CanDoItAll.Modules.Processes`, `CanDoItAll.Processes.Application`, `CanDoItAll.Processes.Contracts`, `CanDoItAll.Processes.Runtime`, and `CanDoItAll.Processes.Templates`.
- SB10 added no new project references; it consumes existing typed template execution-contract metadata and assigned capability state.

## SB11 Check

- CodeAnalytics snapshot `snap-20260708212205-c7d874cd` reported `cycles: []` for `CanDoItAll.Modules.Processes` and `CanDoItAll.Processes.Application`.
- SB11 added no new project references; the runtime-owned executor stays in `Modules.Processes` and consumes existing process assignment, launch-variable, workspace command, and completion-gate contracts.

## SB12 Check

- CodeAnalytics snapshot `snap-20260708214607-6650a5f9` reported `cycles: []` for `CanDoItAll.Modules.Processes`, `CanDoItAll.Processes.Application`, `CanDoItAll.Processes.Contracts`, `CanDoItAll.Processes.Persistence`, `CanDoItAll.Processes.Runtime`, and `CanDoItAll.Processes.Templates`.
- Observed dependency direction remains one-way: module/application/persistence/runtime/templates depend toward contracts and runtime/template contracts; runtime does not depend on `CanDoItAll.Modules.Processes`.
- SB12 made proof/status updates only after implementation validation; no additional design pattern or project-boundary change was introduced in the final validation phase.
