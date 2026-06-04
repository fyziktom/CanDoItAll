# SB09 — Runtime tool-provider observability and receipt tagging

## Status

- Status: `Completed`

## Objective

Make provider ownership visible in progress logs, tool receipts, diagnostics, and proof artifacts so later driver packs can be traced to their provider and purpose.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

- `SB08` must be complete and its progression gate must have passed.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs
- repo://src/CanDoItAll.AgentFramework.Core/README.md
- repo://src/CanDoItAll.AgentFramework.Maf/README.md

## Deliverables

- Provider key/name included in attach progress messages and testable diagnostics.
- Tool ownership metadata carried into receipt trace where available, without breaking existing receipt schema.
- Backward-compatible receipt projection for existing runs.
- Documentation for provider observability.

## Dependency Impact

- Moderate dependency impact; downstream proof must still include regression checks.

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

- `tool receipt semantics tests`
- `runtime tool provider composition tests`
- `process receipt semantics smoke`
- `dotnet test tests/CanDoItAll.Tests.Integration --filter Receipt`

## Browser Validation Logging

- N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

- Receipt schema changes must be backward compatible; existing process artifact lineage and receipt semantics tests must pass.

## Completion Notes

- Provider attach diagnostics now include provider key/display name/tool count.
- MAF invocation traces and workspace audit receipts carry optional runtime provider key/name when the invoked tool came from provider metadata.
- `ToolExecutionReceiptRecord` preserves the existing positional constructor and adds optional init-only ownership fields, so older JSON deserializes with empty provider ownership.
- Provider-native browser receipt projection copies provider ownership from the source launch receipt when present.
- Process receipt semantics were tightened so explicit project-structure node/asset writeback contracts require actual `project_structure_*` receipts.
- Proof is recorded in `bundle://proof/SB09/manifest.md` and `bundle://proof/SB09/semantic-invariants.md`.

## Suggested Agent Prompt

Implement SB09 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
