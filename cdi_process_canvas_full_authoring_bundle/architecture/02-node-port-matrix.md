# Node Port Matrix

## Port Semantics Legend

- `S-S` means `single-to-single`
- `S-M` means `single-to-many`
- `M-S` means `many-to-single`
- `M-M` means `many-to-many`
- `Canonical today` means the current service layer and persistence model already support the relationship as a real entity or field
- `Needs extension` means the canvas can only be honest if the canonical model is expanded

## Definition Node Families

| Node family | Port id family | Direction | Semantic meaning | Cardinality | Canonical status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `process-role` | `role:responsible` | Output | Role participates as responsible executor on a step | `M-M` overall, `S-M` from one role, `M-S` on step input | Canonical today via `ProcessStepRoleAssignmentRequirement` | New visible output required |
| `process-role` | `role:reviewer` | Output | Role participates as reviewer on a step | `M-M` overall | Canonical today | New visible output required |
| `process-role` | `role:approver` | Output | Role participates as approver on a step | `M-M` overall | Canonical today | New visible output required |
| `process-role` | `role:backup` | Output | Role participates as backup on a step | `M-M` overall | Canonical today | New visible output required |
| `process-role` | `role:decision-authority` | Output | Role acts as the decision maker for a decision-capable step or its router | `S-M` from one role, target-side treated as `S-S` or `M-S` depending on router or step identity | Canonical today via `DecisionRoleRequirementId` | Already visible for branch router only |
| `process-step` | `step:inputs` | Input | Upstream step or branch-outcome dependencies required before this step can run | `M-S` on target, overall structural graph is `M-M` | Canonical today via `ProcessStepDependencyDefinition` | Applies to all but `Start` by default |
| `process-step` | `step:next` | Output | Direct downstream dependency from this step | `S-M` from one step, overall structural graph is `M-M` | Canonical today | Suppress or specialize for `End` |
| `process-step` | `step:responsible` | Input | Responsible-role assignment for the step | `M-S` on target, overall `M-M` | Canonical today | New visible input required |
| `process-step` | `step:reviewer` | Input | Reviewer-role assignment for the step | `M-S` on target, overall `M-M` | Canonical today | New visible input required |
| `process-step` | `step:approver` | Input | Approver-role assignment for the step | `M-S` on target, overall `M-M` | Canonical today | New visible input required |
| `process-step` | `step:backup` | Input | Backup-role assignment for the step | `M-S` on target, overall `M-M` | Canonical today | New visible input required |
| `process-step` | `step:decision-authority` | Input | Decision-making role assignment on the step itself or on the derived router contract | Target-side `S-S` | Canonical today | Needed where branch-router projection is not the only decision surface |
| `process-step` | `step:artifact-output:*` | Output | Step produces an artifact expectation that downstream work may consume | `S-M` from one artifact producer | Canonical today only as owned artifact expectation | The output badge can exist today, but linked consumers are not yet canonical |
| `process-step` | `step:artifact-inputs` or `step:artifact-input:*` | Input | Step consumes upstream artifacts as job input or evidence | Likely `M-S` on target, overall `M-M` | Needs extension | Exact per-artifact versus grouped-input design decided in subbundle 02 |
| `process-branch-router` | `branch:step-input` | Input | Structural ownership link from the source decision step into its router | `S-S` in intended design | Canonical today as derived semantic | This connection is system-managed and should stay non-removable unless the step loses routing |
| `process-branch-router` | `branch:decision-role` | Input | Decision-maker role for the router | Target-side `S-S` | Canonical today | Already supported |
| `process-branch-router` | `branch:outcome:*` | Output | Explicit routed outcome path | `S-M` from one outcome port | Canonical today | Already supported |
| `process-branch-router` | `branch:default` | Output | Default path when no explicit outcome matches | `S-M` | Canonical today | Already supported |
| `process-branch-router` | `branch:error` | Output | Error or failure path | `S-M` | Canonical today | Already supported |

## Runtime Node Families

| Node family | Port id family | Direction | Semantic meaning | Cardinality | Canonical status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `process-run-step` | `run-step:inputs` | Input | Read-only projection of upstream prerequisites or completed inbound flows | Mirrors definition semantics | Canonical today through run state and step dependencies | Needed for readability parity |
| `process-run-step` | `run-step:next` | Output | Read-only projection of downstream structural flow | Mirrors definition semantics | Canonical today | Needed for readability parity |
| `process-run-step` | `run-step:responsible` | Input | Read-only assigned role lane for current executor responsibilities | Mirrors definition semantics | Canonical today | Likely read-only only |
| `process-run-step` | `run-step:reviewer` | Input | Read-only reviewer projection | Mirrors definition semantics | Canonical today | Likely read-only only |
| `process-run-step` | `run-step:approver` | Input | Read-only approver projection | Mirrors definition semantics | Canonical today | Likely read-only only |
| `process-run-step` | `run-step:backup` | Input | Read-only backup projection | Mirrors definition semantics | Canonical today | Likely read-only only |
| `process-run-step` | `run-step:artifact-output:*` | Output | Read-only produced artifact evidence | Mirrors definition semantics if surfaced | Depends on definition-side extension for artifact links | Optional if runtime parity is curated |
| `process-run-branch-router` | `run-branch:step-input` | Input | Read-only source step projection | `S-S` | Canonical today | Already partly projected |
| `process-run-branch-router` | `run-branch:outcome:*` | Output | Read-only available routed outcomes | `S-M` | Canonical today | Already partly projected |

## Step-Kind Applicability Rules

| Step kind | Structural input | Structural output | Participant inputs | Router relevance | Notes |
| --- | --- | --- | --- | --- | --- |
| `Start` | Usually suppressed | Allowed | Allowed when kickoff work is role-owned | Optional | Start nodes normally have no upstream prerequisite link |
| `Work` | Allowed | Allowed | Allowed | Optional | Default step behavior |
| `Decision` | Allowed | Allowed to router and direct next paths where model permits | Allowed | Primary | Branch-router projection is most relevant here |
| `Approval` | Allowed | Allowed | Approver and backup inputs are especially relevant | Optional | Still a normal step structurally |
| `Review` | Allowed | Allowed | Reviewer, responsible, approver, and backup inputs may all matter | Optional | Common software-review scenario |
| `Delivery` | Allowed | Allowed | Responsible and approver inputs usually matter | Optional | Good candidate for artifact outputs |
| `End` | Allowed | Usually suppressed | Allowed when closure is role-owned | Optional | End nodes normally have no downstream structural output |

## Implementation Guidance Derived From The Matrix

- Use a strongly-typed process-canvas port catalog in the process module.
- Keep generic CanvasLib unaware of business semantics.
- Let the process module own:
  - port IDs
  - port groups
  - applicable step kinds
  - cardinality rules
  - mapping from port families to canonical entities
- Treat artifact-consumption links as a first-class model decision, not as an incidental UI detail.
