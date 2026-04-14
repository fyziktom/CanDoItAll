# Multi-team software delivery and release governance

**Key:** `software-delivery`  
**Criticality:** High  
**Autonomy level:** Guarded  
**Operating mode:** GovernedLive  
**Customer name:** Enterprise Billing Platform  
**Owner name:** Digital delivery governance board

## Summary
Universal delivery template for planned software change with explicit intake, architecture, implementation, QA, security, release, deployment, and retrospective governance aligned to the current process-module graph model.

## Value statement
Delivers customer-visible or internal software change through a controllable sequence that keeps scope, evidence, dependencies, artifact inputs, and release authority explicit.

## Interface contract summary
Feature delivery crosses process authoring, workspace, billing, and release-management boundaries and therefore needs explicit design, validation, and release gates.

## Governance notes
No role or approval decision may be collapsed into an implicit chat or tribal handoff; every trust-sensitive transition needs explicit accountability, evidence, dependency mapping, and artifact-input traceability.

## Architecture and constitution rules
- Governance policy: Release readiness requires architecture proof, QA evidence, security posture, rollback readiness, and explicit residual-risk ownership.
- Constitution rule: Role contracts, decision rights, and source-of-truth ownership outlive the specific person or agent assigned at runtime.

## Operating and simulation notes
- Operating mode summary: Governed live execution is allowed only after explicit quality, security, and release gates succeed.
- Simulation readiness: This scenario is intentionally rich enough for simulation, canvas authoring, analytics, and large-screen browser walkthroughs.

## Source frameworks
- nist-ssdf
- owasp-samm
- openchain
- spdx
- slsa

## Process metrics
- Median time from approved scope boundary to architecture decision record.
- Share of releases approved without rollback-plan defects.
- Percentage of release approvals with complete QA, security, and rollback artifact inputs.
- Rate of releases requiring rollback or controlled halt inside the first telemetry window.
- Number of corrective actions closed from post-release learning within the committed SLA.

## Process risks
- Architecture drift if implementation bypasses the approved canonical-model decision.
- Weak release decisions if artifact inputs are missing or stale.
- False confidence when QA evidence is verbal instead of durable.
- Security exceptions hidden inside release pressure.
- Rollout instability when telemetry watch points or rollback triggers are implicit.

## Tailoring rules
- Low-risk internal changes may shorten QA depth, but the release-approval step still requires explicit artifact inputs.
- If no tenant-data or secrets boundary is touched, security review may downgrade to reviewer status rather than full approver blocking power.
- Multiple implementation slices may be modeled as separate work steps, but all must converge into the same release-approval artifact-input contract.
- Hotfix or incident-driven work must use the dedicated emergency template rather than bypassing this one.

## Role usages
- `product-owner` / **Product owner** — Convert business intent into an explicit delivery contract with clear acceptance boundaries and prioritized value trade-offs.
- `delivery-manager` / **Delivery manager** — Keep the process executable by turning desired outcomes into a feasible, staffed, time-aware delivery path with explicit escalation triggers.
- `solution-architect` / **Solution architect** — Protect maintainability and operability by reviewing design options, target architecture fit, and downstream integration impact before costly implementation commitment.
- `lead-engineer` / **Lead engineer** — Own the change set and keep implementation evidence aligned with the approved architecture and release boundary.
- `qa-lead` / **QA lead** — Challenge whether the delivered change is proven enough for its risk profile and make test evidence decision-ready for release governance.
- `security-reviewer` / **Security reviewer** — Ensure changes touching trust boundaries, sensitive data, dependencies, or operational attack surface are reviewed proportionally and documented defensibly.
- `release-manager` / **Release manager** — Coordinate go-live preparation and keep rollback and telemetry watch ownership explicit before and during release execution.

## Steps
### 1. Clarify scope and release boundary (`feature-intake`)
- Step kind: Start
- Depends on: None
- Inputs: Feature request, tenant-impact notes, target release window, and customer-facing constraints.
- Outputs: Decision-ready scope packet with acceptance boundary and dependency map.
- Evidence: Intake notes, acceptance criteria, known exclusions, and unresolved dependency register.
- Decision rights: Product owner can refine the ask but cannot waive architecture, data, or release-governance requirements.
- Exception policy: Escalate immediately when timeline pressure conflicts with data-safety or release constraints.
- Artifact expectations:
  - `scope-boundary-packet` => `scope-boundary-packet` / Scope boundary packet
- Checklists: intake-completeness-checklist
- Prompts: prompt-intake-summarizer

### 2. Review architecture and canonical-model impact (`architecture-review`)
- Step kind: Review
- Depends on: feature-intake
- Inputs: Scope packet, touched modules, data-flow map, and integration concerns.
- Outputs: Approved architecture path with explicit trade-offs and rejected alternatives.
- Evidence: Architecture notes, canonical-model decision, and source-of-truth rationale.
- Decision rights: Architecture authority recommends the path; delivery manager remains accountable for choosing the approved option.
- Exception policy: Do not continue while source-of-truth ownership or migration responsibility remains ambiguous.
- Artifact expectations:
  - `architecture-decision-record` => `architecture-decision-record` / Architecture decision record
- Checklists: architecture-gate-checklist
- Validations: validate-architecture-boundaries
- Prompts: prompt-architecture-review

