# CanDoItAll.Processes.Runtime

Owns process-manager decisions, step execution, completion gates, subprocess control,
recovery classification, runtime incidents, and durable execution coordination.

All state transitions use typed process contracts and emit records required for
persistence, recovery, and projection. An optional saved project admission binds a run
to one database profile, project and project lifetime. It is an immutable scope fact,
not a permission grant. Legacy runs retain a null admission; observation and existing
unscoped transitions remain supported without deriving authority from launch variables.

SharedKernel's decision budget values and pure transitions reserve bounded child allowances and settle
exact closed-owner usage without resetting consumed work. Historical retries are read-only;
recorded overruns prevent further reservations. These values do not authorize execution
or verify business receipts. Typed accounting commands now let the persistence owner
compare revisions and update the balance/allocation atomically with runtime state under
the existing root fence. Receipts preserve the original accounting result separately
from current state. Actual child-owner settlement verification and executor wiring remain
required before runtime activation.

The pure decision-generation lifecycle separates active dispatch, manager review and
quiescence. It compares exact generation/revision expectations, retains immutable child
bindings and closed-owner receipt references, and requires every child to close before
supersession or cancellation. Heads and children are separate bounded values; refreshing
material preserves the decision and budget identity. These calculations neither verify
owner evidence nor replace the required durable transaction and effect fences.

```powershell
dotnet build .\src\Processes\CanDoItAll.Processes.Runtime\CanDoItAll.Processes.Runtime.csproj
```

## Decision child occurrences

`ProcessDecisionChildIdentity` derives a versioned child identity from the original
decision, generation, owner and admitted logical ordinal. Retries preserve that ordinal;
claim IDs and mutable inputs do not create new occurrences. `ProcessDecisionChildAdmission`
binds the exact runtime address, material and allocated work. These values do not grant
source permission or prove that an executor supports governed decisions.

## Admitted execution contracts

Policy schema 1 retains its original encoding. A policy with an explicit execution
contract uses schema 2 and binds the exact adapter contract identity, version and
Agent/Workflow owner into preparation and immutable assignment storage. Unsupported
envelopes and contract mismatches fail explicitly. Contract identities belong to the
business adapters; Runtime does not depend on their modules or SDKs. This declaration
does not enable an executor or replace its source, material and publication checks.

## Original inherited material

`ProcessDecisionMaterial` retains an immutable profile/namespace/run-or-step scope,
material generation, execution contract, original UTC capture and bounded UTF-8 JSON
object. Its versioned fingerprint binds every field, and freshness uses only the original
capture time. A child cannot restamp identical content to renew its age. The owner still
validates the business payload schema; these pure values do not establish current scope
membership, permission, canonical persistence or an effect publication fence.

Start/refresh commands can bind an exact `ProcessDecisionMaterialStamp`. The payload
is supplied separately to the runtime commit request; it is not embedded in command
receipts or runtime state. Material-bearing receipts use schema 2, while absent material
retains schema 1. The persistence owner requires the canonical payload for a declared
execution contract and checks its original scope before staging the decision.
Canonical material admission uses policy schema 3 and explicitly declares its material
protocol. Readers of the earlier declaration-only envelope reject that new schema.
Absent protocol retains the exact schema 1/2 encodings. Unsupported executors remain
closed; a declaration is never a capability grant.

`SharedContentV1` separates a business `ProcessDecisionContentContract` from the exact
assignment executor contract. `ProcessDecisionMaterial.FromContent` creates a neutral
snapshot with no capture executor and a distinct fingerprint domain. Legacy material
keeps its original framing. Policy schema 3 requires the content contract only for the
shared protocol; older or executor-bound envelopes reject it. Mixed Agent/Workflow
consumers can share the same business payload while retaining independent dispatch
capabilities. Neither protocol permits relabeling historical material.

## Root decision accounting

`ProcessDecisionRunAdmission` retains the original root run, selected profile, workspace
head, policy inputs and resolved limits. A nullable accounting step denotes that real
root owner. It never denotes an invented step. Funded step admissions retain a typed
reference to this root budget and their own original ceiling.

Each local allocation has a deterministic, domain-separated parent allocation identity.
Reservations and closed usage apply to both ledgers; the root's 256-allocation bound is
shared across captures, orchestration and descendant work. An unresolved child does not
release its grant. RunSnapshot children share their root's read/publication/change policy,
and only the root can charge a shared material replan. StepSnapshot retains per-step policy
resolution within the original workspace and enclosing limits.

Funded admissions and receipts have explicit new wire versions. Legacy encodings remain
unchanged. Root accounting does not itself enable an executor or implement shared material
reconciliation.
