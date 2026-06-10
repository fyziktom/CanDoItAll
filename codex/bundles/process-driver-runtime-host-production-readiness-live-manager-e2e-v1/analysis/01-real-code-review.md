# Real Code Review

## Confirmed progress
- `IProcessVerificationRuntimeHost` now exposes `VerifyAsync(ProcessVerificationHostRequest, CancellationToken)` and keeps the old sync `Verify` only as compatibility wrapper.
- `ProcessVerificationRuntimeHost` returns `ProcessVerificationHostResult` with either `Response` or structured `Denial`.
- Host options include enable/disable, per-lane enablement, max payload items per lane, and max supplied evidence content bytes.
- `ProcessVerificationLaneRegistry` and `ProcessVerificationLaneSelector` are explicit and do not use reflection discovery or fallback selection.
- `EfCoreProcessVerificationAuditStore` and `ProcessVerificationAuditEntry` exist, with redaction on `RequestedBy` before persistence.
- Manager facade/readback contracts exist through `IProcessManagerReadOnlyVerificationFacade` and `ProcessManagerReadOnlyVerificationCommandService`.
- The opt-in live process-run OpenAI test exists and uses `ProcessesService`, `IProcessRunAutomationDispatchService`, AgentFramework execution run readback, and provider usage observations.

## Important discrepancy found in real code
`ProcessesModuleServiceCollectionExtensions.AddProcessVerificationRuntimeHost` still registers:

```csharp
services.TryAddSingleton<IProcessVerificationAuditStore, InMemoryProcessVerificationAuditStore>();
```

Even though `EfCoreProcessVerificationAuditStore` exists. This means the proof of durable audit is not enough for production readiness unless DI is corrected or an explicit configuration switch selects EF by default for app runtime and in-memory only for isolated tests.

## Current architecture posture
The system is ready for a **read-only verification host beta hardening** pass. It is not yet ready for execution-capable process drivers.
