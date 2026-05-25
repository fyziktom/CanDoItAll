You are a senior C#/.NET architect working in the CanDoItAll repository.

Target branch:
- db-remove-sqlite

Use these skills:
- codex/skills/bundles/candoitall-bundle-execution/SKILL.md
- codex/skills/bundles/candoitall-bundle-preparation/SKILL.md only if you need to repair bundle metadata before execution

Execute the bundle:
- candoitall-db-postgres-final-hardening-followup-bundle-v4

Primary objective:
The previous Codex wave removed SQLite runtime support and introduced PostgreSQL-only canonical runtime behavior. This follow-up must close the remaining DB canonicality and throughput risks:
1. stale leased workers must not commit final canonical state,
2. lease renewal loss must be a hard semantic signal,
3. PostgreSQL bounded parallelism must be enabled, safe, and measured,
4. profile-specific contexts must stay maintenance-only,
5. broad validation caveats must be closed or explicitly quarantined.

Critical source areas:
- src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs
- src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs
- src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs
- src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- src/CanDoItAll.Infrastructure/ControlPlane/**
- src/CanDoItAll.Infrastructure/Persistence/**
- src/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs

Execute subbundles in order:
SB01 - merge evidence and residue cleanup
SB02 - conditional finalization for leased outbox work
SB03 - lease-loss hardening and heartbeat contracts
SB04 - throughput defaults and runtime tuning
SB05 - benchmark and query-count proof
SB06 - process dispatch claim-first deep proof
SB07 - PostgreSQL canonicality invariants and admin boundaries
SB08 - final validation and merge readiness

Hard constraints:
- Do not reintroduce SQLite.
- Do not turn restart-first DB activation back into hot switching.
- Do not allow stale workers to write final state after lease loss.
- Do not rely on EF tracked entity SaveChanges for final leased state unless lease ownership is guarded by conditional update/concurrency proof.
- Keep code comments in English.
- Do not claim benchmarks if no numeric data exists. If environment blocks benchmark, record exact limitation and create deterministic fallback proof.

Acceptance:
- Process outbox stale worker cannot finalize after losing/relinquishing/expiring lease.
- Connector command stale worker cannot finalize after losing/relinquishing/expiring lease.
- Automation delivery finalization is reviewed and either hardened or explicitly justified by testable lifecycle.
- Parallelism defaults and max bounds are documented and tested.
- Broad tests pass or are quarantined with exact reason, owner, and follow-up.
- Final execution report is honest and artifact-backed.
