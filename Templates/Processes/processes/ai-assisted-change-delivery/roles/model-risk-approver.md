# Model risk approver

**Key:** `model-risk-approver`  
**Scope:** local  
**Process:** ai-assisted-change-delivery  
**Preferred executor:** person  
**Preferred project role:** Manager  
**Seniority:** Senior AI governance or risk leadership  
**Minimum years in primary discipline:** 8  
**Minimum years in software delivery:** 10

## Summary
Decision authority for whether model-originated uncertainty is acceptable for the intended use.

## Purpose
Accept, conditionally accept, or reject AI-related change exposure based on model behavior risk and control coverage.

## Staffing intent
A senior risk owner combining product, security, compliance, and ML judgment.

## Snapshot summary
Decision authority for whether model-originated uncertainty is acceptable for the intended use.

## Domain tags
model-risk, governance, ai-control

## Knowledge requirements
- Ability to reason about failure modes, misuse, drift, opacity, and human-oversight limits for AI systems or AI-assisted workflows.
- Knowledge of risk acceptance, conditional approval, and compensating-control design in AI contexts.
- Understanding of where model uncertainty is tolerable versus unacceptable for the business or user.
- Ability to weigh evaluation evidence, safety review, and operational controls together.
- Knowledge of provenance, model versioning, and prompt/change governance implications.
- Ability to articulate decision boundaries and review conditions in auditable language.

## Experience requirements
- Has approved or rejected higher-risk AI usage or AI-enabled change proposals.
- Has worked with security, compliance, product, and engineering on AI governance decisions.
- Has handled exceptions or conditional approvals tied to control gaps.
- Has reviewed production or near-production evidence of AI failure or misuse.
- Has translated abstract AI risk into practical release conditions.

## Decision rights
- Authorize, conditionally authorize, or reject AI-related release progression.
- Require additional controls such as human review, throttling, or narrow scope.
- Escalate when residual model risk exceeds delegated tolerance.
- Set review expiry or re-validation conditions for approved usage.

## Owned artifacts
- Model risk approval
- Conditional approval note
- AI risk exception record

## Collaboration expectations
- Coordinate with evaluation lead, AI safety reviewer, product owner, and compliance roles.
- State explicit operating conditions for approval decisions.
- Ensure downstream teams understand approval limits and monitoring expectations.
- Review follow-up signals after initial deployment or process adoption.

## Anti-patterns
- Approving because the AI output looks useful in demos.
- Using generic AI policy language without concrete operating conditions.
- Treating human oversight as sufficient when no one is realistically able to review the output properly.
- Approving indefinitely without re-validation triggers.

## Fitness evidence
- Auditable AI approval decisions with explicit conditions.
- Evidence of rejecting or narrowing unsafe use cases.
- Cross-functional trust in the role’s risk framing.
- Re-validation or monitoring requirements captured and followed.
