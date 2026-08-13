# Merge requirements

| ID | Requirement | Acceptance |
|---|---|---|
| PMR-001 | Legacy plan algorithm classification is deterministic and independent of wall-clock time. | A post-cut-off V1 fixture is accepted as verified legacy and returned non-executable with `NeedsRecompile`. |
| PMR-002 | V2 plans remain hash-stable. | Existing V2 fixtures retain their exact hash; no broad rehash occurs. |
| PMR-003 | Partial V2 payloads fail closed. | Any incomplete seal shape becomes `Unknown` and cannot execute. |
| PMR-004 | Already-applied feature-branch databases receive corrective classification. | An idempotent correction migration updates eligible `Unknown` rows without weakening ambiguous rows. |
| PLO-001 | Start failure is transactional. | An injected attach failure leaves no live root or child process and no leaked native handle. |
| PLO-002 | OS ownership remains authoritative. | Normal Windows Job Object and Unix process-group tests remain green. |
| MGR-001 | Legacy registry payloads never authorize termination. | Schema-1 fixture without boundary becomes `OwnershipUnverified`; fake host termination count remains zero. |
| MGR-002 | Current registry validates boundaries. | Invalid kind/native-id/instance-id combinations are rejected before recovery. |
| OPS-001 | Linux container exposes the bootstrap dependency. | Image build and `command -v setsid` probe pass, preferably with explicit `util-linux`. |
| MAF-001 | MAF 1.17 approval continuation remains request-scoped and durable. | Named approval/session round-trip tests pass on the final build. |
| MAF-002 | Generic agent source authority remains bounded. | Named authority tests pass; no workspace scope is inferred from the generic agents source. |
| GATE-001 | Final evidence binds the exact candidate. | Commit, source fingerprint, dependency mode and assembly hashes are retained. |
| GATE-002 | Final decision follows the current operator policy. | macOS absence is recorded as deferred, not as an automatic NO-GO. |
