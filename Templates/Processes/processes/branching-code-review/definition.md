# Branching code review and merge governance

**Key:** `branching-code-review`  
**Criticality:** High  
**Autonomy level:** Assisted  
**Operating mode:** AssistedExecution  
**Customer name:** Developer productivity platform  
**Owner name:** Engineering governance board

## Summary
Branch-heavy review-routing template aligned to the current process-module graph model, with explicit decision role, routed outcomes, default handling, error handling, artifact inputs, and route-specific merge governance.

## Value statement
Makes software review routing, QA, security, architecture escalation, route-specific merge approval, default handling, and error handling explicit, replayable, and auditable.

## Interface contract summary
Code review is not a single linear gate; it needs an explicit routing surface that shows who decides the next lane and what each routed outcome means.

## Governance notes
Review routing must stay explicit, strongly typed, and replayable so later merge or governance questions can be answered from the model instead of chat fragments.

## Architecture and constitution rules
- Governance policy: Merge readiness depends on the selected routed lane, so the current graph model keeps each merge approval route-specific instead of implying an unsupported OR-join.
- Constitution rule: Role contracts for authoring, routing, QA, security, architecture, and merge approval stay explicit even when individuals change.

## Operating and simulation notes
- Operating mode summary: Assisted execution is acceptable because the branching goal is to make routing explicit before deeper automation is attempted.
- Simulation readiness: This scenario is intentionally branch-heavy so canvas screenshots, authoring validation, and architecture-gap review can use one realistic software-development example.

## Source frameworks
- nist-ssdf
- owasp-samm
- openchain
- spdx
- slsa

## Process metrics
- Percentage of reviewed changes with an explicit routed outcome instead of implicit chat resolution.
- Rate of route-to-lane mismatches caught by validation.
- Median cycle time for repair, QA, security, and architecture review lanes.
- Share of default-lane normalizations that later proved to be workflow errors.
- Merge approvals with complete route provenance and residual-risk framing.

## Process risks
- Implicit routing hidden in comments or chat.
- Default-lane normalization abused to mask malformed workflow states.
- Merge approval bypassing routed evidence.
- QA or security lanes skipped because the route was not modeled explicitly.
- Error lane ignored, leaving branch-router defects undiscoverable.

## Tailoring rules
- Small changes may route directly to merge approval, but the router still records the outcome explicitly.
- Teams may add more routed lanes, yet default and error routes remain mandatory system outcomes.
- Repair loops may repeat, but each cycle must produce a new explicit routing record.
- If the module later gains explicit OR-join support, teams may collapse route-specific merge approvals back into a single merge gate without hiding the routed evidence.

## Role usages
- `author` / **Author** — Keep authorship and first-pass implementation evidence attached to the person or agent that produced the change.
- `review-lead` / **Review lead** — Convert review findings into an explicit next lane and preserve replayable routing for every reviewed change.
- `qa-lead` / **QA lead** — Challenge whether the delivered change is proven enough for its risk profile and make test evidence decision-ready for release governance.
- `security-reviewer` / **Security reviewer** — Ensure changes touching trust boundaries, sensitive data, dependencies, or operational attack surface are reviewed proportionally and documented defensibly.
- `solution-architect` / **Solution architect** — Protect maintainability and operability by reviewing design options, target architecture fit, and downstream integration impact before costly implementation commitment.
- `merge-approver` / **Merge approver** — Approve merge only when review routing, validation evidence, and residual risk framing are explicit.

## Steps
### 1. Prepare pull request and reviewer brief (`prepare-pull-request`)
- Step kind: Start
- Depends on: None
- Inputs: Accepted work item, implementation diff, test evidence, and draft release notes.
- Outputs: Review-ready pull request packet with typed reviewer context and proof references.
- Evidence: Diff summary, screenshots, changed-surface list, and rollback notes.
- Decision rights: The author may prepare the review packet but cannot declare the change ready for merge without explicit routing.
- Exception policy: Pause immediately if proof, rollback notes, or touched-surface inventory is incomplete.
- Artifact expectations:
  - `pull-request-readiness-packet` => `pull-request-readiness-packet` / Pull request readiness packet
- Checklists: pull-request-packet-checklist
- Prompts: prompt-pr-review-packet

