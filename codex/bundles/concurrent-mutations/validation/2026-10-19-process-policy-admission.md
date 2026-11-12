# Immutable Process policy admission foundation

The first foundation in [P07](../plan/P07-process-policy-admission.md) is implemented.
**68/68 focused cases pass: 36 Unit and 32 Integration, with no skips.** This is
immutable admission/storage and compatibility proof. Process and Workflow policy
settings, the real launch producer, governed execution, decision supersession fences
and durable manager pause/resume remain required before activation.

## Behavior and boundaries

Process Runtime owns a versioned admission record with original workspace namespace
and revision, definition version, step identity, preferences, capabilities, enclosing
ceilings and resolved policy. Decoding rejects missing, unknown or duplicate members,
invalid identities, incompatible preferences, changed resolution and excessive input.
Encoding is bounded to 32 KiB and 32 enclosing policies. No Agent, product-module or
EF dependency was added to Runtime; no project reference or DI service was added.

Prepared launches validate definition/step binding and include non-null admission in
a new fingerprint domain. Legacy omission remains omitted on serialization. The
[golden fixture](../../../../tests/Support/Fixtures/Concurrency/README.md) was captured
from the previous compiled implementation before source edits; its original payload,
fingerprint and initial-commit comparison remain exactly compatible.

Initial SQL commit validates binding again. Assignment repair cannot introduce,
remove or substitute the policy. Reload rejects corrupt or wrong-step evidence rather
than reading current preferences. Both actual Process Agent and Workflow adapters
refuse the unactivated contract before child execution. These guards do not implement
snapshot execution and must not be advertised as doing so.

Migration `20261019142619_AddProcessDecisionPolicyAdmission` adds one nullable text
column and a UTF-8 byte-bound constraint. Existing rows stay null; there is no policy
backfill or rewrite. Upgrade/restart preserves every legacy assignment field. Fresh
database and canonical/module model checks pass, as does EF pending-model validation.
Idempotent upgrade SQL was generated and reviewed. Downgrade takes the table lock and
refuses to discard any retained policy. The additive migration still needs a schema
lock and constraint validation; production deployment requires its normal bounded
command timeout and migration window, not a claim of zero migration contention.

## Validation and reviewed findings

Standard Release builds passed for Runtime, Persistence, the Process module, Web,
Migrations, Unit and Integration, with existing NuGet advisory/compiler warnings.
The selected cases cover strict wire/provenance behavior, old launch bytes, new
fingerprints, both executor refusals, mapped Workflow claim recovery, prepared launch
acceptance/rollback/restart, file-scope/lifetime recovery, immutable assignment repair,
malformed storage, PostgreSQL upgrade, UTF-8 constraints and protected downgrade.

The first candidate executed all 68 cases: 47 passed and 21 failed. Seventeen failures
shared an invalid new test fixture: PauseForReview was given a positive automatic
replan allowance. The fixture now gives that allowance to its explicit Replan override.
Four failures came from three pre-existing test call sites using the retired Guid-only
Project deletion API. They now use the existing lifecycle test driver, which captures
the real original definition and dispatches the guarded operation. Production guards
were not weakened. The successor rebuilt both test assemblies and reran all 68 cases.

CodeAnalytics compares `snap-20261019120526-dfe0482c` with
`snap-20261019142902-a6343fab` in the same four Process projects: 682 → 689 types,
7,300 → 7,361 members, 22 entities unchanged, zero cycles and zero DI registrations.
Findings increased 204 → 207: three informational member-count findings for the
immutable admission and explicit wire records. They have one serialization/provenance
responsibility, not orchestration. The existing 189 diagnostics remain; this is a
scoped delta review, not a clean-workspace claim. The two five-line module refusal
guards are outside that four-project comparison and are covered by builds/tests.

Portability scanned all candidate inputs, including new files. Four added findings
are generated EF property names: RootPathState, RootPathSyntax, RaisesEscalation and
EscalationCount. They copy existing schema metadata and perform no filesystem or
process operation. The reviewed baseline diff adds only those four allowances.
After source repairs, the scan was regenerated and final enforcement **without
baseline writing passed: 15,615 reviewed findings unchanged**. Rendering-register,
documentation and diff checks passed. No UI or browser behavior changed here.

## Candidate identity and resources

First candidate `c4-process-policy-admission` retained all failures and verified its
source/runtime unchanged. Proof SHA-256:
`3F8AC59BE8BAF2B524EBEBB97F016AA5B9E77CAEBECE332B68A3C168AE4310D5`.

Successor `c4-process-policy-admission-ready` verified an exact three-file test-only
delta before reusing the Web build and migration generation/model commands. Its
executed source and runtime inventories were verified unchanged after tests:

- 9,393 source inputs: `2D76E95A4A7325C93E105753EFA2ED22523662A57BB4434856302ABDEDBDCC53`.
- 4,340 runtime inputs: `701483D98A0B008CCB5E2463786417FB58D601ABF74588C1869D09A10FD2B526`.
- Retained 68-case proof: `1D13EB29DFC240359909646947435BBB0096D6896348DAE85BFC3D0BF738BC96`.

Both candidates reused the existing isolated PostgreSQL 18 server. Each observer
covered both entire test commands. Both reconciled with zero remaining observed
processes and zero new database delta, preserving six unattributed TEMP deltas each.
No replacement stack was created. Reviewed proof is retained under the existing ignored
`proof/2026-10-19-planning-authority` convention; freezes were released after retention.
Later documentation/commit metadata are outside the executed source identity.

Earlier automatically rejected cleanup remains blocked as recorded in
[resources](../resources.md). SPA remains clean on `codex/concurrent-mutations` in
`C:/repositories/CanDoItAll.SPA-concurrent-mutations`; its main checkout is untouched.
No merge or push occurred. Full writer coverage, runtime activation, sustained capacity
and complete E2E closure remain open.
