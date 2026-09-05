# Independent sandbox and navigation handoff

## First useful sandbox

Preferred candidate: controlled AgentCatalogPanel plus the actual card/list children needed for catalog selection/filter/open intents. A catalog-only scenario can record host intents without loading AgentDetailsDialog, team editors, the entire route page or backend. It must still render the real selected subtree. If that graph remains too broad, assess AgentSelectionCard as an explicitly narrower useful pilot.

SB03 produces a candidate sketch; SB07 produces the evidence-backed handoff. The next independently scoped child should normally implement the small extraction/host before expanding to Providers or waiting for complete bookmarkability. Any earlier start needs a source/ownership handoff and no concurrent edits to the same source.

The follow-up deliverables are: cohesive UI project ownership; lightweight contracts; production host adapter/wiring; small browser host with deterministic scenarios and real assets; current-production behavior regression checks; measured iteration comparison. Do not create one project per component or require AppComponents without an actual dependency.

## Baseline and comparison protocol

At SB01 record machine/OS, SDK/runtime, configuration, sibling source mode/SHAs, selected root project, evaluated project graph, static assets, host startup and readiness signal. Capture the full app's cold startup separately from warm watch readiness.

Measure representative supported edits in Razor markup, ordinary C# UI logic and CSS/CSS isolation. Include Tailwind generation and JS edits when the selected subtree uses them. Record edit-to-visible latency, hot reload versus restart/fallback, reload/refresh actions, failures and repetitions. Use at least three comparable warm repetitions per category and report range and median; record cold runs separately. Keep temporary measurement edits isolated/reverted and do not retain product changes from measurement.

At follow-up repeat with the same environment, source mode, representative real components/assets and categories. Explicitly distinguish full-app watch, small-host watch, initial build and browser render time. Compare evaluated references and invalidated builds with latency; interface count alone predicts no improvement.

Acceptance is a measured useful iteration improvement for the chosen scenario with unchanged production behavior, plus transparent limitations. Do not invent a percentage target before baseline. Unsupported hot-reload edits or infrastructure/build bottlenecks may still require restarts.

## Extraction blocker handoff

For each blocker record the scenario/type/child, defining project and transitive edge, required asset/service, proposed owner/solution, behavior risk and validation. Current likely blockers include implementation-owned Projects/Security DTOs, Workspace storage UI, infrastructure root selection, Memory.Application/drivers, MAF model/component closure and host static assets.

Some do not belong in a catalog-only pilot and therefore need not delay it. They remain requirements for a later editor sandbox. No omitted editor child may be called isolated.

## Separate bookmarkability track

Decide semantic resource/view identities, hybrid path/query conventions, current-link compatibility, direct-entry loading/errors, push/replace/back/forward policy, dirty-draft lifecycle, create/save identity transition, routed modal lifetime, Workbench windows/context and MAUI host behavior.

Use the meeting pack as proposal evidence. Current DialogService closes all overlays on LocationChanged, so a future routed editor must explicitly retain or replace its host/session according to product semantics. Existing declarative Dialog or a justified host adapter may suffice; no global library change is assumed.

The two tracks share typed state, component ownership and lifetime contracts. Sandbox/project extraction does not depend on finalized production URLs.
