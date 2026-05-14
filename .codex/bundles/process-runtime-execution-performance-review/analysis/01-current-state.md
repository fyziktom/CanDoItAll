# Current State

## Scope Snapshot

- `CanDoItAll.Modules.Processes` targets `net10.0` and has 152 C# documents in the scoped code analytics snapshot.
- The module owns templates, launch planning, runtime runs, transitions, outbox dispatch, artifact projection, operator control-plane read models, and Blazor workspace components.
- Historical active-run UI read-model work is already complete in `.codex/bundles/process-runtime-ui-performance`.

## Performance Scan Checklist

Scan scope: `src/CanDoItAll.Modules.Processes`, files `*.cs`, `*.razor`, `*.razor.cs`, excluding `bin` and `obj`.

| Recipe | Count |
| --- | ---: |
| `.IndexOf("literal")` without `StringComparison` | 0 |
| `.Substring(` calls | 3 |
| `.StartsWith` / `.EndsWith` literal likely without `StringComparison` | 0 |
| `.Contains("literal")` likely without `StringComparison` | 0 |
| `async void` candidate | 0 |
| `.Result` candidate | 0 |
| `.Wait(` candidate | 0 |
| `.GetAwaiter().GetResult()` candidate | 0 |
| `Task.Run(` in library code candidate | 0 |
| `static readonly Dictionary<` | 0 |
| `static readonly FrozenDictionary<` | 0 |
| `new List<` | 95 |
| `new Dictionary<` | 93 |
| `StringComparer.CurrentCulture` | 0 |
| LINQ chain methods | 1268 |
| `.Any` / `.All` calls | 172 |
| parameterless `.ToLower()` / `.ToUpper()` | 0 |
| chained `.Replace` 3+ | 0 |
| `params` signatures | 9 |
| `RegexOptions.Compiled` | 0 |
| `[GeneratedRegex]` | 0 |
| `new Regex(` | 0 |
| `new HttpClient(` | 0 |
| `new JsonSerializerOptions` | 1 |
| `JsonSerializer.Serialize` / `Deserialize` | 24 |
| `.ContainsKey(` candidate | 29 |
| `File.ReadAll` / `File.WriteAll` sync or async family | 19 |

## High-Confidence Hot Path

`C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.RunStart.cs` performs repeated in-memory scans while creating step runs:

- `context.StepRoleRequirements.Where(...).ToList()` inside the per-step loop.
- `context.ArtifactExpectations.Where(...).Select(...)` inside the per-step loop.
- `ResolveCurrentExecutorAssignment` and `ResolveStepCapabilityGapSeverity` each rebuild an effective assignment dictionary for the same step.
- `BuildEffectiveAssignmentsByRoleRequirementId` uses LINQ `Where` + `GroupBy` + nested ordering for every call.

This is a moderate performance issue because it multiplies allocation and CPU cost by step count during every process start.

## Lower-Priority Findings

- Synchronous file reads exist in dispatch support paths, but most are small validation/projection reads and are not the first safe change.
- `JsonSerializerOptions` is already cached through a static readonly field despite the scan hit.
- No sync-over-async, per-call regex, `new HttpClient`, or culture-sensitive string comparison problems were found.
