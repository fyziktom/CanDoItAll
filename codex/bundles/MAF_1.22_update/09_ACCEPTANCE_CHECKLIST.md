# Acceptance checklist

All boxes below start unchecked. They describe **Codex implementation acceptance**, not work performed during preparation. Add evidence references and justified N/A dispositions; do not check a box merely because code was read.

## Scope and implementation

- [ ] Actual starting HEAD, user changes, branch, SDK, database and sibling source revisions recorded; audit drift reconciled without resetting user work.
- [ ] Current repository/shared instructions and CodeAnalytics skill read; architecture/test ownership investigated through the MCP before edits.
- [ ] M01 package graph restored coherently across production and test consumers; no introduced downgrade conflicts or suppressed evidence.
- [ ] Every M02–M12 applicability decision has a source anchor, correction/no-change reason and proof requirement.
- [ ] F01–F03 reproduced and repaired, or a precise non-defect/unreachable disposition is protected by tests; confirmed related defects also repaired.
- [ ] F04 retained-state compatibility is established with genuine old/new checkpoints; F05 maintained docs are accurate.
- [ ] Native approval binding, source authority, effect identity, batch/lease boundaries and at-most-once mutation handling remain intact.
- [ ] Dynamic tools and overlapping project/session runs cannot share the wrong toolset, schema, middleware or authority.
- [ ] Process recovery distinguishes transient/permanent/canceled/unknown-effect causes; retry budgets and justified escalation remain enforced.
- [ ] Required finalizer, current-run artifact evidence, child-work reuse and workflow terminal outcomes are validated.
- [ ] Minimal necessary UI/API fixes are complete in their owning seams; no unrelated auth/UI/provider rewrite or global tool-parallelism rollout.

## Validation

- [ ] Impact analysis uses the actual final diff and all affected test workspaces; health, required/conditional scopes and broadening are recorded.
- [ ] Every selected filter has valid, non-zero expected discovery; required and promoted tests pass on current binaries.
- [ ] Final unfiltered Stable suite passes on Windows at the final source/dependency checkpoint.
- [ ] Final unfiltered Stable suite passes on actual Linux at that same checkpoint.
- [ ] Required separate non-browser portability, PostgreSQL 18 migration/restart/restore and live/sibling lanes pass or explicitly block acceptance.
- [ ] Browser execution began only after required standard non-browser proof was green; later code edits invalidated and refreshed the appropriate proof.
- [ ] Final unfiltered Playwright suite passes on Windows.
- [ ] Final unfiltered Playwright suite passes on actual Linux.
- [ ] UJ01–UJ08 have honest Applicable/N/A results; real project-agent, simple process and workflow UI execution are proved, not inferred from catalog tests.
- [ ] Separate real-provider journeys are complete, with platform/provider provenance and bounded spend; mocks are not labeled live proof.
- [ ] Every failed, skipped, quarantined, blocked and retried case is disclosed; no filter change or fixture manipulation conceals a failure.
- [ ] Final full-source portability scan is enforced without write-baseline; intentional baseline changes have reviewed evidence.
- [ ] Documentation/evidence validators and secret checks pass; no unsealed sensitive runtime artifacts are committed.

## Delivery

- [ ] Upgrade/retained-state/rollback notes describe the implemented behavior and operator limitations.
- [ ] Delivery report maps each change/finding/scenario to actual tests and artifacts, including durations and discovery counts.
- [ ] Final Windows/Linux/UI evidence uses the same relevant final source and dependency revisions; no stale checkpoint results.
- [ ] Existing signing settings and user changes preserved; no unauthorized push/merge/PR/publication/deployment.
- [ ] Remaining uncertainty and blockers are explicit; the result is not labeled fully complete while a mandatory gate is missing.
