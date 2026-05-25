# SB03 - Evidence-Bound Manager Recovery

## Status

- Completed

## Objective

Harden manager artifact recovery so it creates only evidence-supported artifacts, blocks when evidence is insufficient, and uses only explicitly eligible process/recovery managers.

## Covered Inputs

- N002, N006, N007
- Findings F003, F004, F005

## Prerequisites

- SB01 finalizer exists.
- SB02 artifact diagnostics exist.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Recovery manager eligibility restricted to explicit process manager/recovery capability.
- Recovery packet includes diagnostics, source evidence references, current run root, allowed outputs, and disallowed actions.
- Recovery artifacts carry provenance linking to previous execution run, recovery decision, source artifacts, tool receipts, and validation results.
- Recovery output is revalidated by SB02 validator before completion.
- Insufficient evidence becomes blocked diagnostic, not fabricated artifact.

## Dependency Impact

- Critical foundation for `SB05`.
- Retry hardening depends on reliable recovery/blocking decisions that cannot invent evidence.

## Validation Depth

- Deep semantic proof is required.
- Include adversarial tests where the recovery manager tries to satisfy an artifact without evidence.

## Implementation Steps

1. Replace fuzzy fallback manager resolver behavior with explicit eligibility.
2. Add or reuse manager capability/tag fields if available.
3. Build typed recovery packet from SB02 diagnostics.
4. Record lifecycle events separately: needed, manager resolved, directive recorded, started, completed, blocked.
5. Ensure recovered artifacts are marked as recovery-created in provenance.
6. Revalidate recovered artifacts before returning Completed.
7. Block with exact evidence gap when no eligible manager or no sufficient evidence exists.

## Scope Exceptions

- Do not implement human manager UI unless required by existing service contracts.
- Do not change unrelated agent catalog behavior.

## Do Not Do

- Do not select arbitrary `lead` agents for recovery.
- Do not let manager recovery produce product changes unless the recovery packet explicitly allows it.
- Do not treat recovery prose as proof without source evidence.

## Acceptance Checklist

- [x] Generic `lead` fallback cannot be selected for artifact recovery.
- [x] Explicit manager/recovery capability can be selected.
- [x] Recovery artifacts keep source execution/provenance metadata from existing projection/recovery paths.
- [x] Recovery insufficient evidence routes back through validation/blocking diagnostics.
- [x] Finalizer revalidates recovery output.
- [x] Completion state does not depend on shared mutable candidate sets.

## Closure Proof

- Manifest: `bundle://proof/SB03/manifest.md`
- Semantic invariants: `bundle://proof/SB03/semantic-invariants.md`
- Passing transcript: `bundle://proof/SB03/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Focused regression suite: `bundle://proof/SB06/transcripts/focused-integration-tests.txt`

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- manager resolver negative tests
- recovery provenance positive tests
- blocked-insufficient-evidence tests
- source assertions for finalizer/recovery integration
- changed-file hashes

## Progression Gate

- Do not start `SB05` until recovery cannot complete without valid evidence-bound artifacts.
- The gate must include manager eligibility, recovery provenance, and blocked-insufficient-evidence proof.

## Browser Validation Logging

- N/A unless this subbundle adds or changes browser-visible UI.
- If browser proof is needed for a process scenario, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

Use the shared implementation prompt at `bundle://shared-prompts/implementation-prompt.md`, then append this subbundle README and the exact source references above. Execute only this subbundle. Record proof before moving on.
