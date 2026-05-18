# Requirement Traceability

| Requirement | Primary subbundle | Proof |
|---|---|---|
| H-FR-001 | `01-source-paging-and-cursor-contracts` | Provider tests or query review showing bounded page retrieval. |
| H-FR-002 | `01-source-paging-and-cursor-contracts` | Cursor contract tests for source kind, scope, version, and anchor. |
| H-FR-003 | `01-source-paging-and-cursor-contracts` | Invalid/stale cursor tests that do not restart silently. |
| H-FR-004 | `02-redaction-and-hash-policy` | Workbench snapshot tests for notes, metadata, sensitivity, and access mode. |
| H-FR-005 | `02-redaction-and-hash-policy` | Process/workflow hash classification tests and source review. |
| H-FR-006 | `03-maf-context-trace-capture` | Context contributor trace tests for provided, skipped, and failed outcomes. |
| H-FR-007 | `04-validation-and-architecture-gate-sync` | Cognitive Memory architecture execution report and gate notes updated. |
| H-NFR-001 | All | No Cognitive Memory implementation added. |
| H-NFR-002 | All | Typed cursor/hash/trace policy objects in source review. |
| H-NFR-003 | All | Existing targeted tests continue passing. |
| H-NFR-004 | `01-source-paging-and-cursor-contracts` | Large-page or query-backed paging proof. |
| H-NFR-005 | `02-redaction-and-hash-policy`, `03-maf-context-trace-capture` | Audit metadata tests and source review. |
