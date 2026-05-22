# Clarify scope and release boundary

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `feature-intake`  
**Step kind:** Start  
**Target lead hours:** 8

## Summary
Value framing and no-go constraints

## Notes
Capture the requested outcome, user or operational impact, target delivery window, known dependencies, and explicit exclusions before delivery commits.

If the request, project structure, or selected work node already identifies a concrete deliverable and target boundary, do not block this first step only because optional governance details are missing. Create the scope boundary packet with explicit assumptions, exclusions, `not applicable` entries, unresolved follow-up questions, and validation hooks for later architecture, implementation, QA, security, or release steps. Return `Blocked` only when the core deliverable, target boundary, mandatory upstream artifact, required authority, required credential, or safe execution boundary is genuinely missing and cannot be inferred or deferred to a modeled review or repair step.

Project-structure source-of-truth requirements must keep their force in the scope boundary packet. Do not turn explicit project-structure requirements into optional items, exclusions, non-acceptance criteria, or follow-up work unless the project structure itself says the item is optional or deferred, or an accepted decision record narrows scope.

## Contracts
- Input contract: Requested change, impact notes, target delivery window, and stakeholder-facing constraints.
- Output contract: Decision-ready scope packet with acceptance boundary, dependency map, assumptions, exclusions, and non-blocking follow-up questions.
- Evidence contract: Intake notes, acceptance criteria, known exclusions, assumptions, and unresolved dependency register.

## Governance
- Decision rights: Product owner can refine the ask but cannot waive architecture, data, or release-governance requirements.
- Exception policy: Escalate immediately when timeline pressure conflicts with data-safety or release constraints.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `product-owner` / Product owner => Responsible; required=True; fallback-order=0; rebind=If the original product owner changes, ownership transfers to the next accountable value owner without changing the process contract.
- `delivery-manager` / Delivery manager => Reviewer; required=True; fallback-order=0; rebind=Delivery review remains explicit even if staffing changes mid-stream.

## Artifact expectations
- `scope-boundary-packet` -> `scope-boundary-packet` / Scope boundary packet | kind=Brief | trust=ReviewRequired | sensitivity=Internal | validation=Must capture no-go constraints, user or operational impact, and acceptance boundary in typed form. Must preserve explicit project-structure source-of-truth requirements without downgrading them to optional, excluded, non-acceptance, or follow-up work unless the project structure itself says the item is optional or deferred.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `intake-completeness-checklist`

## Prompts
- `prompt-intake-summarizer`
