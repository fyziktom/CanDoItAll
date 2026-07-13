# SB16 Governed Proof Manifest

Date: 2026-07-13. Closure decision: `Pass`.

## Owned Raw Notes And Requirements

- `N007`: “show/edit known files with FileInteraction.”
- `N010-N011`: continue with more complex user stories and cover the named main-module surfaces, including the interaction migration.
- `N012`: “large desktop only; no small/medium work.”
- `N013-N014`: force architecture cleanup after phases and preserve maintainability, security, readability, explicit errors, and masked actionable logs.
- `N015`: “Large file sets must remain bounded in work, memory, I/O, and rendered state; UI pagination without provider-side bounds is insufficient.”
- `N016`: “Existing Project Structure asset-node double-click behavior must continue to open its dialog.”
- `N017`: “A known single file must open FileInteraction directly; constructing or loading FileBrowser is forbidden on that path. FileBrowser is reserved for collection/container discovery.”
- Owned normalized requirements are `R012` and `R022-R040`. The SB16-specific outcome is package-selective migration, awaited host save, shared components, desktop proof, focused owners, masked failures, affected-scope validation, direct known-file interaction, preserved asset dialog behavior, and measured performance. Provider-scale requirements remain trusted from prerequisite phases and are regression-checked here rather than reimplemented.

## Scope And Provenance

- Production scope is the Project Structure known-file activation path, explicit FileInteraction composition, focused host dialog/coordinator, revision-aware authorized save target, strict Mermaid adapter, and removal of legacy route-bearing preview branches.
- FileTools continues to own generic interaction state, renderer selection, bounded loading, edit history, preview scheduling, save coalescing, cancellation, conflict UI, and replacement/disposal. The host owns current storage authority, exact known-file activation, allowed edit intent, content limit, save authorization, and host-specific Mermaid policy.
- The host consumes immutable `CanDoItAll.FileTools.FileInteraction.Core/0.1.0`, `CanDoItAll.FileTools.FileInteraction.Components/0.1.0`, `CanDoItAll.FileTools.FileInteraction.Markdown/0.1.0`, and `CanDoItAll.Components.Mermaid/0.1.3` packages. The Mermaid 0.1.3 package is the locally packed strict root-and-flowchart HTML-label policy proved in the Components repository.
- `source-hashes.sha256` records the final source, test, package, transcript, and browser artifact state used for this decision.

## Evidence Index

| Evidence | Purpose | Result |
| --- | --- | --- |
| `semantic-invariants.md` | Named authority, renderer, save, conflict, hostile-content, lifecycle, and no-bypass invariants | Pass |
| `behavioral-proof.md` | Architecture, per-type migration, browser, failure discovery, repair, and progression decision | Pass |
| `transcripts/test-results.txt` | Final host, FileTools, Components, integration, build, format, and hygiene results | Pass |
| `transcripts/source-architecture-audit.txt` | Dependency direction, old-owner shrink, no-browser/no-route/no-partial/service-locator, scale, and tool fallback | Pass |
| `transcripts/browser-proof.txt` | Managed runtime, per-type DOM/effect truth, geometry, console/network, persisted bytes, and cleanup | Pass |
| `transcripts/failing-first-repairs.txt` | Browser-discovered unsigned thumbnail, overlay, Mermaid, and revision-retry defects with final repair evidence | Pass |
| `transcripts/anti-stub-audit.txt` | Exact production TODO/NotImplemented/fixture/template-only audit | Pass |
| `changed-files.md` | Before/after SHA-256 manifest with provenance basis | Pass |
| `browser/*.png` | Governed positive, hostile, close-guard, conflict, retry, Mermaid, PDF, oversize, and responsive visual evidence | Pass |

## Per-Type Producer, Consumer, And Lifecycle Matrix

| Type | Profile / renderer | Authority and producer | Consumer and lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Plain text and code-like text | Built-in text view/edit | Current known-file activation and authorized content source; edit only for writable revisioned storage | Direct FileInteraction; bounded history, awaited save, controlled mode, close guard, explicit release | Oversize rejects before stream materialization; failed/cancelled/conflicting saves remain dirty |
| Markdown | Explicit Markdown package | Same current known-file session; normalized `text/markdown` | Safe Markdown view plus text edit/preview; package owns rendering and edit lifecycle | Hostile script/image/JavaScript-link input produces no script, image request, or execution |
| Mermaid | `workbench-mermaid` profile plus `WorkbenchMermaidFileView` | Same authorized text source; host registers only `.mmd`/`.mermaid` and `text/vnd.mermaid` | Shared Components Mermaid wrapper with strict security, no source actions, and HTML labels disabled at root and flowchart | Final DOM has zero `foreignObject` and zero script; renderer is never raw host markup |
| Raster | Built-in raster view | Authorized bytes only | FileInteraction creates and disposes a browser blob URL; image completed with natural width 1 | Canvas projects no managed route; no unsigned thumbnail or raw route request exists |
| PDF | Built-in PDF view | Authorized bytes only | Browser-native object over a FileInteraction-owned blob URL; read-only | Embedded actions are explicitly browser-controlled; no raw managed route or edit action |
| SVG | Inert fallback | Metadata from the authorized occurrence; bytes remain governed | No active image/markup insertion | Hostile SVG creates no image/script and cannot set the attack sentinel |
| Unknown archive | Inert fallback | Authorized occurrence metadata | Explicit unsupported state; edit disabled | No object/image/script/iframe/video/audio materialization |
| Oversized text | Pre-load 16 MiB bound | Declared size checked before content stream | Explicit error state; no renderer/editor | 16,777,217-byte input produces no content read or active element |

