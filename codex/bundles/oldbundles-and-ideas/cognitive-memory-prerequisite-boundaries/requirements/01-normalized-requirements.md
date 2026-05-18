# Normalized Requirements

## Functional Requirements

### PR-FR-001: MAF Context Contribution Boundary

The system shall allow modules to register agent context contributors without adding feature-specific logic to private MAF context-provider construction.

### PR-FR-002: Contributor Ordering And Policy Context

The MAF boundary shall support deterministic ordering, enable/disable decisions, policy context, cancellation, and trace metadata for each contributor.

### PR-FR-003: Workbench Source Snapshot Contract

The system shall expose Workbench project structure as read-only source snapshots with deterministic source item ids, content hashes, layout metadata, links, references, timestamps, and provenance.

### PR-FR-004: Process Runtime Source Contract

The system shall expose process runs, step runs, decisions, artifacts, journals, conformance observations, and improvement candidates as read-only memory source items or events.

### PR-FR-005: Workflow Runtime Source Contract

The system shall expose workflow runs, events, artifacts, external requests, executor metadata, and run-store evidence as read-only memory source items or events.

### PR-FR-006: Existing Behavior Compatibility

The refactor shall preserve existing runtime behavior and keep the current workspace memory provider as compatibility fallback.

### PR-FR-007: Cognitive Memory Projection Point

The Cognitive Memory architecture shall consume these prerequisite contracts from `00-prerequisite-boundary-gate`, `02-workbench-and-source-ingestion`, `06-consolidation-engine`, and `07-maf-workflow-integration`.

## Non-Functional Requirements

### PR-NFR-001: Minimal Scope

The prerequisite shall add boundaries only; it shall not implement Cognitive Memory.

### PR-NFR-002: Strong Typing

Source kinds, contributor ids, snapshot ids, cursor values, policy states, and result statuses shall use strongly typed values or explicit records, not magic strings.

### PR-NFR-003: Source Truth

Snapshot contracts shall preserve source ownership and never make generated summaries authoritative.

### PR-NFR-004: Testability

Contracts shall be testable with fake contributors and fake source snapshot providers.

### PR-NFR-005: Dependency Direction

Existing modules may implement source/contributor contracts, but they shall not depend on Cognitive Memory durable models.
