# C# Boundary Map

## Ownership Boundaries

| Boundary | Owns | Must not own |
| --- | --- | --- |
| Common MAF runtime | Generic runtime composition, workspace tools, capability access enforcement | Software-delivery, UI-review, Blazor, or process-domain prompt assumptions. |
| Capability abstractions/access | Generic descriptors, selectors, rules, diagnostics, evaluator | Process-template schema or MAF wrapper implementation details. |
| Process core/templates/runtime | Process-neutral step scope intent, assignments, persistence | Direct references to `CanDoItAll.AgentFramework.Maf`. |
| `CanDoItAll.Modules.Processes` AgentFramework integration | Translation from process scope to MAF execution metadata and scoped prompts | Generic process contracts that only work with MAF. |
| Development tool package/module | UI screenshot and development-process image analysis behavior | Common MAF workspace behavior. |

## Required Boundary Changes

1. Keep process scope contracts runtime-neutral.
2. Add MAF-scoped policy DTOs only in AgentFramework projects.
3. Translate process scope inside `AgentFrameworkProcessExecutionAdapter`, not in process core.
4. Keep common MAF image analysis generic.
5. Register development-specific image analysis from a composition root or module, not from common MAF.

## Forbidden Dependencies

- `CanDoItAll.Processes.Core` must not reference `CanDoItAll.AgentFramework.Maf`.
- `CanDoItAll.Processes.Templates` must not reference `CanDoItAll.AgentFramework.Maf`.
- `CanDoItAll.Processes.Runtime` must not reference `CanDoItAll.AgentFramework.Maf`.
- Common MAF must not reference a development-specific tools project.

## Acceptable Dependencies

- `CanDoItAll.Modules.Processes` may reference process projects and AgentFramework projects because it is an integration layer.
- `CanDoItAll.AgentFramework.Models` may reference `CanDoItAll.AgentFramework.Capabilities.Abstractions` only if needed to carry typed runtime capability override DTOs; this is a low-level AgentFramework dependency, not a process dependency.
- A development tool package may reference AgentFramework tooling abstractions and be registered by the application composition root.
