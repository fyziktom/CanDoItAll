# Small repairs versus complex findings

## Repair immediately only when the boundary remains the same

A small fix has a reproduced defect, a understood cause and a bounded change that preserves the existing public protocol, storage model, authority and owners. Examples: exact request cleanup, preserving a later field edit, missing target readiness, a safe projection, an incorrect event binding, an owned dialog not closing, or a test waiting for the wrong state. The number of changed lines alone does not define safety.

First capture a failing original test/trace and source checkpoint. Fix the owner of the defect, add the focused regression, rerun the affected renderer/owner/consumer cases and re-evaluate broad gate invalidation. Keep unrelated edits out of the fix. If the defect was pre-existing, label that honestly; it may still be a small in-scope repair, but don't blame decoupling.

## Map rather than redesign during the campaign

Treat a finding as complex when resolution requires schema/migration changes, new capability or authority semantics, new multi-owner transaction/receipt protocols, canonical database lifetime redesign, broad service relocation, fundamental concurrency semantics or an unclear cross-module blast radius. A security or data-loss finding is not made small by a quick bypass.

Create one record using `templates/complex-finding.md`, a deterministic or precisely described UI reproducer, and a dependency/call/state map. Include exact original and final commit/checkpoint, environment/provider, fixture IDs, safe stage timeline, expected invariant, observed owner state, affected paths/symbols, competing hypotheses, negative controls and downstream surfaces. Preserve evidence hashes.

Use a separate task-owned scratch worktree and equivalent sibling/tree/configuration inputs for before/after comparison when feasible. Never reset the user's checkout. The immediately previous implementation may already contain the bug; select a justified pre-extraction baseline from history rather than merely choosing HEAD's parent. Record attribution as confirmed-refactor-regression / suspected-refactor-regression / pre-existing / environmental / external-provider / unknown. A timing coincidence or an unchanged file is not proof of causality either way.

Propose repair-bundle boundaries, ordering, acceptance tests and migration/compatibility/performance risks. Do not implement that redesign here. Mark the impacted Workspace stage or campaign cases BLOCKED or FAILED, not PASSED. Continue independent safe cases so the final report still maps the application comprehensively.

## Safety escalation and flaky results

On demonstrated permission bypass, credential disclosure, wrong-database write or destructive retargeting, stop the affected mutating lane immediately, protect the fixture/evidence and map the defect. Do not continue leaking data to gather more samples. Continue unrelated safe observations only.

Do not relabel a failure as flaky without evidence. First distinguish readiness/harness timing from the product: await exact operation completion, inspect process/database/provider state and reproduce with controlled ordering. Never remove assertions, increase production timeouts, add broad quarantine, retry mutations or replace real owners with mocks to obtain green.

Retain all attempts. A repaired focused rerun does not retroactively make the original Stable run all-green. If a shared owner, fixture, authority or graph changes after the broad checkpoint, rerun the invalidated broader gates on a fresh frozen checkpoint. If an isolated UI repair does not invalidate a broad owner result, document the exact changed hashes and run every affected UI journey again.

## Required final mapping

Return a prioritised findings index with severity, attribution confidence, impacted case IDs, current containment and next repair inputs. Keep separate: small defects fixed here; complex open defects; unavailable test infrastructure; unsupported product capabilities; optional observations. A missing live key is not a product bug; a supported tool failing under correct permissions may be. An unsupported feature must be tested as a refusal, not implemented incidentally.
