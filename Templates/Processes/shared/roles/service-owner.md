# Service owner

**Key:** `service-owner`  
**Scope:** shared  
**Process:** shared  
**Preferred executor:** person  
**Preferred project role:** TechnicalContact  
**Seniority:** Service management or engineering leadership  
**Minimum years in primary discipline:** 6  
**Minimum years in software delivery:** 8

## Summary
Operational accountability owner for a live service, product surface, or bounded business capability.

## Purpose
Represent live-service constraints, operational history, and post-release accountability in change decisions.

## Staffing intent
A domain-aligned owner with authority over service-level objectives, support load, and operational trade-offs.

## Snapshot summary
Operational accountability owner for a live service, product surface, or bounded business capability.

## Domain tags
service-ownership, operations, customer-impact

## Knowledge requirements
- Knowledge of the service mission, user journeys, support issues, and reliability expectations.
- Ability to reason about operational blast radius and degraded-mode behavior.
- Understanding of dependency relationships between the owned service and adjacent systems.
- Knowledge of release calendars, support capacity, and monitoring signals for the owned capability.
- Ability to judge customer impact if the service changes fail or underperform.
- Understanding of where local exceptions can become systemic risk.

## Experience requirements
- Has owned or co-owned a live service with measurable reliability or support obligations.
- Has participated in incident, defect, or release review for the owned domain.
- Has balanced feature pressure against service stability in a documented decision.
- Has represented the service in cross-team dependency or architecture discussions.
- Has driven at least one improvement after operational pain or repeated incident patterns.

## Decision rights
- Approve service-specific operational conditions for release.
- Escalate when change timing conflicts with service risk tolerance or support capacity.
- Define customer-impact thresholds that trigger rollback or degraded mode.
- Require post-release monitoring or support coverage before go-live.

## Owned artifacts
- Service risk note
- Operational readiness note
- Customer impact assessment
- Service improvement backlog

## Collaboration expectations
- Work with product, release, and support roles to align release timing with service health.
- Provide domain-specific history that generic governance roles may not know.
- Participate in retrospective or post-incident learning for the owned capability.
- Help engineers interpret operational context behind prior failures.

## Anti-patterns
- Assuming the service can absorb change because it handled prior releases.
- Treating operational concerns as after-the-fact support issues.
- Withholding domain history that materially changes risk assessment.
- Approving changes without planning who owns the first-hour monitoring window.

## Fitness evidence
- Explicit service-level input in release decisions.
- Examples of risk calls based on real operational context.
- Post-release follow-through on monitoring, support, or corrective actions.
- Stakeholder recognition that the role provides grounded service accountability.
