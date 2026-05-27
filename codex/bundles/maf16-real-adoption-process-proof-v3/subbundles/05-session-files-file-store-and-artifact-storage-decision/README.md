# SB05: 05-session-files-file-store-and-artifact-storage-decision

## Goal

Clarify MAF session files/file store vs CanDoItAll managed artifact storage.

## Required work

- Adopt AgentSessionFiles if available.
- If unavailable, document and test CanDoItAll storage as authoritative.
- Ensure file/session evidence is correlated to process artifacts, content hashes, and tool receipts.
- Add a test for an artifact written through session/tool receipt becoming a process artifact.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB05` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Clarify that CanDoItAll managed artifact storage remains the process evidence store.

## Covered Inputs

- RQ05 session, file, and managed artifact behavior.

## Prerequisites

- MAF reflection proof shows no adopted session-file abstraction for this runtime path.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs`

## Deliverables

- Content-backed process evidence policy retained in managed artifact validation.

## Dependency Impact

- SB11 and SB13 depend on managed storage paths for content validation.

## Validation Depth

- Integration tests validate missing managed content behavior.

## Implementation Steps

- Keep managed paths as the validation boundary.
- Require readable content only when the required artifact is content-backed.

## Do Not Do

- Do not introduce a second evidence store for this bundle.

## Acceptance Checklist

- Missing content returns a typed validation status.

## Proof Required

- SB11 proof manifest and runtime tests.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Storage policy must be stable before read-model projection.

## Suggested Agent Prompt

Use existing managed artifact storage as the process evidence source and validate content explicitly.
