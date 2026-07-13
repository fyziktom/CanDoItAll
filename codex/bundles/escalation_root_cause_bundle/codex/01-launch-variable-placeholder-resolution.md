# Task 01 – Resolve launch-variable placeholders before agent dispatch

## Problem

Tool-critical launch variables contain unresolved placeholders, especially:

```text
DotNetCreateProjectScriptRef = artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.wire-solution.ps1
```

The same assignment already contains `CurrentProcessRunId`, so this should be resolved before the prompt and before any guidance uses it.

Current tests explicitly assert unresolved placeholders:

- `tests/Unit/CanDoItAll.Tests.Unit/DotNetProcessLaunchVariableContributorTests.cs:97-99`
- `tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs:1725-1727`

These tests must be updated.

## Implementation

1. Add a new service in a process/application or process/runtime appropriate namespace:

```csharp
public interface ILaunchVariableTemplateResolver
{
    LaunchVariableResolutionResult Resolve(
        IReadOnlyDictionary<string, string> variables,
        LaunchVariableResolutionOptions options);
}
```

2. Support `{Key}` and `${Key}` at minimum. Add `{{Key}}` only if the repo already uses it.
3. Use bounded multi-pass resolution, e.g. 5 passes.
4. Detect cycles and unresolved placeholders.
5. Add `LaunchVariableResolutionOptions.ToolCriticalKeyPredicates` or equivalent.
6. Tool-critical unresolved placeholders must become validation issues before agent dispatch.
7. Apply resolver after:
   - `EnrichRunLaunchVariables`,
   - step-specific launch-variable enrichment,
   - subprocess child launch-variable enrichment.

## Tool-critical keys

Start with keys matching:

- `*ScriptRef`
- `*ExecutionPlan`
- `*SideEffectManifest`
- `ProductCompletionRequired*`
- `RequiredRuntimeTool*`
- `Subprocess*Evidence*`
- `*ManagedArtifactRoot*`
- `*ArtifactRef*`

Be conservative: if a value is used as a tool argument, it must not contain unresolved placeholders.

## Acceptance criteria

- `DotNetCreateProjectScriptRef` becomes:

```text
artifacts/process-runs/<actual-child-run-id>/scripts/create-dotnet-project.wire-solution.ps1
```

- `DotNetCreateProjectExecutionPlan` contains the same resolved script path.
- Add test that no `{CurrentProcessRunId}` remains in any `*ScriptRef` launch variable on assigned process steps.
- If a tool-critical value still contains `{...}`, assignment creation fails with a clear template/config diagnostic.

## Regression tests

Update existing tests that assert unresolved placeholders. New expected values must use the actual run id available in the test fixture.

Add unit tests:

```text
LaunchVariableTemplateResolver_resolves_script_refs_from_current_process_run_id
LaunchVariableTemplateResolver_resolves_nested_values_with_bounded_passes
LaunchVariableTemplateResolver_reports_unresolved_tool_critical_placeholder
LaunchVariableTemplateResolver_reports_cycle
ProjectStructure_launch_assignments_do_not_expose_unresolved_script_refs
```
