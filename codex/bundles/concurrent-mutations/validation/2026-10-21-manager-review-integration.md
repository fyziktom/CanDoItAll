# Workspace and Process manager review integration

Status: qualified; 172 cases pass. All three failed candidates are retained.

Implementation checkpoint: signed commit `528d29dbd`.

The prerequisite is signed commit `f083ec3d0`, with 147 passing owner cases.
The [reviewed integration design](../plan/P07-current-manager-actions.md) retains
ownership in Core, Workbench and Process Application/Runtime. Existing workspace
facades bind activity and cancellation, then delegate to the qualified owner and
actual saved-effect runtime. No new provider turn is prepared.

Process Application now admits a fresh review only after checking the retained
waiting result, original single-activity input, displayed local budget/decision and
current child. Runtime completion shares its pure waiting-state checks with this
admission. Receipt replay still precedes fresh-state validation.

Workbench binds the original parent, complete Process request and stable trusted
manager identity into the durable Agent intent. It requires an explicit current
write observer and an operator capability without a new Project admission.
Accounting reloads current state after the Agent lease closes. The historical
completion receipt and latest runtime view remain separate in the result.

The proposed 140-case selection has eight lanes: four actual approve/reject cases,
ten integration boundary/replay cases, 26 Core owner cases, nine existing Process
completion integrations, 38 pure completion cases, 27 completion-owner cases,
23 original single-activity cases and three root-fence cases. Discovery confirmed
all 140 cases before execution.

The first standard Release build passed for Runtime, Core, Workbench, the
AgentFramework module, Application, Unit and Integration, with source unchanged
through compilation. The initial `pwsh -File` array argument selected no projects;
it is not build evidence. The actual recorded build used a native PowerShell array.
Existing dependency warnings remain.

The first portability enforcement found one added `link-reparse` match and no stale
findings. It names the existing Process authority's `linkTarget` parameter, passed
as null to avoid a new Project admission; it performs no filesystem link operation.
That single source fingerprint was reviewed and added to the baseline. A regenerated
scan and final enforcement without `--write-baseline` passed with 15,664 reviewed
findings. Documentation and whitespace checks passed.

The bounded before/after architecture comparison uses
`snap-20261021143730-15074043` and `snap-20261021151819-15074043`.
All project references, eleven existing cycles and 234 collector diagnostics are
unchanged. The 27 added type dependencies connect the new Workbench owner/contracts
to their existing Core and Process ports. One informational finding covers the
nine-member Process review coordinator; its admission and receipt-first completion
share original-input and selection validation for the same child. The existing
workspace owners only acquire thin activity/profile/cancellation delegation.
No new partial file or project reference was added.

The candidate is `.artifacts/concurrent-mutations/validation/c4-manager-review-integration`.
It reuses standard Release outputs, existing fixture ownership/families and the
isolated PostgreSQL server. Final source/runtime identity, bounded architecture
comparison, portability enforcement and observed resource reconciliation are required.
Public HTTP/read views, actual executor activation, parent refresh/cancel,
Workflow/settings/SPA and sustained capacity remain outstanding.

The first four positive cases failed, so the remaining 136 cases did not run.
Approve reached Process completion but the workspace operation attempted success
from `ResolvingSession`. Reject reached truthful closed work but the Process reader
only recognized cancellation represented by `ExecutionState.Completed`; existing
pending-approval cancellation uses `ExecutionState.Failed` with `RunOutcome.Cancelled`.
The repair reports `PersistingResult` before accounting and recognizes both existing
cancelled terminal forms, keeping exact work, effect and outcome validation.
Two focused outcome cases cover cancellation with and without a committed effect.

Source/runtime identities remained unchanged throughout the failed run. The observer
covered its one execution command; reconciliation found zero surviving observed
processes, database-name changes or retained temporary entries. The 111,439-byte
failed proof has SHA-256
`A8EF0705D917763B758EFD5EBC0706FE63DD610612BAB9D976DE88D1D8765774`.
The preflight baseline-pin repair is included; it happened before tests/resources
started and did not change frozen source or binaries. Historical cleanup denials remain.

The successor is `c4-manager-review-integration-repaired`. It repeats the 140 cases
and includes the full Process outcome classification suite affected by the repair.

That successor passed all 32 outcome classifications and all four actual
approve/reject/replay cases. Its ten boundary cases passed four and failed six;
the remaining 126 cases did not run. The four passing boundary cases prove renewed
manager credentials, lost completion acknowledgement, revocation and caller
cancellation after the effect.

