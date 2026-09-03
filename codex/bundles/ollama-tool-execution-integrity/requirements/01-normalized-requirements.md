# Normalized Requirements

## Outcome contract

A provider response ending is not proof that an attempted project mutation succeeded. The runtime must carry trusted, scoped tool outcomes through binding, execution, persistence, public receipts, continuation, and UI refresh. Direct and shared endpoints must have the same application behavior while retaining their own protocol adapters.

| ID | Required behavior | Owner | Acceptance |
|---|---|---|---|
| R01 | Reject missing, wrongly shaped, or invalid typed arguments before the delegate; return bounded, safe field-level correction guidance. | SB01 | The captured project_id payload produces a missing projectId/request diagnostic, zero mutation calls, and a model-visible failure result correlated to the call. |
| R02 | Normalize explicit tool success/failure/unknown and effect state; unknown cannot prove mutation success. Preserve existing valid read results. | SB01 | Typed workspace, MCP, domain, generic SDK failure, and unknown fixtures pass; no raw exception or secret escapes. |
| R03 | Interactive completion uses unresolved failed/unknown mutation evidence. A later assistant promise alone cannot make the operation successful. | SB02 | Failure plus prose is Failed; a verified same-operation correction can resolve a pre-execution failure; unrelated success cannot. |
| R04 | Persist and expose bounded typed outcome/error/effect fields. Historical missing fields stay Unknown without rewriting old records. | SB02 | Real HTTP receipt and persistence round trip match the trusted outcome; raw arguments and exception details stay private. |
| R05 | Preserve relevant prior tool evidence across turns independently of provider serialization. | SB03 | The retry turn sees the preceding failure alongside prior prose, constrained by current session, agent, authorization, and project context. |
| R06 | Direct Ollama and shared OpenAI-compatible relay preserve equivalent schemas, calls, result correlation, errors, and supported streaming behavior. | SB04 | SDK plus real shared relay tests and SB06 live runs prove parity; unsupported capabilities fail explicitly before execution. |
| R07 | Asset registration uses existing authorized managed storage and returns reliable commit/readback evidence. Telemetry failure cannot erase knowledge of an earlier commit. | SB05 | Managed content, parent, node identity, and canonical graph agree; post-commit analytics/cancellation cannot trigger blind mutation retry. |
| R08 | Refresh the matching open project for committed effects even when a later operation fails or is cancelled. | SB05 | The canonical node appears without manual refresh; other projects, disposed contexts, and duplicate events do not cause incorrect refresh. |
| R09 | Demonstrate the whole agent path using deterministic malformed/corrected provider output and live direct/shared Ollama runs. | SB06 | Actual agent calls, public receipts, canonical state, files, and reviewed desktop screenshots agree. |
| R10 | Preserve existing C# project direction and separate SDK adaptation, application outcome policy, domain storage, and UI rendering. | SB01–SB06 | Architecture checkpoints and focused validation pass; no new god service, partial-file split, or provider switch in agent business policy. |
| R11 | Preparation only: preserve evidence, stop inspected 5032 host, create a resumable implementation-ready bundle. | Preparation | Sanitized artifacts and stop evidence exist; production source and original project data are unchanged. |
| R12 | Upgrade the coherent MAF family to 1.20 with required MEAI/A2A/Microsoft.Extensions alignment, while preserving OpenAI 2.12 compatibility. | SB00 | Resolved graph, production build, focused SDK consumers and portability gate pass without downgrade suppression. |
| R13 | Keep ordinary agent outcome policy distinct from workflow terminal mapping and prove both after upgrade. | SB00, SB02 | Hard workflow errors/cancellation remain non-success; ordinary failed mutations remain application-assessed even when MAF returns conversational text. |

## Explicit limits

This bundle makes reported tool outcomes and attempted mutations truthful. It cannot prove arbitrary natural-language task satisfaction when no relevant tool was called. Do not implement a prose keyword classifier or force every interactive answer through a structured finalizer.

Retain workspace/asset separation. A workspace file alone is not a graph node, and a readback must use the canonical structure and managed asset APIs. No mutation or live-model re-execution belongs to preparation.
