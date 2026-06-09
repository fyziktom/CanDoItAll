# Execution-Capable Driver Prerequisites

## Status
Future-gated. Not approved by this bundle.

## Current Position
The restored runtime is process-service owned. UI, API, project-structure, scheduler, workflow-origin, manager, and operator paths must continue to use typed process services, process-owned dispatch/finalizers, and read-only verification adapters.

## Required Approval Before Any Execution-Capable Driver Work
- Runtime ownership and process-state mutation ownership.
- Cancellation, retry ownership, failure handoff, dead-letter behavior, and no-progress escalation.
- Observability, audit persistence, redaction evidence, and operator readback.
- Sandbox policy, allow-list policy, authorization, approval, revocation, emergency stop, and dry-run behavior.
- Compatibility and versioning for driver contracts, API snapshots, and migration docs.
- Source guards that block registry, selector, dependency-injection auto-registration, scheduler/workflow hooks, endpoint mappings, storage/workspace writes, external calls, and process mutation unless explicitly approved.
- Focused unit/integration/Playwright proof, source scans, red-team proof, and critical proof manifests in the same future approval bundle.

## Explicitly Still Blocked
- Generic process-driver runtime host.
- Driver registry or runtime selector.
- Driver dependency-injection auto-registration.
- Manager command for driver execution.
- Scheduler/workflow hook into driver runtime.
- Shell execution, package restore, Office/Graph calls, CRM writes, network calls, storage/workspace writes, transition mutation, claim mutation, finalizer mutation, retry scheduling, or artifact state mutation through drivers.

## Reopen Trigger
Reopen runtime-host design only when the future approval bundle owns the complete safety, audit, authorization, observability, compatibility, and test matrix. A green diagnostic adapter, green deterministic process test, or status-only report is not approval.
