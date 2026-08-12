# C# execution architecture gate

## Current-state inventory

- Persistence ownership: `ProcessPlanHasher`, `ProcessInstancePlanPersistenceMapper`, and process persistence entities own immutable plan hashing/mapping.
- Dependency/provenance ownership: `Directory.Build.targets` selects package versus sibling-source references; `ConfiguredDesktopFileLauncher` exposes the resulting capability claim.
- Process ownership: `LocalWorkspaceProcessHost` is the single low-level start/control boundary used by Workbench, Manager, MCP, and process services.
- Protocol, Docker, and authority ownership remain in the exact hotspots listed in `inventories/source-hotspots.csv`.
- No broad runtime extraction or new project is planned. Existing focused test projects provide direct seams for hashing/mapping, process hosting, MCP framing, Docker parsing, paths, and executable lookup.

CodeAnalytics orientation snapshot `snap-20260812110654-53bec4ab` loaded `CanDoItAll.slnx` at the re-anchored source state. It reported 3,460 findings and 783 diagnostics across the full solution; because that broad result contains substantial support/generated-project noise, it is orientation evidence only. Each C# subbundle must create or reuse a healthy scoped snapshot and record exact symbol/dependency evidence before editing.

## Boundary ownership

- M01 keeps canonical hash algorithms in Builder and persistence migration/mapping in Persistence.
- M03 keeps OS primitives behind the existing process-host ownership boundary; higher layers retain lifecycle intent only.
- M04 keeps JSON-RPC framing/control inside the local stdio connection and preserves one serialized writer.
- M05 keeps Docker recipe validation in the Docker plugin and application secret-file loading in Infrastructure.
- M06 keeps workspace containment in the central workspace guard and executable authority in the executable locator.

## Dependency direction

No project-reference change is planned. Existing direction from runtime/application/infrastructure toward Core, Abstractions, and Contracts must remain unchanged. A cycle or a required inward reference blocks the current subbundle and triggers bundle repair.

## Pattern decisions

- M01 uses an explicit enum plus switch for the closed hash-algorithm set; a new strategy hierarchy is rejected as unnecessary.
- M03 may use small platform adapters/handles owned by the process host. Platform-specific partial interop is allowed only if it remains a cohesive OS slice; runtime partial-class growth is blocked.
- M04-M06 prefer cohesive validators/value parsing and existing facades. New interfaces require a real test or ownership boundary.

## Testability contract

- Behavior tests instantiate the owning hasher/mapper/host/connection/validator/guard directly where practical.
- Every behavioral or governed unit includes a positive case and an adversarial negative that defeats a shallow implementation.
- Composition/lifecycle smoke proves production callers still route through the owner.
- No test may require credentials or weaken fail-closed behavior.

## Partial-class policy

No new runtime partial file is planned. Generated/UI code-behind and cohesive platform interop remain the only applicable allowed cases.

## Architecture proof required

Before closing an architecture-relevant subbundle: record scoped CodeAnalytics snapshot health, exact symbols, dependency/cycle result, changed ownership, isolated tests, composition smoke, and the C# architecture review-gate decision.
