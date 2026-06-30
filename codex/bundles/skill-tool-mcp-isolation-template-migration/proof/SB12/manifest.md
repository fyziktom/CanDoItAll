# SB12 Cleanup Hardening And Docs Manifest

## Result

- Status: `Passed`
- Date: `2026-06-28`
- Validation depth: `Final closure`
- Browser validation: `N/A for SB12`; no visible setup/process/workflow behavior changed. SB11 large-screen browser proof remains the UI regression evidence.
- Progression: Bundle closure is unblocked.

## Implementation Summary

- Removed obsolete private hardcoded capability-construction helpers from `SandboxWorkspaceSeedBuilder`; default catalog capabilities now have a single active seed path through `CapabilityTemplatePackLoader` and `CapabilityTemplateSeedMaterializer`.
- Added `CapabilityMigrationCleanupGuardTests` to prevent reintroduction of private MAF capability descriptor DTOs, hardcoded seed fallback builders, hidden runtime suppression outside the shared evaluator, raw selector matching in runtime access code, and generic external tool/MCP setup errors.
- Expanded `Templates/README.md` with concrete developer guidance for adding Skill, Tool, MCP, exposure descriptor, access policy, setup-test, diagnostics, repair-flow, and managed seed versioning changes.
- Kept compatibility data and adapters needed for existing persisted catalogs.

## Evidence

- Unit regression: `proof/SB12/transcripts/unit-capability-cleanup-regression.txt` (`274` passed).
- Integration regression: `proof/SB12/transcripts/integration-seed-filter-api-workflow-regression.txt` (`34` passed).
- Component regression: `proof/SB12/transcripts/component-setup-process-workflow-regression.txt` (`60` passed).
- Build: `proof/SB12/transcripts/dotnet-build-solution.txt` (`0` warnings, `0` errors).
- Bundle validator: `proof/SB12/transcripts/bundle-validator.txt`.
- Static cleanup scan: `proof/SB12/transcripts/static-cleanup-scan.txt`.
- Documentation review: `proof/SB12/transcripts/documentation-review.txt`.
- File-size scan: `proof/SB12/transcripts/file-size-scan.txt`.
- Changed-file hashes: `proof/SB12/changed-file-hashes.txt`.

## Accepted Exception

- `SandboxWorkspaceSeedBuilder.cs` remains an existing 616-line seed aggregate after cleanup. SB12 removed the obsolete capability builders from it; splitting unrelated provider/agent seed construction would be broader than this capability-migration closure and should be handled as a separate seed-organization refactor.

## Closure Decision

- SB12 success criteria are met.
- Final validator passed.
- The bundle can close with SB01 through SB12 complete.
