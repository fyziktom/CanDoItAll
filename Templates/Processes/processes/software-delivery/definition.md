# Multi-team software delivery and release governance

**Key:** `software-delivery`  
**Criticality:** High  
**Autonomy level:** Guarded  
**Operating mode:** GovernedLive  
**Customer name:** Delivery requester
**Owner name:** Delivery governance board

## Summary
Universal delivery template for planned software change with explicit intake, architecture, implementation, QA, security, release, deployment, and retrospective governance aligned to the current process-module graph model.

## Value statement
Delivers customer-visible or internal software change through a controllable sequence that keeps scope, evidence, dependencies, artifact inputs, and release authority explicit.

## Interface contract summary
Planned delivery can cross application, data, integration, operations, and release-management boundaries, so the process keeps boundary decisions, validation, and release gates explicit.

## Governance notes
No role or approval decision may be collapsed into an implicit chat or tribal handoff; every trust-sensitive transition needs explicit accountability, evidence, dependency mapping, and artifact-input traceability.

## Architecture and constitution rules
- Governance policy: Release readiness requires architecture proof, QA evidence, security posture, rollback readiness, and explicit residual-risk ownership.
- Constitution rule: Role contracts, decision rights, and source-of-truth ownership outlive the specific person or agent assigned at runtime.

## Operating and simulation notes
- Operating mode summary: Governed live execution is allowed only after explicit quality, security, and release gates succeed.
- Simulation readiness: This template is intentionally rich enough for simulation, canvas authoring, analytics, and large-screen browser walkthroughs.

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
- If no sensitive-data, secrets, or trust boundary is touched, security review may downgrade to reviewer status rather than full approver blocking power.
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
- Inputs: Requested change, impact notes, target delivery window, and stakeholder-facing constraints.
- Outputs: Decision-ready scope packet with acceptance boundary, dependency map, assumptions, exclusions, and non-blocking follow-up questions.
- Evidence: Intake notes, acceptance criteria, known exclusions, assumptions, and unresolved dependency register.
- Assumption-forward rule: if the request, project structure, or selected work node already identifies a concrete deliverable and target boundary, do not block this first step only because optional governance details are missing. Record assumptions, exclusions, not-applicable entries, unresolved follow-up questions, and validation hooks for later modeled steps.
- Source-of-truth rule: explicit project-structure requirements remain required in the scope boundary packet and must not be downgraded to optional, excluded, non-acceptance, or follow-up work unless the project structure itself says so or an accepted decision record narrows scope.
- Decision rights: Product owner can refine the ask but cannot waive architecture, data, or release-governance requirements.
- Exception policy: Escalate immediately when timeline pressure conflicts with data-safety or release constraints.
- Artifact expectations:
  - `scope-boundary-packet` => `scope-boundary-packet` / Scope boundary packet. Must preserve explicit project-structure requirements without downgrading them.
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

### 3. Implement bounded delivery change (`implementation`)
- Step kind: Work
- Depends on: architecture-review
- Inputs: Approved architecture path, scope packet, unresolved technical questions, and implementation-slice start criteria.
- Outputs: Reviewable implementation change set that satisfies the current project-structure source of truth, with blockers and rollout checklist inputs visible from the parent process.
- Evidence: Changed or created deliverables, validation outputs, output-placement notes, migration steps when applicable, and touched-surface inventory.
- Decision rights: Parent delivery manager owns sequencing and escalation, while the selected implementation role owns the concrete changes.
- Exception policy: Do not complete the implementation step if any explicit project-structure requirement is omitted, deferred, stack-switched, or unverifiable without an accepted decision record.
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

### 5. Run QA validation and runtime or browser proof (`qa-validation`)
- Step kind: Review
- Depends on: peer-review
- Inputs: Peer-reviewed change set, changed-surface inventory, and release-scope assumptions.
- Outputs: Targeted QA result with runtime/API/browser evidence as applicable, regressions, warning and executed-test counts, shipped entrypoint/runtime consistency, residual quality risk, and an explicit accepted or repair-required branch.
- Evidence: Regression logs, warning-free validation output unless explicitly accepted, nonzero executed-test proof when tests are expected, shipped entrypoint plus referenced-runtime inspection, stale or unreferenced artifact assessment, runtime/API/browser proof as applicable, screenshots for UI surfaces, and defect notes.
- Decision rights: QA lead selects an explicit quality disposition: accepted evidence may continue, while reproducible defects or proof gaps route to repair.
- Exception policy: Do not let schedule pressure replace proof with verbal confidence.
- Branch outcomes:
  - `quality-accepted` / Quality accepted
  - `repair-required` / Repair required
