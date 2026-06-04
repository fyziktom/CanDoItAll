# SB04 — Project-structure tool provider extraction from MAF

## Status

Not started.

## Objective

Move project-structure internal tool attachment out of MAF into a registered provider owned by the module that owns project-structure behavior, while preserving existing tool names and policy behavior.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB03` must be complete and its progression gate must have passed.

## Exact Source References

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `src/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`

## Deliverables

- Inventory all current project-structure tool names and source builders before moving code.
- New ProjectStructureAgentRuntimeToolProvider registered by the owning module.
- Remove AttachInternalProjectStructureToolsAsync from MafAgentRuntime when parity is proven.
- Remove MAF project references to Projects/Workbench only when no other MAF source needs them; otherwise record the remaining dependency reason.

## Dependency Impact

Critical foundation for downstream work.

## Validation Depth

This subbundle requires source assertions, targeted tests, and proof transcripts. Compile-only proof is not sufficient when tool-provider behavior changes.

## Implementation Steps

1. Open every exact source reference and confirm current branch shape.
2. Create or update the smallest set of source files needed for this subbundle.
3. Preserve existing public tool names and policy behavior unless this subbundle explicitly owns the change.
4. Run targeted proof before broader build proof.
5. Record source assertions, test transcripts, and any reopen triggers.
6. Update the execution report and stop at the progression gate.

## Scope Exceptions

- No process-core extraction.
- No process driver packs.
- No unrelated UI work.

## Do Not Do

- Do not silently rename or drop existing tools.
- Do not weaken approval or access policy.
- Do not use broad cleanups that touch unrelated modules without explicit inventory.
- Do not mark placeholder proof as passed.

## Acceptance Checklist

- [ ] Source inventory for this slice is recorded.
- [ ] Implementation is limited to this subbundle scope.
- [ ] Tool parity/access/approval behavior is proven where applicable.
- [ ] Static dependency scans are updated where applicable.
- [ ] Targeted tests pass.
- [ ] Full or relevant project build pass is recorded.
- [ ] Execution report is updated.

## Proof Required

- `rg project-structure tool builder sources before and after migration`
- `dotnet test tests/CanDoItAll.Tests.Unit --filter ProjectStructure`
- `dotnet test tests/CanDoItAll.Tests.Integration --filter RuntimeToolProviderComposition`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

All project-structure tool names, access checks, and approval classifications must match the pre-migration inventory; MAF must not contain project-structure-specific attach code.

## Suggested Agent Prompt

Implement SB04 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
