# Emergency hotfix rollout with shard-risk governance

**Key:** `hotfix-rollout`  
**Criticality:** MissionCritical  
**Autonomy level:** Guarded  
**Operating mode:** Emergency  
**Customer name:** Enterprise Billing Platform  
**Owner name:** Production response command

## Summary
Emergency delivery template for time-critical production remediation with explicit containment, rollback, customer communication, artifact-input, and approval discipline aligned to the current process architecture.

## Value statement
Restores service or mitigates incident impact quickly without disguising rollback, data, or customer communication risk.

## Interface contract summary
Emergency software delivery spans production operations, customer communication, database safety, and release governance, so the process must keep those boundaries explicit.

## Governance notes
Time pressure never removes the need for explicit rollback ownership, communication ownership, typed emergency evidence, or explicit dependency joins.

## Architecture and constitution rules
- Governance policy: Emergency release requires typed blast-radius analysis, focused QA proof, release approval, and explicit rollback trigger ownership.
- Constitution rule: Command, communication, database safety, and rollout execution remain durable role contracts even if people on the rota change.

## Operating and simulation notes
- Operating mode summary: Emergency execution is allowed only inside a bounded release window with explicit rollback and communication obligations.
- Simulation readiness: This scenario is intentionally rich enough for emergency simulation, incident replay, and canvas authoring validation.

## Source frameworks
- nist-ssdf
- owasp-samm
- openchain
- spdx
- slsa

## Process metrics
- Median time from bridge activation to blast-radius assessment.
- Emergency approvals with explicit rollback trigger and customer communication owner.
- Rate of emergency rollouts completed without unplanned rollback.
- Share of post-incident reviews completed with corrective actions and owners inside SLA.
- Number of incidents where emergency scope expanded beyond the approved blast radius.

## Process risks
- Emergency work expanding into an unreviewable normal release.
- Rollback triggers unclear at the moment of production action.
- Customer communication lagging behind operational state.
- Database or shard-risk implications hidden inside platform-only notes.
- Emergency approval proceeding without explicit artifact inputs.

## Tailoring rules
- Minor operational mitigations that do not deploy code may use a reduced package step, but approval still requires explicit blast-radius and validation evidence.
- If no data or shard state is touched, database review may downgrade from mandatory reviewer to optional reviewer with recorded rationale.
- Where no shadow environment exists, validation may use the safest representative proving lane available, but skipped checks must be explicit.
- Long-running or multi-service fixes must be moved out of the emergency lane into planned software delivery.

## Role usages
- `incident-commander` / **Incident commander** — Drive containment and structured decision-making when time pressure and uncertainty are high.
- `platform-engineer` / **Platform engineer** — Provide the reliable platform path required to build, deploy, observe, and if necessary roll back changes safely.
- `database-engineer` / **Database engineer** — Own emergency database diagnostics, hotfix compatibility assessment, and rollback trigger definition.
- `qa-lead` / **QA lead** — Challenge whether the delivered change is proven enough for its risk profile and make test evidence decision-ready for release governance.
- `release-approver` / **Release approver** — Decide whether the accumulated evidence is sufficient to expose the change to real users, data, and operational load.
- `customer-liaison` / **Customer liaison** — Represent the customer communication path during release and incident work without allowing message drift from operational facts.

## Steps
### 1. Activate emergency bridge and classify severity (`activate-emergency-bridge`)
- Step kind: Start
- Depends on: None
- Inputs: Active production signal, customer impact, and first-response telemetry.
- Outputs: Explicit command bridge with severity posture, responder roster, and immediate constraints.
- Evidence: Bridge activation log, severity declaration, and named responders.
- Decision rights: Incident commander owns classification and decision pacing during the emergency window.
- Exception policy: Do not begin emergency packaging while command ownership or severity framing is still implicit.
- Artifact expectations:
  - `emergency-bridge-log` => `emergency-bridge-log` / Emergency bridge activation log

