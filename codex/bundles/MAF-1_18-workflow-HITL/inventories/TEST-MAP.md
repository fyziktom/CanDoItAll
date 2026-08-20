# Test Map

## Test projects

Primary focused unit project:

```text
tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj
```

Primary integration project:

```text
tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
```

Verify exact paths and target frameworks in SB00.

## Existing focused classes

| Class | Purpose | Subbundles |
|---|---|---|
| `MafApprovalSessionRoundTripTests` | Approval binding, restart, replay, streaming/non-streaming | SB02 |
| `MafRuntimeArchitectureServicesTests` | MAF composition/architecture | SB01/SB02 |
| `MafStreamingTurnExecutorRecoveryPolicyTests` | Streaming recovery behavior | SB02 |
| `MafWorkflowAdapterIsolationTests` | Adapter boundary and dependency isolation | SB01/SB03 |
| `MafWorkflowEventNormalizerTests` | MAF event mapping | SB01/SB03 |
| `MafWorkflowExecutorFailureDiagnosticsTests` | Workflow failure diagnostics | SB01/SB03 |
| `WorkflowFoundationTests` | Workflow compiler/backend foundation | SB01/SB03 |
| `WorkflowExecutorTests` | Executor and approval behavior | SB03/SB04 |
| `WorkflowRuntimeLifecycleRedGateTests` | lifecycle, cancellation, response transitions | SB03/SB04 |
| `WorkflowAdoptionHardeningCheckpointTests` | workflow/API/checkpoint architecture gates | SB03–SB05 |
| `WorkflowUsageAnalyticsRedGateTests` | usage aggregation and analytics | SB02/SB06 |
| `WorkflowApiIntegrationTests` | HTTP workflow contract | SB05/SB06 |

SB00 must record actual fully qualified names and baseline discovered counts.

## New tests required

### Tool serial policy

Suggested class: `MafToolInvocationConcurrencyPolicyTests`

Cases:

- multiple tool calls execute in model order;
- max active invocation count is one;
- side-effect dependency succeeds serially;
- probe fails under intentionally concurrent fixture;
- custom/provided chat-client composition cannot bypass the policy;
- approval in a multi-call turn blocks later governed execution.

### MAF JSON checkpoint adapter

Suggested class: `MafJsonCheckpointStoreAdapterTests`

Cases:

- create/read checkpoint;
- index returns oldest to newest;
- parent chain preserved;
- session isolation enforced;
- duplicate checkpoint ID rejected/idempotent according to MAF contract;
- payload hash corruption detected;
- cancellation/EF failure translated safely.

### Native workflow HITL

Suggested class: `MafWorkflowHumanInLoopTests`

Cases:

- HumanInput emits native request and real checkpoint;
- run is `WaitingForInput`;
- disposed start run can be rehydrated into a new run instance;
- response resumes from checkpoint and completes;
- consecutive requests create distinct request/checkpoint pairs;
- request port/executor IDs remain stable across recompilation;
- topology mismatch fails closed;
- missing/corrupt checkpoint never restarts;
- denial bypasses governed executor;
- approval invokes governed executor once;
- cancellation while waiting rejects later response.

### Response operation and replay

Suggested class: `WorkflowExternalResponseOperationTests`

Cases:

- same idempotency key and payload replays same operation;
- same key and different payload conflicts;
- concurrent different-key submissions yield one claim;
- crash after claim before resume is recoverable;
- crash/replay after response delivery uses executor deduplication;
- expired lease recovery;
- terminal failure remains terminal;
- legacy non-resumable run is not marked responded.

### API

Extend `WorkflowApiIntegrationTests` or create a focused class.

Cases:

- typed JSON response body;
- anonymous 401;
- wrong scope/actor 403;
- missing 404;
- invalid schema 400;
- stale request version 409;
- idempotency conflict 409;
- cancelled/superseded 409 or 410 per convention;
- legacy/missing/incompatible checkpoint 422;
- active/resuming 202 if asynchronous;
- successful approve, deny, and HumanInput;
- run detail exposes next pending request/status;
- raw checkpoint and secrets never appear in response/log fixture.

## Focused command pattern

Use a stable fully qualified filter and capture discovery:

```bash
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj \
  --no-build \
  --filter "FullyQualifiedName~MafApprovalSessionRoundTripTests" \
  --logger "console;verbosity=normal"
```

For multiple classes:

```bash
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj \
  --no-build \
  --filter "FullyQualifiedName~MafWorkflowHumanInLoopTests|FullyQualifiedName~WorkflowRuntimeLifecycleRedGateTests"
```

SB00 records the actual baseline count. The expected post-change count is:

- greater than zero;
- no lower than the recorded baseline for retained classes;
- exactly the count recorded in the active subbundle after adding its named cases.

A run reporting zero discovered tests is failure even when the process exits successfully.

## Build map

Focused builds after restore:

```bash
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj --no-restore
dotnet build src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj --no-restore
dotnet build tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore
dotnet build tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore
```

SB00 verifies paths and whether `CanDoItAll.Web.csproj` needs an explicit focused build.

## Frozen broad gate

Named checkpoint: **FG-01 / post-SB05 frozen implementation**

Run once in SB06 after no planned source/schema changes remain:

```bash
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --no-restore
```

Run the solution-wide test gate only when repository convention and available time/resources support it:

```bash
dotnet test CanDoItAll.slnx --no-build
```

If solution-wide tests are too broad or contain known unrelated failures, run every affected test project once and record the baseline exception with evidence. Do not repeatedly rerun known expensive UI suites.
