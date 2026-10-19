# Process decision work allocations

Prospective foundation after `77858d487`, subordinate to the
[decision runtime design](P07-process-decision-runtime.md). No Process/Workflow policy
activation is authorized by this foundation alone.
The pure arithmetic now has [28 passing Unit cases](../validation/2026-10-19-process-decision-budget.md).
Durable owner integration and actual producer proof remain prospective.

## Fixed responsibility and arithmetic

Process Runtime owns immutable budget balances and allocation transitions. They use
the existing SharedKernel policy ceiling but do not capture business material, read
clocks, perform I/O, interpret Agent/Workflow journals or authorize a manager. The
existing manager loop-repeat ledger counts incident recovery attempts; it has neither
work reservations nor child settlement and must not be repurposed for this contract.

Keep a balance and exact allocation as separate values so persistence can update one
indexed child and the head in constant work. Do not load or rewrite every historical
child to reserve one allowance. The persistence successor must compare the balance
revision and read the allocation under the existing root transaction fence; pure
arithmetic by itself is not concurrent admission or a durable ledger.

Each owner-issued allocation identity belongs to exactly one decision lineage. A new
allocation reserves its explicitly requested work duration and automatic-replan count
before child dispatch. Insufficient allowance is a typed refusal; never silently reduce
the grant. A repeated exact allocation returns the original allocation, including its
settlement, without consuming anything again. Another request under the same identity
is refused. Transport claims and decision generation changes cannot reset the balance.

Settlement consumes exact closed-owner work and replan totals and releases only the
unused reservation. A repeated exact settlement is read-only; changed evidence under
the original identity is refused. Unknown execution remains reserved. A zero-consumption
settlement requires the same definite, closed owner evidence as any other settlement;
neither cancellation nor a missing acknowledgement is that evidence. The application
adapter must verify that evidence against the actual Agent/Workflow owner, outside the
root SQL lock, before the persistence owner compares and applies it. These values must
not be accepted directly from an HTTP body or provider response.

Measured work can exceed its grant because a provider may ignore cancellation. Record
the actual usage, retain a sticky overrun flag and refuse further reservations. Other
already-reserved children must still be able to settle. Even an overrun within the
parent's remaining total is a violated child grant; do not silently absorb it. Checked
arithmetic rejects unrepresentable totals before returning a changed value. Original
immutable values remain available for reconciliation.

Parent preparation and coordinator work use allocations too; otherwise the shared
ceiling would omit real work. Deliberate human waiting is not active work. Basis expiry
continues separately and is never renewed by an allocation or settlement. This arithmetic
does not grant read authority, publication authority or permission to recapture material.

Bound retained allocation identities to 256 per decision lineage, including settled
ones, to prevent unlimited zero-effect retries. This is a proposed v1 storage/admission
cap to qualify with the persistence capacity tests before activation, not a measured
throughput promise. Generation and material bounds remain separate. Production launch
must validate its allocation plan against the advertised owner capacity before spending
provider work; a rejected allocation cannot start a child.

## Implementation and validation contract

Use small top-level values and a pure transition class in the existing Runtime project.
Do not add an interface, DI registration, project reference or runtime partial for
arithmetic. Typed decision/allocation identities prevent accidental interchange. Keep
business receipt verification at the real adapter boundary and transaction ownership
in Persistence. No generic cross-engine budget coordinator is introduced.

Unit proof must cover competing allowances, partial and zero-consumption settlement,
exact reservation and settlement replay after unrelated ledger changes, foreign or
changed identities, exhausted work/replans/history, malformed stored balances, checked
overflow and sticky overruns while remaining children settle. No filesystem, database,
wall clock, provider, full runtime or full application host is needed for these rules.

The successor must prove SQL CAS, same-key racing enrollment, restart, rollback,
generation preservation, real owner evidence and unrelated-root progress using the
actual production producer. A manually seeded consumer test cannot close those gates.
Do not describe this foundation as a shared durable budget until that integration passes.