### 2. Assess blast radius and rollback constraints (`assess-blast-radius`)
- Step kind: Review
- Depends on: activate-emergency-bridge
- Inputs: Emergency bridge log, production telemetry, and known change hypotheses.
- Outputs: Explicit blast-radius assessment with rollback constraints and bounded emergency scope.
- Evidence: Impact map, rollback notes, and disallowed expansion paths.
- Decision rights: Incident commander frames the boundary; platform and database owners challenge unsupported assumptions.
- Exception policy: Pause immediately when the required fix grows into an unreviewable multi-area release.
- Artifact expectations:
  - `blast-radius-assessment` => `blast-radius-assessment` / Blast-radius assessment

### 3. Package emergency hotfix and rollback scripts (`package-hotfix`)
- Step kind: Work
- Depends on: assess-blast-radius
- Inputs: Blast-radius assessment, target change scope, and deployment constraints.
- Outputs: Hotfix package with rollout steps, rollback scripts, and changed-surface inventory.
- Evidence: Patch diff, deployment bundle, schema scripts, and operator checklist.
- Decision rights: Platform engineer owns assembly but cannot expand scope beyond the approved emergency boundary.
- Exception policy: Pause immediately when the required fix grows into an unreviewable multi-area release.
- Artifact expectations:
  - `hotfix-package` => `hotfix-package` / Emergency patch and rollback bundle

### 4. Validate emergency fix in shadow environment (`validate-hotfix`)
- Step kind: Review
- Depends on: package-hotfix
- Inputs: Emergency patch bundle, known blast radius, and incident reproduction notes.
- Outputs: Focused validation result with residual risks and unsupported cases.
- Evidence: Checklist output, shadow-environment notes, and residual-risk annotations.
- Decision rights: QA responder may block the rollout if the emergency evidence is too thin for the risk profile.
- Exception policy: Do not convert the gate into a verbal approval; evidence still needs typed reviewable form.
- Artifact expectations:
  - `emergency-validation-evidence-pack` => `emergency-validation-evidence-pack` / Emergency validation evidence pack
- Checklists: emergency-window-checklist

### 5. Approve emergency release window (`approve-emergency-release`)
- Step kind: Approval
- Depends on: assess-blast-radius, validate-hotfix
- Inputs: Validation evidence, rollback trigger, customer-impact status, and operator readiness.
- Outputs: Go / no-go decision with explicit rollback trigger and accountable owners.
- Evidence: Approval note, release window, fallback trigger, and outward-communication owner.
- Decision rights: Release approver owns the emergency release decision and cannot waive missing evidence or unclear rollback control.
- Exception policy: Reject the rollout when rollback conditions or customer-facing obligations are not explicit.
- Artifact expectations:
  - `emergency-window-approval-record` => `emergency-window-approval-record` / Emergency release approval record
- Artifact inputs:
  - from `assess-blast-radius` expectation `blast-radius-assessment`
  - from `validate-hotfix` expectation `emergency-validation-evidence-pack`

### 6. Execute emergency rollout and watch telemetry (`execute-emergency-rollout`)
- Step kind: Delivery
- Depends on: approve-emergency-release
- Inputs: Approved release record, deployment bundle, telemetry checkpoints, and customer message cadence.
- Outputs: Executed rollout with explicit telemetry outcome and rollback state.
- Evidence: Operator notes, telemetry checkpoints, rollback invocation if needed, and customer update timeline.
- Decision rights: Platform engineer may execute only inside the approved window and rollback trigger boundaries.
- Exception policy: Trigger rollback immediately when shard lock duration, tenant impact, or telemetry drift breaches the approved threshold.
- Artifact expectations:
  - `hotfix-rollout-log` => `rollback-plan` / Emergency rollout and telemetry log
- Artifact inputs:
  - from `approve-emergency-release` expectation `emergency-window-approval-record`

### 7. Capture post-incident learning and corrective actions (`post-incident-review`)
- Step kind: End
- Depends on: execute-emergency-rollout
- Inputs: Rollout outcome, telemetry record, customer communications, and command timeline.
- Outputs: Post-incident review with corrective actions, owner assignments, and simulation updates.
- Evidence: Timeline, contributing factors, missing controls, and next corrective actions.
- Decision rights: Incident commander owns follow-up assignment while engineering and communications roles retain accountability for their control gaps.
- Exception policy: Do not close the emergency process while corrective actions remain unnamed or unowned.
- Artifact expectations:
  - `post-incident-corrective-actions` => `retrospective-improvement-log` / Post-incident corrective action log

