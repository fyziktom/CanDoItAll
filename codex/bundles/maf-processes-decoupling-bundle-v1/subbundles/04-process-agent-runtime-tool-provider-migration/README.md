# SB04 - Process tool migration into Processes module

## Status

- Status: Completed

## Objective

Move the current process tool builder from MAF into the Processes module as a registered runtime tool provider while preserving exact tool names, DTOs, access checks, template behavior, and approval semantics.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- SB03 closure gate passed.
- Provider composition has compatibility with old path.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`

## Deliverables

- `ProcessAgentRuntimeToolProvider` in Processes module.
- All current process tool DTOs/exceptions moved or copied into Processes-owned files.
- Processes module registers provider via DI.
- Tool parity test proving all 23 exact names exist.
- Access check tests for read/write/definition scope behavior.

## Dependency Impact

- This is the core behavior migration. SB05 cannot remove MAF's Processes reference until SB04 proves parity.


## Validation Depth

- Critical foundation. Requires semantic adequacy proof, artifact-backed manifest, source assertions, anti-stub audit, and downstream smoke where named in the progression gate.


## Implementation Steps

1. Create `src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`.
2. Move process tool construction and helper logic out of MAF with minimal semantic edits.
3. Preserve exact tool names and descriptions unless a test proves description drift is harmless.
4. Preserve `AgentProcessAccessMetadata.Read(agent.ConfigurationJson)` usage.
5. Register provider in `ProcessesModuleServiceCollectionExtensions` using `TryAddEnumerable`.
6. Ensure provider lifetimes are safe for scoped process services.
7. Add exact-name parity tests.
8. Add access-check tests using fake/minimal services or integration harness.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [x] All 23 process tools exposed by provider.
- [x] Tool names exactly match inventory.
- [x] Read/write/definition-scope checks preserved.
- [x] Provider is registered by Processes module.
- [x] MAF old process path can still exist but should not be needed after provider is registered.
- [x] No process dispatcher files moved.

## Closure Notes

- Entry gate: Passed. SB03 completed and provider composition was available.
- Validation: Targeted provider parity test, provider access-denial test, and full solution build passed.
- Proof: `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/semantic-invariants.md`.
- Progression gate: Passed. SB05 may remove the temporary MAF -> Processes project reference and legacy process tool partial.

## Proof Required

- `dotnet test ... --filter ProcessAgentRuntimeToolProviderParity` transcript
- `dotnet test ... --filter ProcessAgentRuntimeToolProviderAccess` transcript
- `dotnet build CanDoItAll.slnx` transcript
- Tool parity source assertion against inventory
- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`

## Browser Validation Logging

- No browser validation required unless runtime UI smoke reveals a rendered-regression risk. Record `N/A` in execution report if no browser route is exercised.


## Progression Gate

- Pass only when process tools work through the provider path and parity is proven by exact names, not only count.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB04. Do not start the next subbundle until the SB04 closure gate passes and proof artifacts are written.
