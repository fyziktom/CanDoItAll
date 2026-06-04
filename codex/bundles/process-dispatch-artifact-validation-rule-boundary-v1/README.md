# Process Dispatch Artifact Validation Rule Boundary v1

Bundle preparation status: `Ready`
Bundle readiness gate: `Ready for Codex execution after repo-root validation`
Execution status: `Complete`
Profile: `initiative`

## Validation Summary

Bundle preparation status: `Ready`
Bundle readiness gate: `Prepared-stage validator passed after structural repair`
Execution status: `Complete`
Subbundle gate review: `Passed SB01-SB14`
Final closure gate: `Completed-stage validator transcript recorded at bundle://proof/SB14/transcripts/completed-stage-validator.txt`
Browser validation analytics: `N/A throughout; runtime/service refactor only, no UI changed`

## Purpose

Continue the `maf-processes-refactor` dispatcher decomposition without starting Process Core extraction. The previous artifact write-coordinator bundle isolated storage-backed artifact writes and completed-decision record-only writes. The next safe seam is artifact validation: `ProcessRunAutomationDispatchService.ArtifactValidation.cs` remains one of the largest dispatcher partials and still mixes path matching, title/slug matching, content heuristics, provider-native visual matching, placeholder/quality checks, and project-structure requirement preservation.

This bundle extracts **process-module-local validation rule helpers** and typed validation snapshots so later Process Core and driver-pack work can consume stable semantics without duplicating dispatcher logic.

## Important Scope Decision

This bundle is **not** a Process Core extraction and does **not** introduce process driver packs. It prepares both by separating pure validation semantics from dispatcher orchestration.

Driver-readiness work is limited to:

- classifying validation rule families,
- naming expected evidence/proof categories,
- documenting which rule families future process helper drivers may satisfy,
- keeping all production code inside `CanDoItAll.Modules.Processes` and existing neutral `CanDoItAll.Processes.Contracts` only where already safe.

## Source-Backed Current Facts

- Previous bundle completed SB01-SB14 and states that all storage-backed projection writes now use the write coordinator, completed-decision writes use a record-only coordinator, and final closure passed.
- `ProcessArtifactProjectionWriteCoordinator` now returns structured write outcomes with managed path, artifact record id, external reference key, and optional artifact expectation id.
- Final red-team from the previous bundle recommends extracting artifact validation rules from `ProcessRunAutomationDispatchService.ArtifactValidation.cs` next, because that file remains 3434 lines.
- Browser/UI proof is N/A for this runtime/service refactor unless an unexpected rendered UI route changes.

## Hard Non-Goals

- Do not create `CanDoItAll.Processes.Core`.
- Do not create `ProcessDriver`, `DriverPack`, `IProcessDriverPack`, or domain driver packages.
- Do not move EF entities, DbContext usage, Razor UI, runtime workers, MAF composition, Tooling contracts, or storage implementations.
- Do not rename process tools, artifact kinds, expectation modes, producer kinds, external reference key formats, or trust/status semantics.
- Do not run small, medium, mobile, phone, tablet, Android, iPhone, or responsive proof. This is PC/large-screen only when UI proof is unavoidable.

## Expected Outcome

After this bundle:

- artifact validation rule families are inventoried and covered by focused tests;
- dispatcher nested expectation dependencies are reduced through typed validation snapshots;
- pure path/title/content/visual/placeholder/quality/project-structure rules live behind process-module helper classes;
- `ArtifactValidation.cs` has fewer orchestration responsibilities and lower line count or at minimum no new growth;
- validation semantics remain behaviorally identical through regression tests;
- a driver-readiness map exists for future domain-specific process helper drivers without implementing those drivers yet.

## Recommended Execution Order

1. SB01 entry audit and branch hygiene.
2. SB02 current artifact validation inventory.
3. SB03 validation seam design.
4. SB04 Gate A architecture/source guardrails.
5. SB05 expectation snapshot decoupling.
6. SB06 path/managed artifact rules.
7. SB07 title/slug/text matching rules.
8. SB08 Gate B matcher parity review.
9. SB09 provider-native visual validation.
10. SB10 placeholder and quality rules.
11. SB11 project-structure preservation rules.
12. SB12 Gate C validation regression and driver-readiness review.
13. SB13 runtime smoke and viewport policy check.
14. SB14 final red-team and next cutline.

## Bundle Contents

- `inputs/` raw request and reviewed branch evidence summary.
- `analysis/` current state, risks, and next-step rationale.
- `architecture/` target validation-boundary shape and driver-readiness notes.
- `inventories/` seeded source maps and test impact inventories.
- `subbundles/` execution-ready subbundle plans.
- `evidence/checklists/Process_Dispatch_Artifact_Validation_Rule_Checklists.xlsx` detailed workbook checklist.
