# Assumptions And Risks

## Assumptions
- The latest branch already has a stable `CanDoItAll.Processes.Core` seed and pure descriptor/rule families.
- `CanDoItAll.Processes.Drivers.Abstractions` is contract-only and dependency-clean.
- `CanDoItAll.Processes.Drivers.TranscriptVerification` is the first verification-only alpha package and currently has no runtime integration.
- The next safe step is not a driver registry. It is a process-module read-only consumer adapter plus evidence-content boundaries.
- UI/mobile/small/medium proof remains out of scope unless production UI files unexpectedly change; if they do, the bundle should fail and be re-scoped.

## Critical Path Risks
1. A controlled adapter accidentally becomes a generic runtime driver selector.
2. Evidence content resolution accidentally reads arbitrary workspace paths or untrusted files.
3. The verifier diagnostics are attached to process state or artifacts as writes without an explicit later approval.
4. Audit facts are emitted but not redacted or not linked to evidence hashes.
5. The .NET/Rust verifier starts executing commands instead of reading supplied transcripts.
6. Office or business-analysis lanes sneak in Graph calls, task/email mutation, document mutation, or business-record mutation.
7. Core starts referencing driver abstractions or transcript verifier packages.
8. Tests only cover happy-path transcripts and miss hash mismatch, permission denial, mutation attempt, unsupported language, empty transcript, or secret leakage.
9. The process module adapter is too broad and hides side effects behind a neutral name.
10. The next roadmap approves runtime driver registry too early.

## Validation Risks
- Build-only proof is insufficient. Every critical phase requires source scans, architecture tests, focused behavior tests, anti-stub audit, and fake-proof review.
- Shared proof is allowed only for noncritical closure. Critical gates need named transcripts and semantic invariants.
- Focused tests must include negative cases for side effects, not only successful verification.
- Runtime/service-only work must still prove no UI/media drift.

## Reopen Triggers
- Any `IProcessDriverRegistry`, `ProcessDriverRegistry`, `DriverSelector`, `DriverHost`, `DriverRuntime`, `AddProcessDrivers`, `ManagerCommand`, shell execution, Graph/Office operation, workspace/storage write, process mutation, claim mutation, transition mutation, finalizer application, or retry scheduling appears in production source.
- `CanDoItAll.Processes.Core` references driver abstractions or transcript verifier.
- The alpha verifier or process adapter reads files directly instead of receiving supplied transcript content through an explicit evidence boundary.
- Verification-only or manager-readonly mode can mutate state.
- Audit facts omit caller/mode/lane/operation/evidence hash/denial/redaction/no-mutation fields.
- Tests collapse proof rows or skip failing-first evidence at critical gates.