- Artifact expectations:
  - `regression-evidence-pack` => `regression-evidence-pack` / Regression evidence pack
- Checklists: qa-evidence-checklist
- Prompts: prompt-qa-risk-review

### 6. Repair validation findings (`quality-repair`)
- Step kind: Work
- Depends on: qa-validation / `repair-required`
- Inputs: QA repair-required disposition, reviewed implementation package, and failing proof details.
- Outputs: Repaired change set and validation notes ready for QA recheck.
- Evidence: Changed files or deliverables, repair rationale, rerun validation, and remaining risks.
- Decision rights: Lead engineer owns repair execution inside the approved scope and must escalate scope expansion instead of hiding it.
- Exception policy: Do not mark repair complete until the defect cause is addressed and required validation has been rerun.
- Artifact expectations:
  - `quality-repair-change-set` => `implementation-change-set` / Quality repair change set
- Artifact inputs:
  - from `implementation` expectation `implementation-change-set`
  - from `peer-review` expectation `peer-review-note`
  - from `qa-validation` expectation `regression-evidence-pack`

### 7. Re-run QA validation and runtime or browser proof after repair (`qa-recheck`)
- Step kind: Review
- Depends on: quality-repair
- Inputs: Repair change set, original QA findings, and reviewed implementation package.
- Outputs: Recheck result with warning-free validation, nonzero executed-test proof when tests are expected, shipped entrypoint/runtime consistency, runtime/API/browser evidence as applicable, regression evidence, and explicit quality disposition.
- Evidence: Regression logs, warning-free validation output unless explicitly accepted, nonzero executed-test proof when tests are expected, shipped entrypoint plus referenced-runtime inspection, stale or unreferenced artifact assessment, runtime/API/browser proof as applicable, screenshots for UI surfaces, repair verification, and unresolved defects if any.
- Decision rights: QA lead may accept the repaired evidence or escalate when repair remains insufficient.
- Exception policy: Do not approve repaired work when the same failing flow, launch, or proof gap remains unresolved.
- Branch outcomes:
  - `quality-accepted` / Quality accepted
  - `repair-escalation` / Repair escalation
- Artifact expectations:
  - `repaired-regression-evidence-pack` => `regression-evidence-pack` / Repaired regression evidence pack
- Artifact inputs:
  - from `qa-validation` expectation `regression-evidence-pack`
  - from `quality-repair` expectation `quality-repair-change-set`
  - from `implementation` expectation `implementation-change-set`

### 8. Perform security and data-handling review (`security-review`)
- Step kind: Approval
- Depends on: qa-validation / `quality-accepted`
- Inputs: QA-accepted package, changed-surface inventory, and data-handling notes.
- Outputs: Security outcome with explicit approval, block, or exception rationale tied to the declared release boundary.
- Evidence: Security review notes, exception rationale, boundary-applicable controls, and future production controls when they are outside the current boundary.
- Decision rights: Security reviewer owns the sign-off for sensitive-data and boundary-applicable policy exceptions.
- Exception policy: Block release when current-boundary data-handling review capacity is missing, exception rationale is incomplete, or a security risk affects the approved handoff. Record out-of-boundary production controls as recommendations unless explicitly required.
- Artifact expectations:
  - `security-exception-assessment` => `security-exception-assessment` / Security exception assessment
- Artifact inputs:
  - from `implementation` expectation `implementation-change-set`
  - from `peer-review` expectation `peer-review-note`
  - from `architecture-review` expectation `project-structure-context-brief`
  - from `qa-validation` expectation `regression-evidence-pack`
- Checklists: security-review-checklist
- Prompts: prompt-security-review

