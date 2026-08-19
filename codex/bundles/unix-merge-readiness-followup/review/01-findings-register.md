# Findings register

| ID | Priority | Status | Area | Disposition before merge |
|---|---:|---|---|---|
| MR-P0-001 | P0 | Closed locally | Process persistence | Hash-version compatibility and fail-closed legacy migration pass on Windows/Linux |
| MR-P0-002 | P0 | Closed locally | FileTools/build provenance | Package default and explicit clean source provenance are enforced |
| MR-P0-003 | P0 | Closed locally | Process lifecycle | Complete owned-tree termination passes actual Windows/Linux tests |
| MR-P1-004 | P1 | Closed locally | MCP JSON-RPC | Peer ping/server requests and bounded input pass actual hosts |
| MR-P1-005 | P1 | Closed locally | Docker recipes | Strict parsing, budgets, and bounded selectors pass |
| MR-P1-006 | P1 | Closed locally | Workspace paths | Central symlink/reparse-safe resolution passes |
| MR-P1-007 | P1 | Closed locally | Executable resolution | Effective-user executable and PATHEXT contracts pass |
| MR-P1-008 | P1 | Closed locally | Docker/CI contract | Disposable secret and isolated Compose lifecycle pass |
| MR-P1-009 | P1 | Closed locally | Validation tooling | Durable stamp and exact FQN catalog pass both hosts |
| MR-P1-010 | P1 | Closed locally | Canonical evidence | Inventories, reports, redaction, and checksums are reconciled |
| MR-P1-011 | P1 | Closed locally | Repository hygiene | Tracked local artifact removed and recurrence rejected |
| MR-P2-012 | P2 | Closed locally | DB secret file | Size/type/UTF-8 content policy passes |
| MR-P2-013 | P2 | Closed locally | Process diagnostics | Identity mismatch diagnostics are race/permission safe |
| MR-P2-014 | P2 | Closed locally | Unix runtime temp | Per-runtime owned temporary roots pass headless cycles |
| MR-D-015 | Deferred | Accepted alpha deferral | Enterprise vaults | Complete Azure/HashiCorp adapters during beta hardening |
| MR-D-016 | Deferred | ActualHostUnverified | macOS Keychain | Actual-host session validation and native API modernization later |
| MR-D-017 | Deferred | Accepted alpha deferral | CI supply chain | Pin actions by SHA according to repository policy before protected release gate |
