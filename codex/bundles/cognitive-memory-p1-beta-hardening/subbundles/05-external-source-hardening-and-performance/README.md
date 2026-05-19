# External Source Hardening And Performance

## Status

- `Completed`

## Objective

- Harden external source ingestion limits, extraction errors, sensitive-content policy, and add performance baseline guidance for large manifests and recall runs.

## Covered Inputs

- CM-P1-005
- CM-P1-006
- CM-P1-007

## Prerequisites

- Operator audit subbundle passed or no UI dependency exists.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemorySettingsContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemorySettingsServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemoryExternalSourceTextExtractor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.IngestionEndpoints.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemorySourceIngestionTests.cs
- C:\repositories\CanDoItAll\docs\cognitive-memory\operations\validation-and-testing.md

## Deliverables

- Centralized size/content limits.
- Clear extraction error detail in operation failure results.
- Explicit sensitive-content policy.
- Focused tests for limit/policy behavior.
- Performance baseline docs for large manifests and recall runs.

## Dependency Impact

- Docs closure and beta readiness assessment depend on ingestion policy being explicit.

## Validation Depth

- Security/operational hardening gate.

## Implementation Steps

1. Centralize upload/text/chunk limit constants.
2. Add explicit sensitive-content detection or rejection policy.
3. Preserve actionable failure messages without leaking raw secrets.
4. Add tests and docs.

## Do Not Do

- Do not silently redact or ingest sensitive-looking content as safe.
- Do not log raw secret values.
- Do not add broad parser rewrites unless required by tests.

## Acceptance Checklist

- Limits are declared in one service-owned place.
- Oversize/extraction failures are actionable.
- Sensitive-looking content is rejected or marked unsafe explicitly.
- Performance baseline docs identify commands, datasets, and thresholds.

## Proof Required

- Unit tests.
- Web build if API changes.
- Docs update.

## Proof Captured

- External source ingestion limits are centralized in `CognitiveMemoryExternalSourceIngestionLimits`.
- Sensitive source content and sensitive URL query parameters are rejected before source/evidence records are persisted.
- Unit settings focus passed 10/10, including credential-content, oversize upload, and extraction-error tests.
- `docs/cognitive-memory/operations/external-source-policy.md` and `docs/cognitive-memory/operations/performance-baselines.md` document policy and baseline commands.

## Browser Validation Logging

- Browser proof is not required unless rendered UI changes.

## Progression Gate

- Continue only after ingestion behavior is explicit and tested.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Harden external source ingestion limits/errors/sensitive policy, add performance baseline docs, run targeted tests/build, and update proof rows.
```
