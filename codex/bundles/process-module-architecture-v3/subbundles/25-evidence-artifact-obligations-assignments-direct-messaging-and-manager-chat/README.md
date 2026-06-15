# SB25 Evidence, Artifact Obligations, Assignments, Direct Messaging, And Manager Chat

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild evidence and coordination views: artifact obligation ledger, artifact recording, artifact matrix, work briefs, decision records, conformance observations, assignment resolution, direct role messaging, transcript links, and manager chat.

## Covered Inputs

- REQ-015 to REQ-019, REQ-024, REQ-025, REQ-030, REQ-051, REQ-052.
- US-040 through US-043 and US-053.
- AC-014 to AC-017, AC-021, AC-039, AC-040.

## Prerequisites

- SB24 operator control complete.
- SB08 artifact ledger and SB09 manager recovery contracts complete.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsArtifactsSection.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsAssignmentsSection.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsMessagingSection.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Integration/ProcessArtifactStatusProjectionServiceTests.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Integration/ProcessTranscriptVerificationReadOnlyAdapterTests.cs`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Evidence/artifact UI over artifact ledger projections.
- Artifact record command with trust/sensitivity/validation metadata.
- Assignment resolution UI for human, AI agent, and workflow executors.
- Direct role messaging and transcript projection UI.
- Manager chat run selector and message command flow.

## Dependency Impact

- SB26 analytics/live dashboard uses artifact, assignment, and message summary projections.
- SB28 recovery and manager communication regression depends on this proof.

## Validation Depth

- Artifact ledger/recovery tests.
- Assignment resolution and messaging authorization tests.
- Component tests for evidence, assignment, messaging, and manager chat states.
- Playwright proof for evidence and coordination flows.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Bind evidence views to artifact obligation, artifact matrix, decision, brief, and conformance projections.
2. Implement artifact recording with validation and command receipts.
3. Implement assignment resolution commands.
4. Implement direct role messaging and transcript display.
5. Implement manager chat run selection and messaging.
6. Add tests and story coverage.

## Do Not Do

- Do not allow artifact recording without provenance/trust/sensitivity handling.
- Do not bypass messaging authorization.
- Do not expose unrestricted transcripts or diagnostics.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Artifact obligations and evidence can be viewed and recorded.
- [ ] Assignments can be resolved through typed executor bindings.
- [ ] Direct role messaging and manager chat work through authorized services.
- [ ] Browser proof exists.

## Proof Required

- Artifact/assignment/messaging/component test output.
- Playwright evidence/coordination screenshot evidence.
- Story coverage table for US-040 through US-043 and US-053.

## Browser Validation Logging

- Required. Capture evidence, assignment, messaging, and manager chat actions with screenshots and console/network summary.

## Progression Gate

- SB26 may start after evidence and coordination projections are stable.

## Suggested Agent Prompt

Execute SB25 from `codex/bundles/process-module-architecture-v3/subbundles/25-evidence-artifact-obligations-assignments-direct-messaging-and-manager-chat`. Rebuild evidence and coordination surfaces over artifact, assignment, messaging, and manager projections.

## Handoff Notes For Next Bundle

Record analytics and live dashboard summary fields available after evidence/coordination work.
