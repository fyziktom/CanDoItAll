# Gap Analysis And Refactor Opportunities

## Gaps

| Gap | Evidence | Required Direction |
| --- | --- | --- |
| Staged realistic ingestion is not proven | Tests are broad but synthetic; LB4U data was not part of the original closure. | Add a staged ingestion harness using LB4U files and deterministic manifests. |
| Semantic consolidation may be too shallow | Candidate payload examples summarize classification, not extracted project meaning. | Add model-assisted extraction/chunking/candidate generation with strict provenance and review gates. |
| Epistemic drive may not discover reusable planning knowledge | Current scan behavior appears driven by answer-gate gaps and calibration aggregates. | Extend scans to source/canonical coverage, repeated patterns, missing business-plan dimensions, and cross-project candidates. |
| Provider/model policy is not memory-specific enough | Settings expose provider profiles and agents, but not an explicit cognitive-memory model execution profile with output token checks. | Add typed model settings for consolidation/probing/review roles, model ids, max output tokens, timeout, truncation handling, and local/OpenAI validation. |
| Secret exclusion needs first-class proof | LB4U has a likely password file. | Add ingestion manifest exclusions and automated absence checks. |
| API route file is too large | `CognitiveMemoryApi.cs` is roughly 1476 lines. | Split route groups or endpoint mappers while preserving route contracts. |
| Recall and advanced services are too large | Recall and advanced service files exceed normal maintainability bounds. | Split by behavior without changing public contracts first. |
| Page component is too large | Page markup and code-behind are both large. | Extract focused wrapper components using existing component patterns. |

## Shared Helper Candidates

- Source artifact classification and exclusion rules.
- Ingestion manifest parsing and validation.
- Text chunking and section extraction.
- Strongly typed source-stage identifiers.
- Review decision validation.
- Provider/model execution profile selection.
- Token budget and truncation result metadata.
- Recall context source summaries.
- Score component composition.
- Endpoint request validation and error responses.

## Refactor Guardrails

- Refactor after tests and behavioral harness exist.
- Keep persistence schema changes explicit and covered by migration/model tests.
- Do not split files by mechanical class count alone; split around stable behaviors and policy boundaries.
- Avoid new interfaces unless they protect a real boundary or enable tests.
- Do not hide provider failures behind local fallback behavior.
