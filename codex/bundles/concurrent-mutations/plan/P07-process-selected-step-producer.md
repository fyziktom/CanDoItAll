# Original selected step and Agent child production

Status: selected preparation design accepted for implementation, 2026-10-20.
Prerequisite: the root producer has 192 distinct passing behaviors and retained
source/runtime/resource evidence. Its signed checkpoint is waiting for local GPG.
This design continues P07; it does not declare Process, Workflow or R5 complete.

## Current owners and boundaries

The dispatcher now prepares shared RunSnapshot material before creating claims.
`AgentFrameworkProcessStepExecutor` still rejects decision policy execution.
`EfProcessDecisionStore` already atomically starts a generation, joins shared
material, enrolls an original child and reserves its finite grant. Core already
admits an exact inherited execution and Workbench already interprets and publishes
its original task-plan material. The missing producer must connect those owners.

| Responsibility | Owning boundary | Required change |
| --- | --- | --- |
| Original selected assignment, local balance and current claim | Processes.Persistence behind Application query | Bounded accepted header plus one assignment; no synthetic execution identity |
| Capture attempt, generation and child allocation | Processes.Application and existing Runtime commands | Deterministic identities, fresh-commit ownership, measured local work and exact claim attachment |
| Coherent task-plan material and current source | Workbench | Reuse the registered capture operation for root and step scopes |
| Actual authorized Agent session and provider lifecycle | AgentFramework.Core | Preserve exact admission and journal ownership; account initial preparation before provider work |
| Process request, preflight and result routing | Modules.Processes | Delegate governed execution to a cohesive coordinator after the existing host/tool checks |
| Original task target, source/executor ceiling and initial binding | Existing Workbench inherited participant | Bind the actual candidate/session; do not manufacture a background session |
| Review, cancellation and refresh of a waiting Process child | Process application/manager plus Core journal | Separate current manager authority from the historical dispatch claim |

No new project references, runtime partial files or service locators are justified.
Application cannot reference Agent Core, Workbench, EF or a provider SDK.
Core cannot interpret Process bindings or Project task material.
The existing participant is the business seam for original initial binding.
The existing large executor receives a delegation call, not another planning engine.

CodeAnalytics baseline: reuse unchanged Process/Workbench snapshot
`snap-20261020223910-6bd2f38c` (five projects, seven project edges, no project cycle).
The separate Core/Models snapshot `snap-20261020231738-641f95eb` has two projects,
one Core-to-Models edge, no project cycle, 1,687 types, 12,289 members, 490 findings
and 189 diagnostics. Its three type cycles are existing. A combined seven-project
response exceeded the tool transport limit; use these bounded scopes for comparison.

## Selected preparation protocol

1. Read the selected original accepted header and assignment, current exact claim,
   local original budget and optional decision head. Compare current executor,
   readiness, policy and operation contract with original acceptance. Validate root,
   profile, Project lifetime binding and namespace. This observation is preparation
   evidence only; it never grants execution or publication authority.
2. Refuse legacy source projection explicitly without decoding its full launch body.
   Do not read unrelated assignments, large prompts, material payloads or child journals.
   The selected current assignment is separately loaded by its existing owner when
   the real executor needs its prompt. Claim ownership and expiry are checked again
   at each mutating transaction through `ProcessRuntimeDispatchPrecondition`.
   Its optional typed execution-contract expectation also locks the selected
   assignment row and compares the bounded accepted assignment within the commit.
   This closes repair between the preliminary query and material/child attachment.
3. With no generation, reserve a bounded local preparation allocation using the
   original run/step, budget and claim identity. A replayed reservation never permits
   another capture. Preserve any unresolved earlier allocation; a replacement claim
   must not turn unknown work into a new attempt.
4. RunSnapshot selects the already active shared stamp, validates its age, and joins
   the exact shared-scope expectation. StepSnapshot performs one coherent registered
   capture under a finite cancellation deadline. Neither path holds a Process/root
   transaction while waiting for catalog, Project or external work.
5. Measure local preparation monotonically. After all owned read work and leases
   have ended, atomically settle it and attach the material/start the generation
   under the original live claim. If cancellation, source change, deadline or claim
   loss prevents attachment, settle known closed work without attaching material.
   Unknown work keeps its allocation. Accounting cleanup has its own bounded token.
6. Enroll the actual deterministic first child and reserve its finite work grant in
   one existing decision transaction. Derive child identity from decision generation,
   owner and ordinal, never from a random retry or newly invented execution. Only
   the caller that freshly commits enrollment may prepare a missing Agent child.
   Other callers may observe an existing journal, but cannot recreate missing work.
