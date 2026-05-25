# DB bottleneck inventory

## B1: Dead hot-switching/drain model remains

`DatabaseRuntimeSwitching.cs` still contains:
- `AcquireContextLeaseAsync`
- `BeginSwitchAsync`
- `DatabaseContextLease`
- `DatabaseSwitchSession`
- `_activeContextCount`
- `_drainSignal`
- `_contextsAllowed`
- `_switchInProgress`

Normal `AppDbContext` creation no longer uses this because the pooled factory is canonical. This makes the old state model misleading and creates a future risk that agents reintroduce drain-based hot switching.

Expected fix:
- Replace it with a small runtime status/notification service.
- Remove context lease/drain APIs from public abstractions unless a test proves they are still used.
- Audit that no normal `DbContext` creation path can be blocked by a switch/drain signal.

## B2: Misleading `EnableMaintenanceHotSwitch`

`DatabaseOptions.EnableMaintenanceHotSwitch` exists, but the switch coordinator always returns restart-required and does not change runtime in-process.

Expected fix:
- Remove the option, or implement an explicit operator-only maintenance hot switch behind a separate service and proof gate.
- Preferred: remove it for now to protect canonicality.

## B3: Claimed work still processed sequentially

PostgreSQL batch claim exists in:
- `AutomationMessagingServices.cs`
- `ProcessOutbox.cs`
- `ConnectorOutboxService.cs`

But each claimed record is still processed in a sequential `foreach`.

Expected fix:
- Introduce bounded parallel processing after claim.
- Use partition rules:
  - automation: do not process multiple deliveries for the same envelope concurrently unless aggregate update is made race-safe;
  - process outbox: avoid concurrent records for the same process run/step unless command semantics are explicitly independent;
  - connector outbox: partition by connector/plugin/account/tenant where available;
  - always preserve idempotency and lease-token validation.

## B4: Process dispatch claim token is not a full mutation guard

Process dispatch now claims a step with token and renews lease, but if renewal fails it only logs a warning. A stale worker might still continue into artifact projection and transition attempts.

Expected fix:
- All automation-owned terminal mutations must verify the dispatch claim is still held and unexpired.
- Renewal failure should cause the current worker to stop or observe only.
- Final transition request should include claim-token proof or call a claim verification method immediately before commit.
- Artifact projection should also be claim-guarded.

## B5: Heavy candidate loading before durable claim

`LoadDispatchCandidateAsync` loads many run-wide datasets before a dispatch claim:
- all dispatchable steps,
- all step runs,
- artifacts,
- role requirements,
- assignments,
- artifact input definitions,
- branch outcomes,
- dependency definitions,
- execution runs per candidate.

Expected fix:
- Move toward claim-first selection: claim a minimal step row first, then load detailed context only for the claimed step.
- Preserve dependency and branch semantics by calculating eligibility minimally in SQL or with a two-stage "eligible ids -> claim -> hydrate" approach.

## B6: Profile-specific context factory naming and caching

`ISwitchableAppDbContextFactory` is no longer truly switchable for normal runtime. It has two responsibilities:
- canonical pooled runtime context,
- profile-specific maintenance context.

Expected fix:
- Rename/split to `ICanonicalAppDbContextFactory` and `IProfileAppDbContextFactory`, or keep existing interface only as a compatibility wrapper with deprecation docs.
- Avoid profile-specific context creation in hot paths.
