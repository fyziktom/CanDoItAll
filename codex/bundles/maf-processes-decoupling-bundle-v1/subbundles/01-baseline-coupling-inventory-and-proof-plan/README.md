# SB01 - Baseline coupling inventory and proof plan

## Status

- Status: Completed

## Objective

Create the durable baseline that later subbundles must preserve: exact process tool list, source coupling map, affected tests, and failing-first/progression proof plan. This subbundle should not change production behavior.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- Repository builds before refactor or the current build failure is documented as an unrelated baseline blocker.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`

## Deliverables

- Commit/update source-grounded inventory docs if needed.
- Add or update static test plan notes for dependency guardrails.
- Record current failing-first expectation: MAF currently references Processes and the future guard test should fail before SB05.
- Create `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md`.

## Dependency Impact

- All later subbundles depend on this inventory. If the tool list is wrong, SB04/SB06 parity proof is untrustworthy.


## Validation Depth

- Critical foundation. Requires semantic adequacy proof, artifact-backed manifest, source assertions, anti-stub audit, and downstream smoke where named in the progression gate.


## Implementation Steps

1. Run `git grep` / `rg` for `CanDoItAll.Modules.Processes` under `src/CanDoItAll.AgentFramework.Maf` and capture transcript.
2. Run `rg` for `CreateProcessToolBuilder`, `ProcessToolBuilder`, and `AttachInternalProcessToolsAsync` and capture transcript.
3. Extract exact process tool names from current `MafAgentRuntime.ProcessTools.cs` and compare to `inventories/01-process-tool-parity-inventory.md`.
4. Record dispatcher partial inventory and explicitly mark it out of scope.
5. Identify tests that must be updated or added.
6. Do not edit production source except optional docs/inventory files.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [x] Exact current coupling points are documented.
- [x] Exact current process tool names are documented.
- [x] Process dispatcher is scoped out of this bundle.
- [x] Failing-first guard plan is written.
- [x] Proof manifest and semantic invariants exist.

## Closure Notes

- Entry gate: Passed. The prepared-stage bundle validator passed and SB01 has no subbundle prerequisites.
- Baseline MAF project build: Passed in `bundle://proof/SB01/transcripts/maf-project-build-baseline.txt`.
- Full normal Web build blocker: Unrelated running `CanDoItAll.Web` process 48648 locks copied output DLLs; captured in `bundle://proof/SB01/transcripts/baseline-web-lock-process.txt` and `bundle://proof/SB01/transcripts/web-lock-build-baseline.txt`.
- Progression gate: Passed. SB02 can start from the recorded coupling and exact 23-tool process surface without rediscovery.

## Proof Required

- `proof/SB01/transcripts/source-coupling-grep.txt`
- `proof/SB01/transcripts/process-tool-name-extract.txt`
- `proof/SB01/manifest.md` with source file hashes
- `proof/SB01/semantic-invariants.md` covering shallow-pass trap and negative proof plan

## Browser Validation Logging

- No browser validation required unless runtime UI smoke reveals a rendered-regression risk. Record `N/A` in execution report if no browser route is exercised.


## Progression Gate

- Pass only when SB02 can start without rediscovering the MAF/Processes coupling or process tool list.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB01. Do not start the next subbundle until the SB01 closure gate passes and proof artifacts are written.
