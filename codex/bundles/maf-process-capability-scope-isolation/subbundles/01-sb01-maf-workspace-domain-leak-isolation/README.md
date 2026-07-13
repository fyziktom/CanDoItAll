# SB01 MAF Workspace Domain Leak Isolation

## Status

- Status: `Completed`
- Criticality: `Critical foundation`
- Depends on: bundle preparation only

## Objective

Remove development/UI-specific image-analysis prompt assumptions from common MAF workspace runtime tooling while preserving generic image analysis and deterministic image-set evidence behavior.

## Covered Inputs

- User-reported leak in `WorkspaceRuntimePlugin.NormalizeImageSetAnalysisPrompt`.
- REQ-MAF-001, REQ-MAF-002, REQ-MAF-011.
- NFR-001, NFR-003, NFR-004.

## Prerequisites

- Read `bundle://analysis/01-current-state.md`.
- Confirm no production code was already changed in this preparation run.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageSetEvidenceBuilder.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`

| Source | Required attention |
| --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs` | `AnalyzeImageFile`, `AnalyzeImageFiles`, `NormalizeImageAnalysisPrompt`, `NormalizeImageSetAnalysisPrompt`. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageSetEvidenceBuilder.cs` | Keep deterministic evidence generic. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs` | Existing image-analysis tool policy coverage. |

## Scope

- Replace common default image prompts with domain-neutral visible-evidence instructions.
- Preserve caller-supplied prompts without adding software/UI assumptions.
- Keep deterministic image-set evidence, but change wording from screenshot/software UI to image/file evidence if needed.
- Add tests proving domain-neutral defaults.
- Add a scan or assertion for banned domain terms in common MAF prompt normalizers.

## C# Architecture Impact

This subbundle is a behavior cleanup inside common MAF. It must not create a domain-selection switch inside `WorkspaceRuntimePlugin`.

## Boundary Ownership

- Common MAF owns generic image analysis.
- Development processes or development tool packages own UI screenshot analysis.

## Dependency Direction

No new project references should be required for SB01.

## Dependency Impact

- Expected impact is local to common MAF runtime code and unit tests.
- Downstream SB02 relies on this cleanup so capability-scope work does not preserve a known domain leak.

## Pattern Decision

Use small function cleanup and focused tests. Do not add a strategy/provider abstraction unless needed by later subbundles.

## Testability Contract

- Unit-test empty single-image prompt normalization.
- Unit-test custom single-image prompt wrapping.
- Unit-test empty multi-image prompt normalization.
- Unit-test multi-image deterministic evidence inclusion.
- Assert no normalized prompt contains `software-delivery`, `software UI`, `UI state`, `UI design`, `Blazor`, or `browser proof`.

## Validation Depth

- Unit-level validation is required.
- Text-scan validation is required.
- Integration validation is deferred to SB06.

## Partial Class Policy

Do not add a new partial file for prompt behavior. If extraction is needed, use a top-level focused helper type.

## Implementation Steps

1. Replace software/UI-specific defaults in `NormalizeImageAnalysisPrompt` and `NormalizeImageSetAnalysisPrompt`.
2. Reword deterministic image-set evidence text if it assumes screenshots rather than generic images.
3. Add focused unit tests around prompt normalization behavior.
4. Run the targeted unit tests.
5. Capture proof in `proof/SB01/`.

## Do Not Do

- Do not add a `developmentMode` flag to common MAF.
- Do not move process-specific prompt text into common MAF under another method name.
- Do not change tool names or existing tool classification in this subbundle.

## Acceptance Checklist

- Common MAF image analysis is domain-neutral.
- Existing workspace image tools still function.
- Tests prove generic prompt behavior.
- Domain-specific behavior is explicitly deferred to SB05.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- Test command output.
- Text scan results for common MAF prompt terms.

## Browser Validation Logging

- N/A unless execution adds UI-visible diagnostics.

## Progression Gate

- SB02 may start after common MAF prompt defaults are generic and tests pass.

## Suggested Agent Prompt

```text
Execute SB01 only. Remove software/UI prompt assumptions from common MAF workspace image analysis. Keep behavior generic, add focused tests, and capture proof. Do not add process scope contracts yet.
```
