# Stable Core And Domain Driver Roadmap Refresh

## Current State
- `CanDoItAll.Processes.Core` remains deterministic and dependency-clean.
- Runtime side effects remain module-local in `CanDoItAll.Modules.Processes`.
- `CanDoItAll.Processes.Drivers.Abstractions` introduces a narrow contract-only boundary with no runtime behavior.

## Roadmap
- Phase 1: Keep stabilizing Core descriptors and public API governance with dependency scans and source-backed tests.
- Phase 2: Expand driver contracts only when a new read-only verification lane needs a strongly typed shape.
- Phase 3: Rehearse `.NET/Rust transcript verifier` behavior in tests against existing build/test/proof transcripts.
- Phase 4: Harden Office and business-analysis evidence lanes as read-only references, not connectors.
- Phase 5: Approve a future production alpha only after sandbox, allowlist, audit persistence, redaction, lifecycle ownership, and anti-stub proof gates are ready.

## Release Gates
- Core must not reference driver abstractions.
- Driver abstractions must not reference Modules, Infrastructure, AgentFramework, EF, UI, storage, workspace, or external connector packages.
- Production driver runtime remains absent until a separate positive decision approves it.
- Every critical proof manifest must include failing-first evidence, passing tests, source assertions, anti-stub scans, and portable artifact references.
