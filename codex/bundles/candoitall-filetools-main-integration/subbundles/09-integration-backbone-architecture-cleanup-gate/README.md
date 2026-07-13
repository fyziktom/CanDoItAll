# SB09 Integration Backbone Architecture Cleanup Gate

## Status

- `Ready`

## Objective

- Review/refactor/package/secure the complete pre-UI backbone and issue the only decision that may unlock UI.

## Covered Inputs

- N003-N009, N013-N017; R008-R016, R026-R040.

## Prerequisites

- SB06-SB08 Completed with trusted Behavioral/Governed proof; SB05 remains trusted.

## Exact Source References

- `repo://src/App/CanDoItAll.Composition`
- `repo://src/App/CanDoItAll.Web/Infrastructure`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://plan/architecture-checkpoints.md`
- `bundle://subbundles/01-re-entry-package-and-baseline-gate/README.md`
- `bundle://subbundles/06-filetools-package-adoption-and-integration-boundaries/README.md`
- `bundle://subbundles/07-authorized-handles-content-save-and-endpoint-hardening/README.md`
- `bundle://subbundles/08-host-cache-and-file-catalog-revision/README.md`

## Deliverables

- Strict C# architecture/security/dependency/package review and repairs.
- No service locator, false abstraction, contract leak, duplicate mapping, unsigned authority, cache-in-UI/driver, or broad composition logic.
- Fresh CodeAnalytics graph, affected build/tests/format, package/static assets, DI/endpoint/content/cache/revision downstream smoke.
- Fresh scale/performance contract review proving adapter bounds survive translation and known-file interaction has no browser/session dependency.
- Explicit UI unlock Pass or exact reopen list.

## Dependency Impact

- SB10-SB18 blocked until unqualified Pass.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical pre-UI progression gate.

## Implementation Steps

1. Apply Checkpoint B to actual code/references/tests/proof.
2. Red-team shallow adapters, handle bypasses, endpoint policy, cache key/revision, package overreach.
3. Repair concrete findings without adding UI.
4. Rerun complete affected, structural performance, direct-known-file, and dependent smoke.
5. Record C# gate and UI unlock decision.

## C# Architecture Impact

- Review/cleanup only; can shrink/refactor backbone types but no new user story.

## Boundary Ownership

- Confirms Infrastructure/Abstractions/Integration/Web/Composition roles.

## Dependency Direction

- Fresh before/after graph must match architecture/02; no new cycle.

## Pattern Decision

- Validate PSR-02/03/04; remove factories/facades/interfaces without real force/test seam.

## Testability Contract

- Core behavior remains directly tested without Web/pages; host smoke proves wiring/policy.

## Partial Class Policy

- No new partial; old owners do not absorb backbone behavior.

## Architecture Proof Required

- Checkpoint B Pass in `bundle://reviews/csharp-architecture-gate.md` and execution-report row.

## Scope Exceptions

- No product UI or module-specific scope implementation.

## Do Not Do

- Do not downgrade governed proof, waive tool gaps, or use UI pilot as a substitute for foundation repair.

## Acceptance Checklist

- [ ] Package/reference/architecture/security/cache gates Pass.
- [ ] One authorized content open and aggregate revision smoke Pass.
- [ ] Native/FileTools budget/completeness mapping and direct known-file zero-browser-call proof Pass.
- [ ] No unsigned/path fallback or duplicate mapping.
- [ ] Components/watch/browser tools are ready for SB10.
- [ ] UI unlock is explicit.

## Proof Required

- Behavioral review record, commands/results, snapshots/dependencies, dependent smoke, source assertions, C# gate.

## Browser Validation Logging

- No product flow; managed browser readiness only.

## Progression Gate

- Only unqualified Pass plus available Components/watch/Playwright unlocks SB10.

## Reopen Triggers

- Any pilot/story contradiction in package, mapping, authority, cache, revision, or composition reopens owning foundation and SB09; all UI evidence revalidates.

## Suggested Agent Prompt

```text
Review and clean the full pre-UI integration backbone. Use actual code, package graph, security/cache proof, dependencies, and host smoke. Fix concrete blockers, add no UI, and issue an unqualified Pass or reopen exact owners.
```
