# 03 MAF Context Trace Capture

## Status

- Completed

## Objective

- Preserve MAF context contributor trace metadata so future Cognitive Memory recall/context injection can be audited without parsing injected prompt text.

## Covered Inputs

- H-FR-006, H-NFR-002, H-NFR-003, and H-NFR-005.
- Raw note: MAF contributor trace metadata is dropped.

## Prerequisites

- Completed `cognitive-memory-prerequisite-boundaries` implementation.
- This subbundle can run independently from source paging and redaction work.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Context\AgentContextContributionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentContextContributionProvider.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs

## Deliverables

- Trace capture model for contributor id, status, message count, trace metadata, failure message, and optional elapsed duration.
- MAF provider/runtime path that retains trace results for future Cognitive Memory inspection.
- Unit tests for provided, skipped, failed, duplicate id, disabled contributor, and cancellation behavior.

## Dependency Impact

- Cognitive Memory MAF integration depends on these traces to explain context-pack injection, skips, failures, and budget decisions.
- Weak proof here invalidates future recall auditability.

## Validation Depth

- Critical audit foundation.
- Unit tests are required.
- Source review must verify trace metadata is not silently discarded.

## Implementation Steps

- Decide whether trace records live in `RuntimeCapabilityState`, a scoped trace sink, or a typed context contribution collector.
- Retain trace metadata from `AgentContextContributionResult` for all contributor statuses.
- Preserve existing explicit failure behavior.
- Add tests proving trace metadata is available after provider execution and runtime contributor attachment.

## Scope Exceptions

- Do not build the Cognitive Memory recall trace store here.
- Do not expose trace UI here.

## Do Not Do

- Do not swallow contributor failures.
- Do not encode trace data only inside prompt text.
- Do not make trace capture Cognitive Memory-specific.
- Do not log sensitive trace metadata without redaction review.

## Acceptance Checklist

- Provided, skipped, and failed contributor outcomes produce typed trace records.
- Trace metadata survives the MAF provider mapping.
- Existing contributor ordering/cancellation behavior remains intact.

## Proof Required

- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentContextContributionTests --no-restore`
- Source review notes showing where trace data is retained.

## Browser Validation Logging

- No browser proof is required unless implementation unexpectedly changes visible UI.
- If UI changes occur, record route, viewport, Playwright evidence, and screenshot in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to architecture gate sync only after future Cognitive Memory MAF integration can inspect contributor trace records.

## Suggested Agent Prompt

- Implement generic MAF context contributor trace capture only. Keep failure behavior explicit, preserve existing tests, add trace assertions, and do not implement Cognitive Memory recall tracing.
