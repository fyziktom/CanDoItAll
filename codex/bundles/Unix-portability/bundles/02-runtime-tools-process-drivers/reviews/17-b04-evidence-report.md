# B04 evidence report

## Outcome

B04 is implemented and ready for independent Gate R3a review. Local stdio MCP and external process tools now adapt to the B01 process, executable, environment, workspace-path, timeout, cancellation, output, and tree-termination authorities. The MCP layer retains protocol framing and setup semantics but no longer owns `Process`, executable discovery, or stderr pumping.

The proof tier is `Governed` because B04 changes P0 executable authorization, secret lifetime, process ownership, and untrusted diagnostic behavior. Validation follows the operator-requested fast ladder: named regressions, affected-project builds, one focused Windows slice, and the exact same prebuilt assemblies under pinned Linux Docker. No broad solution suite was rerun.

## Implementation

- `LocalWorkspaceProcessHost` remains the only physical process owner. Its narrow derived duplex session exposes stdin/stdout while retaining exact identity, bounded stderr, cancellation, graceful/force-tree shutdown, and disposal.
- Local MCP resolves the working directory through the workspace path authority, resolves executable identity through `WorkspaceExecutableLocator`, and validates the final host-correct identity against capability-owned names.
- Secret bindings persist names/references only, resolve immediately before launch with the host environment comparer, and are cleared from retained launch state. Receipts expose names, not values.
- Playwright MCP requires exact `@playwright/mcp@0.0.78`, rejects linked/reparse managed-root ancestry before writes and use, disables package scripts, and publishes with non-replacing atomic move. Reuse binds the complete dependency tree, entry type, Unix mode, lockfile, and resolved Node executable; production contains no global `_npx` cache discovery.
- Runtime composition requires an explicit `IMcpClientFactory`; no default factory silently creates a second physical authority.
- Runtime composition and the post-composition agent factory retain ownership of acquired MCP leases until `RuntimeBuildResult` accepts them. Any pre-handoff failure disposes resources in reverse order; an attachment failure stops its started client exactly once.
- Local stdio initialization accepts only a complete JSON-RPC 2.0 result for the supported protocol version, validates mandatory capabilities/server identity, and reuses the object-only tool-argument parser shared with remote MCP.
- External process tools route through `WorkspaceExternalProcessRunner`, which resolves the authorized working directory, resolves and authorizes the final executable, and delegates execution to B01.
- MCP and external-tool cleanup rejects an unconfirmed residual process tree. Process diagnostics preserve typed category, capability identity, exit/timeout metadata, and repair hints while omitting raw stdout/stderr, parser messages, input JSON, physical roots, and physical executable paths.
- MCP setup now distinguishes command policy, runtime dependency, package setup, working directory, permission, secret binding, unsupported platform, process start, malformed-valid protocol shape, and cleanup failures. New enum values were appended to preserve all existing numeric identities.
- `AlwaysRequire` local MCP is proven at the composition boundary to emit `ApprovalRequiredAIFunction` and `HasApprovalTools=true`; actual-host process proof remains a separate complementary layer.

## Requirement evidence

| Requirement | Proof |
|---|---|
| MCP-001 | Requested and resolved executable policy tests cover Windows suffix/case and Unix exact-case/no-suffix behavior, explicit/foreign paths, deterministic cross-host profiles, and actual Windows/Linux final symlink-target authorization. |
| MCP-002 | Canonical duplex-session unit proof plus actual Windows/Linux start, JSON-RPC exchange, timeout/caller cancellation, exact child-exit verification, residual cleanup rejection, and pre-handoff lease cleanup. MCP production contains no direct process owner. |
| MCP-003 | Exact-version parsing, no-network managed install, linked-root rejection, atomic publication, complete dependency-tree/type/mode plus lock/Node verification, reuse, tamper rejection, and no-global-cache source assertion. |
| MCP-004 | Raw-value rejection, environment-name validation, invocation-time binding, ProcessStartInfo clearing, receipt-name-only assertions, and sentinel non-disclosure. |
| MCP-005 | Deterministic typed tests for command, runtime, package, working directory, permission, secret, platform, handshake/list, malformed-valid JSON, process start, timeout, cancellation, and cleanup failures. |
| TOOL-001 | The divergent runner is removed; the application adapter shares B01 timeout, output limits, cancellation, and tree termination. Unit and actual-host integration paths use the same contract. |
| TOOL-002 | Invalid JSON, valid non-object JSON, nonzero-exit, and residual-cleanup fixtures prove typed diagnostics without copied stdout/stderr. The complete artifact scan has no coverage gap or unclassified finding. |

