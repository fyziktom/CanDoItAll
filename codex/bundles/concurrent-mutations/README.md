# Concurrent mutations implementation bundle

Current handoff (2026-10-17): implementation stopped at the user's credit-saving
checkpoint. The final C4 successor passed **706/706**. A new native chat browser test
failed twice and is preserved outside the compiled checkpoint; broader runtime policy,
lifecycle cutover, capacity and cleanup remain open. See the
[checkpoint and exact continuation](validation/2026-10-17-checkpoint-handoff.md).
This current handoff supersedes transient next-step/signing statuses in older records.

Status: end-to-end work remains in progress. The Projects original-state and durable
operation foundation is committed through `702d0a93`; see
[the foundation checkpoint](validation/P02bc-foundation-checkpoint.md).
The [historical correctness checkpoint](validation/P02b-R1c-historical-correctness-checkpoint.md)
has passing evidence for 133 distinct ordinary cases across scoped repairs, both actual
restart Facts and the Send browser Fact. Those controlled configurations do not prove
ordinary background recovery or complete producer cutover.

The [C3 checkpoint](validation/P04-C3-focused-runtime-checkpoint.md) passed all 190
application cases on one source/runtime candidate: 60 Unit, 127 Integration, two
LiveProcess and one LongRunning. Independent nine-child process observation remains
incomplete, and earlier blocked resource cleanup remains outstanding. C3 publication
is dormant; finite workload characterization does not qualify capacity.

Dormant D1 saved-effect dispatch has seven passing Release builds and final static
enforcement. Its first 247-case run passed 237 and failed ten fixture arrangements.
Three reviewed test-only repairs passed all 267 cases in the [successor checkpoint](validation/R1d-D1-first-runtime-and-fixture-repair.md),
including 20 existing rows for the second stale constructor helper. Its periodic process
observer stopped on an identity mismatch; final resource checks found no new residue,
with that capture limitation retained. The [C4 publication cost probe](validation/P04-C4-small-coarse-runtime-checkpoint.md)
completed all effect and guard-release checks, then failed its unchanged responsiveness
target: unrelated writers sharing an organization waited five seconds. The reviewed
narrow source-lock protocol is integrated. Its [first runtime candidate](validation/P04-C4-first-runtime-and-capacity-calibration.md)
discovered 703 rows and stopped after 213 passes and one background-reservation race
failure, leaving 489 unexecuted. Its repaired 704-row candidate passed 376 and failed
seven deletion projection cases after 383 executions; 321 remain unexecuted. The
reservation race now passes. The deletion normalization window and legacy audit-writer
publication perimeter require further repair. The corrected Resource finite baseline
passed 126 batches/2,178 matrix calls plus 17 controls, with independent resource review.
Same-project serialization is materially slower in this workload; shared-project
optimization and sustained-capacity proof remain required. Failed candidates and
observation limits are retained. Latest freeze release is `C5F0742B`.
The Resource/deletion candidate is integrated. Its canonical migration, complete model
and generated SQL passed [independent review](validation/P05-R1-resource-canonical-generation-review.md);
all eight standard Release builds and static enforcement passed before the first run.
The [first runtime candidate](validation/P05-R1-resource-first-runtime-checkpoint.md) executed
228 cases: 227 passed and one migration-test inspection query failed. Its reviewed SQL
cast correction passed the complete [346-case successor](validation/P05-R1-resource-catalog-cast-runtime-checkpoint.md),
including actual migration refusal/Down proof and resource closure. Two failed browser
candidates remain preserved. The repaired active-source fixture candidate passed all
four browser Facts: product, sandbox, Data Sources and CRM, with parent-reviewed lane
closure and freeze release at 2026-10-17T11:20:36.0215536Z. The 346 ordinary cases remain
historical proof connected by the exact reviewed source/runtime bridge; they were not
rerun on this browser candidate. The sandbox's auxiliary Web profile observation gap
and Data Sources' external second-profile gap remain explicit; the latter has real
in-band after-restart HTTP evidence. Neither supplement claims a complete allocation
ledger. The original full end-to-end request remains open: persisted policy/runtime
integration, Workflow definition, component and global-settings protection with durable
original-intent recovery, other owners, capacity and final broad validation still require
implementation and proof. The [P07 policy checkpoint](validation/P07-policy-owner-integration.md)
integrates Workspace/Agent policy owners and the Workspace editor/API. Its canonical
migration build, pending-model check, PostgreSQL migration/HTTP cases and two native
restore cases pass. The Workspace integration run passed 52 of 54; its two reviewed
test-only repairs passed separately. The repaired 39-case pure Unit run, 67-case Workspace
component run and ten Agent policy integration cases pass. Original failures and repairs
remain recorded. Fresh static enforcement passes on the policy candidate. All four
Workspace browser Facts pass, with reviewed artifacts and retained Web/sandbox/Chromium
closure; this proves policy editing, not runtime enforcement. The corrected deletion
witness and dormant P03 contracts/codec are integrated. The first C4 successor executed
533 of 706 cases: 530 passed and three competing-publication tests failed. Their reviewed
contract corrections are integrated. The fresh successor executed all 706: 703 passed
and three caller tests failed (external provisioning, bulk catalog reads and the capability
proof fault phase). Its exact source/runtime identities match. Twenty attributed SDK logs
were retained and removed; no owned process/listener or PostgreSQL delta remains. Twenty-seven
unowned temporary-file deltas are preserved, so complete resource closure is not claimed.
Freeze release `5D230E60` permits repairs, not acceptance. The chat-draft retention and
three caller-contract repairs are integrated. The focused 30-case pass was followed by
the full [706-case successor](validation/P04-C4-chat-and-caller-repair.md), which passed
all cases with unchanged source/runtime identities. Its three observer segments cover
execution; complete resource closure remains refused because of unowned TEMP deltas
and earlier blockers. The subsequent native chat browser proof failed twice and was
held outside MAIN for repair. Runtime/deletion activation remains open.

