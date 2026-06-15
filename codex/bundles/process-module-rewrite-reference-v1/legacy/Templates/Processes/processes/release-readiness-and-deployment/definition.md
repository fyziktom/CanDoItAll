# Release readiness and deployment control

**Key:** `release-readiness-and-deployment`  
**Criticality:** High  
**Autonomy level:** Guarded  
**Operating mode:** GovernedLive  
**Customer name:** Release management and service operations  
**Owner name:** Change governance board

## Summary
Control the path from scope freeze to cutover, live watch, rollback readiness, and learning with explicit release evidence, approvals, and watch ownership.

## Value statement
Lower release risk by turning scattered evidence, deployment choreography, and go-live authority into one typed control process that works for human teams and AI-assisted orchestration.

## Interface contract summary
Implementation outputs, quality evidence, operational readiness, support coverage, and release-window constraints are merged into one bounded deployment decision.

## Governance notes
A release is not ready because a team feels ready; it is ready only when the control process can prove scope, evidence, approval, and rollback preparedness.

## Architecture and constitution rules
- Governance policy: Go-live requires scope freeze, evidence synthesis, change-window coverage, rollback readiness, explicit approval, and named watch coverage.
- Constitution rule: No one may reinterpret an approval outside the conditions that were explicitly recorded.

## Operating and simulation notes
- Operating mode summary: Guarded live execution: automation may prepare, validate, and monitor, but final approval and exception handling remain human.
- Simulation readiness: Supports cutover rehearsal, watch roster simulation, dashboard integration, and post-release control tuning.

## Source frameworks
- nist-ssdf
- owasp-samm
- slsa

## Process metrics
- Release readiness lead time
- Number of open exceptions at go/no-go
- Rollback invocation count
- Time to declare post-release stability

## Process risks
- Deployment scope drifts after evidence collection.
- Watch coverage or escalation ownership is incomplete.
- Rollback plan exists on paper but is not operationally ready.
- Approval is issued without clear live conditions.

## Tailoring rules
- For low-risk internal releases, merge evidence review and change-window check if watch coverage remains typed.
- For customer-visible releases, customer communication artifacts should be linked even if not locally stored in the template.
- For AI-generated release notes or deployment plans, require prompt-package and review evidence artifacts.

## Role usages
- `change-manager` / **Change manager** — Coordinate production change timing, communications, operational readiness, and deployment-event discipline.
- `delivery-manager` / **Delivery manager** — Keep the process executable by turning desired outcomes into a feasible, staffed, time-aware delivery path with explicit escalation triggers.
- `service-owner` / **Service owner** — Represent live-service constraints, operational history, and post-release accountability in change decisions.
- `qa-lead` / **QA lead** — Challenge whether the delivered change is proven enough for its risk profile and make test evidence decision-ready for release governance.
- `security-reviewer` / **Security reviewer** — Ensure changes touching trust boundaries, sensitive data, dependencies, or operational attack surface are reviewed proportionally and documented defensibly.
- `platform-engineer` / **Platform engineer** — Provide the reliable platform path required to build, deploy, observe, and if necessary roll back changes safely.
- `release-approver` / **Release approver** — Decide whether the accumulated evidence is sufficient to expose the change to real users, data, and operational load.

## Steps
### 1. Freeze release scope and deployment boundary (`scope-freeze`)
- Step kind: Start
- Depends on: None
- Inputs: Candidate release contents, change logs, linked work items, and deployment boundary.
- Outputs: Frozen release scope with explicit inclusions and exclusions.
- Evidence: Scope freeze note and environment targeting summary.
- Decision rights: Change manager controls the frozen boundary; delivery manager reviews business commitment impact.
- Exception policy: No readiness claim is valid if scope can still silently drift.
- Artifact expectations:
  - `scope-freeze-intake-brief` => `intake-brief` / Release scope freeze note
  - `scope-freeze-implementation-plan` => `implementation-plan` / Deployment strategy outline
- Checklists: release-go-live-checklist
- Validations: validation-proof-sufficient

