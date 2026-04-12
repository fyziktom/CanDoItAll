# Solution architect

**Key:** `solution-architect`  
**Scope:** shared  
**Process:** shared  
**Preferred executor:** person  
**Preferred project role:** Reviewer  
**Seniority:** Principal or staff architecture  
**Minimum years in primary discipline:** 8  
**Minimum years in software delivery:** 10

## Summary
Architecture authority for system boundaries, irreversible decisions, and cross-domain technical coherence.

## Purpose
Protect maintainability and operability by reviewing design options, target architecture fit, and downstream integration impact before costly implementation commitment.

## Staffing intent
A senior technical authority who can reason across components, environments, data boundaries, and operational consequences.

## Snapshot summary
Architecture authority for system boundaries, irreversible decisions, and cross-domain technical coherence.

## Domain tags
architecture, integration, data-boundaries, operability

## Knowledge requirements
- Ability to model component boundaries, data flows, failure modes, and integration seams at a level usable by delivery teams.
- Knowledge of domain-driven modularization, API contract design, and backward compatibility strategies.
- Understanding of deployment topology, observability requirements, and operational failure recovery constraints.
- Ability to identify irreversible design choices and force them into explicit decision records.
- Knowledge of performance, resilience, security, and compliance implications of architectural options.
- Ability to assess whether proposed change scope is aligned with current platform strategy.

## Experience requirements
- Has reviewed or led architecture for production systems with multiple modules or services.
- Has handled at least one change where a short-term solution risked long-term structural damage and documented the trade-off.
- Has collaborated with delivery and platform teams to turn architecture guidance into implementable work packages.
- Has owned or co-owned post-incident architectural remediation resulting from prior design shortcuts.
- Has communicated architecture decisions to both engineers and non-engineering decision-makers.

## Decision rights
- Approve architecture direction before implementation crosses an irreversible threshold.
- Require an ADR when design choices materially affect future change cost or operational risk.
- Reject implementation approaches that violate defined integration or platform guardrails.
- Escalate unresolved domain ownership conflicts before code lands.

## Owned artifacts
- Architecture decision record
- Integration contract note
- Risk exception assessment
- Target-state architecture sketch

## Collaboration expectations
- Work with product owner to turn business demand into technically bounded scope.
- Guide software engineers with concrete guardrails rather than abstract purity arguments.
- Coordinate with security and platform roles when architecture choices affect trust boundaries or deployment safety.
- Review QA evidence when architectural concerns require targeted testing.

## Anti-patterns
- Approving architecture by default because the change seems small.
- Using architecture review to block without proposing viable options.
- Leaving irreversible decisions undocumented because everyone 'already knows'.
- Over-designing low-risk changes instead of proportioning review depth to impact.

## Fitness evidence
- Portfolio of architecture decisions with clear context, options, and consequences.
- Examples where the role holder prevented or repaired costly integration mistakes.
- Evidence of actionable guidance consumed by delivery teams.
- Operational metrics or incident findings improved by architecture interventions.
