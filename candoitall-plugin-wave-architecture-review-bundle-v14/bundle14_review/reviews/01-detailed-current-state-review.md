# Bundle14 detailed review

## Verdict

The current repository is **not fully OK yet**.

The good news is that the previously reviewed gates are materially improved:
- phase10 hard gate passes,
- phase13 hard gate passes,
- the runtime plane, Quartz bridge, internal messages, hosted workers, ingress inbox, and telemetry scaffolding are all present.

However, a deeper manual architecture review still found **hidden runtime-semantic defects** that are not covered by the existing gates. These defects matter specifically for the upcoming plugin wave because they affect restart safety, duplicate side effects, and correctness under concurrency.

## What is already in good shape

- Workbench read path remains zero-write.
- Explicit projection repair boundary is still present.
- Unknown-manifest shared editor proof remains closed.
- Automation runtime options bind from production configuration.
- Durable internal message plane, trigger registry, hosted workers, ingress inbox, and execution telemetry are present.
- The legacy background-job queue no longer has production call sites that schedule new work directly.

## Hidden defects that remain

### 1. Once-like triggers are not retired after fire

**Evidence:** `src/CanDoItAll.Modules.Automation/AutomationTriggering.cs`
- `AutomationTriggerQuartzJob.Execute(...)` updates `LastFiredAtUtc` and `NextPlannedFireAtUtc`, but it does **not** disable or retire `AutomationTriggerKind.Once`, `Relative`, or `DueDateProjection` triggers after the first fire.
- `QuartzAutomationSchedulerBridge.SynchronizeTriggerAsync(...)` still projects every enabled trigger, with no guard that treats an already-consumed once-like trigger as retired.

**Why it matters:**
After a restart, a once-like trigger can be projected again even though it has already fired. That is exactly the kind of restart-boundary duplicate execution that becomes dangerous once plugins start creating background automation.

### 2. Trigger save returns stale pre-projection data

**Evidence:** `src/CanDoItAll.Modules.Automation/AutomationTriggering.cs`
- `AutomationTriggerRegistry.SaveAsync(...)` calls `schedulerBridge.SynchronizeAsync(...)` and then immediately returns `Map(record)` from the original tracked entity.
- The bridge updates `NextPlannedFireAtUtc` and `UpdatedAtUtc` in a different DbContext, so the returned value can be stale.

**Why it matters:**
Callers cannot rely on the returned `AutomationTriggerDefinition` as the canonical post-save state. That creates subtle bugs in UI, APIs, and future plugin provisioning flows.

### 3. Ingress cursor methods are not normalized/atomic enough

**Evidence:** `src/CanDoItAll.Modules.Automation/AutomationIngressService.cs`
- `GetCursorAsync(...)` queries with raw `sourceKind` / `sourceKey` instead of normalized trimmed values.
- `SaveCursorAsync(...)` also queries with raw values and performs read-then-insert without uniqueness-conflict recovery.

**Why it matters:**
Two practical problems remain:
- leading/trailing whitespace can produce inconsistent reads or unexpected uniqueness violations,
- concurrent first writes can still fail instead of converging on the same cursor row.

### 4. Ingress materialization has no single-executor claim boundary

**Evidence:** `src/CanDoItAll.Modules.Automation/AutomationIngressService.cs`
- `MaterializeAsync(...)` loads the envelope and calls `materializer.MaterializeAsync(...)` **before** any persisted claim, lease, compare-and-set, or "in-progress" state transition.

**Why it matters:**
Concurrent or repeated materialization requests can invoke plugin code multiple times for the same ingress envelope. That is unsafe for future plugin materializers that create tasks, nodes, tickets, summaries, or external side effects.

### 5. Direct connector processing still bypasses lease acquisition

**Evidence:** `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs`
- `ConnectorOutboxService.ProcessAsync(Guid commandId, ...)` still delegates directly to `commandProcessor.ProcessAsync(commandId, cancellationToken: ...)`.
- The durable lease claim exists only in `ProcessPendingAsync(...)`, not in the public direct-processing path.

**Why it matters:**
A manual/admin/direct caller can still execute a pending connector command without first claiming the single-executor boundary. Under concurrency, that can duplicate external connector side effects.

## Advisory-only follow-ups

These are real concerns, but I am keeping them advisory in bundle14 rather than turning them into hard blockers immediately:
- cancellation should be propagated explicitly instead of being folded into broad `catch (Exception)` branches around handler execution,
- telemetry publishing is still tightly interleaved with runtime state transitions, which may deserve a dedicated outbox/best-effort boundary later.

## Overall conclusion

Codex did **not** finish the whole job yet.

The repository is clearly much healthier than before and it closes the earlier gate scope, but it is **not yet execution-grade** for the plugin wave because the restart/concurrency semantics above are still incomplete.
