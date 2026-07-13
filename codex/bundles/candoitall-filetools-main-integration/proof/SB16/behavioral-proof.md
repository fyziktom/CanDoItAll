# SB16 Behavioral Proof

## Decision

- Status: `Pass`.
- Scope: direct governed Project Structure FileInteraction for text, Markdown, Mermaid, raster, and PDF; inert SVG/unknown/oversize behavior; awaited revisioned save; legacy preview bypass removal.
- Progression: SB17 is unlocked. Any authority, active-content, revision, lifecycle, duplicate-route, dependency, or C# gate regression reopens SB16.

## Architecture And Responsibility Result

| Owner | Responsibility | Result |
| --- | --- | --- |
| `ProjectStructureKnownFileInteractionCoordinator.cs` | Resolve current node scope/storage/driver, choose intent, activate exact known file, create and release direct interaction session | 159 lines; no FileBrowser dependency |
| `ProjectStructureFileInteractionPolicy.cs` | Strongly typed supported extension/media, edit capability, 16 MiB, and host notice policy | 89 lines |
| `WorkbenchFileInteractionComposition.cs` | Explicit one-profile host Mermaid registration | 51 lines; builder composition only |
| `WorkbenchMermaidFileView.razor` | Adapt FileInteraction text context to shared strict Mermaid wrapper | 45 lines |
| `ProjectStructureAttachmentPreviewDialog.razor(.css)` | Render direct interaction, controlled mode/state, save callback, close guard, and responsive shell | 217 + 26 lines; no content/storage decision |
| `AuthorizedFileSaveTarget.cs` | Exact handle/operation authorization, bounded bytes, storage-driver revision/overwrite write, redacted logging, revision publication | 144 lines |
| `ProjectStructureGraphAdapter.cs` | Canvas metadata projection with deliberately empty preview URL | Legacy route derivation removed |

The migrated legacy owners shrink by 107 net lines across the tracked canvas dialog, support dialog, workflow, and graph projection changes (`22` insertions, `129` deletions). No new Workbench page partial, service locator, renderer discovery, command hierarchy, Infrastructure UI dependency, or Integration-to-Workbench reverse edge was introduced.

## Interaction And Renderer Behavior

The coordinator accepts project/node identity, resolves the current known occurrence and storage registration, requires an enabled resolvable driver, and grants Edit only when storage and driver both support writable revisioned content for an explicitly editable text type. All other known content is ReadOnly. One direct `FileInteractionRequest` carries file name, normalized media type, declared size, and current revision.

Built-in FileInteraction handles text, raster, PDF, and inert fallback. Markdown is added explicitly. Mermaid is a host profile because the host already owns the shared Components wrapper; the adapter passes text only and fixes strict security, disabled HTML labels, disabled source actions, and host-owned dimensions. SVG is deliberately not claimed as raster because active SVG markup is a different security surface.

## Save, Failure, And Conflict Behavior

The host callback awaits the exact session save target. The save target re-resolves current actor/runtime/file authority on every attempt, requires Edit, requires Overwrite when no expected revision is supplied, reads bytes within the configured bound, and calls only a revisioned storage driver. It publishes the catalog change and returns the persisted revision only after success.

FileTools tests prove success, failure, cancellation, edit-during-save coalescing, conflict, rebase, overwrite, detached callbacks, and replacement/disposal. Host tests prove exact revision handoff and real integration persistence. The live two-session browser flow proves the stale first save conflicts while retaining local text, then `Retry against current revision` persists the stale editor safely and clears conflict/dirty state.

That live flow found a genuine host defect: the save target treated the handle's issuance revision as immutable for every later save. The redundant comparison rejected valid later persisted revisions before the storage driver. Removing it does not weaken authority: the handle still binds exact file/context/operations, overwrite still requires explicit permission, and the storage driver still rejects stale expected revisions. The new sequential-save test prevents recurrence.

## Hostile And No-Bypass Proof

