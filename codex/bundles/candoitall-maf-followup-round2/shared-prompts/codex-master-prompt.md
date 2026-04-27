# Codex master prompt — MAF stabilization follow-up round 2

You are a senior C#/.NET architect and Microsoft Agent Framework engineer.

The latest repository snapshot already contains significant MAF hardening work. Your task is not to redo everything. Your task is to close the remaining correctness gaps identified in this bundle.

Read these files first:

- `audit/current-state-audit.md`
- `audit/evidence-map.md`
- `requirements/requirements.md`
- all `subbundles/*/README.md`

## Primary mission

Implement the remaining stabilization fixes:

1. Make finalizer tool attachment and finalizer instructions depend on the effective `AgentFinalizerMode`, not just on `structuredOutput`.
2. Make required-finalizer instructions compatible with JSON-schema response format.
3. Replace broad tool-policy exception catching with a dedicated policy-block exception type.
4. Align provider capability truth across core runtime, Workspace UI defaults, workspace-backed registry, and managed SQLite provider display.
5. Add behavior-level tests for these invariants.

## Important constraints

- Do not rewrite the process engine into full MAF workflows in this round.
- Preserve the dynamic `AgentStructuredOutputContract` path for process automation.
- Keep the `ResponseFormat`/JSON-schema path for structured output.
- Do not remove required finalizer enforcement from governed process-step automation.
- Do not make broad unrelated refactors.
- All source-code comments must be in English.
- Do not claim tests pass unless you ran them.

## Required implementation detail

### A. Runtime finalizer mode alignment

Currently, `MafAgentRuntime.AgentFactory.cs` attaches finalizer tools based only on `structuredOutput`. Fix this.

- Introduce a runtime execution policy/options record or equivalent.
- Pass it through `IAgentRuntime.RunAsync(...)` and `RespondToPendingApprovalsAsync(...)`.
- Compute it from `ExecutionRunRecord.MetadataJson` and `structuredOutput` before invoking runtime.
- Preserve it through approval continuation and temperature retry.
- Required mode attaches finalizer tool and required instruction.
- Shadow mode does not use required instruction.
- Disabled mode attaches no finalizer tool and no finalizer instruction.

### B. Instruction consistency

Required mode must say:

- call finalizer exactly once,
- finalizer arguments are authoritative,
- then return exactly one JSON object matching the same structured-output schema,
- no Markdown/prose/code fences/extra text.

Remove or rewrite “normal assistant text is display-only” from required structured-output runs.

### C. Tool-policy exception boundary

Add a dedicated `AgentToolPolicyBlockedException`.

- Throw it only from policy-block branches.
- Catch only it for policy telemetry/wrapping.
- Remove broad `InvalidOperationException`/`NotSupportedException` classification.
- Do not reclassify actual tool failures as policy blocks.

### D. Provider capability truth

Use `ProviderFeatureMatrix` or equivalent as the canonical runtime capability source.

Fix at least:

- `ProviderManagementPanel.razor.cs`
- `SettingsPage.razor.cs`
- `WorkspaceBackedAgentProviderProfileRegistry.cs`
- `RuntimeHostServiceCollectionExtensions.cs` if it still displays/persists contradictory structured-output flags.

Ollama local/remote must not default to structured-output capable unless there is a tested implementation proving it.

### E. Tests

Add behavior-level tests, not only source-string tests.

At minimum:

- disabled mode does not attach finalizer tool/instruction,
- required mode attaches finalizer tool/instruction,
- shadow mode is not exact-once required,
- allowed tool throwing `InvalidOperationException` is not policy-blocked,
- missing approval path is policy-blocked,
- provider UI/default/registry capability truth matches the core matrix.

## Required commands

Run:

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

If the repo uses a different solution file in your environment, use the actual solution file and document it.

## Final response format

When you finish, provide:

1. Files changed.
2. Exact fixes implemented.
3. Tests added.
4. Commands run with pass/fail result.
5. Anything not completed and why.

Do not say “all done” if any requirement remains unimplemented.
