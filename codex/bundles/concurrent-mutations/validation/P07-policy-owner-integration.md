# P07 policy owner integration

Status: implementation checkpoint in progress. Persisted settings are being validated;
this record does not activate Agent, Process or Workflow execution policy or close P07.
The current repository has the Workspace V2 owner, Agent override owner, native Workspace
editor and V2 HTTP boundary. Existing Workspace V1 writes preserve the stored policy.
Ordinary Agent catalog writes preserve the reserved policy metadata; explicit policy
replacement has its own original-catalog expectation.

## Source and migration

The parent transferred 30 reviewed owner/contract/test sources using the inventories
`2504CCCE`, `9FA5B49A`, `56907108`, `06EFBB81` and `C3D82B7A`. The exact transfer and
predecessors are retained under
`.artifacts/concurrent-mutations/validation/p07-policy-integration/`.
The separate Workspace UI transfer applies the 14 reviewed `C2219F35` sources with
the `F53ED5D7` help-text overlay and exactly two rendering-register entries. Its plan
is `BB588129AFDB406EDB3DBC668B2810C32BC5C3B16B946FC6B35D9C50D251BD54`; retained
predecessors and terminal transfer are under `validation/p07-workspace-ui-transfer/`
within the same artifact root. Transfer is not runtime proof.

Canonical migration `20261017130214_AddWorkspaceDecisionDataPolicy` was generated from
the canonical PostgreSQL project after its Release build passed. The complete generated
scaffold, designer and predecessor snapshot are retained before modification. Its only
model delta is nullable `Workspace_Settings.DecisionDataPolicyJson` of type TEXT and
the named constraint accepting null or 1–1,024 encoded bytes. There is no default,
backfill, revision update or unrelated model change.

The migration uses the previously reviewed guard SQL: exact canonical predecessor,
schema/column/key/index checks, ReadCommitted, bounded five-second lock acquisition,
lifecycle exclusion before Workspace exclusion, and post-DDL verification. Unexpected
row security on Workspace, lifecycle or migration history refuses. Down permanently
refuses; rollback requires the documented coordinated restore procedure. Independent
source review confirmed the embedded SQL matches the reviewed Up and Down exactly.

| Final source | SHA-256 |
| --- | --- |
| Migration body | `F85A162EDAAFA1C2E1BEEFFFA8D603FBF26EC92B0EF76185704DDC249B8BCB34` |
| Designer | `091BC5003D88523A3396E596C179BE7C1D6EABFB8A3EEAD17AC4E1BC991A30E2` |
| Snapshot | `44D56ACE6B3C95D1A3ABCCC434B13A1F212A3956045575FB13FAA180453CB702` |

The final canonical Release build, EF pending-model check and idempotent Up script
generation passed. EF reported no pending model changes. Existing dependency-security
warnings and the EF tool 10.0.3/runtime 10.0.4 version warning remain in the logs; these
commands do not prove the migration applied to a database. The policy migration tests
bind the actual generated ID and add lifecycle/history row-security refusal cases to
the existing drift loop.

## First focused Unit checkpoint

The affected Unit Release build passed. Build-backed discovery found exactly 39 Facts
across the policy resolver/override codec, Agent policy contracts/metadata, Workspace V2
contracts/outcomes and dormant P03 original-state/completion contracts and codec.
Execution passed 38 and failed one:
`AgentDecisionDataPolicyMetadataTests.Acknowledgement_requires_the_exact_original_identity_policy_and_next_revision`.
The invalid negative-replan assertion expected exact `ArgumentException`, while the
existing command deliberately validates through the override codec and throws
`JsonException`. The two accepted-acknowledgement mismatch assertions remain
`ArgumentException`. Independent source review accepted the single test-only correction.
The failed TRX and original test source are retained. After the single assertion repair,
the Unit Release rebuild, fresh 39-case discovery and all 39 executions passed. The P03
contract/codec assertions pass without proving a durable deletion owner or participant
release gate.

## Workspace component checkpoint

