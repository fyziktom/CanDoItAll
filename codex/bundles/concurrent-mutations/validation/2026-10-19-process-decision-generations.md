# Process decision generation lifecycle

The [bounded lifecycle design](../plan/P07-process-decision-generations.md) is implemented
in Process Runtime as immutable generation/child values and a pure transition owner.
All **29/29** selected Unit cases pass. This checkpoint does not enable Process/Workflow
policy execution or close R5. Durable generation storage, actual owner closure verification,
accounting integration, inherited material, manager continuation and producer proof remain
required.

## Behavior and validation

Enrollment and closure preserve exact occurrence, owner and allocation bindings. All
mutations compare decision/generation/revision; unrelated enrollment does not invalidate
an active child's generation for publication. Review blocks ordinary dispatch. Quiescence
blocks enrollment and both publication paths, while accepting actual child closures.
Supersession and cancellation require every child closed. Historical accepted receipt
references remain immutable. Successors preserve decision/budget identity and monotonic
revision. Limits are 32 generations and 64 children per generation; the separate shared
ledger still caps total lineage allocations at 256.

Runtime and Unit standard Release builds passed. Build-backed discovery confirmed 29
cases before execution with `FullyQualifiedName~CanDoItAll.Tests.Unit.ProcessDecisionGenerationTests`.
The cases exercise both effect/quiescence orderings, exact and changed replay, stale and
foreign expectations, review, incomplete closure, terminal states, capacity, malformed
retained values and checked overflow. They use no host, database, filesystem, clock or
provider. They prove pure rules, not PostgreSQL locking or child-owner evidence.

The full portability-static scan and final enforcement passed with **15,619 reviewed
findings unchanged**; the baseline was not rewritten. Rendering-register, documentation
and diff checks passed. Final doc-only closure checks are retained separately.
No broad Stable trigger was introduced by this isolated, not-yet-wired foundation;
historical Stable failures remain open.

## C# architecture gate result

Status: **Pass for pure lifecycle foundation; integration follow-up required.**

CodeAnalytics before `snap-20261019173059-d1ce2c09` and after
`snap-20261019183806-d1ce2c09` cover the same four Process projects. The after snapshot has
976 types, 8,852 members, 24 entities, 323 findings, 190 existing workspace diagnostics
and zero dependency cycles. Two new informational member-count findings were reviewed:
the cohesive generation value has 12 members including bounds/validation; the 159-line
transition class has 15 methods including private invariant checks. No project references,
runtime partials, DI registrations, interfaces or infrastructure dependencies were added.
Existing workspace diagnostics remain disclosed; actual builds passed.

Persistence must load current heads/children under its root transaction and verify complete
sets when rebuilding counts. Application must verify closures with actual owners outside
SQL. These pure values cannot be accepted as authorization or receipt evidence from HTTP
or model output. The dependency direction and tests permit durable-owner implementation
next, not activation of product settings.

## Frozen evidence and resources

Candidate: `.artifacts/concurrent-mutations/validation/c4-process-decision-generations`.
Complete source identity: 9,440 files, SHA-256
`33C1FB876FA03B7B40AC2B51C0B3D91852471C33BF3C8B3E1B74219FD57B5E28`.
Standard runtime identity: 1,472 files, SHA-256
`71ACF8CEB5517DB62751A8ABB7AEDFE9B7812646DDBD33EE42145076D38A8F6F`.
Both were reverified unchanged before release. The 16.5018763-second observer covered
the entire 2.7612836-second test command. Reconciliation found zero remaining observed
processes and zero new database-name delta. Three unattributed TEMP entries were preserved;
no earlier cleanup-review denial was retried or bypassed. The existing isolated PostgreSQL18
fixture remains needed for the next integration lane.

Compact proof: `proof/2026-10-19-planning-authority/c4-process-decision-generations.json`,
SHA-256 `1E82C6F77BC1410FF7E433682A8959DC4935C095FB49981E1EF5AA64D59119E2`.
The source/runtime freeze was released before these documentation closure edits.
