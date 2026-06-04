# SB03 - MAF registered tool-provider composition

## Status

- Status: Completed

## Objective

Teach `MafAgentRuntime` to attach tools from registered `IAgentRuntimeToolProvider` instances while temporarily preserving the old hard-coded process tool path. This creates a safe compatibility bridge before moving process tools.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- SB02 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ImageGenerationTools.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs`

## Deliverables

- MAF composition method that resolves and invokes ordered runtime tool providers.
- Tests proving zero-provider runtime composition is safe.
- Tests proving a fake provider contributes a tool.
- Tests proving duplicate tool names are handled deterministically or rejected with clear diagnostics.
- Old process tool path remains available until SB04/SB05.

## Dependency Impact

- SB04 depends on this compatibility phase. If provider composition is wrong, process tool migration will look like a process bug.


## Validation Depth

- Critical foundation. Requires semantic adequacy proof, artifact-backed manifest, source assertions, anti-stub audit, and downstream smoke where named in the progression gate.


## Implementation Steps

1. Add `AttachRegisteredRuntimeToolProvidersAsync` or equivalent to `MafAgentRuntime.Capabilities.cs`.
2. Pass provider context with agent, provider, capabilities, suppressApprovalRequirements, purpose, session key/tags as available.
3. Resolve providers from DI without requiring Processes services.
4. Call providers in deterministic order.
5. Apply the same approval wrapping policy to provider tools as currently applied to internal process/image tools.
6. Add progress callback line that reports provider count and tool count.
7. Keep `AttachInternalProcessToolsAsync` temporarily.
8. Add unit tests with fake provider.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [x] MAF builds with new Tooling reference.
- [x] Zero registered providers does not fail.
- [x] Fake provider tool appears in runtime build/composition.
- [x] Duplicate tool behavior is explicit and tested.
- [x] Old process tools are still available until migration.
- [x] No MAF -> Processes reference removed yet.

## Closure Notes

- Entry gate: Passed. SB02 completed and Tooling contracts were available.
- Validation: Focused provider-composition tests and solution build passed.
- Proof: `bundle://proof/SB03/manifest.md` and `bundle://proof/SB03/semantic-invariants.md`.
- Progression gate: Passed. SB04 may migrate process tool construction into a registered Processes-module provider.

## Proof Required

- `dotnet test ... --filter MafAgentRuntimeToolProviderComposition` transcript
- `dotnet build CanDoItAll.slnx` transcript
- Source assertion that provider composition path exists
- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`

## Browser Validation Logging

- No browser validation required unless runtime UI smoke reveals a rendered-regression risk. Record `N/A` in execution report if no browser route is exercised.


## Progression Gate

- Pass only when MAF supports provider-based tools and has not yet broken existing process tool attachment.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB03. Do not start the next subbundle until the SB03 closure gate passes and proof artifacts are written.
