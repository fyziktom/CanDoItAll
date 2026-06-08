# Normalized Requirements

| ID | Name | Requirement |
| --- | --- | --- |
| REQ-001 | Crash/source reconciliation | Re-read current branch source and latest proof; do not trust report-only proof after Codex crash. |
| REQ-002 | Unit debt cleanup | Classify and fix or isolate full-unit debt, including stale architecture fixture paths and file-lock test failure. |
| REQ-003 | Core stability | Keep Process Core deterministic, dependency-clean, public API governed, and independent of driver abstractions. |
| REQ-004 | Driver abstraction stability | Keep driver abstractions contract-only and version-governed. |
| REQ-005 | Transcript verifier decomposition | Keep transcript verifier behavior but split parser/policy/audit/redaction responsibilities. |
| REQ-006 | Runtime evidence verifier hardening | Harden runtime evidence verifier and contradiction matrix without side effects. |
| REQ-007 | Controlled verification gateway | Add an allow-listed verification-only gateway/consumer boundary, not a generic runtime registry or selector. |
| REQ-008 | Evidence content boundary | Only supplied evidence/transcript/descriptors may be inspected; no arbitrary file/network/workspace reads. |
| REQ-009 | Audit/redaction/no-mutation | Every verification result must carry audit facts, redaction descriptor and no-mutation proof. |
| REQ-010 | Additional read-only domain lanes | Prepare Office and business-analysis read-only alpha verifiers over supplied evidence only. |
| REQ-011 | Process artifact evidence verifier | Add read-only artifact/projection/validation descriptor verifier over supplied Core descriptors. |
| REQ-012 | Shared verification test harness | Add reusable tests for permission, denial, redaction, hash, no-mutation and runtime-token bans. |
| REQ-013 | Release gates | Keep broad smoke build/unit/focused/source scans and artifact-backed semantic proof manifests. |
| REQ-014 | No UI/mobile proof drift | Runtime-only work must not add browser/small/medium/mobile screenshot proof. |
| REQ-015 | Roadmap | Produce a clear roadmap toward stable Core and domain drivers without prematurely approving execution-capable runtime. |