## Authority And Save Decision

The opaque `FileReference` authorizes the exact file, actor/runtime context, and operations. Its issuance revision is an initial optimistic-concurrency snapshot, not a permanent ban on later persisted revisions. Each save remains bound to the exact handle and requires Edit; overwrite separately requires Overwrite. Revisioned writes are compared by the storage driver against current storage truth.

Browser proof exposed that the former save target compared every later expected revision against the immutable issuance snapshot. That made every second save and conflict rebase impossible. The redundant pre-check was removed. The storage revisioned driver remains authoritative, so stale revisions still conflict while a retry against the driver-reported current revision succeeds. A new sequential-save unit test and a two-session browser conflict/rebase flow prove both sides.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `FileInteractionState` dirty/saving/conflict/error/revision | FileTools edit/save coordinators exercised by `transcripts/test-results.txt` | Direct dialog status/close guard plus `transcripts/browser-proof.txt` | Replacement/disposal and awaited state callbacks in the 72-test FileTools Components suite | Failure/cancel/conflict/overwrite denial keep local content dirty |
| Persisted file revision | Revisioned storage driver result through `AuthorizedFileSaveTarget` | FileTools save acknowledgement and editor preview revision | Produced only after successful bounded Replace; next save uses it | Sequential and stale-concurrent tests reject the immutable-snapshot shortcut |
| File catalog change revision | Authorized save target publishes after successful Replace | Project Structure/file catalog consumers refresh against current storage | One post-success publication; no publication on failure/conflict/cancel | Integration and unit negatives verify failed effects remain unadvanced |
| FileInteraction blob URL | Built-in raster/PDF renderer from authorized bytes | Browser image/object only inside active interaction | FileTools replacement/disposal owns URL revocation | Canvas route is empty; hostile SVG/archive never receive a blob renderer |
| Temporary proof lease/project | Public Project Structure/Projects APIs | Browser proof fixture only, never production feature state | Lease release and project deletion recorded at HTTP 200 | Final current lease is empty, project list omits the ID, and file scan has no residue |

## Package And Dependency Decision

- Workbench owns optional UI composition and directly references the selected interaction and Mermaid packages.
- Integration references only Infrastructure and Integration.Abstractions; it has no Workbench, Projects, Resources, Components, Mermaid, or Markdig dependency.
- Infrastructure has no FileInteraction, Components, Mermaid, or Markdig package leak.
- No renderer package is registered by discovery or service location. The builder registers only built-ins, Markdown, and the one host Mermaid profile.
- The warning-clean Web graph builds, so the checked project graph remains acyclic.

## Anti-Stub And Shallow-Pass Decision

- `transcripts/anti-stub-audit.txt` records the exact production scan and exit code. No TODO, FIXME, `NotImplementedException`, `NotSupportedException`, fixture branch, test-only branch, or template-only path remains in the SB16 production owners.
- The shallow-pass traps were: resolving a known file through a hidden FileBrowser session; preserving a route-bearing canvas thumbnail behind the new dialog; treating SVG as harmless raster; configuring Mermaid strictness only in one nested option; clearing dirty state after failed save; and faking revision retry without durable bytes. Zero-browser spies, route assertions, hostile DOM checks, sequential/concurrent saves, managed logs, and the API byte/hash read reject those implementations.

## Build, Regression, And Tool State

- Host focused unit run: 51 passed, 0 failed.
- Host focused component run: 16 passed, 0 failed.
- Real PostgreSQL integration run: 2 passed, 0 failed.
- FileTools interaction suites: 59 Core, 72 Components, and 23 Markdown tests passed.
- Components Mermaid hardening: 3 passed, 0 failed.
- Web Release build with `-warnaserror`: 0 warnings, 0 errors.
- Focused `dotnet format --verify-no-changes`: Pass. All three repositories pass `git diff --check` with line-ending notices only.
- Fresh CodeAnalytics and Components MCP calls both failed at their installed transport boundary with `Transport closed`. No snapshot/catalog result is invented; closure uses checked project/package graphs, direct source assertions, tests, and the full warning-clean build.

## Browser Artifact Index

- `browser/sb16-markdown-view-1900x1200.png`
- `browser/sb16-edit-close-guard-1900x1200.png`
- `browser/sb16-markdown-saved-1900x1200.png`
- `browser/sb16-hostile-markdown-safe-1900x1200.png`
- `browser/sb16-mermaid-strict-1900x1200.png`
- `browser/sb16-pdf-governed-1900x1200.png`
- `browser/sb16-oversize-inert-1900x1200.png`
- `browser/sb16-markdown-edit-preview-1440x900.png`
- `browser/sb16-conflict-1440x900.png`
- `browser/sb16-conflict-rebase-saved-1440x900.png`

## Downstream And Progression

SB16 closes and unlocks SB17. An unsigned route, browser-owned known-file lookup, active SVG/unknown rendering, loose Mermaid HTML label, save that clears dirty state on failure, conflict retry against stale authority, ungoverned overwrite, content-bound bypass, duplicate legacy renderer, package leak, or new page partial reopens SB16 and dependent closure proof.
