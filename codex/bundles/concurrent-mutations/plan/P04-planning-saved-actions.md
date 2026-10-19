# Planning approval, refresh and publication capability

The next integration connects the schema-six planning ledger to the existing saved
effect dispatcher. A scalar remaining-duration argument is not publication authority.
Approval must atomically open a new work interval under the actual original run lease,
and only its acknowledged owner may mint the live monotonic capability.

## Ownership and minimal implementation

Keep the existing schema-five public saved-effect admission unchanged. Add an explicit
planning-owner entry that supplies the exact displayed run/journal/decision/generation
expectation. Reuse the same atomic approval publication, original proposal, current
read check, exclusive dispatch lease, receipt preflight, claim and checkpoint. Schema
six requires its last admitted step's one original publication and closed approval
interval; it cannot enter through the schema-five API or reconstruct an interrupted
monotonic anchor. Open the interval in the same atomic write that applies approval,
then attach the live capability to that lease only after acknowledgement. Lost
acknowledgement permits no dispatch. Historical SDK origin segments remain immutable.

The existing live-work capability exposes a local publication check against the real
selected run/Agent/chat snapshot lease. It verifies the bound invocation claim, original
session, decision, generation, step, interval and exact publication binding before
checking accumulated and current work. It performs no disk/network IO and never
reacquires a file lease under SQL. Saved claim admission checks work again before
entering an effect; receipt reconciliation and known-outcome retention do not require
spending new mutation authority. The later Workbench adapter owns material/source
fingerprints and translates typed budget refusals into publisher outcomes.

Explicit Refresh and Cancel use the exact displayed expectation and current read
authority. They operate on a closed awaiting-approval or basis-changed checkpoint.
Refresh rejects all original pending proposals atomically, retains their history,
starts a distinct generation and preserves spent work and original policy provenance.
Automatic replan additionally requires a known no-effect basis-change checkpoint and
the original bounded replan policy. Neither path can replan a committed, entered or
unknown effect. Cancel is allowed even after basis/work expiry and never invents
elapsed work for an interrupted interval. Live cancellation continues using the
existing cancellation path and its late-response retention rules.

Core retains decision/work transitions. The journal retains file/lease/CAS authority;
MAF retains SDK dispatch; Workbench retains canonical task facts, parsing and SQL.
No new project, reference, ambient registry, store, provider state reconstruction or
execution-service partial is needed. Keep validation shared inside existing owners.

## Risks and validation

Test real file persistence and two independent owner instances: approval time excluded,
previous work charged, original policy retained, stale actions refused, current read
revoked, wrong decision/generation/publication refused, exactly one concurrent resume,
closed-to-open acknowledgement loss, expired basis/work, disposed or unbound work,
and claim/publication budget expiry. Test Refresh retirement and replan limits with
new intent and approval requirements, no refresh after uncertain/committed outcomes,
and cancellation after expiry. Exercise the existing saved dispatcher and schema-five
regressions. Use the configured PostgreSQL owner for the later full provider-to-effect
proof rather than treating a test capability oracle as that integration.

Build/discover with standard Release outputs, review dependencies, enforce the full
portability scan, freeze once, execute the identified lanes and reconcile resources.
Product admission/UI, process/workflow policy capture and sustained capacity remain
open until their actual integration proofs pass.
