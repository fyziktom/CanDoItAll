# Target Boundaries

## Module-local only

All production source changes remain under:

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/`

No `CanDoItAll.Processes.Core` project is created.

## Intended helper families

- `ProcessArtifactSatisfactionSnapshot`
- `ProcessArtifactRecordedSatisfactionRules`
- `ProcessFreshImplementationArtifactSatisfactionRules`
- `ProcessRequiredArtifactAutoSatisfactionRules`
- `ProcessProviderNativeBrowserEvidenceFacts`
- `ProcessProviderNativeVisualSatisfactionRules`
- `ProcessResponseTextArtifactSatisfactionRules`
- `ProcessExternalTargetReferenceGuard`
- `ProcessShallowManagedArtifactReferenceGuard`
- `ProcessQualityValidationEvidenceAggregator`
- `ProcessIncompleteImplementationSignalRules`
- `ProcessArtifactSatisfactionBlockerSummaryBuilder`

Names may be adjusted by Codex if the intent remains explicit and tests are updated.

## Side-effect classification

| Helper type | Allowed side effects |
| --- | --- |
| Rule/snapshot/facts helpers | None |
| Aggregators over already-loaded detail/response text | None |
| Wrappers in dispatcher | May call existing orchestration only |
| Existing projection/write coordinators | Existing side effects only; not part of this bundle unless tests require wrapper adjustment |

## Forbidden movements

- No EF entity move.
- No public contract promotion.
- No driver registry or driver package.
- No MAF adapter dependency.
- No UI proof or viewport screenshots.
