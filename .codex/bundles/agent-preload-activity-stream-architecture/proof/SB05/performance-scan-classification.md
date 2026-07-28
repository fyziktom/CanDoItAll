# SB05 Targeted .NET Performance Classification

## Scope and method

Target framework: `.NET 10`.

The scan is intentionally limited to the completed backend hot path and its two
adjacent concurrency boundaries:

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/Credentials/SecretStoreAgentProviderCredentialResolver.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`

The review used the required two passes:

1. direct analysis of measured startup I/O, operation counts, and ownership;
2. the `analyzing-dotnet-performance` standard scan with manual classification.

This is not a repository-wide mechanical style scan.

## Pass 1: Initial Performance Review

The material cost is architectural I/O and sequencing, not one allocation API.
The final operation-count matrix removes duplicate catalog, provider, session,
summary, and read-before-write calls and publishes typed `Accepted` activity before
that work. The remaining provider scalar probe, bounded file reads, and process
projection SQL are observable costs with explicit ownership.

No additional parallelism is justified. Runtime capability composition mutates
ordered per-run state, file-store writes share a transaction boundary, and process
EF stores share a scoped context. Parallelizing those stages would trade a small
possible latency gain for correctness risk.

## Pass 2: Deep Pattern Scan

### Scan execution checklist

| Recipe | Exact hits |
| --- | ---: |
| `IndexOf` literal without comparison | 0 |
| `Substring` | 0 |
| literal `StartsWith`/`EndsWith` without comparison | 0 |
| literal `Contains` without comparison | 0 |
| `async void` | 0 |
| `Task.Run` | 2 |
| `.Result`, `.Wait`, or `.GetResult` manual candidates | 1 |
| `Task.WhenAll` or `Parallel.*` | 0 |
| `new HttpClient` | 0 |
| `new JsonSerializerOptions` | 0 |
| static `Dictionary` / `FrozenDictionary` | 0 / 0 |
| per-call `new List` / `new Dictionary` candidates | 29 / 10 |
| `StringComparer.CurrentCulture` | 0 |
| selected LINQ operators | 144 |
| `FileStream` construction | 0 |
| non-sealed public/internal classes / sealed classes | 0 / 19 |

### Classified findings

#### 1. Legacy unscoped credential sync bridge (1 moderate)

`SecretStoreAgentProviderCredentialResolver.ResolveUnscoped` uses
`Task.Run(...).GetAwaiter().GetResult()`. That is a real sync-over-async bridge and remains legacy
debt. The measured dispatch path prepares credentials asynchronously through
`PrepareAsync` and resolves them from the dispatch scope, so this bridge is not in
the validated startup path. Reworking the public synchronous resolver contract is a
separate compatibility change and is not justified inside SB05.

#### 2. Process strategy timeout isolation (1 classified, not a finding)

`ProcessRuntimeDispatchApplicationService.InvokeStrategyWithTimeoutAsync` uses
`Task.Run` deliberately. The strategy may ignore cancellation, while the dispatcher
must return a bounded timeout result and observe a late completion without blocking
the dispatch loop. The code owns the linked cancellation source and late-completion
observation. Removing this boundary would weaken the timeout contract.

### Allocation and LINQ candidates

The 29 list allocations, 10 dictionary allocations, and 144 selected LINQ calls
materialize bounded persistence plans, projection rows, recovery state, and immutable
results. No repeated-enumeration, unbounded query, or measured allocation bottleneck
was confirmed. Replacing them mechanically would add code without evidence of a
meaningful gain.

### Positive findings

- No unsafe shared-stage parallelism was found in the scanned execution, persistence,
  provider, or process query paths.
- No `async void`, uncached `HttpClient`, uncached serializer options, culture-sensitive
  literal comparison, or unsealed runtime service was found in scope.
- The deterministic improvement is the operation-count reduction recorded in
  `bundle://proof/SB05/operation-counts.md`; wall-clock comparisons remain descriptive.

| Severity | Count | Top issue |
| --- | ---: | --- |
| Critical | 0 | None |
| Moderate | 1 | Legacy unscoped credential sync bridge outside measured dispatch |
| Info | 0 | No speculative micro-optimization recommended |

> These scan results are assistant-generated and require measurement and human review.
> They intentionally do not convert raw pattern matches into optimization work without
> hot-path evidence.
