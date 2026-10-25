# Waiting Process review completion

Status: qualified prerequisite with all 331 final cases passing; no executor activation or public manager
action is enabled by this slice. The [reviewed design](../plan/P07-waiting-result-completion.md)
follows the qualified original requirements and shared execution clock.

Runtime owns a pure terminal transition from the retained NeedsManager result. It
requires the original completed claim and attempt, no open claim even if expired, and
the latest original result by sequence. The result digest binds the original child,
both result keys, closure and measured work. Existing history remains unchanged.
Event/outbox construction, sequence allocation and terminal-run aggregation are shared
with ordinary results; their previous implementations are removed from Engine helpers.

Application reads one indexed original input and the existing original-child selection.
It refuses unsupported compiled obligations and derives the terminal result from the
registered owner's closed outcome. Outcome reading and protocol validation now have
one shared owner used by both result coordinators. Review command identity binds the
displayed expectations, immutable original child/input and actor. Durable receipt
lookup precedes live outcome and current-phase checks; changed work cannot borrow an
old result digest. Pending or unknown work remains reserved.

Persistence commits the runtime result, child closure, work settlement, events and
outbox under the existing root transaction. The new waiting-completion check reads a
descendant's current root status under that lock. A cancelling root refuses the whole
commit. For completion on the root itself, its original state remains protected by the
existing comparison; the just-flushed terminal state must not reject its own commit.
There is no new table, project dependency, lock or background polling.

The selected source declares 230 cases: 38 pure transition/aggregation cases, 27 review
coordinator cases, 23 original-contract admission cases, 30 ordinary accounting cases,
75 Runtime Engine cases, nine result-snapshot cases, seven module-registration cases,
nine actual Project publication/review cases, three descendant/root transaction cases
and nine ordinary Project result cases. Build-backed discovery matched all 230 cases.

The new integration scenarios use the real accepted-source producer, original Agent
provider execution and saved publication. They cover exact-once work and event pairs,
pending approval without refund, same/different-key races, rollback after flush,
cancellation or a changed decision after outcome reading, and lost acknowledgement
followed by a full host restart with further outcome reads forbidden. The descendant
root fence also has a direct real-PostgreSQL runtime/accounting fixture.

Runtime, Application, Persistence, Processes module, Unit and Integration Release builds pass.
Two initial Application import attempts and the first Integration declaration errors
remain in command records. Full portability enforcement passes with 15,663 unchanged
reviewed findings; tooling, rendering, documentation and whitespace checks also pass.
The source/runtime freeze contains 9,853 source inputs and 3,976 runtime files.

The scoped architecture comparison uses `snap-20261021090234-c953f4c6` and
`snap-20261021112602-b5477474`. It retains the six project references, zero scoped
cycles and all 295 findings unchanged. Eight cohesive types and 35 members are added;
Runtime Engine loses its terminal-aggregation and sequence methods, and the old
result coordinator replaces its outcome-read implementation with the shared owner.
No new Runtime partial is added. The same 189 existing NuGet/MSBuild diagnostics
still make the collector report blocking errors; direct affected builds pass, and the
structural comparison is not a claim that those diagnostics or the whole graph are clean.

## Initial execution and correction

The [immutable first attempt](../proof/2026-10-19-planning-authority/c4-process-review-completion.json)
retains 209 passing Unit cases. All seven Unit commands finished within the successfully
observed interval. The nine-case real-workspace lane then reported eight failures because
the new fixture constructed its publication journal without the registered background-source
policies. Authorization correctly refused; none reached the new terminal completion.
The lane's 900-second deadline interrupted the final restart case during initial startup.
That lane produced no TRX; its eight console failures and interrupted case are recorded
separately from the 209 reported passes. The remaining three root-fence and nine ordinary
Project cases did not run.

The observer independently stopped on a Windows CIM timeout. Its failure and incomplete
coverage are retained; this attempt does not qualify Integration or resource closure.
Source and runtime were unchanged throughout execution and verified afterward:
source `FC3E4A42DB338AD0C948E3456B8723976AC350922F99D32A23C20943A634D238`,
runtime `C32DF8BE9E8A8BBA6DF186197625E41794D9F18A81F9008393BA5581E3EAB80E`.
The 284,830-byte proof has SHA-256
`855CF0F6E57D010EF788F7165953991B44F11C417B3B0224CC8BA4D76DB5E95B`.

The test-only correction supplies the real registered `IAgentToolBackgroundSourcePolicy`
instances to the fixture journal. It preserves production authorization and the controlled
clock. The successor selects all 21 Integration cases, splitting the heavy workspace
fixtures into explicit one-to-three-case lanes under the unchanged 900-second command
deadline. Its two positive cases gate the later race/recovery lanes. The prior Unit passes
require an exact unchanged production/Unit identity comparison before reuse.

The successor observer samples processes once per second and bounds both process and
listener CIM reads to five seconds per attempt. At most three attempts are permitted;
every failure is retained, exhaustion fails observation, and partial failed snapshots
are discarded. Three deterministic checks prove transient recovery with exact output,
bounded exhaustion and immediate propagation of non-CIM failures.

