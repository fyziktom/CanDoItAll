# Original executable-step requirements

Status: qualified. The 128 original-input prerequisites and final 29 affected workspace
scenarios pass. The [validation record](../validation/2026-10-21-original-step-requirements.md)
retains the initial timing failure and the clock corrections that followed it.
Manager-review admission is qualified by the
[74-case checkpoint](../validation/2026-10-21-manager-review-admission.md). Executor
activation remains gated.

The controlled scenarios also exposed inconsistent clock composition in the workspace.
The factory now forwards its registered clock to the journal and inherited candidate
creation, progress and completion now use that clock. The qualified repair uses one
clock throughout the existing execution owner, with the journal-free
legacy default remaining system time. This adds one dependency field, no new owner or
project edge, and no lock, query, budget relaxation or timestamp clamping. Preserve the
storage guard and the exact-work/deadline assertions. All 29 selected inherited,
ordinary planning and controlled-time cases pass.

## Problem and source evidence

ProcessAcceptedExecutionInput preserves the complete original assignment, including
prompt, role, artifact-slot references and input branch gate. It does not preserve
all compiled execution obligations. ProcessStepExecutionContract additionally contains
configured output branch outcomes, required runtime tools and host capabilities,
artifact descriptors and subprocess mappings. StepInstancePlan also records step kind
and whether it starts a subprocess. These cannot be inferred from an empty assignment
artifact list or from current mutable assignment metadata.

The actual task-plan adapter must refuse unsupported obligations before child enrollment.
Later manager completion must verify the same original contract. Loading the complete
plan for each selected step would repeat unrelated deserialization and could turn a
many-step review into quadratic work. The accepted source already stores indexed,
fingerprinted execution input per admission and step. Extend that existing owner.

## Ownership and dependencies

Application defines a strongly typed ProcessAcceptedStepRequirements value returned
alongside ProcessAcceptedExecutionInput. It describes declared obligations, not current
artifact availability or a serialized authorization flag. No new service interface is
needed; IProcessAcceptedExecutionInputReader remains the selected read boundary.

Persistence captures requirements from the original prepared commit's immutable plan
and initial runtime state. A small pure projection owner validates the corresponding
plan/state/assignment identities and builds the selected values. Serialization remains
in ProcessAcceptedExecutionInputCodec; atomic storage remains in EfProcessAcceptedSource.
The PostgreSQL migrations project owns the check-constraint version expansion.
Existing Application, Runtime, Builder and Persistence references suffice. Core/Workbench
must not gain a dependency on the Modules.Processes implementation.

The actual executor and manager completion will share one explicit supported-contract
validator. This checkpoint supplies trustworthy original data; it does not itself
authorize a provider call, publish an effect, or complete a waiting step.

## Data and version contract

Capture the original step-definition identity and kind, subprocess-start flag, declared
runtime tools and host capabilities, configured output branch identities, artifact
descriptors and subprocess mappings. Preserve the assignment's artifact slots and
branch gate and validate them against the same original plan/runtime step during capture.
Do not capture transient connected-input availability as an immutable requirement.

Use a new execution-input wire version and fingerprint domain for the combined assignment
and requirements payload. New governed acceptance writes only this version. Keep the
existing header/assignment metadata domains and indexed key unchanged. A fingerprint
binds the combined payload to the original header and assignment metadata.

Version 1 remains an explicit historical assignment-only read with absent requirements.
It must never be interpreted as an empty supported contract. The new execution/review
entry requires captured requirements and raises an explicit typed refusal when absent.
Unknown versions and malformed/noncanonical payloads fail. Do not backfill old rows
from current assignments or silently recapture a plan.

Extend the existing PostgreSQL constraint to admit the new version while preserving
versions 0 and 1 and the 4 MiB per-input bound. Use a new migration, not a rewrite of
the retained original-input migration. Down refuses while the new version exists.
The original migration's nonzero-input downgrade guard remains effective.

