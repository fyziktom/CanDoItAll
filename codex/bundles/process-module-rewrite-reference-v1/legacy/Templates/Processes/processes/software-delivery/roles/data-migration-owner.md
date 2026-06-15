# Data migration owner

**Key:** `data-migration-owner`  
**Scope:** local  
**Process:** software-delivery  
**Preferred executor:** person  
**Preferred project role:** TechnicalContact  
**Seniority:** Senior database or data-platform engineer  
**Minimum years in primary discipline:** 6  
**Minimum years in software delivery:** 8

## Summary
Migration-specific authority for schema, data backfill, and rollback safety.

## Purpose
Own all migration mechanics that can compromise data integrity, rollback realism, or release sequencing.

## Staffing intent
A specialist engineer who understands the data model, operational windows, and migration rollback implications in depth.

## Snapshot summary
Migration-specific authority for schema, data backfill, and rollback safety.

## Domain tags
database, migration, rollback, data-integrity

## Knowledge requirements
- Deep knowledge of schema evolution, data backfill sequencing, locking behavior, and rollback limitations.
- Understanding of production data sensitivity, replication, and consistency risk during change rollout.
- Ability to design reversible migration patterns or explicitly document when reversal is not technically safe.
- Knowledge of runbook requirements for staged or multi-tenant data change.
- Ability to interpret telemetry that indicates migration success, degradation, or partial failure.
- Understanding of how code and migration deployment order interact.

## Experience requirements
- Has executed or reviewed production data migrations with rollback planning.
- Has investigated migration-related incidents or degraded performance after release.
- Has coordinated schema changes across application and platform teams.
- Has authored or reviewed migration runbooks consumed by operators.
- Has signed off data integrity evidence before production deployment.

## Decision rights
- Approve migration sequence and rollback viability.
- Block release when migration evidence is incomplete or unsafe.
- Define tenant or batch sequencing constraints for deployment.
- Escalate when data change risk exceeds the normal release window.

## Owned artifacts
- Migration plan
- Rollback script bundle
- Data integrity check report

## Collaboration expectations
- Work closely with platform engineer, release approver, and software engineer.
- Provide QA with data-risk hotspots requiring targeted validation.
- Coordinate with service owner on maintenance-window and support implications.
- Document exactly what 'successful migration' means operationally.

## Anti-patterns
- Treating rollback as a theoretical possibility instead of a rehearsed path.
- Approving data change based only on lower-environment success.
- Ignoring long-running lock or backfill side effects.
- Bundling unrelated schema risk into one opaque change.

## Fitness evidence
- Reviewed migration plans with explicit rollback reasoning.
- Evidence of successful migration rehearsals or controlled dry runs.
- Incident learnings showing the role reduced data-change risk.
- Clear operator-facing runbooks owned by the role.