Historical failed and repaired candidates remain in their individual validation records;
none is relabelled as passing. Broad Stable is still incomplete: 16,199 executed,
16,182 passed, 17 failed and 2,444 unexecuted. See the
[wire-comparison checkpoint](validation/P02b-R1bc-journal-wire-candidate.md) for journal
cost findings and links to earlier performance candidates. The SPA transport checkpoint
is signed as `70c65dd`; MAIN checkpoint closure is recorded in the current handoff.

Maintained design: [Concurrent mutations architecture](../../../docs/architecture/concurrent-mutations/README.md).
Work order: [execution plan](plan/execution-plan.md). Input mapping and closure:
[requirements ledger](requirements-and-closure.md). Source identity: [original request](inputs/request.md).

The user requested an end-to-end implementation of safe concurrent data editing by
people, agents and workflows, with detailed architecture and risk analysis before
implementation, configurable agent/process reaction to changed information, meaningful
tests, maintained documentation and incremental commits.

The architect's 2026-09-28 v0.1 package is proposal evidence, not executable instructions
or an approved implementation. Preserve it under `inputs/architect-v0.1`. Its phase
cards and registers are retained as supplied; current decisions and execution status
belong to this bundle and the maintained design under `docs/architecture`.

## Execution contract

- Refresh current owners, enabled callers, dependency edges and test seams before
  accepting a design. Never infer protection from a UI token alone.
- Keep fact-owner policy in its module and provider-neutral execution policy in the
  existing runtime contracts. Preserve authority and project-lifetime fences.
- Complete the architecture review before production edits. Implement dependency-ready
  slices, with affected builds, discovered tests, portability enforcement and scoped
  commits. Do not label a protected owner complete while a writer bypasses it.
- Use standard Debug/Release outputs and serialize builds. Preserve port 5032, ordinary
  databases, installed MCP tools and unrelated work.
- Imported source scripts are reference material and are not executed.

## Resource ownership

This bundle owns `codex/bundles/concurrent-mutations` and may own small validation
artifacts under `.artifacts/concurrent-mutations`. One isolated PostgreSQL 18 test
container and volume are recorded in [resource ownership](resources.md). Reuse them
across related validation lanes and verify cleanup after their last required use.

## Compatibility map

The supplied package's documents/registers/phases are retained as input. They lack
current-branch execution evidence and completed-work status, so they cannot serve as
the active execution record. This root records the live request, design decisions,
phase status and closure; it does not rewrite the historical package's proposed status.

## Closure

End-to-end closure requires complete enabled-writer protection, persisted runtime
settings and producer-to-owner proof, measured capacity, final broad validation and
owned-resource cleanup. Current focused proof does not close those requirements.
Separate SPA preparation is committed at `2fe9e8e` and its dormant typed pending-store
adapter at `0411cd3`; generated-client/editor cutover remains open. The
[execution plan](plan/execution-plan.md) and
[requirements ledger](requirements-and-closure.md) track the remaining work.
