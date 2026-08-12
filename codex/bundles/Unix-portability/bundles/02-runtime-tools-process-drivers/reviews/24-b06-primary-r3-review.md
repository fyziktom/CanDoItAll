# B06 primary Gate R3 review

## Decision

**GO recommended, pending independent review.** B06 satisfies PROC-001 through PROC-006 within the operator-approved actual Windows/Linux boundary and explicit actual-macOS deferral. The final focused behavior, build, architecture, persistence, source-integrity, graph, and redaction evidence is green.

## Architecture review

- Processes retains semantic ownership. Host/application/plugin adapters project typed availability and ports; MAF remains a generic execution adapter.
- Capability facts are non-granting observations. Workspace/project/mutation/tool/approval/secret/process authority remains at the canonical B01-B05 boundaries.
- Immutable plan/runtime dispatch uses one exact identity chain: plan hash and step definition, driver/strategy/factory/schema/inputs, sealed tool and capability sets, profile/facts, current snapshot, and returned strategy identity.
- Platform packages must declare valid bounded host constraints. Generic and special driver layers are guarded against native process, filesystem, secret, and OS-probe ownership.
- Product filesystem inspection uses a narrow typed owner port. Path comparison uses the host physical-filesystem policy and canonical alias authority.
- The 106-project/639-reference graph is acyclic. Current Process, MAF, module, application, and plugin reference direction remains inward toward contracts and owner ports.

## Security and correctness review

- Compilation/launch and every new execution branch fail before side effects on malformed contracts, unavailable host facts, missing statically knowable runtimes, mixed browser transports, or assignment drift.
- Existing verified child reconciliation is not blocked by later executable loss, preserving lifecycle ownership without authorizing a new launch.
- Capability source ownership, returned facts, profiles, execution ports, counts, uniqueness, and cross-snapshot coherence validate before consumption.
- Public and durable result fields use stable bounded tokens/digests/references and shared narrative sanitization. Native paths, credential-bearing URI components, secret-shaped text, raw provider/exception messages, and unbounded collections do not cross the receipt/API/artifact boundary.
- Persistence validates the same contracts on write and read. Corrupt or legacy rows cannot normalize blank/null/duplicate/malformed requirements or receipts into permissive state.
- Scope and completion-receipt parsing fails closed on invalid enums, JSON shapes, selectors, branch rules, narratives, counts, lengths, and generated keys. Repair cannot widen the immutable contract.
- Local Playwright/npx readiness proves local MCP, Node, and npm before managed-directory mutation. Remote HTTP browser capability does not inherit local requirements.

## Evidence review

- Final TRXs: all 124 governed B06 regression methods execute as Windows 206/206 unit cases plus 1/1 integration and pinned Ubuntu 206/206 unit cases plus 1/1 integration; zero failures/skips.
- Three final affected Release build logs contain zero warning/error hits.
- Governed proof: 18 corrections, 18 assertions, 72 source hashes, and 10 final test/build/host/graph artifact hashes.
- Source references: 171 records, 171 unique IDs, 171 unique paths, zero missing.
- Graph: 106 projects, 639 in-repository references, zero cycles, zero unresolved references.
- Schema-3 scan: 23 candidates, 22 scanned text files plus one control, zero coverage gaps, zero findings.
- `git diff --check` exits zero; four recorded EOL notices remain informational.
- Portable runtime-bundle payload validator before canonical index/checksum refresh: 338 files, zero errors, zero warnings.

## Requirement disposition

| Requirement | Status | Review conclusion |
|---|---|---|
| PROC-001 | Implemented | Exact canonical owner/driver/strategy/factory identity and typed non-granting host facts. |
| PROC-002 | Implemented | Launch and current-host generic dispatch validation fail before new side effects and survive restart. |
| PROC-003 | Implemented | Process semantics remain in Processes; host primitives stay behind owner ports; Platform is capability-constrained. |
| PROC-004 | Implemented | Scope, receipt, assignment, and authority contracts fail closed without widening grants. |
| PROC-005 | Implemented | Immutable bounded non-disclosing plans, results, receipts, evidence, recovery data, APIs, and persisted rows. |
| PROC-006 | Implemented | Special/process-starting tools have explicit supported/unavailable/alternate and transport-specific behavior. |

## Deferred boundaries

Actual macOS is deferred by operator instruction. Hosted CI and final broad R4 evidence remain B07 work. No deterministic macOS fixture is relabeled as actual-host execution.

## Independent review request

Bound the independent decision to B06 source/evidence consistency: Process/MAF ownership, non-granting facts, exact plan/runtime/factory identity, current-host and fail-before-side-effect gates, special-tool inventory, assignment/repair drift, product filesystem owner boundary, bounded non-disclosing receipts/persistence, Platform semantics, all 124 named regression methods in four final TRXs, three builds, 72 source hashes, 10 final artifact hashes, 171/171/171/0 source references, 106/639/0 graph, and schema-3 coverage. Write only `reviews/25-b06-independent-r3-review.md`.