Avoid a full-table validation scan under the initial exclusive schema lock. Widen the
check atomically with `NOT VALID`, then validate in a separate transaction. The previous
check already proves all retained rows satisfy the wider condition; new writes are
checked immediately. PostgreSQL's [ALTER TABLE contract](https://www.postgresql.org/docs/18/sql-altertable.html)
allows the later validation scan under `SHARE UPDATE EXCLUSIVE`, compatible with ordinary
writes. The short replacement still needs an exclusive schema lock and may wait for
existing transactions; this is not a zero-lock migration. A failure before validation
leaves an enforced wider check and unchanged migration history. Re-entry must safely
repeat replacement and complete validation. Prove this interruption state and prove
actual migration validation can finish while another transaction holds a row-write lock.
Keep guarded downgrade transactional so it cannot discard version 2 evidence.

## Cost and failure contract

Build indexes for original plan steps, initial runtime steps and branch outcomes once
per accepted preparation. Capture each selected requirement from these indexes; do not
rescan every route or deserialize the plan for each assignment. Ordinary assignments
without an execution contract keep their existing path and acquire no new prompt limit.

The selected reader remains one indexed query with only the selected input and original
metadata. It must not load whole plan JSON, neighboring prompts or all assignments.
The additional bytes remain within the existing per-input and total governed-projection
limits. Acceptance still rejects before committing partial source rows on overrun.
Required declaration collections remain finite and validate identifiers and required
values. Preserve the existing bounded serializer and explicit cold-allocation limits.

Original plan/state/assignment identity mismatch, duplicate selected keys, invalid
declarations, exceeded limits or missing required evidence fail before acceptance.
Current repairs cannot alter the captured requirements. This preserves the distinction
between a future explicit replan and the original snapshot under review.

## Minimal implementation sequence

1. Add the typed requirements value and explicit absent-requirements refusal at the
   existing input boundary.
2. Add the pure indexed projection and versioned combined input codec. Extend capture
   once at launch acceptance; keep the selected read at one query.
3. Add the constraint migration and canonical model update with guarded Down.
4. Update original-input tests and add requirement-specific protocol, identity,
   immutability, migration and finite-cost cases.
5. Retain architecture, source/runtime and resource proof before using this value in
   adapter preflight or the waiting-result owner.

The simpler alternatives of trusting a DTO flag, inferring no obligations from the
assignment, re-reading mutable definitions, or loading the complete plan per review
are rejected because they either weaken the contract or repeat unrelated work.
No Runtime partial, service locator, generic orchestration layer or new store is needed.

## Testability and acceptance

Pure tests exercise projection and protocol without ProcessRuntimeEngine or a database:
original kind/subprocess/branch/tool/host/artifact declarations survive round trip;
another plan, step, definition or assignment cannot be substituted; missing/duplicate
or oversized declarations fail; version 1 is explicitly incomplete; unknown versions
do not convert. Test the future supported-contract validator separately from this
capture owner when it is implemented.

Real PostgreSQL tests cover first-acceptance atomicity, rollback, immutable reads after
current assignment repair, migration version constraints, guarded Down and restart.
Selected read scaling must remain one query for 1/128/1024 steps and a large neighboring
prompt. Measure total capture bytes/allocations and operation counts for the same shapes
to detect repeated whole-plan or route scans. Existing byte limits remain enforced.

Build affected production owners in standard Release, then refresh test outputs.
Select exact affected test methods and discovered counts before execution. Include
original-source/input/capacity regressions and the actual inherited task-plan fixture.
Run portability enforcement without baseline writing, review scoped dependency/findings
deltas, freeze source/runtime, observe the existing isolated fixture and retain all
failed as well as successful attempts. No new server or custom build configuration.
The repaired command-record reader/writer from the manager admission final candidate
must travel together with its pinned CommandRecord.ps1 helper.