### 3. Implement feature, tests, and migration notes (`implementation`)
- Step kind: Work
- Depends on: architecture-review
- Inputs: Approved architecture path, scope packet, and unresolved technical questions.
- Outputs: Review-ready implementation with tests, migration notes, and rollout checklist inputs.
- Evidence: Change set, test outputs, migration steps, and touched-surface inventory.
- Decision rights: Lead engineer can implement but cannot silently alter the approved architecture or reduce proof depth.
- Exception policy: Pause when migration impact, performance risk, or dependency scope grows beyond the approved path.
- Artifact expectations:
  - `implementation-change-set` => `implementation-change-set` / Implementation change set
  - `migration-rollout-preparation-checklist` => `rollback-plan` / Migration and rollout preparation checklist
- Checklists: implementation-readiness-checklist, delivery-scope-freeze-checklist
- Validations: validate-migration-rehearsal
- Prompts: prompt-release-scope-recap

### 4. Complete peer review and integration readiness (`peer-review`)
- Step kind: Review
- Depends on: implementation
- Inputs: Implementation package, architecture decision record, and changed-surface inventory.
- Outputs: Peer-reviewed change set with explicit residual risk and follow-up items.
- Evidence: Review notes, unresolved issues list, and approved follow-up actions.
- Decision rights: Reviewers may block unsafe merge or release progression until the change set satisfies design and evidence expectations.
- Exception policy: Do not downgrade architecture, data, or migration concerns to cosmetic comments.
- Artifact expectations:
  - `peer-review-note` => `test-evidence-pack` / Peer review note
- Artifact inputs:
  - from `implementation` expectation `implementation-change-set`

### 5. Run QA validation and browser proof (`qa-validation`)
- Step kind: Review
- Depends on: peer-review
- Inputs: Peer-reviewed change set, changed-surface inventory, and release-scope assumptions.
- Outputs: Targeted QA result with screenshots, regressions, and explicit residual quality risk.
- Evidence: Regression logs, browser proof, screenshots, and defect notes.
- Decision rights: QA lead may block release progression when evidence is too thin for the risk profile.
- Exception policy: Do not let schedule pressure replace proof with verbal confidence.
- Artifact expectations:
  - `regression-evidence-pack` => `regression-evidence-pack` / Regression evidence pack
- Checklists: qa-evidence-checklist
- Prompts: prompt-qa-risk-review

### 6. Perform security and data-handling review (`security-review`)
- Step kind: Approval
- Depends on: peer-review
- Inputs: Peer-reviewed package, changed-surface inventory, and data-handling notes.
- Outputs: Security outcome with explicit approval, block, or exception rationale.
- Evidence: Security review notes, exception rationale, and approved controls.
- Decision rights: Security reviewer owns the sign-off for tenant-data and policy exceptions.
- Exception policy: Block release when data-handling review capacity is missing or exception rationale is incomplete.
- Artifact expectations:
  - `security-exception-assessment` => `security-exception-assessment` / Security exception assessment
- Checklists: security-review-checklist
- Prompts: prompt-security-review

### 7. Approve release readiness (`release-approval`)
- Step kind: Approval
- Depends on: implementation, qa-validation, security-review
- Inputs: QA evidence, security outcome, rollback plan, and support ownership.
- Outputs: Approved or rejected release readiness with accountable rationale.
- Evidence: Approval note, residual risk register, and rollback ownership record.
- Decision rights: Delivery manager owns the decision and cannot waive missing proof or missing rollback readiness silently.
- Exception policy: Reject release when security review, rollback ownership, or support readiness remains incomplete.
- Artifact expectations:
  - `release-approval-record` => `release-approval-record` / Release approval record
- Artifact inputs:
  - from `implementation` expectation `migration-rollout-preparation-checklist`
  - from `qa-validation` expectation `regression-evidence-pack`
  - from `security-review` expectation `security-exception-assessment`
- Checklists: release-go-live-checklist

### 8. Execute controlled release rollout (`execute-release-rollout`)
- Step kind: Delivery
- Depends on: release-approval
- Inputs: Approved release record, deployment package, rollback plan, and telemetry watch points.
- Outputs: Executed rollout with explicit telemetry outcome, rollback status, and live-watch notes.
- Evidence: Operator notes, telemetry checkpoints, and any rollback invocation or release halt.
- Decision rights: Release manager may execute only inside the approved window and rollback-trigger boundaries.
- Exception policy: Trigger halt or rollback immediately when telemetry, tenant impact, or operational constraints breach the approved threshold.
- Artifact expectations:
  - `deployment-watch-log` => `release-readiness-report` / Deployment and telemetry watch log
- Artifact inputs:
  - from `release-approval` expectation `release-approval-record`

### 9. Capture post-release learning and corrective actions (`post-release-learning`)
- Step kind: End
- Depends on: execute-release-rollout
- Inputs: Rollout outcome, telemetry record, support observations, and any release incident notes.
- Outputs: Post-release learning review with corrective actions and simulation updates.
- Evidence: Timeline, contributing factors, missing controls, and next corrective actions.
- Decision rights: Delivery manager owns follow-up assignment while architecture and release roles retain responsibility for their own control gaps.
- Exception policy: Do not close the process while critical corrective actions remain unnamed or unowned.
- Artifact expectations:
  - `post-release-learning-log` => `retrospective-improvement-log` / Post-release learning review