### 2. Route code review disposition (`route-review-disposition`)
- Step kind: Decision
- Depends on: prepare-pull-request
- Decision role: `review-lead` / Review lead
- Inputs: Review-ready pull request packet, reviewer comments, and changed-surface risk notes.
- Outputs: Explicit next-lane selection for the reviewed change.
- Evidence: Review notes, chosen route, and reasons for the selected lane.
- Decision rights: Review lead owns the switch outcome and must keep the route explicit on the canvas.
- Exception policy: Do not bury routing logic inside comments or verbal agreements; the chosen lane must stay modeled and replayable.
- Branch outcomes: repairs-required (Repairs required), qa-validation (QA validation), security-review (Security review), architecture-review (Architecture review), ready-for-merge (Ready for merge), __default__ (Default), __error__ (Error)
- Artifact expectations:
  - `review-routing-decision-record` => `review-routing-decision-record` / Review routing decision record
- Artifact inputs:
  - from `prepare-pull-request` expectation `pull-request-readiness-packet`
- Checklists: review-router-safety-checklist
- Validations: validation-review-packet-complete, validate-review-router-safe
- Prompts: prompt-review-normalization

### 3. Complete focused repair pass (`repair-pass`)
- Step kind: Work
- Depends on: route-review-disposition/repairs-required
- Inputs: Review routing decision, blocking review comments, and the original pull request packet.
- Outputs: Updated change set and repair brief ready for another routing decision or merge path.
- Evidence: Repair diff, note of addressed concerns, and any remaining risk.
- Decision rights: Author may fix only the bounded review issues unless the route is re-opened explicitly.
- Exception policy: Escalate if repairs materially expand scope beyond the routed review request.
- Artifact expectations:
  - `repair-brief` => `repair-brief` / Repair brief
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`

### 4. Validate QA lane and browser proof (`validate-qa-lane`)
- Step kind: Review
- Depends on: route-review-disposition/qa-validation
- Inputs: Review routing decision, pull request packet, and changed-surface inventory.
- Outputs: QA lane result with proof depth and residual quality risk.
- Evidence: QA notes, screenshots, regression result, and unresolved concerns.
- Decision rights: QA lead may keep the change out of merge until risk is explicit and acceptable.
- Exception policy: Do not convert QA lane results into chat-only confidence.
- Artifact expectations:
  - `qa-lane-validation-note` => `qa-lane-validation-note` / QA lane validation note
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`

### 5. Perform security review before merge (`perform-security-review`)
- Step kind: Approval
- Depends on: route-review-disposition/security-review
- Inputs: Review routing decision, pull request packet, and trust-sensitive changed-surface notes.
- Outputs: Security lane outcome with explicit approval, block, or exception rationale.
- Evidence: Security notes, decision rationale, and residual risk owner.
- Decision rights: Security reviewer owns the trust-sensitive merge gate for routed changes.
- Exception policy: Do not let merge urgency waive data-handling or secrets review.
- Artifact expectations:
  - `security-lane-note` => `security-review-note` / Security lane review note
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`

### 6. Escalate architecture consequences (`architecture-escalation`)
- Step kind: Review
- Depends on: route-review-disposition/architecture-review
- Inputs: Review routing decision, change packet, and impacted boundary notes.
- Outputs: Architecture escalation outcome with explicit next action.
- Evidence: Architecture concern, decision, and approved next action.
- Decision rights: Solution architect owns the architecture lane and may return the change for redesign or authorize progression with conditions.
- Exception policy: Do not hide canonical-model or module-boundary concerns inside informal reviewer comments.
- Artifact expectations:
  - `architecture-escalation-brief` => `architecture-escalation-brief` / Architecture escalation brief
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`

### 7. Approve direct merge route (`approve-merge`)
- Step kind: Approval
- Depends on: route-review-disposition/ready-for-merge
- Inputs: Review routing decision, residual risk framing, and release-note completeness for the direct merge route.
- Outputs: Approved or rejected direct merge decision with explicit rationale.
- Evidence: Direct merge note, residual risks, and next action if the direct merge route is blocked.
- Decision rights: Merge approver owns the direct merge route and must keep the rationale explicit.
- Exception policy: Reject the direct merge route when review routing evidence or residual risk framing is incomplete.
- Artifact expectations:
  - `direct-merge-readiness-note` => `merge-readiness-note` / Direct merge readiness note
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`

### 8. Approve merge after QA validation (`approve-merge-after-qa`)
- Step kind: Approval
- Depends on: route-review-disposition/qa-validation, validate-qa-lane
- Inputs: Review routing decision, QA lane validation note, residual risk framing, and release-note completeness.
- Outputs: Approved or rejected post-QA merge decision with explicit rationale.
- Evidence: QA merge note, residual risks, and next action if merge is blocked after QA.
- Decision rights: Merge approver owns the post-QA merge gate and cannot bypass QA evidence.
- Exception policy: Reject merge when QA evidence or route provenance is incomplete.
- Artifact expectations:
  - `qa-merge-readiness-note` => `merge-readiness-note` / QA merge readiness note
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`
  - from `validate-qa-lane` expectation `qa-lane-validation-note`

