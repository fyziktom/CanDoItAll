# Independent source and evidence review

## Review boundary

The review inspected the GitHub source tree and retained evidence at exact branch head `e282446daa2b775b93f2d70ea7fc0e282e26d802`. The reviewer did not execute the .NET build or tests in the review environment; all existing test counts are repository-retained evidence and must be independently reproduced by this follow-up.

## Architecture disposition

**Pass with P0 merge blockers.** The core architecture is suitable for continued hardening:

- logical and physical path domains are separated;
- host-bound persisted roots require explicit rebind;
- physical filesystem semantics are centralized;
- vault capability and protection levels are typed;
- mandatory and optional host capabilities degrade independently;
- one low-level process host owns start/control;
- Workbench and Manager own separate lifecycle registries;
- local stdio MCP and Docker invoke executables without a shell;
- Process drivers seal host profile/capability requirements.

No broad rewrite is justified.

## Merge blockers

### MR-P0-001 — persisted process-plan compatibility

The current plan hasher includes newly sealed host profiles and host capability decisions. The previous hasher did not. The persistence mapper deserializes old JSON, recomputes with the current hasher, and rejects a mismatch, but no explicit hash-algorithm version or legacy verification path is present. Missing capability fields also deserialize to empty defaults. A pre-change persisted fixture must prove compatibility or drive a versioned migration; empty legacy fields must never become an implicit capability-free execution contract.

### MR-P0-002 — FileTools direct-source provenance

B05 direct-source evidence depends on FileTools commit `f31e20d054003348c7557b9634e0838fc5996ae0` plus three uncommitted sibling files. The CanDoItAll build automatically enables sibling-source mode when directories exist, and compile-time capability logic treats that mode as implementation validated. A clean checkout cannot reproduce the reviewed graph. Package mode truthfully disables unverified desktop launching and is acceptable for alpha; direct-source mode must become explicit and cryptographically/version anchored.

### MR-P0-003 — owned process-tree termination

On Unix, `GracefulThenForceTree` sends SIGTERM to the root PID. If the root exits within the grace period, the operation returns without proving that descendants exited. A child that survives or becomes reparented can remain as an orphan. The same ownership guarantee must hold for Workbench sessions, Manager discoveries, local MCP servers, and external tools.

## P1 hardening

- MCP must answer peer `ping` requests while waiting for another response and must bound incoming line/message handling.
- Docker recipe parsing must reject malformed booleans, integers, durations, option-like values, excessive mappings, and excessive argument bytes.
- Workspace path guard must itself enforce symlink/reparse safety rather than relying on every downstream consumer.
- Unix executable validation must test executability for the current effective identity, not merely the presence of any execute bit.
- `PATHEXT` and executable candidates need bounded syntax validation.
- Future Docker CI must materialize a disposable `.secrets/db-password` before `compose up`.
- `-SkipBuild` validation must reject stale assemblies and use a fully qualified test catalog.
- Canonical runtime inventories, source anchors, handoffs, checksums, and statuses are stale and contradictory.
- The tracked empty `.local/share/NuGet/Migrations/1` file must be removed.

## Latest Docker-stack delta

The final `start as docker` commit is a positive alpha addition: package-mode build, non-root application image, read-only application filesystem, internal backend network, loopback publishing, persistent data volume, health dependencies, and file-based database password injection. It invalidates the preceding exact B07 source anchor and therefore requires focused Docker/Web validation and later inclusion in the integrated checkpoint. It does not require an immediate 7,000-test run.
