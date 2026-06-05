# process-dispatch-candidate-factory-cooperation-boundary-v1

Status: Completed

## Objective

Continue the gradual Processes dispatch decomposition by isolating route-specific `DispatchCandidate` construction and cooperation metadata into module-local helpers.

This is the next safe step after candidate header selection and hydration readback. It intentionally does **not** start Process Core extraction and does **not** introduce production process-driver APIs.

## Why this bundle

The previous bundle completed candidate hydration and route helpers, but `LoadDispatchCandidateAsync` still owned route-specific `new DispatchCandidate(...)` construction, direct-agent assembly, recovery id integration, binding outcome integration and cooperation metadata.

## Scope

- Add candidate assembly context/factory.
- Move subprocess/workflow/direct-agent candidate construction behind factory methods.
- Move cooperation metadata resolution to a local helper.
- Keep side effects explicit and outside pure factories.
- Update documentation-only driver-readiness map.
- Add strong parity tests and source scans.

## Non-goals

- No Process Core.
- No production driver API.
- No UI work.
- No responsive/mobile proof.
- No public contract expansion.

## Validation Summary

- Bundle preparation status: `Completed`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A runtime/service refactor`

Recorded proof includes:

- subbundle gate results in `reviews/01-execution-report.md`,
- source scans and line counts in `proof/SB16/manifest.md`,
- focused unit/integration tests in `proof/SB04/manifest.md`, `proof/SB08/manifest.md`, `proof/SB12/manifest.md`, and `proof/SB13/transcripts/`,
- full solution build in `proof/SB16/transcripts/sb16-full-solution-build.txt`,
- completed-stage validator output in `proof/SB17/transcripts/sb17-completed-validator.txt` after final closure validation.
