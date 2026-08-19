# B04 primary Gate R3a review

## Decision

`GO recommended; independent review pending.`

B04 satisfies MCP-001 through MCP-005 and TOOL-001 through TOOL-002 on the frozen source and focused evidence set. B05 remains blocked until the independent reviewer records Gate R3a GO and the bundle index/checksum records are regenerated.

## Architecture review

- `LocalWorkspaceProcessHost` remains the sole physical process owner. The duplex interface is a narrow derived session needed by stdio protocols, not a second process abstraction.
- MCP owns JSON-RPC framing, tool protocol, setup validation, and MCP-specific lifecycle orchestration; it consumes B01 execution, executable, environment, and workspace-path contracts.
- External process tools retain tool descriptor/result semantics while the composition adapter owns physical resolution and delegates execution to B01.
- Playwright package setup is application-managed, exact-versioned, linked-root safe, complete-tree/type/mode/runtime integrity-bound, and atomic. Global package caches are not execution authorities.
- One approved inward production reference was added from AgentFramework.Tools to SharedKernel for the shared secret-argument policy; no new production layer was introduced. The only new project is a dependency-free deterministic integration fixture.
- The local graph remains acyclic at 106 projects and 634 in-repository references.

## Correctness and security review

- Authorization occurs after executable resolution and uses host-correct final identity semantics. Foreign path syntax and unapproved suffix/case variants fail closed.
- MCP working directories pass through the same workspace/external-target authority as other runtime owners.
- Secret values are invocation-scoped, absent from persisted descriptors/receipts, and cleared from launch state after process creation.
- MCP shutdown is bounded and mandatory even when cleanup cancellation is interrupted; residual cleanup fails explicitly, and pre-handoff composition/agent-build failures release every acquired lease.
- Managed Playwright installation rejects mutable version specifications, linked/reparse managed ancestry, dependency or mode drift, and tampered runtime evidence; it disables install scripts and uses no-replace publication.
- External invalid JSON, valid non-object JSON, nonzero exit, timeout, cancellation, and residual-cleanup diagnostics do not copy process output or physical paths.
- Actual-process caller cancellation is proven for local MCP and external tools on Windows/Linux with exact child-exit checks. `AlwaysRequire` approval wrapping is proven separately at runtime composition.
- Local stdio initialization fails closed on missing or unsupported initialize results, and both local and remote transports require object-shaped tool arguments.
- New diagnostic enum members are appended, preserving existing numeric compatibility.

## Evidence integrity

- Windows/Linux focused unit slices: 154/154 on each host.
- Windows/Linux focused integration slices: 18/18 on each host.
- Ten affected builds: zero warnings/errors.
- Governed proof: 66/66 source and 16/16 artifact hashes match; sixteen semantic assertions and 29 failing-first/correction records are present.
- Source manifest: 115/115 unique IDs/paths, zero missing.
- Schema-3 scan: 19 candidates, 18 scanned text, one control output, zero coverage gaps, and four classified occurrences of one intentional synthetic fixture fingerprint with no captured value.
- Static ownership scan: no second MCP/external-tool `Process` owner and no global Playwright cache authority.
- `git diff --check`: clean apart from the three previously recorded bundle CSV line-ending notices.
- Runtime portable validator: 326 files, zero errors, zero warnings with checksums skipped pending the independent review append.

## Validation proportionality

The validation is intentionally focused. It ran named regressions first, the affected production/support builds once, then the exact finalized assemblies on Windows and pinned Linux. The broad suite remains reserved for aggregate runtime closure, matching the operator's fast-validation requirement.

## Residuals

- Actual macOS is explicitly operator-deferred; deterministic host semantics remain green but are not actual-host evidence.
- Hosted CI and final R4 proof remain deferred to B07.
- Remote HTTP tools are not included in TOOL-002's local-process stdout/stderr claim.

## Handoff

Request independent Gate R3a review against reviews 17–18 and the governed artifact. Do not begin B05 until that decision is GO and integrity bookkeeping is complete.
