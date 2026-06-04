# Assumptions And Risks

## Assumptions

- The current runtime behavior is the source of truth unless a test reveals a bug.
- Projection source paths must be migrated one at a time.
- The dispatcher may continue to own orchestration, storage and DB side effects until a later bundle proves a safe service boundary.
- Helper/adapters may remain internal to `CanDoItAll.Modules.Processes`; this is not a Process Core extraction.

## Critical Path Risks

- Migrating projection paths without exact external-reference-key parity can create duplicate or missing artifacts.
- Moving validation rules without producer/mode parity can mark invalid evidence as satisfied.
- A side-effect writer introduced too broadly could hide storage/DB errors or break dispatch claim renewal.
- Tests may pass on execution-artifact path while mock/workspace/response/provider-native projection paths silently drift.

## Validation Risks

- Artifact tests are numerous and can be timeout-prone; use focused named slices plus one broader artifact regression filter at closure.
- Source scans must reject accidental `Processes.Core` or driver-pack creation.
- Browser proof should remain N/A; mobile screenshots are wasted effort for this runtime bundle.

## Reopen Triggers

- Any public tool name or required-tool classification changes.
- Any external reference key format changes without explicit migration proof.
- Any artifact lineage field is dropped or renamed.
- Any helper references UI, MAF, Workbench provider, DbContext, or storage implementation unexpectedly.
- Any proof artifact path contains mobile/small/medium/phone/tablet tokens.
