# SB05 - Remove MAF -> Processes project reference

## Status

Not started.

## Objective

Delete the old MAF process tool builder path and remove all compile-time MAF dependencies on `CanDoItAll.Modules.Processes`.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- SB04 closure gate passed with provider parity proof.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/README.md`
- `repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`

## Deliverables

- Remove `CanDoItAll.Modules.Processes` project reference from MAF csproj.
- Delete or empty obsolete MAF process tool file.
- Remove `ProcessToolBuilder` field from `RuntimeCapabilityComposition`.
- Remove `CreateProcessToolBuilder` and `AttachInternalProcessToolsAsync` from MAF.
- Add static architecture guard tests.

## Dependency Impact

- This is the architectural decoupling gate. SB06/SB07 cannot proceed if the dependency still exists.


## Validation Depth

- Critical foundation. Requires semantic adequacy proof, artifact-backed manifest, source assertions, anti-stub audit, and downstream smoke where named in the progression gate.


## Implementation Steps

1. Remove MAF project reference to Processes.
2. Remove `using CanDoItAll.Modules.Processes` from all MAF files.
3. Delete old MAF process tool builder file after SB04 provider tests pass.
4. Update `RuntimeCapabilityComposition` so it no longer has `ProcessToolBuilder`.
5. Ensure registered provider composition remains.
6. Add static tests for forbidden project reference and forbidden namespace.
7. Build MAF project alone.
8. Build solution.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [ ] MAF csproj has no Processes project reference.
- [ ] MAF source has no `CanDoItAll.Modules.Processes` usage.
- [ ] MAF source has no `ProcessToolBuilder` implementation.
- [ ] MAF project builds.
- [ ] Solution builds.
- [ ] Process provider still supplies process tools through app composition.

## Proof Required

- `rg "CanDoItAll.Modules.Processes" src\CanDoItAll.AgentFramework.Maf` transcript showing no matches
- `dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj` transcript
- `dotnet build CanDoItAll.slnx` transcript
- Static architecture test transcript
- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`

## Browser Validation Logging

- No browser validation required unless runtime UI smoke reveals a rendered-regression risk. Record `N/A` in execution report if no browser route is exercised.


## Progression Gate

- Pass only when direct MAF -> Processes compile-time dependency is gone and guarded.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB05. Do not start the next subbundle until the SB05 closure gate passes and proof artifacts are written.
