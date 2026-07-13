# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Generic memory provider module | `requirements/01-normalized-requirements.md#r01` | SB01-SB10 | Contract tests, runtime tests, mock provider tests | Critical foundation. |
| Multiple providers with selection per agent/workflow | `requirements/01-normalized-requirements.md#r02` and `requirements/01-normalized-requirements.md#r23` | Historical SB02 and SB16-SB18; repair SB36-SB37 | Typed settings persistence, alias validation, two-provider routing/fan-out tests | Historical selection support is insufficient: one agent must bind many providers and selection must not depend on registry order. |
| Structured memory protocol | `architecture/02-protocol-contract-model.md` | SB01 | Schema and envelope tests | Supports simple and eventful providers. |
| Source Gateway ingestion | `architecture/04-runtime-operations-and-feedback.md` | SB04, SB11-SB14 | Source adapter contract tests | Prevents AppDbContext leakage. |
| Feedback correlation and delayed outcomes | `architecture/04-runtime-operations-and-feedback.md` | SB03, SB21, SB33 | Feedback lifecycle tests | Includes optional IPFS snapshot metadata. |
| MAF generic tool and workflow executor | `requirements/01-normalized-requirements.md#r09` | SB15-SB19 | Tool/executor shared handler tests | No duplicate operation logic. |
| Native service and own DB | `architecture/06-native-service-extraction.md` | Historical SB24-SB29; repair SB39 | Native DB, access-policy, hosted API, isolation, and main-driver conformance tests | The target repo is now implemented; its security and protocol claims require current proof. |
| Remove Qdrant/base native memory dependency | `requirements/01-normalized-requirements.md#r17` | SB30, SB33 | Startup and dependency guard tests | Base app should require PostgreSQL plus app only. |
| UI common plus provider-specific surfaces | `architecture/05-ui-composition.md` | SB20-SB23 | Browser/component tests | RCL and iframe supported. |
| Checkpoint refactoring gates | `plan/02-checkpoints.md` and `plan/architecture-checkpoints.md` | Historical SB05, SB10, SB14, SB19, SB23, SB29, SB34; repair SB35, SB40 | Architecture records, proof manifests, dependency/partial guards, and independent gate review | SB35 blocks production repair; SB40 blocks renewed closure. |
| Live re-entry MAF alignment | `analysis/04-live-repo-reentry-alignment.md` | Historical SB04, SB11-SB19, SB24, SB30, SB33; repair SB35-SB40 | Source audits, baseline characterization, MAF integration tests, and current validation | Both implementations exist as of 2026-07-12; historical closure labels are evidence inputs, not current acceptance. |
| Zero-provider operation | `requirements/01-normalized-requirements.md#r02` | Historical SB02, SB06, SB15-SB18, SB20, SB30, SB33; repair SB36-SB37, SB40 | Startup, UI, MAF, selection-policy, and operation-handler tests | Must not default silently to native, OpenAI, Qdrant, mock, or registry-first providers. |
| Typed invocation modes | `requirements/01-normalized-requirements.md#r22` | SB37, SB40 | Settings codec/UI tests plus disabled, automatic, and explicit-mode runtime tests | Absence of a directive in explicit mode must not query memory. |
| Typed provider bindings and aliases | `requirements/01-normalized-requirements.md#r23` | SB36, SB37, SB40 | Round-trip, uniqueness, authorization, deterministic fan-out, and labelled merge tests | A single preferred-provider string is not sufficient. |
| Explicit memory directive | `requirements/01-normalized-requirements.md#r24` | SB37, SB40 | Parser/sanitizer tests and real agent invocation proof | `/mem:<alias>` must be stripped and authorized before provider/model dispatch. |
| Fail-closed selection and operation ownership | `requirements/01-normalized-requirements.md#r25` | SB36, SB40 | Deny-fallback, ambiguity, cross-agent/session status, and cancellation tests | Provider registry order and possession of an operation GUID grant no authority. |
| Typed execution and project context | `requirements/01-normalized-requirements.md#r26` | SB37-SB40 | Runtime-to-envelope integration tests and project-isolated external recall | Required identity cannot live only in optional tags. |
| Modular C# ownership and no capability-grouping partials | `requirements/01-normalized-requirements.md#r27` | SB35-SB38, SB40 | Inventory, dependency graph, CodeAnalytics, architecture guards, isolated tests, and independent review | The architecture gate must pass before production edits. |
| Safe and truthful transport configuration | `requirements/01-normalized-requirements.md#r28` | SB38, SB40 | Profile preservation/secret scans, driver DI tests, and capability-manifest conformance | Unsupported behavior is omitted or explicitly rejected, never advertised optimistically. |
| External Cognitive Memory security and conformance | `requirements/01-normalized-requirements.md#r29` | SB39, SB40 | Authentication/authorization, access-policy, isolation, limits, manifest, and hosted main-driver tests | In-process engine tests alone do not prove the external provider seam. |

## Current Repair Closure State

| Requirement | Implementation owner result | SB40 terminal result |
| --- | --- | --- |
| R22 invocation modes | SB37 completed typed Disabled/Automatic/ExplicitDirective settings and focused routing proof. | Passed real-host editor/browser and real contributor-handler-driver-ledger proof. |
| R23 bindings and aliases | SB36-SB37 completed allowlisted ordered bindings, aliases, required/optional policy, and bounded stable-order fan-out. | Passed two-provider UI/runtime/ledger correlation and full affected suites. |
| R24 explicit directive | SB37 completed leading directive parsing, authorization, prompt removal, and unknown-alias rejection. | Passed explicit single-provider sanitization and unknown-alias zero-dispatch proof. |
| R25 selection and ownership | SB36 completed fail-closed selection and exact operation-owner authorization. | Passed 196-test Memory aggregate plus terminal negative/red-team matrix. |
| R26 typed context | SB37-SB39 completed typed runtime context, transport mapping, and external claim/project enforcement. | Passed live main-driver/external process trace. |
| R27 modular ownership | SB35-SB39 completed the documented project/type extractions and prohibited-partial removal. | Passed CodeAnalytics, 88-project cycle scan, 227-file partial/size scan, and independent red team. |
| R28 safe transport configuration | SB38 completed strict HTTP/MCP configuration, credential references, lossless editor mapping, supported capability restrictions, and worker hosting. | Passed provider-editor browser proof, response caps, and PostgreSQL distributed lease disposition. |
| R29 external security/conformance | SB39 completed authentication, authorization, access policy, project isolation, Protocol v1 ownership, and isolated 59/59 tests. | Passed actual main NativeRemote driver against the launched external service. |

The architecture-repair input is `Solved` for the requested provider/agent/runtime/security scope. Explicit non-advertised future capabilities and operational follow-ups remain in `bundle://proof/SB40/manifest.md`.
