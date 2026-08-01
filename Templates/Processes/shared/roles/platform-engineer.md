# Platform engineer

**Key:** `platform-engineer`  
**Scope:** shared  
**Process:** shared  
**Preferred executor:** person-or-agent  
**Preferred project role:** TeamMember  
**Seniority:** Senior platform/SRE engineering  
**Minimum years in primary discipline:** 5  
**Minimum years in software delivery:** 7

## Summary
Environment and delivery-pipeline owner for build integrity, deployment mechanics, and operational guardrails.

## Purpose
Provide the reliable platform path required to build, deploy, observe, and if necessary roll back changes safely.

## Staffing intent
A technical specialist in CI/CD, environments, infrastructure, and observability integration.

## Snapshot summary
Environment and delivery-pipeline owner for build integrity, deployment mechanics, and operational guardrails.

## Domain tags
platform, ci-cd, infrastructure, observability

## Knowledge requirements
- Ability to manage CI/CD pipelines, deployment automation, environment controls, and configuration safety.
- Knowledge of secrets, identity, access, and least-privilege controls in build and deployment systems.
- Understanding of observability stacks, alerting, rollout telemetry, and rollback triggers.
- Ability to assess infrastructure dependencies, schema changes, and environment drift.
- Knowledge of reproducible builds, provenance, and artifact integrity signals.
- Ability to guide teams on what must change in delivery mechanics before code is releasable.

## Experience requirements
- Has built or maintained deployment pipelines for production software.
- Has executed or supported releases, rollback, and incident containment in live environments.
- Has debugged environment-specific failures that blocked otherwise correct code.
- Has implemented monitoring or telemetry needed for safe rollout decisions.
- Has worked with security and compliance requirements around platform access and evidence retention.

## Decision rights
- Approve or block platform readiness for build and deployment activities.
- Require environment or observability changes before risky releases.
- Escalate when release assumptions depend on unstable platform conditions.
- Choose rollout mechanics within the approved operational policy.

## Owned artifacts
- Build provenance report
- Deployment plan
- Environment readiness note
- Operational telemetry summary

## Collaboration expectations
- Collaborate with engineers to make deployment and observability requirements explicit early.
- Coordinate with release approvers on rollout and rollback realism.
- Work with security on platform trust and access-control expectations.
- Support incident response with high-fidelity operational evidence.

## Anti-patterns
- Treating platform concerns as someone else's problem until release day.
- Automating blindly without observability or rollback triggers.
- Using production-specific knowledge that is not documented or transferable.
- Assuming a successful build implies a safe deployment.

## Fitness evidence
- Reliable deployment and rollback exercises.
- Evidence of improved pipeline stability or reduced release friction.
- Clear platform runbooks or operational notes owned by the role holder.
- Strong trust from delivery teams in platform signals and readiness calls.
