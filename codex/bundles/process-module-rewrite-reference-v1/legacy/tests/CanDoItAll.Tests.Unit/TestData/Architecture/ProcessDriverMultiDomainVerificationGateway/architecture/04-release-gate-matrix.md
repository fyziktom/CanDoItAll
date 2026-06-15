# Release Gate Matrix

| Area | Gate |
| --- | --- |
| Process Core | No references to driver abstractions, modules, infrastructure, storage, workspace, EF, UI, runtime services. |
| Driver Abstractions | Contract-only; no registry, host, selector, provider runtime, DI extension, manager command. |
| Transcript Verifier | Supplied transcript only; no command execution; audit/redaction/no-mutation proof. |
| Runtime Evidence Verifier | Supplied Core descriptors only; no lifecycle mutation; contradiction diagnostics only. |
| Office Verifier | Supplied evidence only; Graph/email/task/document mutations denied. |
| Business Verifier | Supplied evidence only; CRM/business-record mutation denied. |
| Gateway | Explicit allow-list only; no runtime discovery, registration, manager/scheduler/workflow hook. |
| Evidence Policy | Approved URI schemes, SHA-256 hashes, size limits, no arbitrary file/network reads. |
| Tests | Build, full unit, focused unit/integration, source scans, anti-stub, semantic adequacy, completed validator. |
