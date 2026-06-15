# Architecture decision governance and ADR stewardship

**Key:** `architecture-decision-governance`  
**Criticality:** High  
**Autonomy level:** Assisted  
**Operating mode:** AssistedExecution  
**Customer name:** Product and engineering leadership  
**Owner name:** Architecture governance board

## Summary
Guide architecture decisions from intake through option analysis, governance review, approval, and downstream rollout guidance without losing domain accountability or reusable evidence.

## Value statement
Improve decision quality and prevent repeated architectural churn by forcing explicit options, trade-offs, approval reasoning, and follow-up obligations into durable ADR-style records.

## Interface contract summary
Decision demand, domain context, system constraints, and change risks are transformed into a reviewable architecture decision record with named owners and adoption conditions.

## Governance notes
Architecture decisions are treated as operating contracts, not slideware. The process requires evidence, option comparison, and explicit domain-owner participation.

## Architecture and constitution rules
- Governance policy: No architecture decision may be accepted without explicit context, considered options, decision rationale, risk notes, and named adoption obligations.
- Constitution rule: The board may delay decisions, but cannot hide uncertainty by approving vague direction that nobody owns.

## Operating and simulation notes
- Operating mode summary: Assisted execution with human approval. Analysis may be AI-assisted, but acceptance and organizational commitment stay human.
- Simulation readiness: Good fit for decision-board rehearsal, architecture review automation, and portfolio-level consistency analysis.

## Source frameworks
- owasp-samm
- nist-ssdf
- nist-ssdf-ai

## Process metrics
- Decision cycle time
- Percentage of decisions with explicit adoption owners
- Reversal rate within 90 days
- Number of decisions with unresolved security or compliance follow-ups

## Process risks
- Domain owners are missing and decisions become detached from operational reality.
- The board approves direction without concrete trade-off evidence.
- Security/compliance concerns are added too late to influence the actual decision.
- Decisions are written but not translated into rollout guidance.

## Tailoring rules
- For low-risk local decisions, merge governance review and approval into one step if domain-owner coverage remains explicit.
- For AI or data-sensitive architecture changes, make compliance-steward review mandatory.
- For cross-team platform decisions, add linked rollout work items after approval.

## Role usages
- `governance-facilitator` / **Governance facilitator** — Keep architecture governance lightweight but rigorous by ensuring the right decision, evidence, and follow-up happen at the right time.
- `domain-owner` / **Domain owner** — Represent domain semantics, operational realities, and long-term consequences when architecture choices affect a specific capability area.
- `solution-architect` / **Solution architect** — Protect maintainability and operability by reviewing design options, target architecture fit, and downstream integration impact before costly implementation commitment.
- `product-owner` / **Product owner** — Convert business intent into an explicit delivery contract with clear acceptance boundaries and prioritized value trade-offs.
- `security-reviewer` / **Security reviewer** — Ensure changes touching trust boundaries, sensitive data, dependencies, or operational attack surface are reviewed proportionally and documented defensibly.
- `compliance-steward` / **Compliance steward** — Ensure required compliance considerations are identified early, translated into actionable checks, and retained as evidence.
- `service-owner` / **Service owner** — Represent live-service constraints, operational history, and post-release accountability in change decisions.

## Steps
### 1. Capture architecture decision demand (`decision-intake`)
- Step kind: Start
- Depends on: None
- Inputs: Design concern, change proposal, incident learning, or product demand requiring architecture direction.
- Outputs: Typed decision intake with named context owner and impacted domains.
- Evidence: Decision intake brief and initial affected-system list.
- Decision rights: Governance facilitator may reject vague requests that do not define a real decision.
- Exception policy: Do not proceed when scope and affected domains are not explicit.
- Artifact expectations:
  - `decision-intake-decision-brief` => `decision-brief` / Decision brief
  - `decision-intake-intake-brief` => `intake-brief` / Architecture demand intake
- Checklists: decision-readiness-checklist
- Validations: validate-domain-owner-coverage
- Prompts: prompt-adr-question-framing

### 2. Assemble context, constraints, and options (`context-and-options`)
- Step kind: Work
- Depends on: decision-intake
- Inputs: Decision intake, current system context, risk drivers, and domain constraints.
- Outputs: ADR draft with options, trade-offs, and recommendation.
- Evidence: Draft ADR and supporting context evidence.
- Decision rights: Solution architect authors the option analysis; domain owner confirms local reality and adoption cost.
- Exception policy: If fewer than two meaningful options are considered, explain why explicitly.
- Artifact expectations:
  - `context-and-options-architecture-decision-record` => `architecture-decision-record` / Architecture decision record
  - `context-and-options-decision-brief` => `decision-brief` / Option comparison brief
- Checklists: architecture-gate-checklist, decision-readiness-checklist
- Validations: validation-architecture-aligned, validate-domain-owner-coverage
- Prompts: prompt-architecture-review

### 3. Review security, compliance, and policy impact (`security-compliance-review`)
- Step kind: Review
- Depends on: context-and-options
- Inputs: ADR draft, system context, data flows, control inventory, and third-party implications.
- Outputs: Review note covering security and policy implications.
- Evidence: Security and compliance findings tied to decision options.
- Decision rights: Security reviewer and compliance steward may force rework when controls are under-specified.
- Exception policy: No approval when control obligations are ambiguous.
- Artifact expectations:
  - `security-compliance-review-security-review-note` => `security-review-note` / Security review note
  - `security-compliance-review-provenance-report` => `provenance-report` / Dependency and provenance impact note
- Checklists: security-review-checklist
- Validations: validation-security-clear
- Prompts: prompt-architecture-review

### 4. Make the board decision and record rationale (`board-decision`)
- Step kind: Approval
- Depends on: security-compliance-review
- Inputs: Reviewed ADR draft, option analysis, and security/compliance notes.
- Outputs: Approved or deferred architecture decision with adoption conditions.
- Evidence: Board decision record and named follow-up obligations.
- Decision rights: Governance facilitator records the decision; service owner and domain owner must both acknowledge adoption obligations.
- Exception policy: Do not approve with placeholders for ownership or decision rationale.
- Branch outcomes: approved (Approved), deferred (Deferred)
- Artifact expectations:
  - `board-decision-architecture-decision-record` => `architecture-decision-record` / Approved architecture decision record
- Checklists: decision-readiness-checklist
- Validations: validation-architecture-aligned, validate-domain-owner-coverage

### 5. Publish rollout guidance and revisit triggers (`rollout-guidance`)
- Step kind: End
- Depends on: board-decision
- Inputs: Approved ADR and adoption conditions.
- Outputs: Published rollout guidance and revisit criteria.
- Evidence: Architecture guidance note and future revisit triggers.
- Decision rights: Solution architect owns technical guidance; governance facilitator ensures revisit criteria are durable.
- Exception policy: If rollout expectations cannot be stated clearly, the decision is not ready for downstream implementation.
- Artifact expectations:
  - `rollout-guidance-implementation-plan` => `implementation-plan` / Architecture rollout guidance
  - `rollout-guidance-retrospective-improvement-log` => `retrospective-improvement-log` / Architecture revisit trigger log
- Checklists: architecture-gate-checklist
- Validations: validation-architecture-aligned

