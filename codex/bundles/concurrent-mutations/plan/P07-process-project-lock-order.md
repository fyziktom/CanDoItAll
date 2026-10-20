# Process initial admission and Project/root lock ordering

Status: implemented and validated with 194 distinct passing selected cases across an
exact test-only repair bridge. The selected Agent snapshot prerequisite
is committed at `1d91b061d`; inherited business publication remains closed.

## Observed reverse acquisition

EfProcessRuntimeUnitOfWork acquires every affected root advisory lock before
CommitCoreAsync. For a new run, ValidateProjectAdmissionAsync then invokes the Project
admission policy. ProjectProcessAdmissionPolicy calls RequireUnderMutationGateAsync,
which acquires the Project mutation advisory key. Project-owned executable definitions
also enter that Project gate through their authoring admission policy. Launch authority
validation may additionally take key-share locks on original source Project lifetimes.

The task-plan publisher acquires Project mutation keys before its proposed enlisted
Process root fence. A subprocess/new initial commit sharing an existing root can
therefore hold Root while waiting for Project, against a publisher holding Project and
waiting for Root. Do not activate the publisher with this reverse path outstanding.

The order remains catalog authority, all original Project gates in one deterministic
set, then all Process roots in their existing deterministic set. A root-only runtime
update does not acquire Project gates. No new lock implementation or project reference
is needed; use the existing coordinated transaction and owner-defined keys.

## Preserve recovery semantics

Do not move the existing current-lifetime validation ahead of the root lock. CommitCore
first returns an already completed command's original receipt; only a new state reaches
Project lifetime, prepared authority and executable validation. An accepted launch must
still recover its original receipt after its Project is deleted or authority expires.

Add a lock-only operation at the existing Process/Project admission boundary. It checks
structural identity and the owning profile, enlists in the existing transaction and
acquires Project keys, but does not require current Project rows, lifetimes, permissions
or authoring state. Existing validation stays in CommitCore after duplicate lookup.
An unsupported relational policy must fail explicitly rather than silently skip gates.

## Complete initial gate set

Resolve the small, bounded set from original inputs before taking root locks: the new
run's Project admission, any Project-owned executable closure, and the prepared launch's
original target/source Project admissions. Inspect the prepared tool source as well;
its parent dispatch can carry a distinct source Project. Link-target validation must
reject a different target before it reaches an undeclared Project lock. Global executable
definitions have no Project gate. Conflicting lifetimes for the same Project must refuse
the request rather than be deduplicated into an invented choice.

Collect the set in the Process persistence admission helper using neutral Runtime and
Application values. The owning Projects service maps it to its existing keys in one
acquisition, so multiple sources cannot introduce different per-policy orderings. Keep
the complete prepared document out of the retained helper result; retain only the small
typed Project set and the existing launch authority lease/source root.

Enter the existing ReadCommitted/coordinated transaction, acquire those Project gates,
then take the existing root advisory locks and execute CommitCore. Preserve the InMemory
test path, exact launch identity checks, current authority checks, cancellation, rollback,
source root ordering and duplicate receipts. A subsequent validation may reacquire the
same transaction-owned Project key; it must not introduce a Project absent from the
predeclared set. Audit all legal initial paths before accepting that invariant.

The implementation also moves the in-process root sequence lease after the Project
gates on the relational path. The root-only event appender does not acquire Project
locks. The nonrelational test path retains its original sequence lease and validation.
Project-scoped initial commits, including replay, require the owning Project policy and
coordinator; a manually constructed read-only unit can still load history and commit
root-only commands, but cannot skip the new lock boundary during an initial replay.
Application composition already supplies both dependencies. The lost-acknowledgement
fixture now wires the real policy when replaying its retired Project admission.

The authority audit found that current valid Agent launch/tool contracts bind their
source and target to the same Project, while the legacy source resolver uses the saved
target admission. All are included without recapturing current lifetimes. The collector
also includes the explicit saved source and tool-source authority so validation cannot
introduce an omitted key. Different lifetimes for one Project fail before acquisition;
link-target mismatches fail before a target query. The executable closure contributes
its original Project even when the runtime has no Project admission.

## Proof and remaining integration

Use actual PostgreSQL contexts and deterministic barriers to race a new Process child
commit with a Project-plus-root publication transaction. Prove bounded completion and
absence of deadlock, without hiding a PostgreSQL deadlock through retries. Exercise both
arrival orders, cancellation while waiting, Project deletion/recreation, duplicate
receipt recovery after deletion/revocation, executable Project admission, and prepared
tool/source Projects where distinct. Repeat existing initial admission, prepared-launch,
hierarchy, generation and mutation fence tests, and complete static/architecture gates.
The new race cases exercise initial roots and actual child commits under a persisted,
claimed parent. Their competing transaction uses the exact Project gate and root
advisory protocol planned for publication; actual inherited task publication remains a
later integration proof and is not implied by these prerequisite races.

This ordering repair alone does not supply inherited mutation authority. The later
selected fence must compare original dispatch/source fingerprint, full child/material
binding and supported contracts under the root lock. Held-source checks must use the
already selected Agent snapshot rather than call an observer that acquires another
coarse catalog lease. Manager review remains distinct from current-claim dispatch.
