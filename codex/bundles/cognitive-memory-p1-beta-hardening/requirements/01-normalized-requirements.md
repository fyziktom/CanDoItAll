# Normalized Requirements

| Id | Requirement | Validation |
| --- | --- | --- |
| CM-P1-001 | Provide a stable v1 Cognitive Memory API contract surface and examples while preserving existing legacy routes. | API/unit tests or source inspection plus docs. |
| CM-P1-002 | Add deterministic provider/projection failure proof and an executable live-provider/Qdrant runbook. | Unit/integration tests and operations docs. |
| CM-P1-003 | Add explicit retention cleanup policy and operator/API execution with dry-run semantics. | Unit/integration tests proving counts and deletion behavior. |
| CM-P1-004 | Expose mutation command, claim/evidence, and projection failure audit signals to operators. | Review UI service/component/browser proof. |
| CM-P1-005 | Harden external source ingestion limits, extraction errors, and sensitive-content policy. | Unit tests and API/service behavior proof. |
| CM-P1-006 | Add performance baseline guidance for large manifests and recall runs. | Docs and any lightweight deterministic test/proof available locally. |
| CM-P1-007 | Update Cognitive Memory docs, stage assessment, mermaid diagrams, roadmap, and bundle closure to the real post-P1 state. | Docs diff, validation commands, completed bundle validator. |