Four negative cases exposed exception classification at the module boundary:
Core correctly normalizes unknown owner-read failures to unavailable, including
the Process-specific pending-selection refusal. Workbench now unwraps only this
known typed Process refusal, preserving its stack and code. Core acquires no
Process dependency and its general read-failure policy stays unchanged.

Two sibling cases seeded a `Planned` sibling, whereas the actual scheduler selects
`Pending` steps. Their setup now persists the sibling as `Pending` in its existing
budget-reservation commit, then uses the real scheduler to advance it to `Ready`.
The historical completion must retain `Pending` while the fresh replay view has
`Ready`. This is a fixture correction; no scheduling rule was relaxed.

The failed successor's source/runtime identities stayed unchanged and its observer
covered all three commands. Reconciliation found zero new processes, database names
or temporary entries. Its 168,985-byte proof has SHA-256
`0FC6DD600F77C9B0351CCB93880641BFBC0D381C7C2477817DD44A578E6BA0C4`.
The before/after integration architecture review remains, supplemented by
`snap-20261021154007-15932d1a` for the two repaired owners: no new dependency
topology, finding or cycle. Collector occurrence weights differ with scope and
are not used as evidence of dependency changes.

`c4-manager-review-integration-boundaries` repeats the 172-case selection, running
the six repaired boundary cases first. All preceding failures remain retained.

That candidate passed all four explicit Process-refusal cases. Both sibling cases
reached successful completion and replay, but failed the assertion that replay
must return the historical sibling runtime state. Inspection of the actual
`EfProcessRuntimeUnitOfWork.FindCompletedCommandAsync` showed that it reloads current
runtime state while retaining the original decision and budget receipts. The
earlier historical-runtime statement was incorrect. The test now proves unchanged
accounting receipts, the original completion observation's `Pending` sibling and
the replay/current observations' `Ready` sibling. Production is unchanged.

All seven Release builds, 172-case discovery, full portability enforcement
(15,664 reviewed findings), documentation and whitespace checks passed. The
Workbench correction snapshot `snap-20261021160330-58935274` adds no dependency
topology, findings or cycles; its six scoped cycles match the preceding snapshot.
Source/runtime identity remained unchanged across the six executed cases:
source `79237F8F7FE4F23E0EA7DD1AA039548E8D625AA2970D8965CC29ADE476D9C9E7`,
runtime `EAEBFD4DEBA55726D37B709C34C072FD2BE60EF869320E67B52FAED124CFFCA2`.
The observer covered its one test command and reconciliation found zero surviving
observed processes, new database names or retained temporary entries. The failed
103,162-byte proof has SHA-256
`7F2FDAF3B407DC3BD62160106956A499F22F476259AC3C2A3AF413DA0906112B`.
Its four passes and two failures are retained; 166 cases were unstarted.

The test-only successor is `c4-manager-review-integration-qualified`. It keeps
the 172-case selection, splits the SQL-heavy review groups to stay within the
existing 900-second command bound, and starts with the two corrected sibling
cases. No production behavior or timeout is changed. The API continuation remains
source-only in its existing isolated checkout until this prerequisite qualifies.

The qualified successor passed all 172 cases in 13 lanes. Standard Integration
Release outputs were refreshed for the test-only correction, and source stayed
unchanged during compilation. Comparison with the preceding candidate confirms
all 5,393 production source inputs and 1,137 non-test assembly copies are identical.
All earlier seven-project Release builds and architecture observations therefore
remain applicable. Full portability enforcement passes with 15,664 reviewed
findings; documentation and whitespace checks also pass.

The complete frozen inputs contain 9,868 source files and 4,566 runtime files:
source `43DC001A2FC67ED78F91DC54D4CA8EA4DE50265E8533656E01F8667B45CFEA01`,
runtime `585A717506096A4FB09E0BFCBA0B9039B31826A94E4286F63931EF562C9F460A`.
Final verification found no source or runtime changes. Observation covered all 13
commands over 1,127.0644032 seconds. Reconciliation found no surviving observed
processes, new database names or retained temporary entries.

The local 281,941-byte qualified proof has SHA-256
`40F2E0D9AA6ADA6F5AF0EEDC5E1F3A1B46679D64030C7715C81FF2D60D44A56A`
and no secret findings. The implementation commit was created while this candidate
ran; file identities stayed unchanged. The proof remains local under the existing
ignored proof directory. Historical policy-denied cleanup remains unresolved and
is not cleared by the zero new deltas.
