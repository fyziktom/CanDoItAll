# Codex Master Prompt - CanDoItAll MAF Round 3 Rework/Recovery Stabilization

You are a senior C#/.NET architect, Microsoft Agent Framework engineer, and production AI workflow reliability engineer.

You are working in the CanDoItAll repository. Codex round 2 already implemented most structured-output/finalizer hardening. Your current task is round 3: stabilize process failure recovery, retry, QA rework, context carry-forward, proof reuse, and remaining tool-governance gaps.

Do not perform broad unrelated refactoring. Keep comments in source code in English.

## Main objectives

1. Remove and rotate any committed plaintext secret.
2. Classify process mutation tools correctly and ensure they participate in approval/finalizer sequence policy.
3. Introduce typed recovery decisions and typed rework packets.
4. Make retries efficient: use fresh sessions for unsafe retries, but carry durable typed context for rework.
5. Make QA returns minimal-delta rework continuations rather than full restarts.
6. Replace tool-name proof carry-forward with fingerprint-based proof reuse.
7. Add retry ledger/backoff/loop detection.
8. Verify provider approval capability for Chat Completions vs Responses against the installed MAF package.
9. Move domain-specific recovery guidance behind providers/templates.
10. Add behavior-level tests and truthful docs.

## Important current findings to verify

Inspect these files first:

- `src/CanDoItAll.Web/appsettings.json`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Rerun.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`

## Required implementation details

### A. Secret safety

Remove any real-looking API key from source. Add a secret scanning regression. Do not print or copy the key.

### B. Process tool policy

Ensure process mutation tools classify as `Mutation`. Include at least:

- `processes_definition_save`
- `processes_definition_publish`
- `processes_definition_delete`
- `processes_definition_import`
- `processes_run_start`
- `processes_step_transition`
- `processes_assignment_resolve`
- `processes_artifact_record`

Ensure read-only process tools remain read-only.

If process mutation tools are exposed to agents, enforce approval or explicit internal-policy suppression. Required-finalizer sequence validation must treat process mutations after finalizer as violations.

### C. Typed recovery decisions

Introduce `AgentRecoveryMode`, `AgentRecoveryDecision`, and `AgentReworkPacket` or equivalent. Recovery decision should not be just boolean retry/no-retry.

Recovery modes:

- `FormatRepair`
- `FreshStepRetry`
- `ReworkContinuation`
- `ProviderFallbackRetry`
- `ApprovalContinuation`
- `HumanEscalation`

### D. Rework continuation

For QA rejection, build/test/browser proof failure, missing artifacts, or manual repair requests, create a typed rework packet containing:

- process run id;
- step run id;
- source execution run id;
- failure category;
- objective;
- findings;
- artifacts/files to inspect;
- failed tool receipts;
- proof requirements to rerun;
- reusable proof refs;
- minimal next actions;
- prohibited actions;
- human directive when present.

Render the packet into prompts only after it is persisted.

### E. Context strategy

Implement explicit context strategy:

- format repair: no new run;
- fresh step retry: fresh session, compact durable context;
- rework continuation: typed packet and exact artifacts/receipts;
- provider fallback retry: fresh session with provider failure context;
- approval continuation: same compatible session.

Do not blindly replay failed chat history.

### F. Proof fingerprinting

Build/test/browser proof reuse must use fingerprints, not just tool names.

Capture:

- command/arguments;
- working directory;
- relevant file hashes;
- artifact hashes;
- environment/tool version;
- status;
- timestamp.

Invalidate dependent proofs after relevant mutations.

### G. Provider approval capability

Official MAF documentation currently demonstrates tool approval with Azure OpenAI Chat Completion. Verify whether the installed package and current adapter support it. Update matrix/tests accordingly, or document a specific limitation.

### H. Tests

Add behavior tests. Avoid relying only on static source scans.

Minimum tests:

- no real-looking secrets;
- process mutation tool classification;
- process mutation after finalizer sequence violation;
- required finalizer missing cannot complete governed process step;
- wrapped JSON repair does not create a new agent run;
- QA rejection creates rework packet;
- manual rerun attaches packet;
- proof fingerprint reuse and invalidation;
- provider approval matrix proof;
- approval continuation session strategy;
- provider failure fresh retry/fallback.

## Commands to run

Run:

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

Also run any focused test filters you add.

## Final response expected from Codex

Provide:

1. audit confirmation;
2. implementation summary;
3. changed files;
4. tests added;
5. exact commands run and results;
6. remaining risks;
7. confirmation that no secrets remain in tracked files.

Do not claim success if any command failed or was not run.
