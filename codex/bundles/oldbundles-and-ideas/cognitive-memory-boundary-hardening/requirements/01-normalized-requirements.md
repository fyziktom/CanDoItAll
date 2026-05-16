# Normalized Requirements

## Functional Requirements

### H-FR-001: Query-Backed Provider Paging

Source providers shall avoid materializing entire source sets before returning a requested page whenever the source can be ordered and paged through the database.

### H-FR-002: Anchored Cursor Contract

Source snapshot cursors shall carry enough information to detect stale, invalid, mismatched, or unsupported cursor use.

### H-FR-003: Explicit Cursor Failure

Invalid or stale cursors shall not silently restart from the beginning. The provider shall return or throw a typed outcome that callers can trace and handle predictably.

### H-FR-004: Workbench Redaction Metadata

Workbench source items shall carry accurate sensitivity/access metadata for notes, metadata JSON, storage locators, and future context/projection usage.

### H-FR-005: Restricted Hash Semantics

Hashes derived from raw sensitive payloads shall be explicitly marked as restricted integrity data or replaced with a safe non-exportable hash strategy.

### H-FR-006: Context Contribution Trace Capture

MAF context contribution shall retain contributor id, status, trace metadata, failure state, and generated message count for future Cognitive Memory audit.

### H-FR-007: Cognitive Memory Gate Sync

The Cognitive Memory architecture bundle shall identify this hardening bundle as a prerequisite before source ingestion, recall, and MAF integration implementation.

## Non-Functional Requirements

### H-NFR-001: Minimal Scope

The implementation shall harden boundaries only and shall not implement Cognitive Memory features.

### H-NFR-002: Strong Typing

Cursor status, hash classification, redaction/access mode, and context trace outcomes shall use typed values or records instead of magic strings.

### H-NFR-003: Backward Compatibility

Existing Workbench, Process, Workflow, and MAF behavior shall remain compatible unless a contract change is explicitly required and tested.

### H-NFR-004: Scale Safety

Provider APIs shall be safe to call repeatedly by background ingestion jobs over large source sets.

### H-NFR-005: Auditability

Every hardened path shall produce enough state for a future Cognitive Memory trace to explain source-page movement, redaction decisions, hash class, and context injection.
