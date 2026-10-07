# Execute Workbench Content & Files WB4

You are the implementing senior C# architect. Complete this whole bounded work package,
using the current checkout, the current repository rules and the supplied shared foundation.
Read all referenced family and validation documents before changing their owners.

## Mission and the exact starting point

Continue the component/UI decoupling wave for faster independent UI development. This is
not API-only migration, backend replacement, a visual redesign or an interface-count exercise.
WB1 Planning, WB2 Insights and WB3 Structure are preserved, along with completed Agents,
Workflow, Projects, Workspace and provider UI. A long run should finish coherent families,
not repeatedly run the same broad suite or stop after scaffolding a new project.

Review provenance: application product `3957fe73e2a0736c042daa504a2767050423c36b`;
remote HEAD `d7384b12f435978165b4b73b40cb39f03c377892` adds only the old WB3 input.
Do not reset, checkout or pin the working branch to these SHAs. Inspect drift at entry.
Do not remove, rewrite or re-execute historical bundles; they remain until separate pre-merge cleanup.
Include the current supplied bundle in the appropriate historical-input commit if added to the repo,
rather than silently omitting it from the agreed handoff. Keep execution artifacts outside it.

Before edits read AGENTS.md, .github/copilot-instructions.md, docs/testing.md,
.github/workflows/ci.yml, docs/architecture/ui-component-seams.md and current SharedInfo
standards/skills where installed. Read WB3's actual closure record, not only its title.
Use available CodeAnalytics/Components/watch tools; if absent, use evaluated MSBuild,
explicit source/caller tracing, CLI builds and real Playwright. Do not invent tool results.

## S0 — dependency continuity and the original text-asset target

Read [DEPENDENCY_DELIVERY.md](DEPENDENCY_DELIVERY.md) and [S0_TEXT_TARGET.md](S0_TEXT_TARGET.md).
The WB3 report's required primary Components revision is
`a120106bc3d4576a40c16aac29b1b9654fb31d93` (tested isolated equivalent `af7aace210a84c1b9931467d3d41284959811f15`).
At review, remote development still resolved to `24d182c664d0b1f293098643e52caed7384a5d50`
and the newer primary ref returned 404. Locate and verify the existing local work rather
than reimplementing it. Record the exact usable source/assembly/asset pair and separate
local verification from remote delivery. If it is unavailable, report the concrete blocker;
do not suppress compiler/API errors or silently fall back to older CanvasLib behavior.

First reproduce WB4-T1 using the actual text-asset dialog, original page and native writer.
The text coordinator currently holds a ProjectId plus a delegate; its delegate resolves
CreateObjectAsync without capturedSurface/navigation, allowing late submission to use the
current page's project admission. Prove or refute the exact counterexample before patching.
A reliable negative case reuses the same public project/node identity under a new lifetime;
an unrelated missing parent alone is not sufficient proof. Hold upload/preparation and
exercise real route/profile/actor transitions, replacement openings and retired callbacks.

Fix at the existing original-opening/creator boundary. Capture the native project admission,
node/parent occurrence, immutable input, actor/profile, receiver and opening before awaits.
Do not obtain fresh authority for an old draft. Preserve writes already accepted for their
original target, exact receipts and unknown outcomes. No global CloseAll or blanket shutdown
of the feature. Then continue to the larger extraction; S0 is not the deliverable by itself.

## W1 — complete text creation/upload

Read [TEXT_AUTHORING.md](TEXT_AUTHORING.md). Move the complete actual text form and its
children, validation presentation, source-mode controls, filename/content/notes and assets.
Use native asset generation/adaptation/size checks through narrow explicit operations.
Keep all currently supported text/JSON/Markdown/Mermaid/log paths and actual uploads.
One submission captures every field and upload identity before preparation; late typing
must not produce a mixed request. A known created node survives link/placement/readback
failure. A general post-dispatch exception does not justify automatic retry or a second file.

## W2 — complete collection browsing and governed content

Read [FILES_AND_INTERACTION.md](FILES_AND_INTERACTION.md). Move the actual compact browser,
include-subproject control, states, read-only file preview, download and permitted local
commands. Separately complete the direct known-file interaction, which CAN be editable
when its actual native save target authorizes it. Preserve mode rules, revision conflicts,
Save receipts, pending/dirty/conflict close guards and notes comparison.

These are not interchangeable surfaces: collection activation is read-only; a direct file
interaction may edit; metadata editing is another action. Do not make everything read-only
or everything editable to simplify the extraction. Use actual FileBrowser/FileInteraction,
real content sources and the existing trust, storage, grant and save owners. No unsigned URL,
Notes substitute, arbitrary local path or new filesystem abstraction bypasses these owners.

Fence every result/error/finally by its operation AND opening, not just project or file ID.
Detach owned handles before asynchronous cleanup; release each acquired grant/session once.
A superseded browser read must not dispose a successor, a busy predecessor must not clear
successor state, and a dead callback must not obtain a fresh target or download lease.

## W3 — image generation, transcript actions and stored exports

Read [GENERATED_CONTENT.md](GENERATED_CONTENT.md) and [SUMMARY_AND_TRANSCRIPT.md](SUMMARY_AND_TRANSCRIPT.md).
Complete the existing image setup presentation and operation-state feedback, with safe
provider/model metadata. Native shared display names remain display names; opaque model IDs
remain routing IDs. Preserve source-managed restrictions and no personal-provider fallback.