### 9. Approve merge after security review (`approve-merge-after-security`)
- Step kind: Approval
- Depends on: route-review-disposition/security-review, perform-security-review
- Inputs: Review routing decision, security lane review note, residual risk framing, and release-note completeness.
- Outputs: Approved or rejected post-security merge decision with explicit rationale.
- Evidence: Security merge note, residual risks, and next action if merge is blocked after security review.
- Decision rights: Merge approver owns the post-security merge gate and cannot bypass security evidence.
- Exception policy: Reject merge when security evidence or route provenance is incomplete.
- Artifact expectations:
  - `security-merge-readiness-note` => `merge-readiness-note` / Security merge readiness note
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`
  - from `perform-security-review` expectation `security-lane-note`

### 10. Approve merge after architecture escalation (`approve-merge-after-architecture`)
- Step kind: Approval
- Depends on: route-review-disposition/architecture-review, architecture-escalation
- Inputs: Review routing decision, architecture escalation brief, residual risk framing, and release-note completeness.
- Outputs: Approved or rejected post-architecture merge decision with explicit rationale.
- Evidence: Architecture merge note, residual risks, and next action if merge is blocked after escalation.
- Decision rights: Merge approver owns the post-architecture merge gate and cannot bypass architecture evidence.
- Exception policy: Reject merge when architecture evidence or route provenance is incomplete.
- Artifact expectations:
  - `architecture-merge-readiness-note` => `merge-readiness-note` / Architecture merge readiness note
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`
  - from `architecture-escalation` expectation `architecture-escalation-brief`

### 11. Normalize default review lane (`normalize-default-lane`)
- Step kind: Review
- Depends on: route-review-disposition/__default__
- Inputs: Ambiguous or unclassified review state plus the review router context.
- Outputs: Explicit normalization note and merge-lane readiness or follow-up instruction.
- Evidence: Observed ambiguity, normalized lane, and rationale.
- Decision rights: Review lead owns default normalization and may not use it to hide actual error states.
- Exception policy: If the state is actually malformed or contradictory, use the error lane instead of normalization.
- Artifact expectations:
  - `review-normalization-note` => `review-normalization-note` / Review normalization note
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`

### 12. Approve merge after default normalization (`approve-merge-after-default`)
- Step kind: Approval
- Depends on: route-review-disposition/__default__, normalize-default-lane
- Inputs: Review routing decision, normalization note, residual risk framing, and release-note completeness.
- Outputs: Approved or rejected post-normalization merge decision with explicit rationale.
- Evidence: Default-route merge note, residual risks, and next action if merge is blocked after normalization.
- Decision rights: Merge approver owns the post-normalization merge gate and cannot bypass normalization evidence.
- Exception policy: Reject merge when normalization evidence or route provenance is incomplete.
- Artifact expectations:
  - `default-merge-readiness-note` => `merge-readiness-note` / Default-route merge readiness note
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`
  - from `normalize-default-lane` expectation `review-normalization-note`

### 13. Capture workflow failure and recovery path (`capture-workflow-failure`)
- Step kind: End
- Depends on: route-review-disposition/__error__
- Inputs: Failing branch-router state, canvas or runtime symptom, and any partial decision evidence.
- Outputs: Workflow failure record with recommended recovery path.
- Evidence: Failing state, visible symptom, and accountable recovery owner.
- Decision rights: Review lead owns the escalation into the error lane until recovery ownership is explicit.
- Exception policy: Do not guess a lane when the workflow state is broken; capture failure explicitly.
- Artifact expectations:
  - `review-workflow-failure-record` => `review-workflow-failure-record` / Review workflow failure record
- Artifact inputs:
  - from `route-review-disposition` expectation `review-routing-decision-record`
