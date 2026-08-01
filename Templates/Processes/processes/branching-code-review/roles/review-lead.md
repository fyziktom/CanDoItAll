# Review lead

**Key:** `review-lead`  
**Scope:** local  
**Process:** branching-code-review  
**Preferred executor:** person  
**Preferred project role:** Reviewer  
**Seniority:** Senior staff engineer, QA lead, or delegated review authority  
**Minimum years in primary discipline:** 6  
**Minimum years in software delivery:** 8

## Summary
Governed reviewer who owns code-review lane selection, normalization, and escalation discipline.

## Purpose
Convert review findings into an explicit next lane and preserve replayable routing for every reviewed change.

## Staffing intent
Decision owner for review routing, normalization, and escalation safety.

## Snapshot summary
Decision owner for review routing, normalization, and escalation safety.

## Domain tags
code-review, routing, merge-governance, quality

## Knowledge requirements
- Ability to design risk-based test strategy across functional, regression, integration, and exploratory layers.
- Knowledge of evidence quality, reproducibility, and how to communicate remaining uncertainty honestly.
- Understanding of production-like environment limitations and how they affect release confidence.
- Ability to challenge weak acceptance criteria or under-scoped implementation evidence.
- Knowledge of defect taxonomy, escape analysis, and prioritization under release pressure.
- Ability to connect customer impact and business criticality to appropriate validation depth.

## Experience requirements
- Has owned test strategy for medium or high-risk releases with documented go/no-go input.
- Has triaged critical defects during a release or hotfix decision window.
- Has built or refined regression suites and shown where manual verification remained necessary.
- Has worked directly with engineering to repair inadequate observability or testability.
- Has participated in post-release or post-incident quality learning loops.

## Decision rights
- Approve or reject evidence sufficiency for the quality gate.
- Require additional targeted validation when risk hotspots remain unproven.
- Escalate when release pressure is bypassing necessary proof.
- Classify residual quality risk in language decision-makers can act on.

## Owned artifacts
- Test strategy
- Regression evidence pack
- Defect triage log
- Quality gate recommendation

## Collaboration expectations
- Partner with product to understand customer impact if defects escape.
- Work with engineers to ensure evidence is reproducible and decision-ready.
- Coordinate with release approvers and security when validation scope crosses their gate.
- Capture retrospective actions when evidence was missing or too expensive to produce.

## Anti-patterns
- Equating test quantity with confidence quality.
- Approving releases because the schedule is fixed rather than because evidence is sufficient.
- Treating exploratory findings as optional anecdotes instead of structured input.
- Ignoring environment drift that invalidates otherwise good test results.

## Fitness evidence
- Test plans and evidence sets consumed successfully by release boards.
- Escape-rate or defect-severity trends improved through interventions by the role holder.
- Examples of clear go/no-go recommendations with explicit residual risk.
- Acknowledged influence on better testability and observability practices.
