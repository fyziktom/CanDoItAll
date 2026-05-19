# 03-source-truth-transfer-completeness

## Status

- `Ready`

## Objective

Transfer complete validation source truth, including project structures and external file/data manifests, without direct truth-table writes.

## Required Edits

- Extend database transfer preview and execution with file/data manifest groups.
- Include content hash, locator, redaction state, and skip reason in transfer proof.
- Add tests for idempotent re-transfer into a clean profile.

## Closure Proof

- Transfer preview lists projects, structures, files/data manifests, and excluded items.
- Transfer execution proof shows stable counts and hashes.

## Covered Inputs

- Project structures, source items, evidence anchors, and external file/data manifests are source truth for realistic memory validation.

## Prerequisites

- A clean target profile must exist and the source profile must be readable before transfer execution.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\DatabaseTransfer`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.DatabaseEndpoints.cs`

## Deliverables

- Transfer preview and execution paths that copy validation source truth through application services with counts, hashes, and exclusions.

## Dependency Impact

- Dreaming, clustering, probe, and recall validation are invalid if source truth is incomplete or copied into a dirty target.

## Validation Depth

- Use integration tests to prove clean target transfer and API tests to prove the transfer routes stay exposed.

## Implementation Steps

- Implement safe preview/execution, refuse unsafe replacement, copy source manifests/items/evidence anchors, and expose proof fields.

## Do Not Do

- Do not write directly into memory truth tables or overwrite a target with conflicting dependent rows.

## Acceptance Checklist

- Preview reports expected source-truth counts before execution.
- Execution preserves source locators and evidence anchors.

## Proof Required

- Transfer service integration proof and API route proof.

## Browser Validation Logging

- Record large-screen transfer UI proof when the UI surface is added or changed.

## Progression Gate

- Proceed only when source truth can be copied into a clean target without corrupting existing memory state.

## Suggested Agent Prompt

- Complete Cognitive Memory source-truth transfer so project structures and file/data manifests can be moved into a clean validation profile with auditable proof.
