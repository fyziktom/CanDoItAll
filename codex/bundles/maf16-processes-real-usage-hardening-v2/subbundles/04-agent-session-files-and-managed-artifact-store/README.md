# SB04: Agent Session Files And Managed Artifact Store

## Status

- Completed

## Objective

Evaluate and adopt session file/file store support for process artifacts where it improves durable evidence.

## Covered Inputs

- RQ03: evaluate `AgentSessionFiles` and file-store support.
- RQ05: prove live-use artifact validation paths.

## Prerequisites

- SB02 adoption matrix must classify session files/file store.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Deliverables

- Decision record for MAF session files versus CanDoItAll managed storage.
- Durable lineage and content-hash tests for file-store or managed artifact records.

## Dependency Impact

- SB10 and SB11 depend on reliable artifact storage identifiers and content hashes.

## Validation Depth

- Critical semantic proof must show a current-run durable artifact can satisfy an expectation and wrong lineage cannot.

## Implementation Steps

- Map current file writes to MAF session/file-store concepts.
- Implement or document the selected storage strategy.
- Add tests for content hash and lineage.
- Update `proof/SB04`.

## Do Not Do

- Do not store process artifacts only as transient chat text.
- Do not accept unreadable or wrong-run content as satisfied.

## Acceptance Checklist

- Storage strategy is explicit.
- Current-run file/session identifiers are durable.
- Tests cover positive and negative lineage/content cases.

## Proof Required

- Failing-first/adversarial transcript.
- Passing integration-path transcript.
- Source assertions, anti-stub audit, and hashes in `bundle://proof/SB04`.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB10 and SB11 may start only after storage/reference semantics are proven or explicitly deferred.

## Suggested Agent Prompt

Map MAF session-file capability to CanDoItAll managed process artifacts and prove durable current-run artifact storage with content hashes.

## Closure Proof

- bundle://proof/SB04/manifest.md
- bundle://proof/SB04/semantic-invariants.md

