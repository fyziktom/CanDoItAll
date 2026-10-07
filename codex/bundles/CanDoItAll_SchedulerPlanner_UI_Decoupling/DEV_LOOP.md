# Development-loop and dependency evidence

Goal: make the **complete SchedulerPlanner renderer** practical to develop in a small real
sandbox, not claim a speedup from moving file names. Full Web will still host other modules.
Plugins' previous measurements and graph sizes are historical context, not this baseline.

## Comparable starting point

Before moving Scheduler renderers, record actual main/sibling commits, dirty state, SDK,
MSBuild/runtime, OS, source/package mode, output configuration, restored/cold cache state,
viewport, representative dataset and concurrent machine load. Keep S0 fixes separate in
provenance. Use a supported isolated configuration such as `SchedulerUiProof`; never overwrite
ordinary application output or stop the app on 5032.

After extraction compare the same original Web `/scheduler` journey, extracted Web journey
and complete standalone sandbox. Use owned processes and ports, restore measurements exactly,
and keep different hosts from concurrently writing the same output paths. A machine-specific
smoke result is not a universal benchmark.

## Four independent evidence dimensions

| Dimension | What to inspect |
| --- | --- |
| Evaluated build graph | Root-inclusive project closure after props/targets and sibling source replacement; cycles, unresolved edges, real build dependencies |
| Package/runtime/public types | Restore assets and transitive packages/native dependencies, exported type closure, concrete owner leaks |
| Static assets and startup | Real BaseLib/CanvasLib, scoped CSS, calendar JS, theme/fonts, published URLs, actual services started by sandbox |
| Watch and runtime edits | Actual watch list plus executed edit-to-visible probes, supported patch versus restart, cache/load conditions |

Example inventory commands, adjusted to created project paths and current supported SDK:

```powershell
dotnet watch --list --project src/App/CanDoItAll.Web --configuration SchedulerUiProof
dotnet watch --list --project src/Sandboxes/CanDoItAll.SchedulerPlanner.UiSandbox --configuration SchedulerUiProof
```

Inspect evaluated references and restore dgspec/project.assets metadata, not only literal
csproj ProjectReference entries. A small .UI project can still pull in Workflow/Agent/EF
through a public model or Canvas asset shortcut. Explain allowed light model/component
closures and reject unexplained unresolved edges. Runtime tests complement static closure.

## Asset parity is part of the cut

The original Scheduler has feature-scoped CSS and a custom calendar JS module. Move CSS to
the component that owns the matching DOM and inspect generated scoped selectors after the
split. Verify `::deep` selectors visually/computationally; a stylesheet loading with no matching
selectors is not parity. Reuse the authoritative theme input with an explicit watch/rebuild
path rather than copying a stale generated artifact or referencing Web for content.

The sandbox must render the actual CanvasCalendar with its scripts, styles and required
assets. Preserve native/browser support as applicable to the actual component closure.
Inspect static-web-asset URLs both from source/watch and a published Production host with no
backend services. Do not fetch real customer avatars or external accounts to make proof work.

JavaScript is **applicable**: current module-global binding is being corrected and relocated.
Verify a changed executed callback/marker, proper new module URL/cache behavior, and host-local
mount/unmount ownership. A changed JS file in a watch list is not proof the browser ran it.

## Measured probes

Capture at least three comparable samples for each applicable edit class and report all
samples and their median. Explicitly record missing/failed samples and unsupported edits.
Do not average startup, full process restart and in-place hot reload into a single number.

| Probe | Start and end observation |
| --- | --- |
| Restore/build | Owned direct target command → successful completion; distinguish restored incremental and clean outputs |
| Startup | Owned host/watch start → actual interactive tab and ready real calendar/assets, not only port listening |
| Razor | Edit visible extracted label/expression → changed DOM in the actual page |
| C# | Edit an executed presentation/helper expression → new displayed behavior; record patch/restart |
| CSS | Edit authoritative feature/theme source → changed computed style on real scope-matching element |
| JavaScript | Edit executed per-host calendar behavior → observed new browser effect after correct refresh/cache handling |

Keep representative dataset, viewport and source dependencies comparable. Record owner call
counts under typical typed-input edits and options changes; no-per-keystroke-I/O claims need
those observations, not a stopwatch alone. Cap fake data reasonably and retain the large
case; never omit the real calendar or difficult dialog to make the sandbox appear fast.

No stale sibling packages, disabled required watch inputs, weakened build targets or fake
static markup as performance shortcuts. Full-Web hot reload can remain broad/noisy or slower;
report it honestly, while separately reporting the useful independent sandbox behavior.

Retain sanitized raw build/watch logs, graph inventories, samples and screenshots in task-owned
ignored evidence. Update the canonical Scheduler boundary record with conditions, source
provenance and the four independent dimensions. Restore all probe changes byte for byte and
stop only recorded owned process trees.
