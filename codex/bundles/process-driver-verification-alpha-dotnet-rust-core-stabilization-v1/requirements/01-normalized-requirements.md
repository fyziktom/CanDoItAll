# Normalized Requirements

| ID | Requirement | Acceptance signal |
| --- | --- | --- |
| REQ-001 | Verify latest Codex output | Recheck completed status, proof quality, source changes, tests, scans, and decision docs from the latest bundle. |
| REQ-002 | Preserve Core stability | Keep CanDoItAll.Processes.Core dependency-clean, deterministic, and public-API governed. |
| REQ-003 | Preserve driver contract boundary | Keep CanDoItAll.Processes.Drivers.Abstractions contract-only; no runtime registry/selector/DI/manager command. |
| REQ-004 | Implement first verification-only alpha carefully | Introduce a .NET/Rust transcript verifier only as a read-only driver implementation with no command execution and no state mutation. |
| REQ-005 | Enforce permission and capability modes | VerificationOnly and ManagerReadonly must deny mutation, command execution, Graph/Office calls, workspace/storage writes, transitions, claims, finalizers, and retries. |
| REQ-006 | Audit and redaction proof | Every verification response must include audit facts, evidence references, redaction descriptor, and no-mutation proof. |
| REQ-007 | Domain lane roadmap | Prepare .NET/Rust first, then business-analysis, then Office, and keep execution-capable drivers deferred behind sandbox/allowlist gates. |
| REQ-008 | Broader but coherent phases | Use fewer broader subbundles spanning several meaningful areas rather than many micro steps. |
| REQ-009 | Validation rigor | Build, full unit, focused integration, source scans, anti-stub, no UI/media, prepared/completed validators, and red-team review must pass. |
| REQ-010 | No functionality loss | Existing process dispatch, Core descriptor consumers, and verification contract tests must remain green. |
