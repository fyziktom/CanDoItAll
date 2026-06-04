# SB08 — Process provider purpose/access hardening and manager read-only groundwork

## Status

- Status: `Completed`

## Objective

Start using provider context purpose and tags for process tools so future manager-verification and driver work can be added safely, without introducing process-core extraction yet.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

- `SB07` must be complete and its progression gate must have passed.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs
- repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderPurpose.cs
- repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs

## Deliverables

- Explicit purpose-handling policy in ProcessAgentRuntimeToolProvider.
- Read-only process-manager verification groundwork: read tools allowed, mutation tools denied unless existing access metadata explicitly grants write.
- Tests proving GovernedProcessAutomation, InteractiveChat, AutoApprovedNonInteractive, and A2AEndpoint purpose behavior.
- No new process drivers yet.

## Dependency Impact

- Critical foundation for downstream work.

## Validation Depth

- This subbundle requires source assertions, targeted tests, and proof transcripts. Compile-only proof is not sufficient when tool-provider behavior changes.

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

- [x] Source inventory for this slice is recorded.
- [x] Implementation is limited to this subbundle scope.
- [x] Tool parity/access/approval behavior is proven where applicable.
- [x] Static dependency scans are updated where applicable.
- [x] Targeted tests pass.
- [x] Full or relevant project build pass is recorded.
- [x] Execution report is updated.

## Proof Required

- `purpose matrix unit tests`
- `read/write denial tests`
- `zero-provider and provider failure tests`
- `dotnet test tests/CanDoItAll.Tests.Unit --filter ProcessAgentRuntimeToolProvider`

## Browser Validation Logging

- N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

- Purpose-aware behavior must be observable in tests, but existing process automation must not lose required mutation capability when explicitly permitted.

## Suggested Agent Prompt

Implement SB08 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.

## Completion Notes

- Added explicit purpose policy in `ProcessAgentRuntimeToolProvider`.
- Supported purposes now include `InteractiveChat`, `GovernedProcessAutomation`, `AutoApprovedNonInteractive`, and `A2AEndpoint`.
- Process read tools are exposed only with process read access; process mutation tools are exposed only with explicit process write access.
- Explicitly write-enabled automation still receives the exact 23-tool process inventory.
- Proof manifest: `bundle://proof/SB08/manifest.md`.
- Semantic invariants: `bundle://proof/SB08/semantic-invariants.md`.
