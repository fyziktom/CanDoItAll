# Assumptions And Risks

## Assumptions

- Existing output text and launch-variable keys are compatibility contracts even where they are not formal public APIs.
- Both current duplicate implementations are intended to produce the same result.
- A top-level deterministic builder/policy in the existing Workbench project is a real boundary; DI and new projects would add ceremony without lifecycle or dependency benefit.

## Critical Path Risks

- The two production launch paths can diverge if either bypasses the shared owner; SB02 blocks all downstream work until both-caller proof passes.

| Risk | Mitigation |
| --- | --- |
| Context ordering, limits, redaction, or visual classification changes during movement | Direct characterization tests for positive, negative, and limit cases plus existing integration assertion inventory |
| Output-root precedence or aliases drift | Direct metadata/ancestor/alias tests |
| Page and agent path stop sharing one owner | Source architecture test names both callers and forbids duplicate private builders |
| Hierarchy cycle protection changes | Direct duplicate, ancestor, descendant, current-parent, self, and valid-candidate tests |
| A facade merely delegates back to the old private code | Delete duplicate methods and assert they are absent from both old owners |
| Page state becomes a second source of truth | Extract only pure read-policy; do not move or wrap mutable page state |
| Build/test environment is affected by a running Web host | Prefer Workbench/Unit/Component builds and tests; use isolated artifacts or record the integration blocker without stopping user-owned processes |
| Scope expands into unrelated partials | Reopen bundle preparation before adding another responsibility |

## Validation Risks

- The running Web host locks its normal output directory, so builds must target unaffected projects or isolated artifacts.
- Integration bootstrap writes a test secret to user-local storage, which is denied in the restricted sandbox.
- Neither limitation may be represented as passing behavior proof; direct unit/source/component proof is required and any remaining integration gap stays explicit.

## Reopen Triggers

- Evidence shows UI and agent launch paths intentionally differ.
- The extraction needs mutable page state, a new project reference, or an implementation-specific dependency.
- Direct tests cannot cover the behavior without a host.
- A regression contradicts any preserved output, key, hierarchy, or source-of-truth invariant.
