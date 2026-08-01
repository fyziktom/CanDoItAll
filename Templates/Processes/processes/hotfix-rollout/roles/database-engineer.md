# Database engineer

**Key:** `database-engineer`  
**Scope:** local  
**Process:** hotfix-rollout  
**Preferred executor:** person  
**Preferred project role:** TechnicalContact  
**Seniority:** Senior database engineering  
**Minimum years in primary discipline:** 6  
**Minimum years in software delivery:** 8

## Summary
Specialist responder for shard, schema, and persistence hotfix risk.

## Purpose
Own emergency database diagnostics, hotfix compatibility assessment, and rollback trigger definition.

## Staffing intent
A hands-on production database specialist for emergency change paths.

## Snapshot summary
Specialist responder for shard, schema, and persistence hotfix risk.

## Domain tags
hotfix, database, incident-response

## Knowledge requirements
- Deep understanding of the live persistence topology, shard behavior, and emergency rollback constraints.
- Knowledge of schema lock behavior, replication lag, and transaction-impact risk under emergency change.
- Ability to assess whether a hotfix can be applied safely under ongoing incident pressure.
- Understanding of forensic evidence needs while still containing customer impact.
- Knowledge of telemetry required to detect silent data-side failure quickly.
- Ability to design narrowly scoped emergency remediation paths.

## Experience requirements
- Has supported emergency production changes affecting database or storage systems.
- Has diagnosed live outage or degradation tied to schema or data-path behavior.
- Has executed or aborted emergency change due to database safety signals.
- Has documented rollback or compensating controls under severe time pressure.
- Has worked with incident commanders on containment versus integrity trade-offs.

## Decision rights
- Approve or block shard/database aspects of emergency rollout.
- Define rollback trigger thresholds tied to persistence risk.
- Escalate when the emergency path threatens data integrity.
- Choose the least harmful containment path for database-specific risk.

## Owned artifacts
- Shard risk note
- Emergency rollback trigger sheet
- Database hotfix runbook

## Collaboration expectations
- Work directly with incident commander and platform engineer.
- Provide concise risk summaries under pressure.
- Coordinate with QA on data-path validation limits.
- Preserve factual detail for the post-incident review.

## Anti-patterns
- Optimizing for service restoration while hiding integrity risk.
- Guessing about shard behavior without reading live telemetry.
- Treating rollback as trivial during emergency conditions.
- Leaving undocumented emergency data operations.

## Fitness evidence
- Emergency change notes with explicit risk and rollback triggers.
- Evidence of incident support grounded in real telemetry.
- Post-incident findings showing the role preserved integrity or accelerated diagnosis.
- Operator trust in the role’s emergency judgments.
