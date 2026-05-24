# DB bottleneck inventory

## B1 — Per-context runtime switch lease

Source:
- `src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`
- `src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`

Current shape:
- `CreateDbContextAsync` acquires a `DatabaseContextLease`.
- Every normal context waits on the runtime switching gate.
- `DatabaseRuntimeState` tracks active context count and blocks new contexts during switch.

Why this is now a bottleneck:
- PostgreSQL-only canonical runtime does not need profile switching checks on every DB context.
- The active runtime DB should be resolved once per process generation.
- Hot switching is an admin/maintenance concern, not the hot path.

Required direction:
- Introduce canonical runtime DB mode and pooled DbContext factory.
- Separate normal runtime contexts from admin/profile-specific contexts.
- Keep switch/drain only for explicit development/maintenance flow.

## B2 — Per-context DbContextOptions creation

Source:
- `AppDbContextOptionsConfigurator.CreateOptions`
- `SwitchableAppDbContextFactory.CreateDbContextAsync`

Current shape:
- New `DbContextOptionsBuilder<AppDbContext>` is built per context.
- Profile is resolved for each context.

Required direction:
- Build Npgsql data source/options once for canonical runtime.
- Prefer `AddPooledDbContextFactory<AppDbContext>` or equivalent custom pooled canonical factory.
- Keep profile-specific option creation only for Data Sources admin actions and test support.

## B3 — Hot database switching in normal runtime

Source:
- `DatabaseSwitchCoordinator` in `RuntimeHostServiceCollectionExtensions.cs`.
- `DatabaseRuntimeState.BeginSwitchAsync`.

Current shape:
- The UI/API can switch runtime DB in-process.
- Switch waits for active contexts to drain.

Required direction:
- Make profile activation write control-plane state and require restart by default.
- Allow in-process hot switch only behind explicit development feature flag.
- Preserve canonicality: no operation can straddle two DB profiles.

## B4 — Process automation per-step in-memory guard

Source:
- `ProcessRunAutomationDispatchService.cs`
- `ProcessRunAutomationDispatchService.Dispatch.cs`

Current shape:
- Static `ConcurrentDictionary<Guid, SemaphoreSlim> StepDispatchGuards`.
- The semaphore wraps the full dispatch flow including long-running workflow/agent execution.

Risk:
- It is process-local, so it does not protect multi-process workers.
- It serializes long-running execution inside one process.
- It can mask missing durable claim semantics.

Required direction:
- Use PostgreSQL-backed step execution claims/leases.
- Keep only short in-memory fast-path guards if needed, never as the canonical protection.
- Long-running agent execution must be represented by durable `ExecutionRunId`, lease token, heartbeat, and recovery policy.

## B5 — Sequential/per-delivery automation/outbox claim

Source to verify:
- `src/CanDoItAll.Modules.Automation/**`
- `src/CanDoItAll.Modules.Processes/**`
- `src/CanDoItAll.Modules.SchedulerPlanner/**`

Known prior shape:
- list due IDs,
- claim one ID at a time,
- execute per delivery,
- update aggregate state after each delivery.

Required direction:
- Use PostgreSQL batch claim:
  - `FOR UPDATE SKIP LOCKED`,
  - `UPDATE ... RETURNING`,
  - lock token,
  - monotonic attempt count,
  - stale lease rescue,
  - duplicate-claim negative tests.

## B6 — InMemory path leaks

Source:
- `DatabaseProfileModels.cs`
- `DatabaseProfileControlPlaneService.cs`
- `DatabaseTransferService.cs`
- `DatabaseSourcesSettingsPanel.razor`

Current shape:
- `InMemory` remains in the provider enum.
- Control-plane service can still build and save InMemory profiles.
- Data transfer lists all profiles except target.

Required direction:
- Keep InMemory only for explicit config override/test harness.
- Do not persist InMemory as a normal Data Sources profile.
- Transfer source/target lists must be PostgreSQL-only.

## B7 — Evidence/build scope noise

Current shape:
- Branch includes prepared bundles and `.codex` proof artifacts in the diff.
- This may be intentional for archive, but it is likely noisy for product merge.

Required direction:
- Decide and document artifact policy.
- Keep implementation evidence in `evidence/` only if repository convention supports it.
- Otherwise remove generated proof inputs from the product branch before merge.