- Hostile Markdown creates no script element, image request, or JavaScript link and cannot set the sentinel.
- Strict Mermaid renders SVG text without `foreignObject` or script. The Components 0.1.3 JavaScript specifies `htmlLabels: false` at the root and flowchart levels.
- Hostile SVG and unknown ZIP remain metadata-only with zero active content and no edit action.
- Oversized text fails before stream materialization and exposes no editor/view.
- Raster and PDF consume FileInteraction-owned blob URLs. The canvas projects an empty `MediaPreviewUrl`; no `/storage/objects/preview` or managed media route is requested.
- Source scans find no iframe, video, audio, raw image, browser-session, or generic new-tab bypass in the migrated path.

## Automated Proof

| Surface | Command scope | Result |
| --- | --- | --- |
| Main unit | authority/save plus Project Structure policy, scope, action, and helper contracts | `51/51 Pass` |
| Main components | direct attachment dialog, zero-browser file window regression, graph route removal | `16/16 Pass` |
| Main integration | real PostgreSQL direct save/revision/stale conflict/overwrite denial | `2/2 Pass` |
| FileTools Core | full FileInteraction Core suite | `59/59 Pass` |
| FileTools Components | full FileInteraction Components suite | `72/72 Pass` |
| FileTools Markdown | full Markdown suite | `23/23 Pass` |
| Components | Mermaid visualization hardening | `3/3 Pass` |
| Build | Release Web graph with `-warnaserror` | `Pass`, 0 warnings, 0 errors |
| Format and diff | focused format; all three repository diffs | `Pass`; line-ending notices only |

## Performance And Scale Review

The content limit is 16 MiB and is enforced from declared size through bounded stream copy. Text history is capped at 50 entries and 2 MiB for the host Mermaid profile. Preview debounce is 400 ms. Save coordination allows one active save and coalesces the latest edit. Raster/PDF use one owned blob URL per active surface and replacement/disposal releases old resources.

Focused scans find no sync-over-async, `Task.Run`, per-call `HttpClient`, unbounded enumeration, retained FileBrowser session, or duplicate content buffering added by the host coordinator. The bounded host copy is required at the authorization boundary; FileTools owns the remaining renderer/editor buffers.

## Managed Browser Proof

The final repaired Release DLL ran through the managed `PublishedDll` lane as `app_243a5c6830394e0583f412fa32b4ba58` at `http://127.0.0.1:5506`. A temporary nine-node project contained editable Markdown, hostile Markdown, Mermaid, raster, PDF, hostile SVG, unknown ZIP, and a 16,777,217-byte text object.

At 1900x1200 the repaired overlay expanded the direct interaction to the available dialog width. At 1440x900 the final Markdown edit/preview geometry had no horizontal or vertical clipping: dialog 1150/1150, body 1115/1115, interaction 1071/1071, workspace 1071/1071, editor 535/535, preview 534/534. The final screenshot visibly shows revision 4 and Saved.

The final navigation reported zero console errors and zero warnings. The only non-static requests shown were Blazor initializers and negotiate, both 200. Managed logs recorded two authorized opens and two successful saves: the concurrent 70-byte write and the rebased 80-byte write. A public Project Structure content read decoded to the exact expected 80-byte text with SHA-256 `4c59757dbede866eeb3ea2a435b029ff7cb3298941b154f2f2150b31109550ce`.

The proof lease was released, the temporary project was deleted through the public API, the project disappeared from the list, no matching repository file remained, and the managed runtime was explicitly stopped.

## Dependency And Tool Gate

Workbench directly references selected FileTools and Mermaid packages. Integration references Infrastructure and Integration.Abstractions only. Infrastructure's transitive package scan has no FileTools interaction, Components, Mermaid, or Markdig entry. The full warning-clean Web build is the executable acyclic graph proof.

Fresh CodeAnalytics and Components MCP calls returned `Transport closed`. The result is recorded as a tool-boundary limitation; no snapshot or recommendation is fabricated. Deterministic source/package/project scans and direct tests cover the required architecture assertions.

## Closure

Every SB16 checklist item passes. Known types resolve explicitly, Project Structure uses a direct zero-browser path, hostile and oversized input remains safe, save/conflict/failure/cancel/edit-during-save/overwrite behavior is covered, duplicate route-bearing preview behavior is removed, and desktop renderer/dialog/console/C# gates pass. SB17 may begin.
