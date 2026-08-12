# B06 evidence report

## Outcome

B06 implementation and governed proof are complete for bounded independent Gate R3 review. Processes remains the semantic owner of strategy eligibility, alternate/recovery behavior, failure interpretation, immutable contracts, and receipts. Host owners expose only bounded typed facts and execution ports; those facts never grant tool, workspace, project, mutation, approval, secret, or process authority.

The proof tier is `Governed`. Validation follows the operator-requested fast ladder: exact regressions while editing, affected Release builds, one final focused Windows unit/integration slice, and the same prebuilt assemblies under pinned Ubuntu Docker. No broad suite was rerun.

## Implementation

- Stable typed host capability IDs, availability/reason states, execution ports, profile IDs, and bounded fact snapshots now cross the Process boundary without paths or free-form host messages.
- Process driver and strategy catalogs reject default, malformed, over-bound, or unconstrained Platform contracts. `Platform` means a capability-constrained strategy package, not a general operating-system service layer.
- Compilation and launch seal effective runtime-tool and host-capability sets after assignment enrichment. The immutable plan hash includes those requirements plus driver, strategy, inputs, profile, and fact identity.
- Persistence validates requirements, plan hashes, result receipts, host evidence, recovery decisions, and blocked-action ledgers on both write and read. Blank, duplicate, malformed, tampered, or oversized data fails closed.
- Generic strategy dispatch exact-matches plan, runtime state, work item, factory, driver, strategy, schema, inputs, requirements, and current host facts before factory/strategy side effects. Existing owned-child reconciliation remains possible after capability loss.
- Runtime-owned, workflow, subprocess, and agent branches share fail-before-new-side-effect tool/capability gates. Assignment repair and execution reject any drift from the sealed runtime-tool contract before reference data, catalog, repair, reconciliation, or execution work.
- Special-tool inventory maps direct execution, .NET, Python/spreadsheet, PowerShell, POSIX, Node/npm, Docker, local/remote MCP, desktop, terminal, Git, and lifecycle-only stop semantics. Local Playwright requires local stdio MCP, Node, and npm; remote HTTP does not.
- Product-completion filesystem inspection now delegates through the scoped workspace file owner, preserves canonical external-target aliases, uses host physical-path policy, and emits bounded logical labels/hashes rather than native roots or compared content.
- Public receipt policy bounds and sanitizes summaries, codes, references, URI credentials/query/fragment data, physical paths in Windows/Unix syntax, provider/exception messages, operator/API results, and managed evidence before persistence.
- Self-proving architecture guards cover generic Process drivers and module special drivers for forbidden host/process/filesystem/secret/OS ownership while retaining typed owner delegation.

## Requirement evidence

| Requirement | Proof |
|---|---|
| PROC-001 | Exact driver/strategy/factory/plan/runtime identity; one canonical managed adapter source; typed owner ports only; stale/foreign package tests. |
| PROC-002 | Launch and generic dispatch current-host gates; sealed plan/state requirements; fail-before-side-effect workflow/subprocess/runtime-owned/agent tests; snapshot-drift failure evidence. |
| PROC-003 | Processes owns semantics; generic and special drivers do not own native process/filesystem/secret/OS primitives; Platform requires valid typed host constraints. |
| PROC-004 | Capability facts are non-granting; malformed scope/receipt shapes become one invalid-contract marker; assignment repair requires exact immutable tool equality. |
| PROC-005 | Bounded non-disclosing receipts/evidence; canonical digests/codes/references; write/read persistence validation; 72 source hashes and ten final artifact hashes. |
| PROC-006 | Explicit process-starter/special-tool inventory; supported, unavailable, local, remote, mixed, alternate, and lifecycle-only behavior covered. |

## Focused behavior evidence

| Host/profile | Slice | Result | Artifact |
|---|---|---:|---|
| Windows 10.0.26200 x64 / .NET SDK 10.0.302 | Exact 124 governed B06 regression methods, including unit, architecture, persistence, and runtime cases | 206/206 | `artifacts/unix-portability/B06/windows/b06-unit-windows.trx` |
| Windows | Process capability portability integration | 1/1 | `artifacts/unix-portability/B06/windows/b06-integration-windows.trx` |
| Ubuntu 24.04.4 Docker / image digest `sha256:72dd743...01b0` | Same prebuilt exact regression slice; FileTools sibling mounted read-only for the ownership guard | 206/206 | `artifacts/unix-portability/B06/linux/b06-unit-linux.trx` |
| Same Ubuntu container | Same prebuilt integration class | 1/1 | `artifacts/unix-portability/B06/linux/b06-integration-linux.trx` |

The retained characterization TRXs are historical inputs only. The governed test hashes bind the four final artifacts above.

## Build and architecture evidence

The final affected Release builds for Modules.Processes, Unit tests, and Integration tests report zero warnings and zero errors. These three builds traverse the changed Process contracts, drivers, builder, application, runtime, persistence, module adapters, Web/operator boundaries, and tests.

- The project graph contains 106 projects, 639 in-repository references, zero cyclic projects, and zero unresolved project references. Artifact: `artifacts/unix-portability/B06/b06-project-graph.json`.
- The source-reference manifest contains 171 records, 171 unique IDs, 171 unique paths, and zero missing paths. B06 contributes 36 one-to-one source references.
- The 36 governed B06 reference files contain zero `TODO`, `FIXME`, or `NotImplementedException` markers.
- `git diff --check` exits zero. Four existing working-tree line-ending notices are informational.
- The module and generic driver boundary tests pass in both final 206-case host slices.
- Portable runtime-bundle payload validation covers 338 files with zero errors and zero warnings before the canonical index/checksum refresh and independent review.
- The governed generator derives 124 exact B06 method names across 17 source files and rejects either host TRX when any named method is missing; the 206-case expansion comes only from theory data.

## Governed integrity

`artifacts/unix-portability/B06/b06-governed-proof.json` binds:

- 18 failing-first/correction records;
- 18 semantic source assertions;
- 72 source hashes;
- four final TRX hashes;
- three final build-log hashes;
- two host-environment hashes;
- one project-graph hash.

The proof generator rejects unexpected TRX totals, build warning/error markers, graph count drift, cycles, unresolved project references, and missing source inputs.

## Redaction and artifact coverage

The schema-3 scanner accounts for 23 candidates as 22 scanned text artifacts plus its output control. It reports zero oversized, non-text, unreadable, or otherwise uncovered files and zero findings. The intentionally secret-shaped inline fixture was renamed to the scanner's explicit `test-only` form and both final unit TRXs were refreshed; no rule was suppressed. Artifact: `artifacts/unix-portability/B06/b06-secret-scan.json`.

## Residual boundaries

- Actual macOS execution remains deferred by explicit operator instruction. Deterministic macOS fixtures are not represented as actual-host proof.
- Hosted CI and the final broad Windows/Linux/macOS R4 aggregate remain B07 scope.
- The final broad suite was intentionally not rerun at this subbundle gate under the fast-validation policy.

## Gate recommendation

Recommend `Gate R3 GO`, subject to the frozen independent review. B07 remains blocked until that decision and canonical gate/index/checksum bookkeeping are recorded.
