# SB03 Final Validation Evidence

## Scope

- Repository: `C:\repositories\CanDoItAll`
- Branch: `agents-loading-refactor`
- Baseline head: `797d7ce11205d630756ec9335b1b84295257a315`
- Date: `2026-07-28`
- Process E2E: intentionally not run

## Deterministic Validation

The final focused unit command covered the shared 1.15 options policy, loaded
package identities, stable approval identifiers, native session
serialize/scrub/restore, exact-once continuation, timeout/cancellation behavior,
and the runtime activity transition used by actionable approvals.

```powershell
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj `
  --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.AgentExecutionActivityRuntimeProgressPolicyTests|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentExecutionActivityCoordinatorTests|FullyQualifiedName~CanDoItAll.Tests.Unit.MafApprovalSessionRoundTripTests|FullyQualifiedName~CanDoItAll.Tests.Unit.MafRuntimeArchitectureServicesTests|FullyQualifiedName~CanDoItAll.Tests.Unit.MafPackageBaselineReflectionTests" `
  --nologo `
  --verbosity minimal
```

Result: `71 passed, 0 failed, 0 skipped`, managed operation
`op_fb7745a704f447b096be0558e6198fad`.

The handoff integration slice completed separately with
`6 passed, 0 failed, 0 skipped`.

The approval round-trip class completed `10 passed, 0 failed, 0 skipped`. It
covers native function approvals plus a provider-hosted
`McpServerToolCallContent` envelope in buffered and streaming modes. The MCP
fixture serializes, scrubs, and restores the native session, submits a response
from deliberately tampered persisted arguments, and proves that MAF binds it
back to the original request ID, call ID, server name, tool name, and safe
arguments. It does not claim MCP transport or server execution.

## Live Approval Validation

The rebuilt 5032 host was exercised with `.NET Application Developer` and an
approval-required `workspace_write_file` request:

```text
path: artifacts/approval-probe-20260728-final.txt
content: MAF approval probe only.
overwrite: false
```

Observed behavior:

1. MAF produced the exact tool name and arguments shown above.
2. The activity timeline transitioned through `Persisting / Session` and then
   `WaitingOnTool / Approval`; the former invalid
   `PersistingResult -> UsingTool` transition did not recur.
3. Rejecting restored the persisted workflow checkpoint and replayed the denial
   through the native MAF session.
4. The provider honored the original forceful prompt by requesting the same
   denied call again. Each rejected continuation was recorded as cancelled; no
   tool invocation occurred.
5. `Test-Path` against the canonical workspace returned `False` for the target.

This proves the approval boundary and denial safety. It does not claim the
forceful model prompt terminates after a denial.

## Known Warning

The inherited `System.Security.Cryptography.Xml` `10.0.7` `NU1903` advisories
remain visible. They predate this package migration and were not suppressed.
