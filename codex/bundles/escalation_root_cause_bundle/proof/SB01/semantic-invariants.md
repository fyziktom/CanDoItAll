# SB01 Semantic Invariants

## Invariants

1. All final step assignment launch variables are resolved after contributor enrichment and before `ProcessRuntimeStepAssignment` construction.
2. The resolver supports `{Key}`, `${Key}`, and `{{Key}}` with bounded multi-pass resolution.
3. Tool-critical unresolved placeholders become `ProcessLaunchReadinessSeverity.Error` findings and block launch even when normal executor readiness is not requested.
4. Non-tool-critical unresolved display text is reported by the resolver but does not block dispatch.
5. Cycles are detected without unbounded recursion and become blocking diagnostics when they affect tool-critical variables.
6. Workbench remains responsible only for producing dotnet setup variables; application launch orchestration owns generic placeholder resolution.
7. No dependency is added from `Processes.Runtime` to Workbench or `Modules.Processes`.

## Incident Closure

The blocked 5032 class depended on `DotNetCreateProjectScriptRef` and `DotNetAddTestProjectScriptRef` reaching agent/tool guidance with a managed process-run scripts folder that still contained `{CurrentProcessRunId}`. SB01 changes the child launch path so the assignment stores the actual child run id in both script refs and execution plans.

Validated by:

- `ProjectStructureAgentIntegrationTests.StartProcessSubprocessAsync_supplies_dotnet_solution_setup_scaffold_contract`
- Assertions at `tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs:1678`, `:1729`, `:1731`, `:1739`, and `:1750`.

## Negative Behavior

- `Resolve_reports_unresolved_tool_critical_placeholder_as_blocking` proves a missing `{CurrentProcessRunId}` in `DotNetCreateProjectScriptRef` blocks.
- `Resolve_reports_cycles_as_blocking` proves cycle detection is bounded and blocking for tool-critical roots.
- `Resolve_preserves_non_tool_critical_unresolved_placeholder_without_blocking` proves optional text is not silently rewritten or over-blocked.

## Source Assertions

- Resolver contract and implementation: `src/Processes/CanDoItAll.Processes.Application/LaunchVariableTemplateResolver.cs:5`.
- Tool-critical predicate: `src/Processes/CanDoItAll.Processes.Application/LaunchVariableTemplateResolver.cs:35`.
- Launch integration point: `src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs:531`.
- Readiness diagnostic mapping: `src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs:576`.
- DI registration: `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs:70`.

## Residual Risk

SB01 does not convert prompt-only deterministic plans into runtime-owned typed plans. That remains owned by SB07, SB08, SB09, and SB11.


## Completed Validator Contract

- Invariant ID: SB01-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB01/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB01/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/01-sb01-launch-variable-resolution/README.md and bundle://proof/SB01/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.


## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB01 semantic proof metadata | proof/SB01/semantic-invariants.md | proof/SB01/transcripts/00-validator-metadata.txt | final proof closure | proof/SB01/manifest.md rejects missing semantic proof |
