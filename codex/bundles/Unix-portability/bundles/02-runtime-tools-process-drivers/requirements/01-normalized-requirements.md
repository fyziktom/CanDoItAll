# Runtime/tools/process requirements

## Requirement catalog

| ID | Priority | Owner | Requirement | Acceptance |
|---|---|---|---|---|
| RPREP-001 | P0 | B00 | The runtime bundle is rebased to an exact accepted core handoff anchor before any runtime production edit. | Handoff report records either C4 GO or the explicit operator-authorized provisional exception, immutable core/sibling anchors, local evidence, deferred support proof, current HEAD/delta, dirty state, and invalidated source references. |
| RPREP-002 | P0 | B00 | Every process launch, terminal, runtime node, Manager supervisor, MCP, external tool, plugin host tool, and process-driver surface is inventoried. | No unclassified P0/P1 runtime surface remains at gate R0. |
| RPREP-003 | P0 | B00 | Ownership between MAF execution primitives, Workbench presentation, Manager supervision, Plugins, and Processes domain semantics is approved before refactoring. | Architecture map has one authoritative owner for plan, execution, lifecycle, recovery, capability, receipt, and domain failure semantics. |
| RPREP-004 | P1 | B00 | The bundle split decision is revisited using measured file/project/dependency scope. | If split triggers are met, child bundles are generated before implementation; no miscellaneous overflow is accepted. |
| EXEC-001 | P0 | B01 | Direct execution uses a typed immutable plan containing executable identity, argv, working directory, environment names/values, timeout, output limits, side effects, and boundary metadata. | No ordinary .NET/Docker/Python/Node command is reconstructed from display text or passed through a shell. |
| EXEC-002 | P0 | B01 | One tested low-level process execution primitive is authoritative for workspace commands, external tools, and injected plugin runtimes unless a documented boundary requires a wrapper. | Duplicate local runners do not diverge in cancellation, kill, output, environment, or receipt behavior. |
| EXEC-003 | P0 | B01 | Executable resolution is deterministic, capability-owned, and OS-correct. | Windows PATHEXT behavior, Unix execute bits/shebang, explicit paths, PATH order, case rules, and missing/ambiguous candidates have tests. |
| EXEC-004 | P0 | B01 | Environment inheritance preserves host key semantics and uses safe common plus OS/tool-specific allowlists. | Unix case-distinct variables remain distinct; secret values are opt-in bindings and only names enter receipts. |
| EXEC-005 | P0 | B01 | Cancellation and timeout terminate the owned process tree or report a bounded residual-process failure. | Windows/Linux/macOS tests cover grandchildren, TERM/grace/KILL where implemented, output drain, races, and cancellation source. |
| EXEC-006 | P0 | B01 | Process host, registry, and workspace runtime services have one explicit owner and disposal lifecycle. | No plugin/workspace factory constructs an untracked parallel host; disposal and kept-alive leases are proven. |
| EXEC-007 | P1 | B01 | Execution receipts are deterministic, redact sensitive output, preserve logical paths, and record truthful isolation/capability data. | Sentinel-secret and cross-OS golden receipt tests pass. |
| EXEC-008 | P1 | B01 | Native adapters are added only after characterization proves .NET Process APIs insufficient. | Decision records capture the failing behavior and scope any Windows Job Object or Unix process-group adapter. |
| NODE-001 | P0 | B02 | ProjectStructure runtime metadata compiles to a shell-neutral typed execution plan. | Plan compilation is pure/testable and separate from direct execution, terminal presentation, and UI display. |
| NODE-002 | P0 | B02 | PowerShell, POSIX shell, and other script languages are explicit script modes with separate policy and executable dependency. | Raw scripts never become an implicit fallback for ordinary commands. |
| NODE-003 | P0 | B02 | Python virtual environments resolve their interpreter/tool paths by OS and execute directly without requiring activation. | Windows Scripts and Unix bin layouts are tested; Conda behavior is capability-gated. |
| NODE-004 | P1 | B02 | Opening an interactive terminal is optional presentation, not the execution authority. | Headless mode executes directly or reports the terminal-only feature unavailable without blocking startup. |
| NODE-005 | P0 | B02 | Elevation is a separate optional capability and is unavailable by default on Linux/macOS. | No automatic sudo, pkexec, osascript, or password prompt is introduced; Windows runas remains explicitly governed. |
| NODE-006 | P1 | B02 | Legacy PowerShell/cmd command metadata has a bounded compatibility/migration path into typed runtime-node fields. | Migration does not attempt to safely parse arbitrary dynamic shell code; unresolved nodes require operator repair. |
| NODE-007 | P1 | B02 | Workbench UI labels and actions reflect Run, Open terminal, Open file, and Elevated launch capabilities rather than PowerShell-centric assumptions. | Playwright snapshots cover available, unavailable, dependency-missing, headless, and foreign-path states. |
| NODE-008 | P0 | B02 | Runtime node path authority uses the core logical/physical path contracts and cannot escape approved roots. | Agent-selected and operator-selected modes retain their distinct authority rules. |
| MGR-001 | P0 | B03 | Manager persists identity for processes it launches and uses that registry as primary ownership truth. | PID, start identity, executable, argv hash, workspace, user/owner, parent/lease, and lifecycle state are captured without secrets. |
| MGR-002 | P1 | B03 | Windows WMI recovery discovery is isolated behind a Windows leaf adapter. | Neutral Manager code compiles/runs without directly calling System.Management. |
| MGR-003 | P0 | B03 | Linux recovery discovery uses a bounded /proc or equivalent adapter and validates ownership before action. | Missing permissions, exited processes, PID reuse, and unreadable command lines fail safely. |
| MGR-004 | P0 | B03 | macOS recovery discovery uses a proven bounded adapter and validates ownership before action. | The implementation does not parse locale-dependent human output without strict fixtures and fallback behavior. |
| MGR-005 | P0 | B03 | Manager never terminates a process using name-only or path-substring evidence. | Ambiguous or incomplete identity produces a diagnostic/manual cleanup state. |
| MGR-006 | P1 | B03 | Watch/Tailwind supervisors share tested process lifecycle and watcher-rescan primitives without merging domain responsibilities. | Restart/backoff, duplicate start, cancellation, and output-file convergence are tested on all target OSes. |
| MGR-007 | P1 | B03 | Manager path collections and comparisons use core logical/physical semantics rather than global OrdinalIgnoreCase. | Case-collision fixtures pass on Linux and configured macOS filesystems. |
| MCP-001 | P0 | B04 | Local MCP command authorization validates the resolved executable identity using one capability-owned policy. | Suffix, PATH, explicit path, symlink, and case tests pass on Windows/Linux/macOS. |
| MCP-002 | P0 | B04 | Local stdio MCP launch reuses authoritative process/environment semantics and has bounded startup/shutdown ownership. | Cancellation leaves no server process and diagnostics expose no secret values. |
| MCP-003 | P1 | B04 | Playwright MCP is installed/resolved from a controlled versioned application tool root. | Global npx cache scanning is removed from production selection or explicitly limited to a non-authoritative diagnostic path. |
| MCP-004 | P0 | B04 | MCP environment secret bindings resolve at invocation time through the secret runtime boundary. | Persisted descriptors contain references/names, not secret values; receipts contain approved names only. |
| MCP-005 | P1 | B04 | MCP setup validation reports command, runtime, package, working-directory, secret-binding, and platform capability failures separately. | Repair hints are deterministic and tested. |
| TOOL-001 | P0 | B04 | External process tools share or wrap the authoritative process primitive and do not implement a divergent local runner. | Timeout, output limit, cancellation, and tree-kill tests are common. |
| TOOL-002 | P0 | B04 | External tool stdout/stderr diagnostics are bounded and redacted before entering exceptions, receipts, agent context, or CI logs. | Sentinel-secret tests cover JSON parse and non-zero-exit paths. |
| PLUG-001 | P0 | B05 | Docker host tools receive the authoritative process host, executable resolver, environment policy, and workspace scope through composition. | No direct new LocalWorkspaceProcessHost remains in plugin code. |
| PLUG-002 | P1 | B05 | Docker capability reports daemon/socket/context availability separately from executable presence. | Linux socket permission, macOS desktop absence, remote DOCKER_HOST, and timeout states are tested without weakening policy. |
| PLUG-003 | P0 | B05 | The pinned FileTools desktop package has an explicit Windows/Linux/macOS compatibility report and test evidence. | Unsupported or unverified OS capabilities are disabled truthfully; package upgrade/change is isolated. |
| PLUG-004 | P1 | B05 | Desktop open/reveal/preferred-application behavior is modeled as optional capabilities with host-bound executable preferences. | Headless and service profiles never attempt desktop launch. |
| PLUG-005 | P1 | B05 | Every native/external plugin dependency has a version, probe, tested OS matrix, failure mode, and remediation entry. | Unknown dependencies block support claims but not unrelated core startup. |
| PROC-001 | P0 | B06 | Processes remains the owner of process-domain semantics, recovery, eligibility, evidence, and failure interpretation. | No process-specific rule is moved into MAF Core, generic Infrastructure, or a global OS service. |
| PROC-002 | P0 | B06 | Process strategies and special/domain drivers declare required host capabilities rather than branching directly on OS. | Compilation/launch produces a deterministic missing-capability diagnostic before side effects. |
| PROC-003 | P1 | B06 | ProcessDriverLayer.Platform is used only for process strategy composition that consumes declared host capabilities. | Architecture tests prevent filesystem/secrets/native process primitives from migrating into process drivers. |
| PROC-004 | P0 | B06 | Runtime capability facts cannot grant project/workspace authority or override approvals/tool policy. | Current canonical authority and per-run workspace ownership invariants remain green. |
| PROC-005 | P1 | B06 | Process receipts/evidence serialize logical paths and platform capability facts without leaking host-sensitive absolute paths or secrets. | Cross-OS golden fixtures and evidence policy tests pass. |
| PROC-006 | P1 | B06 | Special tools/domain drivers have explicit behavior for unavailable terminal, shell, Docker, desktop, Python, Node, and MCP capabilities. | Templates can be validated before a run and offer safe remediation or alternate strategy. |
| RCI-001 | P0 | B07 | Windows, Ubuntu, and macOS run focused process-host, executable, environment, runtime-node, Manager, MCP, plugin, and process-driver tests. | Actual-host gates are active and not replaced by mocked platform tests. |
| RCI-002 | P1 | B07 | Workbench Playwright tests prove capability-aware actions and no PowerShell-only language on Unix/headless profiles. | Evidence includes route, viewport, capability fixture, screenshots, and result. |
| RCI-003 | P0 | B07 | Local stdio MCP and at least one governed external tool execute end-to-end on each claimed OS profile. | Approval, secret binding, workspace containment, timeout, output, and cleanup invariants remain intact. |
| RCI-004 | P0 | B07 | Manager launch/restart/recovery/termination tests prove no unrelated process is killed and no owned child is leaked. | PID reuse, process exit race, unreadable metadata, watcher overflow, and shutdown interruption are injected. |
| RCI-005 | P0 | B07 | A representative process using special tools completes or fails with the planned capability diagnostic on every target profile. | Processes ownership, receipts, recovery, and host capability selection are reviewed independently. |
| RCI-006 | P0 | B07 | Windows regression remains green and all runtime features can be disabled without blocking the core host. | Runtime bundle closure cannot weaken the Core C4 evidence. |
| RCI-007 | P0 | B07 | Final Gate R4 records supported profiles, known limitations, external dependency versions, rollback, and evidence paths. | Only R4 may mark Unix portability implementation complete. |

## Status rules

- `Planned` during preparation.
- `In progress` only while the owning subbundle is active.
- `Solved` only with linked validation evidence and a GO gate.
- `Blocked` must name the gate/finding/dependency.
- A later source or evidence change reopens the requirement.
