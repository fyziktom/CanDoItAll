# Assumptions And Risks

## Assumptions

- Current branch already contains the completed artifact satisfaction helper extraction.
- `ArtifactValidation.cs` remains module-local and is still the next best hotspot.
- No public API consumer needs `Process Core` contracts yet.
- Current tests contain enough negative cases for artifact contracts, recovery routing, critical failures, and browser output handling; Codex must add missing focused cases if a rule family lacks coverage.

## Critical Path Risks

- Moving rule helpers may silently change branch ordering in `CanAutoSatisfyRequiredArtifact`.
- Browser-output path validation can accidentally become too permissive or too strict.
- Critical tool failure suppression is easy to break because it depends on receipt ordering, failure kind, provider-native browser outputs, placeholder request summaries, and recovered scaffold behavior.
- Artifact kind/content classification can change storage semantics if helpers normalize too aggressively.
- Helper extraction can look complete while leaving duplicate logic in `ArtifactValidation.cs`.

## Validation Risks

- Source scans can pass even when behavior changes. Every critical gate must include focused regression tests.
- Broad architecture tests can include unrelated old-bundle failures. Codex must separate scoped failures from blocking failures and document unrelated baseline failures.
- No UI changed; any browser/mobile proof path is likely proof churn and must be rejected.

## Reopen Triggers

- Any Process Core project/path/API is added.
- Any production driver API/registry/package is added.
- Any helper has hidden EF/storage/file/network side effects without a coordinator name and explicit source assertion.
- Any `ArtifactValidation.cs` wrapper changes branch order or proof semantics.
- Any artifact contract or recovery-routing integration test fails.