The Integration and Web Release builds passed. The first 67-case Workspace component
run passed 65 and failed two boundary assertions: their lightweight-assembly allowlist
omitted the existing pure SharedKernel dependency used by Workspace.Contracts. The
repair adds only that assembly and an explicit negative traversal case proving that an
infrastructure dependency reached through SharedKernel still fails. SharedKernel has
no project or package references. The failed TRX and predecessor are retained. The
component Release rebuild, fresh discovery and all 67 executions then passed.

## PostgreSQL, HTTP and native restore checkpoint

Build-backed discovery found 54 ordinary Workspace integration cases. Their first run
executed all 54: 52 passed and two failed. The HTTP capability test incorrectly used the
bootstrap administrator for its initial business-settings read; that principal has only
session/access-management capabilities. Moving the read after the existing reader login
preserves the actual reader/write refusal assertions. The lock-timeout test expected a
bare PostgreSQL exception, while the installed EF provider deliberately wraps transient
SQLSTATE `55P03` in `InvalidOperationException`. The correction requires that exact outer
type, exact inner PostgreSQL type, SQLSTATE and transient flag. No production permission
or migration behavior changed. After the Integration Release rebuild and fresh discovery,
both repaired cases passed against PostgreSQL. This is a 52-pass first run plus two
passing repaired cases, not a fresh passing 54-case run. Original failures are retained.

The ten Agent policy integration cases passed after corrected build-backed discovery.
An earlier discovery used the wrong namespace and selected zero; it is retained and is
not accepted as coverage. Both native policy restore cases passed using the existing
PostgreSQL 18.6 clients: configured policy bytes remain preserved with explicit target
reactivation, and a legacy whole archive retains values/revisions with explicit policy
absence. The client paths, binary hashes and actual bounded command records are retained.

The first resource interval observed 31 processes over 839 polls; the repaired two-case
interval observed ten over 587 polls. Both reconciliations found no remaining exact
process/listener identities, seven-family temporary-root deltas or PostgreSQL deltas.
The repair observer recorded one completed test PID reused by an unrelated PowerShell
process; its distinct start identity was preserved and never treated as test ownership.
The corrected Agent discovery has its own bounded terminal record outside the first
observer's fixed command list. Sampling can miss short-lived children and does not prove
a complete allocation ledger or cleanup of anonymous SDK temporary files outside those
seven families. The pre-existing blocked database remains untouched with zero clients.

## Static and browser checkpoint

The complete proposed-source portability scan, reviewed baseline refresh and final
enforcement without `--write-baseline` passed before the last test/browser source edits.
The reviewed delta contains four generated-model property-name findings and two changed
witness fingerprints, with two stale fingerprints removed; it does not suppress a
portability defect. Scanner tests, secret-scanner tests, rendering tests/register and
documentation checks passed. After the final source edits, a fresh complete-source scan
and enforcement passed with no further baseline refresh. Git diff checking found and
then accepted removal of one extra final blank line in WorkspaceOwnerContracts; the
predecessor is retained and the post-build change contains no code-token differences.

The Workspace browser fixture now contains the original three Facts and a fourth proving
all six persisted policy controls, reload, stale-editor refusal and explicit reconciliation
without losing the original draft. All four use 1920×1080 viewports. Its Release build
passed. Exact build-backed discovery and execution passed all four Facts. All 16 TRX
counters, labels, definitions and unique execution identities match; the retained TRX is
`EA7B4FAD5CFBB85B5C5A7E738D3FBBB4B5833BA44BC9306D05FEE4B3029B9A3D`.
The parent reviewed the three policy screenshots, production/sandbox defaults and the
actual policy JSON. Conflict keeps the original notes/draft, separates current saved
values and disables save; explicit reconciliation persists the selected policy. The six
controls are below the viewport in these screenshots; their values are established by
the browser DOM assertions and JSON, not complete visual field coverage.