### 9. Perform security review after repair (`security-review-after-repair`)
- Step kind: Approval
- Depends on: qa-recheck / `quality-accepted`
- Inputs: QA-accepted repaired package, changed-surface inventory, repair notes, and data-handling notes.
- Outputs: Security outcome with explicit approval, block, or exception rationale tied to the declared release boundary.
- Evidence: Security review notes, exception rationale, boundary-applicable controls, and future production controls when they are outside the current boundary.
- Decision rights: Security reviewer owns the sign-off for sensitive-data and boundary-applicable policy exceptions.
- Exception policy: Block release when repaired current-boundary data-handling review capacity is missing, exception rationale is incomplete, or a security risk affects the approved handoff. Record out-of-boundary production controls as recommendations unless explicitly required.
- Artifact expectations:
  - `security-exception-assessment` => `security-exception-assessment` / Security exception assessment
- Artifact inputs:
  - from `quality-repair` expectation `quality-repair-change-set`
  - from `qa-recheck` expectation `repaired-regression-evidence-pack`
  - from `architecture-review` expectation `project-structure-context-brief`
- Checklists: security-review-checklist
- Prompts: prompt-security-review

### 10. Approve first-pass release readiness (`release-approval`)
- Step kind: Approval
- Depends on: implementation, architecture-review, qa-validation / `quality-accepted`, security-review
- Inputs: QA evidence that names the shipped entrypoint and referenced runtime, security outcome, rollback or removal plan, support ownership, and declared release boundary.
- Outputs: Approved or rejected release readiness with accountable rationale and boundary-applicable conditions only.
- Evidence: Approval note, residual risk register, rollback or removal ownership record, declared-boundary confirmation, and confirmation that QA proof matches the actual shipped entrypoint rather than stale or unreferenced artifacts.
- Decision rights: Delivery manager owns the decision and cannot waive missing proof or missing rollback readiness silently.
- Exception policy: Reject release when security review, rollback/removal ownership, support readiness, or proof required by the declared release boundary remains incomplete. Do not reject solely for public deployment, CI, production telemetry, or broad-host controls that the boundary does not require.
- Artifact expectations:
  - `release-approval-record` => `release-approval-record` / Release approval record
- Artifact inputs:
  - from `implementation` expectation `migration-rollout-preparation-checklist`
  - from `qa-validation` expectation `regression-evidence-pack`
  - from `security-review` expectation `security-exception-assessment`
- Checklists: release-go-live-checklist

### 11. Execute first-pass controlled release rollout (`execute-release-rollout`)
- Step kind: Delivery
- Depends on: release-approval
- Inputs: Approved release record, delivery package or artifact root, rollback or removal plan, declared release boundary, and applicable watch points.
- Outputs: Executed rollout, publish, export, or handoff with explicit boundary outcome, rollback/removal status, and watch notes where applicable.
- Evidence: Operator notes, artifact placement or deployment receipt, applicable telemetry or smoke checkpoints, not-applicable entries for out-of-boundary production controls, and any rollback, removal, or release halt.
- Decision rights: Release manager may execute only inside the approved boundary, window, and rollback-trigger limits.
- Exception policy: Trigger halt, rollback, removal, or no-go immediately when applicable telemetry, artifact integrity, user impact, data impact, or operational constraints breach the approved threshold. Do not block for missing public deployment, CI, or production telemetry when the approved boundary is a package or output-folder handoff.
- Artifact expectations:
  - `deployment-watch-log` => `release-readiness-report` / Deployment, handoff, and watch log
- Artifact inputs:
  - from `release-approval` expectation `release-approval-record`

### 12. Capture first-pass post-release learning (`post-release-learning`)
- Step kind: End
- Depends on: execute-release-rollout
- Inputs: Rollout outcome, telemetry record, support observations, and any release incident notes.
- Outputs: Post-release learning review with corrective actions and simulation updates.
- Evidence: Timeline, contributing factors, missing controls, and next corrective actions.
- Decision rights: Delivery manager owns follow-up assignment while architecture and release roles retain responsibility for their own control gaps.
- Exception policy: Do not close the process while critical corrective actions remain unnamed or unowned.
- Artifact expectations:
  - `post-release-learning-log` => `retrospective-improvement-log` / Post-release learning review

