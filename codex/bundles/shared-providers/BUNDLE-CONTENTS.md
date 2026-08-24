# Bundle contents

This index describes the prepared execution package. It is an implementation plan and governed
work queue; it does not claim that the product feature has already been implemented.

## Root control files

| File | Purpose |
| --- | --- |
| `README.md` | Mission, baselines, start order, and architectural headline. |
| `START-CODEX-PROMPT.md` | Ready-to-paste Codex 5.6 ultra kickoff prompt. |
| `CODEX-EXECUTION-CONTRACT.md` | Non-negotiable execution, security, architecture, test, and final-state rules. |
| `STATUS.md` | Single-source subbundle lock/progression state. Only SB00 is initially ready. |
| `bundle.json` | Machine-readable identity, baseline, and critical gate metadata. |
| `test-budget.json` | Machine-readable focused-test and broad-gate limits. |
| `EXECUTION-REPORT.md` | Executor-owned cumulative implementation report template. |
| `CLOSURE.md` | Executor-owned final traceability and closure document. |
| `PREPARATION-REPORT.md` | Preparation-time analysis, decisions, and validation result. |
| `preparation-validation.json` | Machine-readable preparation validator and content-check result. |
| `bundle-file-manifest.json` | SHA-256 and size inventory for prepared files, excluding itself. |

## Analysis and architecture

| Directory | Prepared role |
| --- | --- |
| `inputs/` | Verbatim request, repository baselines, and primary protocol/standard sources. |
| `requirements/` | Functional, security/non-functional, and acceptance requirements. |
| `current-state/` | Provider/runtime, Workspace, API/auth/OpenAPI, test/Compose, and risk findings. |
| `architecture/` | Required C# architecture sections plus target data, protocol, relay, sync, security, UI, and Docker decisions. |
| `inventories/` | Source/symbol/test/SharedInfo impact maps. |
| `plan/` | Dependency graph, execution order, focused test budget, Docker E2E, OpenAPI/SharedInfo, and architecture checkpoints. |
| `traceability/` | Input coverage, FR/NFR ownership/proof matrix, and risk-to-proof map. |
| `evidence/` | Preparation-time repository and standards evidence index. |
| `reviews/` | Architecture, backend checkpoint, UI, and final closure gates. |

## Execution support

| Directory | Prepared role |
| --- | --- |
| `subbundles/` | Thirteen dependency-ordered work units, SB00 through SB12. |
| `templates/` | Proof, handoff, execution, manual operator, and architecture review templates. |
| `scripts/` | Cross-platform structural validator and wrapper scripts. |

## Subbundle progression

| ID | Outcome | Initially |
| --- | --- | --- |
| SB00 | Re-characterize current code and lock architecture decisions. | `READY` |
| SB01 | Protocol contracts, public identities, routing codec, and access-context middleware. | `LOCKED` |
| SB02 | PostgreSQL publication/source/import/audit persistence and services. | `LOCKED` |
| SB03 | Authorized sanitized catalog with revision and ETag behavior. | `LOCKED` |
| SB04 | Bounded OpenAI-compatible relay, streaming, images, cancellation, usage, and redaction. | `LOCKED` |
| SB05 | Secure shared-source client, catalog sync, selection, and deterministic reconciliation. | `LOCKED` |
| SB06 | Shared connector runtime projection and hybrid shared/personal provider use. | `LOCKED` |
| SB07 | Mandatory backend gate through three real CanDoItAll app instances. | `LOCKED` |
| SB08 | Central publication and client source/import desktop UI. | `LOCKED` |
| SB09 | Component, focused Playwright, screenshot, accessibility, and failure-state proof. | `LOCKED` |
| SB10 | Repeatable operator tooling, Compose documentation, and manual handoff workflow. | `LOCKED` |
| SB11 | OpenAPI freeze/export and SharedInfo shared-provider API skill. | `LOCKED` |
| SB12 | Final architecture/stable gate, clean multi-instance proof, running stack, and closure. | `LOCKED` |

Each subbundle contains its own README, session handoff, test-selection record, proof manifest,
and artifact directories. Downstream work remains locked when an owning gate fails.