## Focused behavior evidence

| Host/profile | Slice | Result | Artifact |
|---|---|---:|---|
| Windows / .NET SDK 10.0.302 | MCP, external process, executable policy, process duplex, capability hardening, seed, and contract tests | 154/154 | `artifacts/unix-portability/B04/windows/b04-unit-windows.trx` |
| Windows | `McpPortability`, `ExternalToolPortability`, and shared `ProcessPortability` integration | 18/18 | `artifacts/unix-portability/B04/windows/b04-integration-windows.trx` |
| Linux Docker / `mcr.microsoft.com/dotnet/sdk:10.0` / digest `ed034a8...ad4664` | Exact same prebuilt unit assembly/filter | 154/154 | `artifacts/unix-portability/B04/linux/b04-unit-linux.trx` |
| Same Linux container | Exact same prebuilt integration assembly/filter | 18/18 | `artifacts/unix-portability/B04/linux/b04-integration-linux.trx` |

## Build and architecture evidence

Ten affected production/support projects build with zero warnings and zero errors: SharedKernel, Capabilities.Abstractions, AgentFramework Core, MCP, Tools.Abstractions, Tools, Persistence, Maf, Modules.AgentFramework, and the deterministic MCP test host. Durable logs are under `artifacts/unix-portability/B04/windows/`.

- MCP and external-process production sources contain no `Process.Start`, `ProcessStartInfo`, `new Process`, or name-based process enumeration. The canonical host remains the only B04 process owner.
- The former MCP executable resolver and stderr collector are deleted; final resolution belongs to B01.
- B04 adds one approved inward production reference from AgentFramework.Tools to SharedKernel for the shared secret-argument policy, plus one dependency-free deterministic test-host project and one incoming Integration reference. The resulting graph contains 106 projects, 634 in-repository references, and zero cyclic projects.
- The requested CodeAnalytics snapshot was rejected because the service would export private source. The security boundary was not bypassed; local graph, build, source-owner, and anti-stub assertions are authoritative for R3a.
- The source-reference manifest contains 115 records, 115 unique IDs, 115 unique paths, and zero missing paths.
- `artifacts/unix-portability/B04/b04-governed-proof.json` binds 29 failing-first/correction records, sixteen semantic assertions, 66 source hashes, and sixteen test/build/host hashes. Primary recomputation found zero mismatches.

## Redaction and artifact coverage

The schema-3 scanner uses an explicit 200 MiB limit and accounts for 19 candidate files: 18 text artifacts scanned and its output excluded as the control input. It reports zero oversized, non-text, unreadable, or otherwise uncovered files. Four entries share one fingerprint and are the same intentional synthetic secret-argument fixture recorded in the result and test-definition sections of both host TRXs; no captured value or source excerpt is stored. The classification is `artifacts/unix-portability/B04/b04-secret-scan-classification.md`, and the report is `artifacts/unix-portability/B04/b04-secret-scan.json`.

## Residual boundaries

- Genuine macOS execution remains deferred under the operator instruction. Deterministic host-profile and unsupported-platform tests do not constitute actual-host proof.
- Network/npm service availability is operational. Production fails explicitly on setup failure and never replaces a tampered or conflicting managed installation automatically.
- Remote HTTP tool diagnostic hardening is outside TOOL-002's local-process stdout/stderr boundary and is not claimed here.
- Hosted CI and final broad Windows/Linux R4 evidence remain deferred to B07.

## Gate recommendation

Primary recommendation: `Gate R3a GO`, pending the required independent architecture/runtime/security review.
