# Core Candidate Boundary Plan

This bundle does not create Process Core. It prepares the first safe Core proposal by separating candidate code into three categories.

## Category A: Pure candidates

These may be eligible for a later narrow Core project after this bundle:

- Route stage descriptors and route order.
- Route eligibility pure rules.
- Route kind classification without side effects.
- Subprocess lifecycle status mapping.
- Subprocess transition request shaping without execution.
- Artifact expectation snapshots.
- Artifact expectation matching and satisfaction pure rules.
- Projection/validation read-only DTOs.

## Category B: Application-local

These must remain in `CanDoItAll.Modules.Processes`:

- EF-backed hydration.
- Claim leases, heartbeat, claim-held checks, claim release.
- Step transition execution.
- Finalizer application and transition application.
- Materialization journals and rerun requests.
- Subprocess child-run observation and parent artifact persistence.
- AgentFramework execution, retry, provider repair, no-progress journals.

## Category C: Infrastructure-local

These must not move to Core:

- Workspace path resolution.
- Filesystem reads/writes.
- Storage placement and storage catalogs.
- Database profile resolution.
- Service scopes / DI runtime dispatch.

## Target of this bundle

- Create a source-payload-free route DTO path for pure decisions.
- Move dispatcher source payloads into explicit adapter/envelope types.
- Stabilize finalizer intent DTOs without moving finalizer application.
- Split hydration and subprocess services into smaller collaborators.
- Make a final Core candidate map with exact next-bundle extraction list.
