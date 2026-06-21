# Security reviewer

**Key:** `security-reviewer`  
**Scope:** shared  
**Process:** shared  
**Preferred executor:** person  
**Preferred project role:** Reviewer  
**Seniority:** Senior application or product security  
**Minimum years in primary discipline:** 7  
**Minimum years in software delivery:** 9

## Summary
Security gate owner for threat assessment, exception handling, and trust-boundary decisions.

## Purpose
Ensure changes touching trust boundaries, sensitive data, dependencies, or operational attack surface are reviewed proportionally and documented defensibly.

## Staffing intent
A senior security practitioner who can translate security risk into concrete engineering and governance decisions.

## Snapshot summary
Security gate owner for threat assessment, exception handling, and trust-boundary decisions.

## Domain tags
security, threat-modeling, dependency-risk, exception-governance

## Knowledge requirements
- Ability to perform practical threat modeling on application, API, infrastructure, and data flows.
- Knowledge of secure design, secure coding, and dependency/supply-chain risk controls.
- Understanding of authentication, authorization, logging, secrets handling, and data classification concerns.
- Ability to define compensating controls and document exception boundaries when ideal remediation is not feasible.
- Knowledge of vulnerability severity, exploitability, exposure context, and remediation prioritization.
- Ability to evaluate AI-assisted changes for hidden prompt, model, or dependency risk where relevant.

## Experience requirements
- Has signed off or blocked production changes on security grounds with documented rationale.
- Has handled at least one security exception or compensating-control decision involving business pressure.
- Has collaborated with engineers and architects to remediate or contain identified risk.
- Has reviewed third-party components or supply-chain inputs before production use.
- Has supported post-incident or vulnerability-response work with concrete corrective actions.

## Decision rights
- Approve, reject, or conditionally approve security-relevant change progression.
- Require threat analysis or dependency review before promotion to release.
- Authorize documented security exceptions only with compensating controls and expiry expectations.
- Escalate unresolved high-severity risk to accountable governance owners.

## Owned artifacts
- Threat assessment
- Security review note
- Security exception record
- Dependency risk assessment

## Collaboration expectations
- Work early with architects and engineers instead of only at the end.
- Coordinate with release approvers when unresolved security risk affects go-live authority.
- Request focused QA evidence where security concerns require targeted validation.
- Keep product owners informed in clear business-impact language.

## Anti-patterns
- Becoming a late-stage gate that only says no without timely guidance.
- Approving unresolved risk without explicit compensating controls and expiry logic.
- Using severity labels without describing exploit context or customer impact.
- Assuming vendor or OSS popularity is equivalent to acceptable trust.

## Fitness evidence
- Security reviews with traceable mitigation or exception outcomes.
- Evidence of threat models or dependency assessments accepted by engineering and governance.
- Post-incident learning records showing the role holder drove concrete security hardening.
- Demonstrated judgment that balances security rigor with delivery proportionality.
