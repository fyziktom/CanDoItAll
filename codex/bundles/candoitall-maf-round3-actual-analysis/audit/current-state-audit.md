# Current-State Audit

## Overall verdict

Codex round 2 materially improved the MAF integration. I would not roll it back. However, the system is not yet production-stable around failure recovery and rework. It can retry the current step, but it does not yet distinguish enough between:

- output format repair;
- provider failure retry;
- fresh step retry;
- QA-driven rework continuation;
- build/test/browser proof revalidation;
- manual human-requested rerun;
- approval continuation.

This creates safe-but-expensive behavior: failed work is often retried as a fresh attempt with a text directive instead of being completed with typed context and minimal changes.

## What is good now

- `ProcessRunAutomationDispatchService.Execution.cs` creates `ExecutionInvocationPolicy` with `FinalizerMode: Required` for governed process automation.
- `ProcessStepOutcomeStructuredOutputContract` is passed to workspace execution.
- The execution service validates structured/finalizer output before completing and before assistant-message persistence.
- Failed sessions are marked incompatible/cleared so they are not blindly restored after failure.
- Retry limits exist: default 3 attempts, concrete implementation proof steps get 5 attempts.
- Provider fallback repair exists for provider/model failures.
- Successful tool names can be carried forward across attempts except current-attempt-only proof tools.
- Manual rerun exists for blocked/failed agent-owned steps.
- Static and unit test files now exist for key hardening areas.

## What still needs improvement

### P0 - Plaintext secret in appsettings

A real-looking OpenAI API key pattern exists in `src/CanDoItAll.Web/appsettings.json`. Remove and rotate it immediately. Do not include real secrets in appsettings, docs, tests, fixtures, or generated bundles.

### P1 - Process mutation tools bypass mutation classification

`AgentToolInvocationPolicyMetadata.IsMutationTool(...)` only recognizes workspace mutation tools. It does not include process mutation tools such as:

- `processes_definition_save`
- `processes_definition_publish`
- `processes_definition_delete`
- `processes_definition_import`
- `processes_run_start`
- `processes_step_transition`
- `processes_assignment_resolve`
- `processes_artifact_record`

These tools mutate process definitions, process runs, assignments, and artifacts. They must be classified as mutation tools and must participate in approval/finalizer sequence policies.

### P1 - Recovery context is text, not typed

`BuildRecoveryDirective(...)` is detailed and useful, but it returns a string. It includes missing tools, critical failures, and a short previous-run summary. That is not enough for efficient QA rework. The system should persist a typed packet with exact findings, affected artifacts, failed receipts, reusable proof receipts, and minimal next actions.

### P1 - Current retry strategy is safe but not efficient

The retry loop usually sets `automationChatSessionId = null`, creating a fresh MAF session. That is good for avoiding poisoned model context, but it means recovery depends on a text directive plus durable artifacts. For partial implementation and QA returns, use a fresh or controlled session plus a typed packet, not blind chat-history reuse.

### P1 - Tool proof carry-forward is too coarse

The current carry-forward logic operates primarily on tool names. It correctly refuses to carry forward current-attempt-only proof tools in certain cases, but it cannot prove whether a prior build/test/browser proof is still valid after a small change. Add receipt fingerprints.

### P2 - Provider approval matrix may be too strict

The current `ProviderServices.ResolveFeatureMatrix(...)` sets tool approval support only for OpenAI/Azure Responses transport. Official MAF tool approval documentation demonstrates function tool approvals with Azure OpenAI Chat Completion. Verify the actual installed MAF package behavior, then update the matrix/tests accordingly.

### P2 - Domain recovery guidance is embedded in generic dispatch code

The recovery directive contains detailed calculator/Blazor/project-structure guidance. It is better than having it in generic MAF runtime, but it still makes the process dispatcher domain-specific. Move it behind process-template/project-type guidance providers.

## Recommended round 3 target state

```text
Failure detected
  -> classify failure into typed recovery mode
  -> create/update retry ledger
  -> create AgentReworkPacket if needed
  -> choose session/context strategy
  -> rerun only current step or repair step, never the whole process
  -> preserve durable artifacts and successful receipts where fingerprint-valid
  -> rerun invalidated proof tools
  -> validate finalizer/structured outcome
  -> update process state or escalate
```
