# Normalized Requirements

| ID | Requirement | Owning phase |
| --- | --- | --- |
| REQ-001 | Re-read current branch and reject report-only closure. | P01 |
| REQ-002 | Preserve clean build and full unit proof; no reintroduced skipped architecture debt. | P01 |
| REQ-003 | Keep Core driver-free and dependency-clean. | P02 |
| REQ-004 | Split process domain adapters into lane-specific files without changing behavior. | P03 |
| REQ-005 | Split payload builder into lane-specific builders and shared identity/evidence helpers. | P04 |
| REQ-006 | Harden batch orchestrator while preserving typed lanes and no generic dispatch. | P05 |
| REQ-007 | Stabilize explicit gateway for all current lanes; no registry/selector/DI/runtime host. | P06 |
| REQ-008 | Consolidate evidence policy and content-boundary checks across all lanes. | P07 |
| REQ-009 | Consolidate audit/redaction/no-mutation semantics across all lanes. | P08 |
| REQ-010 | Add manager-visible read-only projection planning over verification observations without UI/persistence. | P09 |
| REQ-011 | Harden observation aggregation and read-only snapshot behavior. | P10 |
| REQ-012 | Harden transcript/runtime/artifact/Office/business corpus and fake-proof tests. | P11 |
| REQ-013 | Govern process-module driver package references and allow-listed consumers. | P12 |
| REQ-014 | Maintain Core/API/driver contract version governance. | P13 |
| REQ-015 | Improve docs and samples without implying runtime host approval. | P14 |
| REQ-016 | Prepare runtime-host prerequisite backlog without implementation. | P15 |
| REQ-017 | Add release-candidate smoke matrix and red-team proof. | P16-P18 |
