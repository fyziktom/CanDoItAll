# Domain owner

**Key:** `domain-owner`  
**Scope:** local  
**Process:** architecture-decision-governance  
**Preferred executor:** person  
**Preferred project role:** TechnicalContact  
**Seniority:** Senior domain lead or engineering manager  
**Minimum years in primary discipline:** 6  
**Minimum years in software delivery:** 8

## Summary
Business-technical steward for a bounded domain affected by architectural change.

## Purpose
Represent domain semantics, operational realities, and long-term consequences when architecture choices affect a specific capability area.

## Staffing intent
A leader who understands the domain model deeply enough to challenge superficially convenient architecture decisions.

## Snapshot summary
Business-technical steward for a bounded domain affected by architectural change.

## Domain tags
domain-modeling, ownership, architecture-governance

## Knowledge requirements
- Deep knowledge of the owned domain’s rules, invariants, operational pain points, and integration surface.
- Ability to evaluate whether an architectural proposal preserves domain boundaries and team ownership clarity.
- Understanding of business impact if domain semantics are oversimplified or coupled incorrectly.
- Knowledge of historical debt or prior incidents tied to domain boundary mistakes.
- Ability to distinguish local optimization from portfolio-level harm.
- Understanding of how the domain evolves and where future change cost is likely to land.

## Experience requirements
- Has owned delivery or operations for a specific product domain over time.
- Has corrected or challenged architecture choices that harmed domain clarity.
- Has reviewed cross-team changes touching owned domain boundaries.
- Has captured domain-specific lessons after incidents or major changes.
- Has aligned domain stakeholders on non-obvious architectural trade-offs.

## Decision rights
- Approve or challenge architectural impact on the owned domain.
- Require an ADR when domain boundary or ownership is materially affected.
- Escalate cross-domain coupling risk before implementation begins.
- Demand clear ownership and migration planning for affected domain responsibilities.

## Owned artifacts
- Domain impact note
- ADR domain appendix
- Ownership transition note

## Collaboration expectations
- Collaborate with architect, product owner, and engineers.
- Contribute historical context without blocking needed evolution.
- Ensure downstream domain teams understand changes affecting them.
- Capture domain-specific follow-up actions after a decision.

## Anti-patterns
- Defending the current domain shape as sacred regardless of evidence.
- Ignoring future ownership ambiguity because the short-term change works.
- Withholding domain context until late design review.
- Approving architecture without considering team-operability consequences.

## Fitness evidence
- Domain impact notes accepted by architecture review.
- Reduced domain-coupling surprises after decisions involving the role holder.
- Traceable domain ownership decisions.
- Evidence of better architectural continuity across releases.
