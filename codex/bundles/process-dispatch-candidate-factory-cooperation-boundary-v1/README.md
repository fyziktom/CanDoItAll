# process-dispatch-candidate-factory-cooperation-boundary-v1

Status: Prepared

## Objective

Continue the gradual Processes dispatch decomposition by isolating route-specific `DispatchCandidate` construction and cooperation metadata into module-local helpers.

This is the next safe step after candidate header selection and hydration readback. It intentionally does **not** start Process Core extraction and does **not** introduce production process-driver APIs.

## Why this bundle

The previous bundle completed candidate hydration and route helpers, but `LoadDispatchCandidateAsync` still owns route-specific `new DispatchCandidate(...)` construction, direct-agent assembly, recovery id integration, binding outcome integration and cooperation metadata.

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

## Validation Summary Required

Codex must record:
- subbundle gate results,
- source scans,
- line counts,
- focused unit/integration tests,
- full solution build,
- completed-stage validator output if local validator is available.
