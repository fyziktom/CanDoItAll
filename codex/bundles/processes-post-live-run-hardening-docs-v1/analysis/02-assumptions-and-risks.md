# Assumptions And Risks

## Working Assumptions

- The current branch is `processes-hardening` and the observed head for preparation was `85b91aaa8c1745c98a78d0c5eeb787962eab6949`.
- Existing tests and source paths remain the primary source of truth; if a bundle claim conflicts with the repo, repair the bundle before implementing.
- Browser proof is required only for subbundles that change user-visible Blazor behavior or operator workflows.

## Critical Path Risks

- A broad all-in-one integration filter may time out and hide which runtime slice failed.
- Dispatch grounding and project-structure projection can regress silently if regex/path logic stays implicit.
- Manager fallback scoring can choose the wrong agent if assignment and capability signals are not dominant.
- Documentation and API skill drift can make operators or agents follow stale runtime contracts.

## Validation Risks

- Structure-only tests can pass while invalid artifacts still appear satisfied.
- Manually seeded positive fixtures can fake production signals that real dispatch never emits.
- Docs-only proof cannot close runtime behavior requirements.
- UI assertions without browser screenshots cannot prove operator readability or overlay state.

## Reopen Triggers

- Any downstream test shows artifact status, manager resolution, output grounding, or run projection semantics differ from the upstream proof.
- Final closure lacks transcript-backed proof for a completed critical subbundle.
- A source assertion or anti-stub audit finds `TODO`, `NotImplemented`, template-only output, or fixture-specific branching in production paths.
- Docs, OpenAPI, templates, or skills describe fields or behaviors no longer present in source.