The observer positively correlated the owned Web and sandbox listeners with retained
process identities and captured the Web profile/workspace binding. All 21 retained
Execute descendants, including Chromium, exited; both listeners disappeared. The declared
temporary-root and PostgreSQL baselines have no delta, and the positively identified
fixture root is gone. Two attributed SDK logs were retained before removing their exact
original leaves. All 18 output artifacts are preserved with hashes alongside the 14 prior
artifacts. Sampling is not an exhaustive OS allocation ledger. The first parent-review
helper failed on a PowerShell argument continuation before writing acceptance; its logs
and source remain, and the corrected helper completed without rerunning application tests.

Final Close rechecked the frozen source, runtime and served assets and passed. The parent
released the source/runtime freeze in record `98FE45A4`. The complete source manifest has
8,955 inputs; browser runtime has 3,790 files; served assets have 103 roots and 28,417 files,
including long paths. These controls prove persisted editing only, not runtime policy
enforcement. Evidence is under `validation/p07-workspace-browser-validation/` in the
concurrent-mutations artifact root.

## Required closure

The policy-control browser proof is complete. After subsequent protected-source edits,
run the final portability scan, review every added/stale finding, refresh only intentional
baseline deltas, and require final enforcement and rendering/documentation gates.

The corrected C4 deletion witness is integrated from inventory `8D237295`; its prior
seven failures and the full 706-row successor are separate validation obligations. The
successor discovered all 706 rows and executed 533: 530 passed and three failed; 173
remain unexecuted. The repaired deletion cases passed. Three older publication tests
still expected competing stale callbacks both to succeed; their direct prepublication
conflicts require deterministic contract corrections, without callback replay or witness
weakening. The failed TRX is retained as `DA0C4EB8`. The actual chat caller also has a
separate newer-typing/detached-draft loss defect under repair; test changes do not close it.
The C4 observer saw 91 processes over 2,102 polls with no read races. No exact owned
process/listener or PostgreSQL delta remains; 19 attributed SDK logs were retained and
removed. Six anonymous temporary-file changes have no proved ownership and remain
preserved, so complete C4 resource closure is not claimed. Its final source/runtime
identities match the frozen candidate. Application failure and resource limitations remain
explicit in `validation/c4-narrow-owner-deletion-repair-validation/`.
Legacy audit/process-lease writer coordination remains an activation blocker. Settings
alone do not prove actual snapshot selection, current-input publication, approval
binding, effect identity, bounded replan or complete Agent/Process/Workflow caller
cutover. Resource's accepted finite baseline and remaining owner work retain their
own closure requirements. Commit signing remains blocked by the existing pending
GPG request; no unsigned substitute or second queued commit was issued.

### Fresh C4 publication-contract run

The deterministic competing-publication corrections passed in the fresh
`c4-concurrent-publication-contract-validation` candidate. All 706 discovered cases
executed: 703 passed and three failed. The first nine lanes passed all 533 cases; the
173-case caller lane passed 170. Its retained TRX is `D4D7E156`. Failures concern the
external provisioning parallel response, catalog policy query-count expectation, and
capability proof fault injection. They remain failures pending reviewed repairs and
new execution; this is not a passing C4 gate.

Final identity verification matched 8,955 source inputs (`972D28C4`) and 4,340 runtime
files (`BA987952`). All twenty commands and the observer terminated. The observer
retained 92 process identities over 2,821 polls with zero read/parse faults; seven
completed-PID reuses were classified as unrelated. Twenty attributed SDK logs/39,671
bytes were retained, hash-verified and their exact original leaves removed. No exact
owned process/listener or PostgreSQL delta remains. The shared temporary directory has
27 unowned changes, preserved without deletion or ownership inference. Parent review
`5D230E60` releases the source freeze for repairs while rejecting application acceptance
and full resource closure. Earlier failed candidates and blocked cleanup remain recorded.

The [chat and caller repair](P04-C4-chat-and-caller-repair.md) subsequently passes all
30 focused cases, including these three failures and sixteen new chat-retention rows.
It does not replace the failed 706-case candidate or prove the remaining runtime gates.
