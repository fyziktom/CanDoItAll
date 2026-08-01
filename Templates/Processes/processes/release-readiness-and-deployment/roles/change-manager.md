# Change manager

**Key:** `change-manager`  
**Scope:** local  
**Process:** release-readiness-and-deployment  
**Preferred executor:** person  
**Preferred project role:** Manager  
**Seniority:** Senior change or release operations management  
**Minimum years in primary discipline:** 5  
**Minimum years in software delivery:** 7

## Summary
Change-window and stakeholder-readiness owner for production deployment events.

## Purpose
Coordinate production change timing, communications, operational readiness, and deployment-event discipline.

## Staffing intent
A release/change specialist responsible for controlled execution during go-live windows.

## Snapshot summary
Change-window and stakeholder-readiness owner for production deployment events.

## Domain tags
change-management, deployment-window, stakeholder-readiness

## Knowledge requirements
- Knowledge of change windows, deployment communications, stakeholder notifications, and cutover discipline.
- Ability to coordinate activities across release, support, infrastructure, and customer-facing teams.
- Understanding of operational readiness checks and fallback ownership during go-live.
- Ability to recognize when an apparently ready release lacks event-level discipline.
- Knowledge of maintenance-window governance and approval paths.
- Ability to document go-live conditions, hold points, and rollback triggers cleanly.

## Experience requirements
- Has run or coordinated production change events with multiple parties involved.
- Has handled go/no-go checkpoints and late-breaking blockers.
- Has executed communication and support readiness for customer-impacting releases.
- Has worked with release approvers and operators during deployment windows.
- Has captured lessons after difficult cutovers or rollback events.

## Decision rights
- Approve change-event readiness from a coordination perspective.
- Stop the event when required stakeholders or communications are missing.
- Escalate change-window or support-coverage conflicts.
- Own the authoritative event plan and hold points.

## Owned artifacts
- Change event plan
- Stakeholder notification set
- Deployment watch roster

## Collaboration expectations
- Coordinate with release approver, platform engineer, and service owner.
- Keep deployment-event timing and contact points unambiguous.
- Ensure customer-facing teams know what to expect.
- Record deviations and lessons for future release playbooks.

## Anti-patterns
- Assuming technical readiness automatically implies event readiness.
- Running change windows without contact, escalation, or support clarity.
- Treating maintenance-window decisions as clerical.
- Failing to preserve a clean go-live timeline.

## Fitness evidence
- Event plans that enabled controlled go-live execution.
- Documented hold points and escalation paths actually used during releases.
- Stakeholder feedback that deployment coordination was clear.
- Improvement of cutover discipline over time.
