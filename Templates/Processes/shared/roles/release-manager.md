# Release manager

**Key:** `release-manager`  
**Scope:** shared  
**Process:** shared  
**Preferred executor:** person  
**Preferred project role:** Manager  
**Seniority:** Senior release or operations manager  
**Minimum years in primary discipline:** 6  
**Minimum years in software delivery:** 9

## Summary
Operational owner for release window control, rollback readiness, and live-watch discipline.

## Purpose
Coordinate go-live preparation and keep rollback and telemetry watch ownership explicit before and during release execution.

## Staffing intent
Operational owner for production execution, release windows, and rollback decisions.

## Snapshot summary
Operational owner for rollout control, rollback readiness, and telemetry review.

## Domain tags
release, rollback, deployment-control, telemetry

## Knowledge requirements
- Ability to judge release readiness from business, technical, QA, security, and operational signals together.
- Knowledge of rollback strategy, deployment controls, maintenance windows, and support readiness.
- Understanding of incident escalation, customer communication, and operational ownership after go-live.
- Ability to distinguish reversible risk from unacceptable irreversible exposure.
- Knowledge of auditability and approval traceability expectations.
- Ability to enforce a no-go decision when evidence quality is not credible.

## Experience requirements
- Has owned or chaired at least one production release or change approval forum.
- Has made go/no-go decisions under time pressure with incomplete but structured evidence.
- Has handled release rollback or controlled stop conditions in real environments.
- Has coordinated post-release monitoring and support readiness across teams.
- Has worked through customer-impacting defects or incidents after approving a release.

## Decision rights
- Authorize or reject production deployment.
- Require explicit rollback and monitoring coverage before release.
- Escalate to executive authority when residual risk exceeds delegated tolerance.
- Freeze release progression when mandatory gates remain unresolved.

## Owned artifacts
- Release approval note
- Go-live checklist
- Rollback plan
- Production watch plan

## Collaboration expectations
- Consume input from product, QA, security, platform, and service owners before deciding.
- State release decision rationale in explicit, reviewable language.
- Trigger support and communications readiness when customer impact is plausible.
- Capture follow-up actions for conditions attached to release approval.

## Anti-patterns
- Approving because teams appear confident without checking the evidence.
- Conflating deployment success with business-release success.
- Ignoring rollback realism because the change is 'small'.
- Treating approval as ceremonial rather than consequential.

## Fitness evidence
- Signed release decisions with clear rationale and conditions.
- Examples of stopped or delayed releases where later evidence justified the decision.
- Operational metrics showing disciplined release governance.
- Trust from delivery teams that the role is demanding but consistent.