### 13. Escalate unresolved repair findings (`repair-escalation`)
- Step kind: Approval
- Depends on: qa-recheck / `repair-escalation`
- Inputs: Post-repair QA escalation, repair notes, and remaining release-blocking evidence.
- Outputs: Explicit no-go, scope reset, or replan decision with accountable owner.
- Evidence: Escalation decision, unresolved defect list, required next repair scope, and owner.
- Decision rights: Delivery manager owns escalation and cannot treat unresolved repair evidence as release-ready.
- Exception policy: Do not continue to security or release approval while quality remains unresolved.
- Artifact expectations:
  - `repair-escalation-record` => `release-approval-record` / Repair escalation record

### 14. Approve repaired release readiness (`release-approval-after-repair`)
- Step kind: Approval
- Depends on: implementation, architecture-review, qa-recheck / `quality-accepted`, security-review-after-repair
- Inputs: Repaired QA evidence that names the shipped entrypoint and referenced runtime, post-repair security outcome, rollback or removal plan, support ownership, and declared release boundary.
- Outputs: Approved or rejected repaired release readiness with accountable rationale and boundary-applicable conditions only.
- Evidence: Approval note, residual risk register, rollback or removal ownership record, declared-boundary confirmation, and confirmation that repaired QA proof matches the actual shipped entrypoint rather than stale or unreferenced artifacts.
- Decision rights: Delivery manager owns the decision and cannot waive missing proof or missing rollback readiness silently.
- Exception policy: Reject release when security review, rollback/removal ownership, support readiness, or proof required by the declared release boundary remains incomplete. Do not reject solely for public deployment, CI, production telemetry, or broad-host controls that the boundary does not require.
- Artifact expectations:
  - `release-approval-record` => `release-approval-record` / Release approval record
- Artifact inputs:
  - from `implementation` expectation `migration-rollout-preparation-checklist`
  - from `qa-recheck` expectation `repaired-regression-evidence-pack`
  - from `security-review-after-repair` expectation `security-exception-assessment`
  - from `architecture-review` expectation `project-structure-context-brief`
- Checklists: release-go-live-checklist

### 15. Execute repaired controlled release rollout (`execute-release-rollout-after-repair`)
- Step kind: Delivery
- Depends on: release-approval-after-repair
- Inputs: Approved repaired release record, delivery package or artifact root, rollback or removal plan, declared release boundary, and applicable watch points.
- Outputs: Executed repaired rollout, publish, export, or handoff with explicit boundary outcome, rollback/removal status, and watch notes where applicable.
- Evidence: Operator notes, artifact placement or deployment receipt, applicable telemetry or smoke checkpoints, not-applicable entries for out-of-boundary production controls, and any rollback, removal, or release halt.
- Decision rights: Release manager may execute only inside the approved boundary, window, and rollback-trigger limits.
- Exception policy: Trigger halt, rollback, removal, or no-go immediately when applicable telemetry, artifact integrity, user impact, data impact, or operational constraints breach the approved threshold. Do not block for missing public deployment, CI, or production telemetry when the approved boundary is a package or output-folder handoff.
- Artifact expectations:
  - `deployment-watch-log` => `release-readiness-report` / Deployment, handoff, and watch log
- Artifact inputs:
  - from `release-approval-after-repair` expectation `release-approval-record`

### 16. Capture repaired-release learning (`post-release-learning-after-repair`)
- Step kind: End
- Depends on: execute-release-rollout-after-repair
- Inputs: Repaired rollout outcome, telemetry record, support observations, and any release incident notes.
- Outputs: Post-release learning review with corrective actions and simulation updates.
- Evidence: Timeline, contributing factors, missing controls, and next corrective actions.
- Decision rights: Delivery manager owns follow-up assignment while architecture and release roles retain responsibility for their own control gaps.
- Exception policy: Do not close the process while critical corrective actions remain unnamed or unowned.
- Artifact expectations:
  - `post-release-learning-log` => `retrospective-improvement-log` / Post-release learning review
