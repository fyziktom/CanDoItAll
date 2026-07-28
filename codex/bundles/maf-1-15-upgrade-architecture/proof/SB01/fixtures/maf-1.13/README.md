# MAF 1.13 Compatibility Fixtures

## Scope

These fixtures are deterministic, sanitized contracts captured against repository commit
`797d7ce11205d630756ec9335b1b84295257a315` with the stable MAF package train at
`1.13.0` and the A2A preview train at `1.13.0-preview.260703.1`.

They are not production-state dumps. Every identifier, provider value, prompt, path, and
payload is synthetic. The shapes come from the application records, the framework-managed
StateBag shape already exercised by unit tests, or deterministic fake-agent behavior. Each
fixture has a sibling manifest conforming to
`machine/state-fixture-manifest.schema.json`, and `fixture-hashes.sha256` hashes fixture
payloads rather than manifests.

## Fixture Index

| Fixture | Kind | Source basis | Target expectation |
|---|---|---|---|
| `empty-local-session.json` | framework-managed chat | `ChatSessionRecord`; `InMemoryChatHistoryProvider` StateBag | native restore |
| `framework-history-session.json` | two-turn framework history | `ChatSessionRecord`; attachment scrubber test StateBag shape | native restore without transcript duplication |
| `provider-conversation-session.json` | provider-managed history | `AgentFinalizerPolicyTests.Contextual_approval_continuation_restores_provider_managed_session` | preserve runtime and provider conversation IDs |
| `legacy-function-approval.json` | app-owned function approval | `MafRuntimeArchitectureServicesTests.MafApprovalContinuationDriver_rehydrates_legacy_pending_approval_records` | reissue; never execute directly |
| `legacy-mcp-approval.json` | app-owned MCP approval | `MafApprovalContinuationDriver` MCP mapping/rehydration branches | reissue; never execute directly |
| `legacy-mixed-approvals.json` | ordinary call plus two legacy approvals | `ChatSessionRuntimeCompatibilityRecord.PendingApprovals` list contract | keep requests distinct and reissue |
| `attachment-scrub-session.json` | request-scoped attachment | `MafAgentRuntimeAttachmentTests.RemoveRequestScopedDataContentFromSerializedSession_RemovesPersistedImagePayloads` | persisted state contains no data content |
| `governed-step-isolated.json` | governed step | `MafAgentRuntimeAttachmentTests.CreatePromptInputMessages_for_governed_process_step_ignores_prior_chat_transcript` | isolated input; no unrelated transcript |
| `handoff-scripted.json` | deterministic handoff | `MafAgentRuntimeHandoffTests` scripted agents | one entry and one target invocation; depth guard enforced |
| `workflow-approval-shadow.json` | app-owned checkpoint shadow | `WorkflowBackedAgentExecutionCheckpointBridge.StoredExecutionCheckpointPayload` | validate shadow then reissue approval |

## Explicit N/A and Inactive Classifications

### Native 1.13 approval binding

`N/A`. MAF 1.13 does not persist the 1.15 approval-response binding StateBag entry.
CanDoItAll 1.13 instead stores `PendingToolApprovalRecord` and keeps live
`ToolApprovalRequestContent` objects in a process-local cache. The function, MCP, and mixed
fixtures therefore represent app-owned legacy records. Their expected outcome is
`approval-reissue`, not native continuation and not private JSON reconstruction.

### Hosted A2A message/session

`Inactive`. Product composition registers `IAgentA2AHostCardFactory`, but no product caller
invokes `AddAgentFrameworkA2AServer`/`AddA2AServer` and no A2A message route is mapped.
`AgentA2AHostCardFactoryTests.CreateAgentCardMapsHostingSettingsToA2ACard` is the active card
baseline. No A2A session fixture is fabricated. Server/message/session coverage begins only
after an explicit hosted endpoint exists.

### Native workflow checkpoint

`Inactive`. Ordinary workflow checkpoints are `MetadataOnly` with
`WorkflowResumeAvailability.NotSupported`. The approval checkpoint bridge owns a separate
shadow payload and validates it against the execution run. `workflow-approval-shadow.json`
captures that app-owned consistency record only; it is not represented as native MAF
workflow state and does not assert native resume.

## Behavioral Cases Without Durable 1.13 Payloads

- Background response and reasoning-update cases have no deterministic persisted 1.13
  artifact in the current test suite. They remain target-version behavior tests and are not
  replaced by invented provider JSON.
- Handoff update ordering is a behavior assertion over deterministic fake agents. The
  fixture records the scripted contract, not private workflow serialization.
- A real provider is available in the execution environment by presence check only. No
  credential, raw response, or live provider identifier is committed.

## Integrity Check

From this directory:

```powershell
Get-Content fixture-hashes.sha256 | ForEach-Object {
    $expected, $name = $_ -split '\s+', 2
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $name.Trim()).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Fixture hash mismatch: $name"
    }
}
```
