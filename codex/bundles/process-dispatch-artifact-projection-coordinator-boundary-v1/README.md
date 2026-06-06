# process-dispatch-artifact-projection-coordinator-boundary-v1

Status: Prepared for Codex implementation.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `N/A - service refactor with explicit no-UI constraint; source scan passed`
## Mission

Continue the `maf-processes-refactor` branch with a **module-local artifact projection coordinator boundary**.

The last completed observation/outcome bundle reduced `ToolValidation.cs` to 793 lines and moved session / execution-log / declared-outcome parsing behind local helpers. The next safe seam is `ProcessRunAutomationDispatchService.ArtifactProjection.cs`, because it still combines source-specific selection, file IO, duplicate handling, projection planning, write coordination, and candidate state mutation across many artifact source families.

This bundle must **not** start Process Core extraction and must **not** introduce production process driver APIs. It prepares those future directions by stabilizing projection source vocabulary, side-effect ownership, and evidence/source-family maps.

## Hard Constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not add `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, or process-driver packages.
- Do not change public process contracts unless explicitly required by current tests and documented in the gate.
- Do not remove existing projection paths or alter their order.
- Do not hide file IO, storage writes, DB writes, or `RecordArtifactAsync` behind pure-looking planners.
- Do not touch UI/Razor/CSS/JS/TS files.
- Do not create small, medium, mobile, phone, or tablet proof artifacts. Browser validation is expected to be `N/A`.
- Keep all comments in source code in English.

## Primary Source Hotspot

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`

## Bundle Shape

This is an initiative-profile bundle with 56 subbundles and critical refactor gates at:

SB04, SB08, SB14, SB20, SB26, SB32, SB38, SB44, SB48, SB52, and SB56.

## Expected End State

- `ArtifactProjection.cs` becomes an orchestration shell over module-local projection source coordinators.
- Projection source order is preserved:
  1. execution artifacts
  2. process mock artifacts
  3. workspace-written artifacts
  4. existing managed artifacts
  5. response text artifacts
  6. provider-native browser artifacts
  7. completed decision artifacts
- Planners/adapters remain side-effect-free.
- Coordinators own explicit side effects.
- Candidate state updates are centralized and tested.
- Future driver-readiness is documented only.
