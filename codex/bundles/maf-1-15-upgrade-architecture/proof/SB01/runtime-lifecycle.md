# SB01 Runtime Lifecycle Baseline

## Canonical Host

The canonical development host is
`src/App/CanDoItAll.Web/CanDoItAll.Web.csproj` on `http://localhost:5032`. The shared
dotnet-watch manager owns the long-lived app; a second manual `dotnet run` must not be
started beside it.

Existing governed runtime evidence identifies:

- logical app: `candoitall-web-5032`;
- managed session: `app_c4340c64f0b3453e8f3f45bb687f8bc5`;
- launch profile URL: `http://localhost:5032`;
- project launch settings: `src/App/CanDoItAll.Web/Properties/launchSettings.json`.

That identity is corroborating lifecycle evidence, not a promise that a process ID remains
stable. The manager may replace the child process during build or restart.

## Baseline Observation

At `2026-07-28T05:06:15.0852951+00:00`:

- `netstat -ano` showed PID `52052` listening on `127.0.0.1:5032` and `[::1]:5032`;
- `GET http://127.0.0.1:5032/` returned HTTP `200`;
- response content length was `106829` bytes;
- the request was read-only and did not exercise agent mutations.

The final rebuilt/restarted 1.15 host needs separate closure evidence. This SB01
observation establishes only the pre-upgrade lifecycle and health baseline.

## Runtime Ownership

```text
managed dotnet-watch session
  -> CanDoItAll.Web process
     -> application composition
        -> scoped AgentFrameworkWorkspaceService
        -> singleton MafAgentRuntime
        -> provider-specific AIAgent and AgentSession per execution
```

- The web host owns DI composition, provider settings, API and Blazor entry surfaces.
- `MafAgentRuntime` owns MAF agent creation and orchestration for a run.
- Provider agents/sessions are created and disposed inside the execution lifecycle.
- The custom pending-request cache in `MafApprovalContinuationDriver` is process-local and
  is lost on restart.
- Persisted `ChatSessionRecord.Compatibility`, execution-run state, approval audit records,
  workflow checkpoint metadata/shadows, and workspace artifacts are durable authorities.

## Restart Semantics

For a managed restart:

1. stop accepting new mutation/approval traffic;
2. wait for in-flight calls to terminalize or explicitly mark them interrupted;
3. capture the database/workspace/control-plane consistency boundary;
4. let the managed host stop and replace its child process;
5. wait for the manager to report a new healthy generation;
6. verify HTTP health and then re-open persisted sessions.

The process-local approval cache must never be treated as restart authority. Under the
1.13 baseline, app-owned pending approval records can be rehydrated into response objects,
but A1 classifies those records for 1.15 reissue rather than direct legacy execution.
Native 1.15 approval binding and exact restart continuation are target-version gates.

## State Boundary

The development profile spans three components:

- PostgreSQL database `candoitall_development`;
- `%LOCALAPPDATA%\CanDoItAll\workspace`;
- `%LOCALAPPDATA%\CanDoItAll\control-plane`, including
  `database-profiles` and `dataprotection-keys`.

All three must be captured and restored together. The control-plane keys are required to
decrypt protected profile secrets. Remote provider conversation state is outside this
boundary; only its opaque identifier can be preserved locally.

See `rollback-consistency-boundary.md` for the quiesced snapshot and rehearsal procedure.
