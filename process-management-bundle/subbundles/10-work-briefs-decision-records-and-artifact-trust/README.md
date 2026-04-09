# 10 Work Briefs, Decision Records, And Artifact Trust

## Status

- `Ready`

## Objective

- Make the process the canonical collaboration graph by generating work briefs, persisting baton handoffs, recording explainability metadata, and attaching trust-aware artifact and evidence metadata.

## Covered Inputs

- `REQ-006`
- `REQ-007`
- `REQ-008`
- `REQ-012`
- Legacy feature `PRM-F22`
- Additional architecture notes on explainability and artifact trust

## Prerequisites

- `09-runtime-state-machine-approvals-and-decision-rights`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F22-process-native-work-briefs-baton-handoffs-and-governed-triage\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\02-architecture\IMPORTANT ADDITIONAL NOTES.md`
- `C:\repositories\CanDoItAll\process-management-bundle\02-architecture\12-process-native-orchestration-and-baton-handoffs.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\WorkspaceStorage.cs`
- `C:\repositories\CanDoItAll.IPFS\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\inventories`

## Deliverables

- Normalized work-brief and baton-handoff contracts.
- Decision-record and assignment-reason structures for explainability.
- Artifact snapshot, trust-state, lineage, review, and allowed-usage metadata plan and implementation slice.
- Initial evidence-storage seam that works with managed storage now and can adopt IPFS later.

## Dependency Impact

- Bridge, replay, metrics, conformance, and management surfaces all depend on trust and explainability being modeled here.
- If this subbundle is weak, later phases will fall back to logs and notes instead of real decision and artifact truth.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Generate canonical work-brief and handoff records from process, role, and step-contract context.
2. Record decision and assignment reasons for explainability.
3. Attach artifact trust, validation, approval, and provenance metadata to evidence snapshots.
4. Keep payload storage abstract enough for current managed storage and later IPFS-backed evidence placement.

## Scope Exceptions

- Full IPFS payload transport is deferred, but evidence descriptors must reserve the seam now.

## Do Not Do

- Do not hide handoffs inside prompt text only.
- Do not treat all artifacts as equal trust objects.
- Do not reduce explainability to unstructured logs.

## Acceptance Checklist

- Work briefs and baton handoffs are first-class process artifacts.
- Important orchestration decisions carry structured rationale.
- Artifact trust states and lineage metadata are explicit.
- Evidence storage remains compatible with future IPFS adoption.

## Proof Required

- Integration tests for work-brief generation and handoff persistence.
- Tests or schema proof for decision-record and artifact-trust metadata.
- Review proof that storage integration stays behind a process-owned abstraction.

## Browser Validation Logging

- Route:
  runtime work-brief or handoff inspection surfaces if introduced
- Viewport:
  `1920x1080`
- Evidence:
  required only for browser-visible baton or work-brief UI changes

## Progression Gate

- Later journal, bridge, and analytics phases may not start unless work briefs, decision records, and artifact trust are explicit enough to attribute later evidence correctly.

## Suggested Agent Prompt

```text
Implement only the process-native collaboration layer. Generate normalized work briefs and baton handoffs, add structured decision records, and model artifact trust and provenance without forcing a first-wave IPFS dependency.
```
