# Customer onboarding orchestration

**Key:** `customer-onboarding`  
**Criticality:** Standard  
**Autonomy level:** Assisted  
**Operating mode:** AssistedExecution  
**Customer name:** Customer Success  
**Owner name:** Operations governance board

## Summary
Turn approved customer demand into a governed onboarding motion with explicit commercial intake, staffing review, kickoff approval, and durable sidecar resources.

## Value statement
Reduce handoff loss across sales, staffing, approval, and kickoff preparation.

## Interface contract summary
Sales hands over to delivery through explicit contracts and approval gates.

## Governance notes
All external commitments require accountable owner review before kickoff.

## Architecture and constitution rules
- Governance policy: Kickoff readiness depends on explicit policy, accountability, and evidence retention.
- Constitution rule: Policy decisions stay explicit and reviewable.

## Operating and simulation notes
- Operating mode summary: Manual and guarded autonomy are both supported depending on step criticality.
- Simulation readiness: Seed pack models the same typed steps used in runtime validation.

## Source frameworks
- nist-ssdf
- owasp-samm
- openchain
- spdx
- slsa

## Process metrics
- Median time from commercial intake to staffing decision.
- Kickoffs approved with no later ownership drift.
- Rate of onboarding delays caused by missing staffing data.
- Percentage of kickoff approvals with explicit milestones and owners.
- Post-onboarding issues traced to weak intake or staffing evidence.

## Process risks
- Commercial promises made before staffing feasibility is known.
- Kickoff approved with vague owners or target dates.
- Customer success criteria hidden in informal communication.
- Fallback coverage omitted during specialist scarcity.

## Tailoring rules
- Large enterprise onboarding may expand after kickoff approval into deeper delivery phases using the richer onboarding extension template.
- Simple self-serve onboarding may stop after staffing review with a typed no-kickoff decision.
- If customer-side responsibilities are incomplete, the kickoff gate must block rather than compensate silently.

## Role usages
- `account-owner` / **Account owner** — Own the customer-facing commercial context for onboarding and keep success criteria explicit before staffing or kickoff commitments are made.
- `staffing-manager` / **Staffing manager** — Validate whether the required people, environments, and sequencing exist to deliver onboarding without hidden bottlenecks.
- `kickoff-lead` / **Kickoff lead** — Run the kickoff decision point so participants leave with explicit owners, timeline, risks, and next actions.

## Steps
### 1. Capture commercial intake (`intake`)
- Step kind: Start
- Depends on: None
- Inputs: Signed scope, target dates, stakeholder summary, and customer expectations.
- Outputs: Typed intake packet ready for delivery and staffing review.
- Evidence: Scope summary, named stakeholders, and decision-ready onboarding notes.
- Decision rights: Account owner can prepare intake but cannot commit delivery without review.
- Exception policy: Escalate immediately if customer commitments are implied rather than explicit.
- Artifact expectations:
  - `customer-onboarding-brief` => `customer-onboarding-brief` / Customer onboarding brief
- Checklists: customer-handoff-checklist

### 2. Review staffing intent (`staffing-review`)
- Step kind: Review
- Depends on: intake
- Inputs: Intake packet and delivery constraints.
- Outputs: Recommended staffing path with explicit fallback and coverage gaps.
- Evidence: Candidate list, allocation picture, and fallback recommendation.
- Decision rights: Staffing manager recommends; governance owner approves kickoff readiness.
- Exception policy: Do not treat partial staffing assumptions as committed staffing.
- Artifact expectations:
  - `staffing-readiness-note` => `staffing-readiness-note` / Staffing recommendation
- Artifact inputs:
  - from `intake` expectation `customer-onboarding-brief`
- Checklists: staffing-feasibility-checklist
- Validations: validate-staffing-feasible
- Prompts: prompt-staffing-summary

### 3. Approve kickoff readiness (`kickoff-approval`)
- Step kind: Approval
- Depends on: staffing-review
- Inputs: Staffing recommendation and draft kickoff plan.
- Outputs: Approved or rejected kickoff readiness.
- Evidence: Approval record and managed kickoff artifacts.
- Decision rights: Kickoff lead can approve, block, or refuse unsafe launch.
- Exception policy: Reject kickoff when ownership, milestones, or dependencies remain vague.
- Artifact expectations:
  - `kickoff-approval-record` => `kickoff-packet` / Kickoff approval record
- Artifact inputs:
  - from `staffing-review` expectation `staffing-readiness-note`
- Checklists: kickoff-alignment-checklist
- Validations: validation-kickoff-ready
- Prompts: prompt-kickoff-agenda

