# SB18 Semantic Invariants

| Invariant | Final proof | Result |
| --- | --- | --- |
| Exact package intake | 7 FileTools nupkgs plus 7 symbol artifacts match the accepted SHA-256 manifest; published FileInteraction DLL equals the accepted package payload | Pass |
| Static assets | Published roots exist for FileBrowser.Components, FileInteraction.Components, FileInteraction.Markdown, and Components.Mermaid; object-URL and strict Mermaid modules exist | Pass |
| Dependency direction | 90 product projects, 426 internal edges, zero project cycles; Infrastructure has no FileTools/UI package edge | Pass |
| Explicit composition | Strict storage-placement decoration is invoked only after Infrastructure at production and test roots; reusable runtime modules have no hidden concrete prerequisite | Pass |
| Current authority | Handles, occurrences, actor/context, operation, revision, save, overwrite, revoke, and unsigned endpoint negatives remain directly tested | Pass |
| Cache/revision isolation | Disabled mode, actor/runtime/scope/query/revision separation, bounds, cancellation/failure non-retention, and post-persistence revisions remain directly tested | Pass |
| Known-file fast path | Direct Project Structure interaction constructs one FileInteraction and zero FileBrowser roots after handoff | Pass |
| PDF visibility | Successful `blob:` binding makes the `application/pdf` object visible without waiting on an event suppressed by `hidden`; error fallback/revocation remain explicit | Pass |
| Project hierarchy | Projects uses BaseLib `TreeView`, recursively builds parents/subprojects, expands selected paths, detects cycles, and filters the selected subtree | Pass |
| Large-source bounds | Accepted 100,000-entry filesystem proof and bounded provider/search/cache counters remain green; no new hot-path anti-pattern was introduced | Pass |
| Desktop contract | Projects, Project Structure, Processes, Resources, and migrated interaction evidence pass at 1900x1200 and 1440x900 with no horizontal overflow | Pass |
| No bypass | Workbench makes no legacy preview request; the only `MediaPreviewUrl` assignment is empty; forged unsigned preview returns 401 | Pass |

