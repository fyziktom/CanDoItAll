# Detailed findings

## F1 — Critical — The structure load path still writes to the database
**Bundle9 status:** not closed  
**Affected bundle9 gate:** HG-06  
**Phase10 gates:** HG-10-01, HG-10-02, HG-10-03

### Evidence
1. `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:130-176`
   - `LoadAsync(...)` calls `RetireLegacyProjectionRowsAsync(...)` at line `135`.
   - the same method deletes stale projection layout rows and calls `SaveChangesAsync(...)` at lines `167-175`.

2. `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:361-388`
   - `RetireLegacyProjectionRowsAsync(...)` queries stale system-managed rows,
   - removes stale links and stale nodes,
   - then calls `SaveChangesAsync(...)`.

### Why this is a real blocker
This is not an old dead code path or a compatibility helper sitting unused. It is in the active read seam used by:
- `ProjectWorkbenchService.TryGetStructureAsync(...)`,
- `ProjectWorkbenchService.GetStructureAsync(...)`,
- `FindNodeAsync(...)` through `LoadAsync(...)`.

The architecture therefore still violates the phase9 promise: **reads are not pure reads**.

### Why this matters for the upcoming plugin wave
Future connector-driven plugins will increase the amount of structure assembly and projection composition. If the load path deletes rows:
- read traffic can trigger data cleanup at arbitrary times,
- concurrent behavior becomes less predictable,
- plugin authors can unknowingly depend on side effects that should live in maintenance or migration seams.

---

## F2 — High — The phase9 gate script produced another false green
**Bundle9 status:** not closed  
**Affected bundle9 gate:** HG-06  
**Phase10 gates:** HG-10-04

### Evidence
`candoitall-plugin-wave-architecture-review-bundle-v9/scripts/gate_check_phase9.py` checks for:
- old `NormalizeAndHydrateAsync(...)` calls,
- retired legacy carrier symbols,
- old marker and enum symbols.

It does **not** fail on:
- `SaveChangesAsync(...)` inside `ProjectStructureAssemblyService.LoadAsync(...)`,
- `RemoveRange(...)` inside `LoadAsync(...)`,
- a helper invoked from `LoadAsync(...)` that itself performs deletes and saves changes,
- stale layout cleanup still happening in the read seam.

That is why phase9 can report:
- “No hard-gate failures detected.”
while the repo still contains active write-on-read behavior.

### Why this matters
The repeated Codex miss pattern is now clear:
- old bundles focused on symbol retirement,
- Codex removed or renamed the targeted symbols,
- behavior-level invariants remained broken,
- the gate stayed green because it matched the previous implementation shape, not the actual architecture invariant.

---

## F3 — High — The current tests do not prove zero-write reads
**Bundle9 status:** proof gap  
**Phase10 gates:** HG-10-03

### Evidence
`tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:294-392`
already proves one useful thing:
- `GetStructureAsync(...)` no longer backfills binding/reference rows for the legacy carrier scenario.

But that test does **not** cover:
- stale system-managed projection rows,
- stale projection layout rows,
- full zero-write behavior of the active `LoadAsync(...)` path.

As a result, the current suite can stay green while the read seam still performs deletes.

---

## F4 — High — Manifest-driven editor proof is still too narrow for the next plugin wave
**Bundle9 status:** architecture mostly fixed, validation still too narrow  
**Phase10 gates:** HG-10-05

### Evidence
Current coverage is real but limited to known plugins:
- `tests/CanDoItAll.Tests.Components/SettingsPageProvidersTests.cs`
- `tests/CanDoItAll.Tests.Components/ResourcesPageTests.cs`
- `tests/CanDoItAll.Tests.Integration/ConnectorPluginIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs:144-214`

These tests validate the shared editor path for built-in plugin manifests such as:
- Ollama provider,
- folder resource,
- webhook resource.

They do **not** prove that a brand new plugin manifest with mixed field types:
- `Text`,
- `Url`,
- `Number`,
- `Boolean`,
- `Json`,
- `SecretReference`

can render, edit, save, load, and round-trip without page-specific code changes.

### Why this matters
The next wave is explicitly plugin-heavy. A generic editor that works only for the current built-ins is not enough; the team needs proof that unknown manifests survive future additions without reopening page-level switches or editor-model property bags.

---

## Advisory — Remaining read-only compatibility fallbacks still exist
These are not the primary phase10 blocker, but they remain visible and should not be expanded:

- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:77-82`
  - projection nodes still fall back to legacy marker payload from metadata when `MarkersJson` is empty.

- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:390-408`
  - canonical node marker JSON is normalized in-memory and still falls back to legacy metadata when marker JSON is missing.

- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs:391-395`
  - runtime node references still fall back to legacy metadata when persisted reference rows are absent.

These are currently read-only compatibility seams, not write-on-read seams. Phase10 keeps them visible as warnings and prevents Codex from claiming they are “fully retired” unless they are actually removed or explicitly migrated.