7. A competing caller observing active preparation returns an explicit pending
   outcome without releasing the winner's dispatch claim. Do not use an invented
   ExecutionRunId to represent pre-child preparation. A retained real child can use
   the existing original-outcome/reconciliation path.

Attempt, complete and abort command identities remain stable across lost responses.
Optimistic retries are bounded. Original receipts are checked before new work.
No independent child policy resolution may replace the accepted parent's policy.
Grant selection is bounded by both local and root funding; exhaustion is explicit,
and fairness/capacity remains a measured release gate.

## Actual Agent admission and manager continuation

The initial binder must inspect the actual Core candidate and its authorized
background session, exact Process child/material and registered execution/content
contract. It checks source and executor together in one catalog read lease, including
the same-Agent case. It obtains a child-specific write context without changing
shared material bytes. Publication still revalidates current authority.

Initial Core preparation before the first durable child save is real budgeted work.
It must be measured by Core and recorded in the child's closed work before provider
entry; replay cannot reset it. An absent journal following an uncertain initial
attempt is not a zero-work receipt. Recovery of a durable preparing child must own
its live work interval before repeating provider startup. These are activation gates.

Manager approval names the exact displayed journal revision, parent generation,
proposal binding and digest. The API/native host supplies current ExecuteProcesses
authority. A typed manager continuation permits only the original saved effect;
it cannot start another provider call or authorize unrelated pending tools.
Workbench's enlisted publication fence selects ManagerReview only when this actual
continuation is present. Selecting an enum alone is insufficient authority.

Waiting-step completion needs a separate typed runtime transition and atomic original
child settlement. It preserves the old strategy result and expired/completed claim;
it does not resubmit a changed result with an old command key or revive the claim.
Cancellation first quiesces the child and retains uncertain effects. Refresh requires
closed old work, fresh parent material/generation and a new proposal requiring approval.
Shared refresh belongs to the root and cannot use a claim it already quiesced.

## Tests and performance acceptance

Use actual accepted launches and SQL transactions for producer proof. Manually seeded
child fixtures remain supporting consumer regression evidence.

- Bounded source reads at one and 128/1,024 steps; unrelated prompts are not decoded.
- Operator, same-Agent and different-Agent source; revocation/profile/lifetime changes.
- RunSnapshot reuse, StepSnapshot capture, original canonical material and age checks.
- Two competing preparations, lost reserve/attach/enrollment acknowledgements,
  cancellation, claim expiry while waiting/after flush and known capture failure.
- No repeated work from original receipts, no zero refund for missing journals,
  no provider or Project write under a Process transaction.
- Actual registered Core admission and MAF input from the produced binding; initial
  preparation accounting, restart, duplicate start and current source denial.
- Exact displayed manager approval, replay after commit, refusal after replacement,
  expiry, cancellation, refresh and source revocation; atomic parent outcome.
- Existing ordinary executor host/tool preflight, dispatch/result and source consumers.

Build affected production projects directly in serialized Release outputs. Freeze
source before building and verify it afterward; discovery must match current method
names as well as counts. Run scoped tests, full portability enforcement and docs.
Reuse the owned PostgreSQL fixture and observer; preserve port 5032 and unrelated
resources. Retain exact failed candidates and their correction boundaries.

The initial build caught a projection of `CapturedAtUtc` where material persistence
stores exact `CapturedAtUtcTicks`; reconstruct UTC from those ticks. The first static
scan adds one reviewed case-policy finding in `ProcessAcceptedAssignmentGuard`.
Its ordinal-insensitive ordering applies to existing operation protocol identifiers,
not paths or culture-sensitive text. Preserve that deliberate policy in the baseline,
then regenerate the full scan and enforce without writing the baseline.

The first implementation portion covers selected source/preparation and enrollment.
The caller supplies the finite maximum child-work request; the producer also bounds
it by the remaining original local and root balances. Product grant sizing is an
explicit activation/capacity decision, not an unbounded reservation or a hidden reset.
Keep the executor refusal until actual Core admission/accounting and manager proof
above pass. Workflow node occurrence, UI/settings activation, sustained capacity,
historical broad failures and final resource cleanup remain explicit later work.

The initial 186-case execution has 185 passes and one test failure. The simulated
two-hour claim expiry also exceeds the material's ten-minute age, correctly raising
the explicit material-expiry exception after settling closed work. Correct the
fixture to use a five-minute claim and six-minute observation, and add a separate
eleven-minute material-expiry case that verifies its exception and settlement.
Production remains unchanged. Repeat both producer/source classes and verify an exact
source/output bridge before retaining the 164 unaffected passing cases.
