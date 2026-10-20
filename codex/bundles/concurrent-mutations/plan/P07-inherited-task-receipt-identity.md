# Inherited task-plan caller and receipt identity

Status: implemented with [75 passing selected cases](../validation/2026-10-20-inherited-task-receipt-identity.md)
after the parent saved-effect continuation proof. This is the storage prerequisite for
the Workbench inherited adapter, not producer activation.

The actual task publisher currently supports only `OrdinaryAgent` caller kind 1 and
`OrdinaryAgentDecision` source kind 1. Both receipt SQL checks hard-code that identity.
Reusing it for an inherited Process child would conflate distinct authority origins.
Add explicit `ProcessAgent` caller kind 2 and `ProcessAgentDecision` source kind 2.
Keep all existing numeric values, digest domains, field order and operation keys intact.
The existing caller binding and receipt primary key already include caller kind. Use the
existing trusted Process caller key when the later adapter binds the original selected
source; never accept a caller key from a request or fabricate interactive governance.

Inherited sources require active-work interpretation version 2. Ordinary sources retain
versions 1 and 2. Require the exact caller/source pairing before receipt lookup or
publication, when encoding a receipt and when decoding retained evidence. Reject unknown
identities and cross-origin pairs explicitly. This prevents a new caller from claiming
an old source merely because both use a supported semantic version. The new kind carries
no mutation authority: existing publication leases, finite epochs, selected Process
fences, permissions and receipt access remain mandatory.

Workbench owns both typed validation and its receipt entity mapping. The canonical
PostgreSQL migration changes only the two relevant check constraints. Preserve columns,
indexes, primary key and existing receipt bytes. Caller kind permits 1 or 2; source must
match caller, and kind 2 additionally requires schema/result/source version 2 and
normalization version 1. Preserve kind 1's existing positive-version SQL contract so
the upgrade does not rewrite or reinterpret historical data. Runtime readers continue
to reject unsupported interpretations. No new project references or runtime partials.

Upgrade uses a transaction and the required table lock for constraint replacement; this
is a bounded schema maintenance operation, not a zero-contention migration. Down takes
the same table lock and refuses if any kind-2 receipt remains. It must not delete,
rewrite, reclassify or hide retained evidence. Empty/legacy-only downgrade may restore
the original checks. Existing activation pools, namespaces and receipt retention remain
unchanged. Old binaries cannot execute inherited work; deployment activates the producer
only after compatible code and schema are present.

Validation: fixed legacy caller-binding and operation-key characterization; same key and
operation ID across kinds remain distinct; inherited version-1 and unknown kind refusal;
exact kind-2 receipt roundtrip; caller/source substitution refusal; existing version-1/2
contract and interactive adapter regressions. Real PostgreSQL publication and replay must
keep one exact receipt in its own partition and reject cross-origin input without effects.
Migration proof covers empty/legacy upgrades, restart, unchanged retained rows and schema,
kind/version checks, transactional rollback and downgrade refusal with retained evidence.
Compare canonical and owner models, require no pending model change, direct affected
Release builds, exact discovery, full portability enforcement and observed resource
closure. Existing broad failures and sustained capacity remain separate open gates.

After this storage prerequisite, implement the separate inherited target/material codec,
participant and authority. Retain neutral shared material unchanged and a child-specific
finite write context. The selected source query already binds Agent execution ID exactly
to the enrolled Process child ID. Current source checks must select the launching Agent
from the same held catalog set as the executor, without a nested lease. Actual producer,
historical recovery, workspace continuation, manager, Workflow and capacity remain open.
