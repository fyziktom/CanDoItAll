# Validation coverage matrix

These are mandatory coverage families, not test/class/interface quotas. Expected discovery
comes from the current compiled owning suite, not a number copied into this document.
The complete runbook is [APPLICATION_JOURNEYS.md](APPLICATION_JOURNEYS.md). Stage and
qualification rules are in [EXECUTION_AND_CLOSURE.md](EXECUTION_AND_CLOSURE.md).

| Group | Coverage | Distinguishing evidence |
|---|---|---|
| G00 | Entry, exact dependency pair and preservation census | Verify usable local Components and real source/asset provenance, not main push alone. |
| G01 | WB4-T1 original native text destination | Failing-first same-ID replacement/late-original test through actual dialog and writer. |
| G02 | Retired openings, immutable submissions and exact outcomes | A-B-A, actor/profile changes, duplicate events, known commit and unknown reply. |
| G03 | Evaluated Content dependency and public contract closure | Backend-free transitive graph, preserved sibling leaves, public/wire/API docs. |
| G04 | Complete text create/upload renderer and raw draft | All five supported subtypes, exact fields, keyboard and two independent editors. |
| G05 | Actual bounded upload and payload preparation | Oversize/truncated/encoding/extension cases with native rejection and original file identity. |
| G06 | Content validation and safe presentation | Invalid JSON/filenames do not write; unsafe text never becomes active HTML. |
| G07 | Native text/storage identity and byte readback | Exact project/parent/lifetime, node/storage identity and actual downloaded bytes. |
| G08 | Text partial commit and observation without replay | Post-create link/layout/readback fault keeps known node and does not create again. |
| G09 | Native file collection and descendant scopes | Project/node paths, correct sources/revisions, missing and unavailable references. |
| G10 | Bounded browse/search and coherent source replacement | Preserve budgets/order/partial states; no eager content load for every item. |
| G11 | Read-only collection activation | Actual FileBrowser to FileInteraction, correct returned file/bytes and default action. |
| G12 | File lifetime, cancellation and exact cleanup | A/B late success/error, back/close/reopen, cancel-ignoring operation, two sessions. |
| G13 | Direct editable native known-file interaction | View/Edit policy, actual SaveTarget and exact persisted revision receipt. |
| G14 | Content concurrency and authorization negatives | Old revision, read-only/expired/wrong handles, changed actor/lifetime and no overwrite. |
| G15 | Dirty, conflict and pending-save close guard | Discard affects draft only; closing a successor or replaying a save is impossible. |
| G16 | Supplemental notes and content isolation | Content comparison uses original authority; Notes never bypasses denied bytes. |
| G17 | Download and permitted local host actions | Actual byte hash, fresh lease and native harmless launch/denial; no path substitution. |
| G18 | Image provider/model display and routing parity | Native seeded defaults/source aliases and opaque IDs remain distinct across two clients. |
| G19 | Placeholder, enqueue, worker and output native identity | Captured target through separate worker scope; exact operation and media result. |
| G20 | Generation faults, interruption and no replay | Retired placeholder, enqueue failure, unknown provider/persistence, honest volatile queue. |
| G21 | Transcript scaffold and explicit provider confirmation | No speech call on scaffold, no call on cancel, one exact explicit disclosure. |
| G22 | Transcript output persistence and concurrency | Provider completion vs stored result, current metadata/ref preservation, no resend. |
| G23 | Progress Summary exact status and accepted source | Correct original rows/root and unchanged neighbor across two openings. |
| G24 | Stored XLSX/Mermaid/canvas exports | Real graph assets and decoded content, known create before follow-up failure. |
| G25 | Governed and legitimate legacy Mermaid paths | Native .mmd bytes and strict legacy rendering are not interchangeable authority. |
| G26 | Source/published Fast-Parity hosts and real child assets | Same renderers, two instances, no native bootstrapping; served-byte evidence. |
| G27 | Large-desktop interaction and accessibility | 1920x1080/DPR1 geometry, scroll, keyboard, footer, nested overlays; no mobile tuning. |
| G28 | Measured repeated watch loop and restoration | Three probes per owned kind; report sticky CSS and navigation/restart honestly. |
| G29 | Native operator content journey | UI-created project through create/browse/edit/export/readback with canonical data. |
| G30 | Native Agent file read/write/attach and exact approvals | Hidden canary, denied proposal, actual receipts/bytes and unchanged unrelated state. |
| G31 | Source and two-client native consumer parity | Final-image provider choices, image content, retired source denial and History identities. |
| G32 | WB1/WB2/WB3 and Workflow/Scheduler/History continuity | Preserve native graph/task/report semantics and accepted/incomplete/restart behavior. |
| G33 | Final broad scope and original-run disposition | One justified final gate or reviewed scope decision; no relabeling original failed runs. |
| G34 | Portability, current security and documentation closure | Required gates pass; separate historical/full-checkout scan limitations. |
| G35 | Current caller map, signed commits and honest completion | No unimplemented in-scope renderer, native ownership retained and remaining scope explicit. |

S0 owns G00-G02; subsequent phases establish the relevant code and native groups together.
W4 closes G26-G35 on the actual final source pair. Group counts do not establish thoroughness:
positive scenarios need the counterexample that would fail if the boundary were still wrong.

Use existing owning families as starting points: ProjectStructureTextAssetCreateDialogTests,
ProjectStructureTextAssetCreationCoordinatorTests, ProjectStructureFileInteractionIntegrationTests,
native scope/known-file/lease families, ProjectStructurePageActionLifetimeTests and current
SharedProviderNativeConsumerTests. Discover their current paths and exact cases; names here
are not authorization to run zero-discovery or stale assemblies. Add real barriers at owner
boundaries for the new races rather than requiring wall-clock sleeps.

Do not substitute archive/package-tooling tests for product tests. `templates/evidence.json`
starts with NOT_RUN across every group. An evidence checker result is never an application
pass. Only explicitly allowed non-functional groups can be qualified; a native wrong-target
or unauthorized content issue is always blocking.
