# Module slice · <module / coherent surface>

Shared reference: `CDA-UI-DECOUPLING-SHARED-v3`.
This file is a compact brief, not a prescribed phase sequence. Replace the placeholders before implementation; remove irrelevant sections with a short reason.

## Outcome and authorization

Name the production surface and the useful UI task that must become independently developable. State what the owner authorized, the completion boundary, non-goals and whether local signed commits are permitted. Do not infer push, merge, publication or an API/backend redesign.

Record actual application branch/HEAD, dirty state, SDK, dependency/asset modes and relevant sibling revisions. Treat the shared review SHA as reference only. Link current canonical module/architecture/testing guidance and record any relevant change since that review.

## Rendered closure and ownership

| Surface / child / delayed overlay | Current host and implementation | Proposed renderer / contract | Reads, writes, state/effect owner | Assets / public consumers | Disposition |
|---|---|---|---|---|---|
| <real path or type> | <actual owner> | <existing or justified seam> | <explicit target and lifetime> | <actual dependencies> | <extract / retain-as-host / already-separated / verified-unused> |

Include route and non-route consumers, slots, private nested components, common dialogs, JS/CSS and test hosts. Discover outside `src/UI` and outside the module's own directory too. An unused component is removed only with reference/dynamic-consumer evidence.

Choose presentation+intent, workspace view or narrow read-port variation per responsibility. Identify the public types that currently cause an implementation reference and the smallest owner-preserving repair. Record the evaluated dependency cut, permitted light dependencies and any narrow prerequisite in another owner. No interface/file quotas.

## Behavior and transitions

| Behavior or transition | Preserve / intentional correction | Current evidence | Proposed owner | New/current test or browser action |
|---|---|---|---|---|
| <actual operation> | <classification and reason> | <code/test location> | <one authority> | <observable result> |

Cover meaningful target/parameter changes, loading/error/retry/stale behavior, editor/validation lifetime, nested presentations, access control, writes and committed refresh failures. State edit-during-save policy. Include existing deep links and relevant cross-module journey. Separate a discovered old defect from a newly introduced regression.

## Development loop and assets

Name the real sandbox entry point or justify reuse of an existing host. State representative scenarios and how real children/slots are rendered without production services. Specify source/package and asset modes independently, generation/launch commands, relevant CSS/JS/static paths and the actual render geometry to inspect.

Record the comparison design for original production, changed production and sandbox. Capture graph/watch provenance and measured edit-to-visible samples. Do not set a fictional universal time budget. If blocked, name the missing resource and leave performance as not measured.

## Validation plan

Select exact production projects, owning test solutions and current filters from discovered impact. Record expected case counts and then the actual `--list-tests` result; include theory cases. Plan real-owner integration and production browser proof where behavior crosses those boundaries. Explicitly assess static/documentation gates and wider stable/platform/package/live/container triggers.

For each planned lane state required environment and the claim it supports. A missing PostgreSQL/provider/browser environment blocks that claim, not every independent piece of work. Reuse [the evidence record](evidence.md) rather than making a second checklist for every file.

## Closure and recovery

Update the maintained module boundary/completion record and sandbox/project README. Summarize moved and retained responsibility, compatibility, test evidence, graph/asset/performance results and remaining debt. A partial extraction stays labelled partial.

State the safe source rollback point and any irreversible data/external effects; do not equate reverting a UI commit with undoing committed operations. Record separately any next slice. Do not automatically execute it.
