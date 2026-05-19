# Normalized Requirements

## Contract Preservation

- FR-FU-001: Preserve original cognitive-memory v2 invariants: raw provenance as truth, projection rebuildability, review-gated canonical mutations, and no direct probe-to-truth writes.
- FR-FU-002: Preserve existing public API behavior unless a deliberate route or contract change is documented and tested.
- FR-FU-003: Keep PostgreSQL-first validation and include SQLite/provider checks only where the current test suite already expects them.

## LB4U Staged Ingestion

- FR-FU-010: Define a typed ingestion manifest for LB4U stages, source files, excluded files, asset-node files, and expected memory categories.
- FR-FU-011: Support read-only ingestion of LB4U document, presentation, spreadsheet, and asset references without modifying the source folder.
- FR-FU-012: Exclude `routery hesla` and prove it does not enter memory, prompts, logs, recall, or asset nodes.
- FR-FU-013: Extract useful chunks from docx, pdf, pptx, and xlsx sources with source identity and section/table/page references.
- FR-FU-014: Feed LB4U knowledge in stages with consolidation and probing between stages.

## Consolidation And Knowledge Quality

- FR-FU-020: Improve consolidation so candidates contain source-backed project facts, decisions, procedures, risks, assumptions, and reusable planning knowledge instead of shallow classification payloads.
- FR-FU-021: Keep generated candidate payloads explicitly tied to raw source item ids and source spans.
- FR-FU-022: Require review decisions for canonical truth changes produced by model-assisted consolidation.
- FR-FU-023: Detect and surface weak, unsupported, or over-generalized candidate knowledge.
- FR-FU-024: Add coverage and gap analysis for business-plan, marketing, staffing, expenses, procurement, release planning, and technical architecture dimensions.

## Probing And Human Feedback

- FR-FU-030: Probe sessions must record prompts, answer summaries, recall context sources, feedback decisions, and follow-up actions.
- FR-FU-031: Probe answers must distinguish LB4U-specific facts from inferred reusable knowledge.
- FR-FU-032: The system must improve after a user asks it to study a missed stage more deeply, provided the missing source exists.
- FR-FU-033: Accepted recommendations must be traceable; rejected recommendations must retain rejection reason.

## Model Provider Validation

- FR-FU-040: Main validation must run with OpenAI `gpt-5-mini`.
- FR-FU-041: Local validation must run with Ollama `gptoss20b64k`.
- FR-FU-042: Cognitive memory must expose or record model id, provider profile, max output tokens, timeout, and truncation state for model-assisted operations.
- FR-FU-043: Silent fallback from OpenAI to local, local to OpenAI, or model-assisted to deterministic-only behavior is not allowed.
- FR-FU-044: Ollama output token limits must be explicitly configured or detected and must be visible in validation proof.

## Maintainability Refactor

- FR-FU-050: Split oversized cognitive-memory files around stable responsibilities after behavioral tests exist.
- FR-FU-051: Isolate shared helpers for source classification, staged ingestion, chunking, model profile selection, token/truncation metadata, review validation, score composition, and API response validation where doing so reduces duplication or improves testability.
- FR-FU-052: Keep Blazor UI logic predictable by extracting focused components or services from the large cognitive memory page.
- FR-FU-053: Do not introduce trivial one-implementation interfaces unless they protect a real boundary or enable tests.

## API, Skill, Docs, And Workbook

- FR-FU-060: Update cognitive memory API docs and the `candoitall-api-cognitive-memory` skill when endpoints, request bodies, settings, or validation workflows change.
- FR-FU-061: Maintain an `.xlsx` workbook with phases, requirements, source references, checklists, probes, validation matrix, risks, and evidence log.
- FR-FU-062: Keep bundle reviews and execution reports current after every subbundle.

## Non-Functional Requirements

- NFR-FU-001: All code must remain strongly typed and avoid stringly typed identifiers where project-local types can be used.
- NFR-FU-002: Logs must include actionable state and must mask sensitive data.
- NFR-FU-003: No silent truncation, fallback, or ignored provider failure is acceptable.
- NFR-FU-004: Tests must cover behavior before and after refactors.
- NFR-FU-005: Refactors must be incremental and reversible by subbundle, not a broad rewrite.