### 2. Synthesize readiness evidence and open risks (`readiness-synthesis`)
- Step kind: Work
- Depends on: scope-freeze
- Inputs: Frozen release scope, QA runs, security notes, environment status, and known exceptions.
- Outputs: Release readiness package with explicit open-risk inventory.
- Evidence: Release readiness report and linked evidence references.
- Decision rights: Delivery manager and QA lead own evidence synthesis; security reviewer and service owner contribute risk posture.
- Exception policy: Do not summarize evidence that you have not actually inspected or linked.
- Artifact expectations:
  - `readiness-synthesis-release-readiness-report` => `release-readiness-report` / Release readiness report
  - `readiness-synthesis-test-evidence-pack` => `test-evidence-pack` / Test evidence pack
- Checklists: qa-evidence-checklist, release-go-live-checklist
- Validations: validation-proof-sufficient
- Prompts: prompt-release-decision, prompt-qa-test-design

### 3. Rehearse cutover and confirm watch coverage (`cutover-rehearsal`)
- Step kind: Review
- Depends on: readiness-synthesis
- Inputs: Release readiness package, deployment sequence, monitoring plan, and on-call availability.
- Outputs: Rehearsed cutover package and watch roster with gaps called out.
- Evidence: Watch roster, cutover rehearsal notes, and gap log.
- Decision rights: Change manager owns choreography; platform engineer and service owner validate live-operability.
- Exception policy: If critical watch roles are missing or unreachable, hold the release.
- Artifact expectations:
  - `cutover-rehearsal-cutover-watch-roster` => `cutover-watch-roster` / Cutover watch roster
  - `cutover-rehearsal-rollback-plan` => `rollback-plan` / Rollback plan
- Checklists: cutover-rehearsal-checklist, release-go-live-checklist
- Validations: validate-watch-coverage, validation-rollback-ready
- Prompts: prompt-cutover-command-brief

### 4. Run final security review and go/no-go approval (`security-and-go-no-go`)
- Step kind: Approval
- Depends on: cutover-rehearsal
- Inputs: Readiness package, rehearsal output, rollback plan, and any exception requests.
- Outputs: Approved or held release decision with explicit conditions.
- Evidence: Release approval record and exception note if applicable.
- Decision rights: Release approver owns the final decision; security reviewer may hold the release for unresolved control gaps.
- Exception policy: No release may proceed on informal approval.
- Branch outcomes: go (Go), hold (Hold)
- Artifact expectations:
  - `security-and-go-no-go-security-review-note` => `security-review-note` / Security review note
  - `security-and-go-no-go-release-readiness-report` => `release-readiness-report` / Final go/no-go decision
- Checklists: security-review-checklist, release-go-live-checklist
- Validations: validation-security-clear, validation-release-authorized, validation-rollback-ready
- Prompts: prompt-release-decision, prompt-security-review

### 5. Execute cutover and observe live telemetry (`execute-cutover`)
- Step kind: Delivery
- Depends on: security-and-go-no-go
- Inputs: Go decision, cutover brief, watch roster, telemetry thresholds, and rollback triggers.
- Outputs: Executed release with live status and next action record.
- Evidence: Operator execution note, telemetry snapshots, and rollback note if used.
- Decision rights: Platform engineer performs the cutover; change manager controls timing and handoffs.
- Exception policy: Rollback immediately when thresholds or instructions dictate it.
- Artifact expectations:
  - `execute-cutover-rollback-plan` => `rollback-plan` / Rollback plan
  - `execute-cutover-provenance-report` => `provenance-report` / Release execution provenance note
- Checklists: release-go-live-checklist
- Validations: validation-release-authorized

### 6. Confirm stability, close watch, and capture learning (`stabilize-and-close`)
- Step kind: End
- Depends on: execute-cutover
- Inputs: Cutover outcome, telemetry trend, incident or support notes, and watch coverage observations.
- Outputs: Closed release watch with stability declaration and improvements.
- Evidence: Stability declaration and improvement log.
- Decision rights: Service owner declares stability; change manager closes the change record; delivery manager captures learning.
- Exception policy: Do not declare stability without observed evidence for the agreed horizon.
- Artifact expectations:
  - `stabilize-and-close-retrospective-improvement-log` => `retrospective-improvement-log` / Retrospective improvement log
  - `stabilize-and-close-release-readiness-report` => `release-readiness-report` / Release closure note
- Checklists: release-go-live-checklist
- Validations: validation-proof-sufficient

