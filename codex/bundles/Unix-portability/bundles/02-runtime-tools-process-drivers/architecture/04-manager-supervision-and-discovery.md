# Manager supervision and process discovery

## Primary ownership

A process launched by Manager/runtime is registered immediately with:

- PID;
- process start identity/time;
- executable identity;
- normalized argv hash/signature;
- authorized workspace/root;
- current user/owner;
- parent/lease/supervisor ID;
- lifecycle state;
- non-secret capability/recipe ID.

This registry is the primary truth.

## Recovery discovery

OS adapters may recover evidence after Manager restart:

### Windows

WMI/System.Management behind a Windows-only Manager adapter.

### Linux

Bounded `/proc` reads or equivalent. Handle:

- process exit during read;
- permission denied;
- kernel threads/empty cmdline;
- symlink/exe resolution;
- PID reuse;
- namespaces/containers;
- missing parent.

### macOS

Use a proven native API or a strictly controlled adapter. If command output is used, pin non-localized format and test parsing/races/permissions. Do not authorize action from locale-dependent human output.

## Termination authorization

Never terminate with only:

- process name;
- substring;
- executable basename;
- workspace text in command line.

Require PID/start identity plus sufficient registry/owner/executable/command/workspace evidence. Parent identity must match the current Manager at launch and is persisted for audit, but recovery must not require the historical PPID because Unix reparents surviving children when their original Manager exits. Ambiguity in authoritative identity yields a manual cleanup diagnostic.

## Supervisors and watchers

Share process lifecycle and Core watcher convergence primitives, but retain separate domain owners for:

- dotnet watch;
- Tailwind;
- tuning;
- other supervisors.

Events schedule work; deterministic fingerprint/rescan confirms state.

## B03 implementation decision

### Responsibility and ownership inventory

| Responsibility | Current owner | Target owner |
|---|---|---|
| Process creation, output capture, exact PID/start/executable identity, bounded tree termination | `WatchSupervisorService`, `TailwindWatchSupervisorService`, and `LocalProcessTuningExecutionAdapter` each use `System.Diagnostics.Process` | B01 `IWorkspaceLongRunningProcessHost`, consumed through a Manager-specific lifecycle coordinator |
| Durable launched-process authority | None | Manager-owned process registry under the configured Manager artifacts root |
| Restart discovery | Static `WorkspaceRuntimeProcessTools`; WMI on Windows and name-only enumeration elsewhere | Narrow Manager discovery contract with Windows WMI, Linux `/proc`, and macOS kernel `libproc` identity plus strictly parsed invariant `ps` evidence |
| Termination authorization | Name/path/command substring heuristics | Registry-first verifier requiring exact start identity plus owner, executable, observed command, and workspace-bound launch evidence; parent is launch-time evidence, not restart equality |
| Domain behavior | Watch and Tailwind supervisors | Remains in the separate Watch and Tailwind supervisors |
| File-change convergence | Tailwind-specific watcher/queue/polling code | Shared small convergence primitives; Tailwind keeps domain-specific roots/build decisions |

### Dependency direction

`CanDoItAll.Manager` is an outer executable and may depend inward on the existing B01
`CanDoItAll.AgentFramework.Core` process contracts and implementation. Core does not depend on
Manager. The Manager composition root selects its OS discovery leaf. `System.Management` remains
referenced only by the Windows leaf implementation; neutral registry, verifier, supervisors, and
tests do not reference WMI types.

### Selected patterns and boundaries

- **Registry-first ownership:** a durable record is written immediately after launch evidence is
  complete. It stores no raw argv or environment values; planned argv and observed command are
  represented by length-delimited SHA-256 fingerprints.
- **Manager lifecycle coordinator:** adapts B01 sessions to Manager purposes, registry state, output
  pumping, natural completion, cancellation, restart reconciliation, and termination diagnostics.
- **Strategy adapters:** one narrow process-discovery contract has Windows, Linux, and macOS leaves.
  Missing, raced, permission-denied, or incomplete evidence never authorizes termination.
- **Convergent watcher hints:** watcher events only schedule work. Debounce, duplicate suppression,
  overflow recovery, polling, and deterministic fingerprints confirm the resulting state.

### Rejected alternatives

- A second generic process runner in Manager: duplicates B01 identity and kill semantics.
- A broad cross-platform service project: creates a new abstraction layer without another consumer.
- Name, executable basename, or command substring recovery: cannot distinguish PID reuse or a
  foreign process and therefore cannot authorize termination.
- Persisting raw argv/environment: can retain credentials and is unnecessary for identity proof.
- Locale-dependent macOS process parsing: unstable across hosts and languages.

### Testability and proof seams

- Registry storage is replaceable by an in-memory test implementation; the durable implementation
  is restart-tested against a temporary managed root.
- Process launch/termination is tested through a fake `IWorkspaceLongRunningProcessHost`; a focused
  composition smoke test proves the real B01 host is wired once.
- Reparenting is covered by deterministic verifier proof and an actual Linux parent-exit/recovery
  integration. Final B01 termination independently revalidates exact start and executable identity.
- Linux `/proc` access plus macOS `libproc` identity and command execution are injected readers/runners with deterministic
  fixtures for races, permissions, malformed records, and PID reuse.
- The Windows mapper is tested without invoking WMI; a static architecture assertion limits
  `System.Management` usage to its leaf file.
- Manager portability tests are explicitly categorized so the pinned Linux command cannot silently
  discover zero tests.

### B03 dependency audit

The pre-B03 Manager project referenced SharedKernel and Infrastructure. The target adds exactly one
reference, from the outer Manager executable to `CanDoItAll.AgentFramework.Core`, so Manager can
consume the already-approved B01 host contract and implementation. No inner project references
Manager; only test projects reference the executable.

The configured CodeAnalytics service was not used because its scoped snapshot request was rejected
by the environment's private-source export policy. The required audit therefore used local project
XML plus compilation evidence: 105 repository projects, 632 resolved in-repository project-reference
edges, and zero project cycles. `dotnet list` confirms Manager's three direct references, and the
Release Manager build completes with zero warnings and zero errors. The reference direction remains
outer executable → Core/Foundation; there is no Core → Manager edge and no SDK-specific type leaks
through the Manager ownership contracts.
