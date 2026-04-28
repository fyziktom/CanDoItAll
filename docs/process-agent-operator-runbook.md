# Process Agent Operator Runbook

## Scope

This runbook covers process runs that use AgentFramework-backed steps and need human operation through the process workspace control plane.

## Triage Order

1. Open the process run and review the Control tab.
2. Check open escalations first. Journal-backed escalations can be assigned, resolved, reopened, or converted into rework.
3. Check pending approvals. Approve only when the tool details and process context match the intended work. Reject or request changes when the action is unclear, unsafe, or outside the process contract.
4. Check dead-lettered automation dispatch records. Treat them as failed automation evidence until the underlying error is understood.
5. Use the attempt timeline to reconstruct execution runs, approvals, outbox dispatch, recovery decisions, rework packets, and manual reruns.

## Escalations

Blocked, failed, refused, and waiting-approval transitions create durable escalation journal entries. Each escalation records kind, severity, status, owner, due date, source run/step, reason, resolution, and correlation id.

Use assignment when a person is actively triaging the escalation. Use resolution only after the process state is no longer blocked by that escalation. Reopen if the earlier resolution was premature.

## Rework

Manual rework creates a typed rework packet and queues a governed agent rerun. The rerun is allowed only for blocked or failed agent-owned steps. The directive should describe the smallest required correction and should not ask the agent to regenerate unrelated work.

## Approvals

Execution tool approvals continue the paused AgentFramework run. The operator note is recorded in the process journal and decision ledger. "Changes requested" is represented as an explicit rejection plus the operator note because the execution continuation API supports approve/reject decisions.

## Secrets

Do not paste provider keys into appsettings, reports, screenshots, logs, or operator notes. Configure provider credentials through environment variables or the runtime secret mechanism documented in `docs/secure-configuration.md`. If a provider key pattern is found in tracked files, stop work, remove the value, rotate or revoke the credential outside the repository, and run the secret-scanning gate.

## Validation Gates

Run the focused process gates after changing operator behavior:

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --filter "FullyQualifiedName~ProcessWorkspaceTests"
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~SecretScanningTests|FullyQualifiedName~SnapshotIntegrityTests"
```
