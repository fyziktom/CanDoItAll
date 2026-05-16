# Normalized Requirements

## Functional Requirements

| Id | Requirement | Observable Success |
|---|---|---|
| PR-001 | Preserve the completed CanDoItAll source and MAF boundary hardening as the accepted prerequisite for source ingestion. | Bundle analysis records the passed tests and does not ask the implementation agent to redo source snapshot work. |
| PR-002 | Add provider-neutral typed filter contracts to the RAG driver. | `RagSearchRequest` can carry a typed filter tree; tests cover equality, set membership, range, existence, boolean composition, validation failures, and Qdrant translation where applicable. |
| PR-003 | Add payload index contracts to the RAG driver without making them Cognitive Memory-specific. | RAG exposes index request/result/status contracts or equivalent provider-neutral APIs; Qdrant mapper/driver tests cover index creation calls or explicit unsupported capability behavior. |
| PR-004 | Add projection lifecycle cleanup operations to RAG. | The driver supports delete by filter/source-equivalent payload criteria or an explicitly generic delete-by-filter request; tests prove stale projections can be removed without enumerating every point id. |
| PR-005 | Extend RAG driver capabilities so callers can discover filter, payload index, delete-by-filter, and optional named-vector support. | Capabilities are strongly typed and tests prove unsupported operations fail predictably rather than silently ignoring filters. |
| PR-006 | Add stable embedding profile metadata to SemanticCompletion embedding results. | Embedding results expose provider/model/profile/dimension/normalization information; tests prove local hashing and ONNX paths produce deterministic metadata. |
| PR-007 | Keep Cognitive Memory semantics out of generic repos. | No RAG or SemanticCompletion model names mention Cognitive Memory, memory kinds, source manifests, or project-specific policy. |
| PR-008 | Synchronize the Cognitive Memory architecture bundle after implementation. | `cognitive-memory-architecture` records this follow-up as closed before projection-backed recall, RAG adapters, or strict-mode vector context can start. |

## Nonfunctional Requirements

| Id | Requirement | Observable Success |
|---|---|---|
| PNFR-001 | All new public contracts must be strongly typed. | No new ad hoc string expression language for filters or lifecycle operations. |
| PNFR-002 | Existing consumers must remain compatible where practical. | Samples/tests compile after additive API updates; breaking changes are explicitly justified in execution notes. |
| PNFR-003 | Failure behavior must be explicit. | Unsupported filters/indexes/deletes throw predictable exceptions or report unsupported capability; tests cover this path. |
| PNFR-004 | Projection behavior must be rebuildable and auditable. | Tests and architecture notes show projection cleanup and embedding profile metadata can drive rebuild decisions. |
| PNFR-005 | No browser proof is required. | Execution report records N/A browser analytics with rationale. |

## Scope Boundaries

- This bundle does not implement Cognitive Memory.
- This bundle does not redesign Qdrant collection strategy beyond generic RAG driver features.
- This bundle does not require live Qdrant for closure if mapper and contract tests are complete.
- This bundle does not change Workbench, Process, Workflow, or MAF source boundaries except architecture documentation sync.