Separate placeholder commit, enqueue acknowledgement, external generation, media persistence
and view reconciliation. The existing queue is a bounded in-memory channel, not a durable
replay guarantee. Preserve accepted operation identity and native target through the worker;
add only necessary narrow owner-context plumbing. Do not build a new job/replay platform or
claim queued work survives restart. After a crash, display observable native state honestly;
never silently reissue an uncertain model request.

Complete Progress Summary (not WB2 Manager Summary), exact inline-status actions and exports.
XLSX/Mermaid/canvas-image exports CREATE stored graph assets; they are not pure downloads.
Keep exact source snapshot, root identity, known output and partial follow-up facts.
Complete transcript scaffold and explicit provider-confirmed Summarize/Find tasks/deliveries.
Scaffold is not speech recognition; analysis text is not automatic task creation. Sending
content is explicit, one accepted attempt, never a render/readback effect. A completed
provider response and failed native save require observation, not automatic resend.

Preserve genuine legacy Mermaid presentation with strict rendering and keep actual stored
Mermaid content on its authorized FileInteraction path. A missing grant does not permit
using a metadata field as a bypass. Inspect real callers before removing a legacy branch.

## Architecture constraints

Read [SCOPE_AND_ARCHITECTURE.md](SCOPE_AND_ARCHITECTURE.md) and [NATIVE_OWNERS_AND_OUTCOMES.md](NATIVE_OWNERS_AND_OUTCOMES.md).
Prefer `src/UI/CanDoItAll.Workbench.Content.UI` and
`src/Sandboxes/CanDoItAll.Workbench.Content.UiSandbox` for this coherent content family.
A justified split between file-heavy interaction and pure authoring is permitted after
measuring the actual graph; there is no project/interface quota. Optional contracts contain
only genuinely shared stable values. Do not move the mixed Workbench models, entities,
serializers or provider/driver services wholesale. Preserve public identities/wire semantics
and generated API descriptions where types move.

The leaf owns actual renderers, children, CSS, JS and shared-asset registration. The module
owns route, original authority, native ports, effects and lifetime. An old module component
inside a RenderFragment is not an extracted family. A typed composition slot is legitimate
for a specifically deferred owner. Do not grow a universal page service bag or a second
operation framework. Native helpers remain local when they express ownership.

## Preserve deferred integrations

Read [DEFERRED_AND_ROADMAP.md](DEFERRED_AND_ROADMAP.md). Participant/meeting/directory
assignment, protected secret forms, terminal/runtime/web-preview, and Workflow/Process
linkage/start/recovery remain reachable at their original targets. Do not claim them
extracted or begin the Processes product module. Reuse completed WB1 task editors,
WB2 support and WB3 canvas rather than moving them again or adding reverse dependencies.

## Execution, tests and safety

Read [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md), [APPLICATION_JOURNEYS.md](APPLICATION_JOURNEYS.md),
[SANDBOX_AND_DEV_LOOP.md](SANDBOX_AND_DEV_LOOP.md), [EXECUTION_AND_CLOSURE.md](EXECUTION_AND_CLOSURE.md),
and [COMMITS_AND_SIGNING.md](COMMITS_AND_SIGNING.md).
Arrange existing native PGP unlock early. Keep the same host signing environment and make
verified signed commits at coherent stages. No passphrase in chat/logs/environment/files;
no unsigned fallback. Do not push, merge, release or remove historical bundles.

Build changed production projects, refresh the owning test assembly, verify exact discovery,
then run the narrow relevant tests. Use actual native PostgreSQL/owner and physical browser
checks where required; a sandbox echo is not proof of authority, persistence or content.
Use 1920x1080/DPR1 only for new visual work. No small-screen/mobile/tablet tuning campaign.
Use owned isolated projects, paths, databases, containers and upstream fixtures; port 5032
and retained unrelated fixtures are not test resources. Preserve accepted effects and failed
attempt evidence before continuing. Do not repeat an operation just because a selector failed.

The final source pair must prove source/published parity, exact file bytes/revisions and
original target identities, relevant operator/Agent/file approvals, generated image bytes,
Workflow/Scheduler and provider consumer continuity. External model replies may be scripted;
actual tools, native owners, authorization and writes may not be substituted. No paid model
requests and no reset of the historical 40/40 budget. Reuse the source/two-client runbook in
an owned fixture. Reassess a single broad gate after code stabilizes against actual owner,
contract, shared component and CI changes; do not run it after every stage.

Run required portability enforcement without baseline-write mode, current/delta secret
reviews and documentation checks. Keep original failed broad runs failed. Distinguish
repaired focused results, test-only deltas, production fingerprints and unresolved findings.
Do not relabel a qualifier, gated-off test or source-only scan as a passing complete product.

Use an external work copy of [templates/evidence.json](templates/evidence.json). The checker
validates evidence shape only; it cannot establish execution authenticity. Limited qualified
closure for measured tooling/historical broad-scan issues is explicit and cannot excuse
unverified file authority or a wrong-target write. Report any complex newly reproduced
protocol/schema issue with a concrete repair map; stop only its unsafe path, not all safe work.

Finish all W1-W4 coverage, the current Workbench caller map and signed handoff. Do not stop
after S0, a text dialog or a new empty library. State exactly what remains for the subsequent
Workbench integration cut and keep Processes last.
