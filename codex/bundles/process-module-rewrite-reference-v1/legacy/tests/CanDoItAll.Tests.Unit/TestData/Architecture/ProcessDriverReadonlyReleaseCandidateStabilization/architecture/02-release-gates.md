# Release Gates

| Gate | Requirement |
| --- | --- |
| Clean build | `dotnet build CanDoItAll.slnx --no-restore` passes with 0 warnings/0 errors. |
| Full unit | Full unit project passes with 0 failures and no unowned skips. |
| Focused driver unit | Domain driver, gateway, aggregation, evidence policy, and fake-proof tests pass. |
| Focused process integration | Process read-only adapters, payload builders, batch orchestrator, and projection planner tests pass. |
| Core dependency scan | Core has no references to driver packages, Modules, Infrastructure, AgentFramework, storage, workspace, UI, file/network/runtime-host tokens. |
| Driver runtime scan | Driver/gateway/process adapter paths have no registry, selector, DI, host, scheduler, workflow, manager, file, network, workspace/storage, process mutation, or shell tokens. |
| UI drift scan | No Razor/CSS/JS/TS/image/media changes. |
| Proof quality | Every critical gate has manifest, semantic invariants, changed-file hashes, command transcripts, source assertions, anti-stub audit, positive proof and adversarial negative proof. |
| Completed validator | Prepared and completed validators pass after report/proof updates. |
