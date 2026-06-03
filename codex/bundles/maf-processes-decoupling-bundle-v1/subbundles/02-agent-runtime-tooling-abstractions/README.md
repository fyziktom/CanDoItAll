# SB02 - Agent runtime tooling abstractions

## Status

Not started.

## Objective

Introduce the minimal provider-neutral runtime tool-provider abstraction that allows modules to contribute runtime tools without MAF depending on those modules.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- SB01 closure gate passed.
- No production tool migration has started.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://src/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`

## Deliverables

- New `CanDoItAll.AgentFramework.Tooling` project or a justified equivalent seam.
- `IAgentRuntimeToolProvider` and context/purpose contracts.
- Solution/project references updated.
- Unit tests proving the Tooling project has no `CanDoItAll.Modules.*` references.

## Dependency Impact

- SB03 cannot implement provider composition until this abstraction exists. A bad dependency in Tooling invalidates the whole decoupling.


## Validation Depth

- Critical foundation. Requires semantic adequacy proof, artifact-backed manifest, source assertions, anti-stub audit, and downstream smoke where named in the progression gate.


## Implementation Steps

1. Create `src/CanDoItAll.AgentFramework.Tooling` with minimal references.
2. Add contracts: `IAgentRuntimeToolProvider`, `AgentRuntimeToolProviderContext`, `AgentRuntimeToolProviderPurpose`.
3. Keep contracts free of process-specific DTOs.
4. Add project to `CanDoItAll.slnx`.
5. Add MAF and Processes project references to Tooling, but do not remove old MAF -> Processes reference yet.
6. Add architecture test: Tooling must not reference any `CanDoItAll.Modules.*` project.
7. Build solution.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [ ] Tooling project builds.
- [ ] Tooling project has no product-module references.
- [ ] MAF can reference Tooling.
- [ ] Processes can reference Tooling.
- [ ] No process tool moved yet.
- [ ] No dispatcher file moved.

## Proof Required

- `dotnet build src\CanDoItAll.AgentFramework.Tooling\CanDoItAll.AgentFramework.Tooling.csproj` transcript
- `dotnet build CanDoItAll.slnx` transcript
- Source assertion for Tooling project references
- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`

## Browser Validation Logging

- No browser validation required unless runtime UI smoke reveals a rendered-regression risk. Record `N/A` in execution report if no browser route is exercised.


## Progression Gate

- Pass only when the abstraction is small, clean, compiled, and dependency-safe.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB02. Do not start the next subbundle until the SB02 closure gate passes and proof artifacts are written.