The final interrupted test left one database and its exact pending-start workspace.
Automatic approval review rejected their cleanup with “blocked by policy”; neither
resource was retried. Exact identities and required manual cleanup are recorded in
[resources](../resources.md). A later zero-delta check must not erase this blocker.

## Publication scope correction

The corrected-policy candidate built and passed static enforcement but failed both
positive publication cases; the other 19 integration cases were not started.
Its observer completed and reconciliation found no new database, process or temporary
path delta. The retained failed proof is `c4-process-review-completion-verified.json`,
54,522 bytes, SHA-256 `16829F82C730B9EEAE45E2632DF14544E571D9B6AE5E029B3B1FF6BB2BE978CC`.

The diagnostic candidate identified `SourceChanged` in both snapshot modes. It also
passed static enforcement and observed resource reconciliation, with 19 cases
unstarted. Its failed proof is `c4-process-review-completion-final.json`, 51,382 bytes,
SHA-256 `31E7D3A20533B5456575856BE55B61A9CDD562E2797FFE779067C34595D287F4`.

Source review found a production guard comparing organization catalog scope to
Project execution context. The [reviewed correction](../plan/P07-inherited-publication-context.md)
removes that faulty comparison while preserving exact original metadata and every
other publication check. It adds Project-context positive and context-tampering
coverage. A new Core build invalidates the earlier binary-only unit proof bridge;
the affected validation is being repeated. Neither failed attempt qualifies the
waiting-result integration. Earlier blocked cleanup remains unresolved.

The publication-context candidate completed all 331 selected cases: 328 passed,
including both actual Project-context publication modes, waiting completion,
rollback, restart and ordinary result regression. The three root-fence cases
failed before their assertions because the capture fixture created a separate
transaction owner without the required canonical database profile. The correction
uses the already configured owner for that reservation; the production guard is
unchanged. These three cases gate the next attempt.

Its resource observer failed independently when a concurrently rewritten command
record was temporarily empty. JSON parsing returned null outside the read-error
handler. The corrected reader explicitly refuses incomplete shapes inside that
handler; six malformed/incomplete reads and a complete identity round-trip are
checked before use. Process/listener timeouts, sampling and per-command deadlines
remain unchanged.

Source/runtime verification passed for all 9,854 source and 4,320 runtime files.
Final read-only reconciliation found no known surviving processes, added database
names or new entries in the three temporary families. Continuous observation was
incomplete, so this is not a resource-closure claim. The failed candidate remains
in `c4-process-publication-context.json`: 343,726 bytes, SHA-256
`54A64C2AEB2DB2C5AAB99573EFF615D89342CDB8642E80B94FB45AAA4C7E2B6F`.
All 331 cases will be repeated against the corrected fixture and observer.

## Qualified final candidate

`c4-process-publication-qualified` rebuilt Integration in standard Release after the
test-only root-profile correction. Build-backed discovery matched all 331 cases and
their declared methods. The root-fence gate passed first; all 20 execution commands
then passed, including 51 inherited saved-effect cases, 15 interactive saved actions,
18 inherited publication snapshots, 17 Core continuation/snapshot cases, 209 Process
unit cases, nine real waiting-result completion cases, three root-fence cases and
nine ordinary Project result regressions.

The Core publication-scope correction adds no type, member, lock, query or dependency.
Its scoped before/after snapshots retain 784 types, 5,823 members, 225 findings and
189 existing collector diagnostics. Process architecture retains the earlier reviewed
six project references and zero scoped cycles. Direct affected builds remain the
compilation evidence; the collector diagnostics are not reported as clean.

Final portability enforcement passes with all 15,663 reviewed findings unchanged.
Documentation, rendering register and whitespace checks pass. The checkpoint's
baseline delta comprises eight generated-designer property-name findings and four
intentional existing ordinal-insensitive executor/tool/operation identifier comparisons;
none changes filesystem path comparison policy.

All 9,854 source and 4,320 runtime files remained unchanged throughout execution.
Source manifest SHA-256:
`4D472803D68AE26BEB491D08E374910E2E3F9BD8350173D04BEE6F2F676C444F`.
Runtime manifest SHA-256:
`93725CFC9F9D40A6CAED68296291CEC4FA4A9C55F282A12800D606FC0096A84E`.
The repaired observer completed and covers every test command. Final reconciliation
found no surviving observed process, new database name or retained temporary entry.
Earlier denied cleanup remains a separate blocker.

The locally retained [immutable proof](../proof/2026-10-19-planning-authority/c4-process-publication-qualified.json)
is 470,702 bytes, SHA-256
`90E498284F80AEFBE9C0284E47F8543A1677249886898CBCEA8A0365C29670D5`;
secret review found no issues. This qualifies the waiting-result prerequisite and
allows the [manager action design](../plan/P07-current-manager-actions.md) to proceed.
It does not activate the Process executor, Workflow or SPA settings.
