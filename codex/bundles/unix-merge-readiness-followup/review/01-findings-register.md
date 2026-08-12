# Findings register

| ID | Priority | Area | Disposition before merge |
|---|---:|---|---|
| MR-P0-001 | P0 | Process persistence | Implement hash-version compatibility and fail-closed legacy capability migration |
| MR-P0-002 | P0 | FileTools/build provenance | Make source mode explicit and verifiable; remove dirty-sibling dependency |
| MR-P0-003 | P0 | Process lifecycle | Prove complete owned-tree termination after root exit |
| MR-P1-004 | P1 | MCP JSON-RPC | Handle peer ping/server requests and bounded input |
| MR-P1-005 | P1 | Docker recipes | Strict parsing, count/length budgets, bounded log selectors |
| MR-P1-006 | P1 | Workspace paths | Central symlink/reparse-safe resolution |
| MR-P1-007 | P1 | Executable resolution | Effective-user executable check and PATHEXT hardening |
| MR-P1-008 | P1 | Docker/CI contract | Disposable test secret and clean-checkout compose validation |
| MR-P1-009 | P1 | Validation tooling | Build stamp, FQN catalog, deterministic affected-test selection |
| MR-P1-010 | P1 | Canonical evidence | Re-anchor and reconcile inventories/reports/checksums |
| MR-P1-011 | P1 | Repository hygiene | Remove tracked `.local` artifact and reject recurrence |
| MR-P2-012 | P2 | DB secret file | Bound size/type and document rotation behavior |
| MR-P2-013 | P2 | Process diagnostics | Make identity mismatch diagnostics race/permission safe |
| MR-P2-014 | P2 | Unix runtime temp | Avoid shared multi-user fallback root where applicable |
| MR-D-015 | Deferred | Enterprise vaults | Complete Azure/HashiCorp adapters during beta hardening |
| MR-D-016 | Deferred | macOS Keychain | Actual-host validation and native API modernization later |
| MR-D-017 | Deferred | CI supply chain | Pin actions by SHA according to repository policy before protected release gate |
