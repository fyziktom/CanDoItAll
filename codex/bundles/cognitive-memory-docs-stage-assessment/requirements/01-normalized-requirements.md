# Normalized Requirements

| ID | Requirement | Observable success criteria | Owning subbundle |
| --- | --- | --- | --- |
| CMR-DOC-001 | Preserve a bundle-backed workflow for the request. | Raw request, structured input, requirements, phase plan, traceability, subbundle READMEs, and execution report are populated. | `01-current-implementation-audit-and-stage-truth` |
| CMR-DOC-002 | Audit the actual Cognitive Memory implementation before writing docs. | Current state doc references module registration, API, persistence, source providers, tests, and prior validation evidence. | `01-current-implementation-audit-and-stage-truth` |
| CMR-DOC-003 | Create a dedicated Cognitive Memory docs section with subfolders. | `docs/cognitive-memory` exists with `current-state`, `architecture`, `operations`, and `roadmap` subfolders. | `02-documentation-section-and-mermaid-diagrams` |
| CMR-DOC-004 | Document the true stage and maturity caveats. | Stage assessment names validation-grade alpha and lists done work, alpha limits, and beta blockers. | `02-documentation-section-and-mermaid-diagrams` |
| CMR-DOC-005 | Add Mermaid diagrams for the current implementation. | Docs include Mermaid `architecture-beta`, `flowchart`, `classDiagram`, and `sequenceDiagram` blocks. | `02-documentation-section-and-mermaid-diagrams` |
| CMR-DOC-006 | Add roadmap content for done work and next steps. | Roadmap lists already done work, P0/P1/P2 next work, and beta release gates. | `03-roadmap-and-closure-validation` |
| CMR-DOC-007 | Update existing docs entry points. | Root and docs README files, API docs, control-plane docs, and architecture docs point to the new section. | `03-roadmap-and-closure-validation` |
| CMR-DOC-008 | Validate documentation and bundle closure. | Bundle validator and `git diff --check` pass, or any failure is recorded with a clear residual risk. | `03-roadmap-and-closure-validation` |
