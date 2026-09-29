# Workspace completion and next-slice decision

## Current status, not a percentage

The latest implementation is API Access plus Files correction, not a new Memory change.
Compare EV02 identifies two commits: archived input followed by product work. AP01 explicitly
leaves Data Sources and Storage deferred. WS02 confirms they remain concrete active hosts.

| Surface/family | Source status at review | Next action |
|---|---|---|
| Settings shell/navigation | Extracted Core; production composes active sections | Preserve route tokens, lazy mounting, status callback and Providers redirect |
| Workspace defaults | Core extracted earlier | Regression only; profile ownership and currency behavior stay |
| Secrets | Core extracted earlier | Regression only; explicit payload lifetime and metadata-only observations stay |
| Files | Core extracted; latest WSC-R1 target acceptance present (WS01) | Preserve focused target identity proofs |
| Provider history | Core extracted earlier through original policy owner | Preserve explicit Load, authorization, versioning and confirmation |
| API Access | Independent leaf extracted in current commit | Repair AP-R1/AP-R2; no new auth protocol |
| Storage catalog administration | Original module-bound monolith (ST01–03) | Selected in this bundle |
| Storage placement recovery/owner continuation | Original concrete dialog; distinct original-intent protocol (ST07) | Keep reachable; later dedicated extraction after full audit |
| Shared Storage catalog picker field/dialog | Original Workspace family consumed outside Settings (ST08) | Preserve references/AllowAll semantics; separate follow-up extraction |
| Data Sources | Original panel and runtime-profile owner (ST09) | Later dedicated work, including switching/transfer inventory |
| SettingsRendererHost / configuration fallback | Original host family; prior Configuration.UI child extraction is not whole-host completion | Audit remaining consumers before declaring Workspace complete |

Only five of the seven locally rendered Settings sections have an extracted feature boundary;
the eighth navigation item is Providers, a redirect to Agents. This count is not a percentage
of effort or proof of all descendant families. The current canonical seam document's worked
examples are historical, not a live queue (EV04).

## Why catalog administration next

The wizard is a coherent user task and has existing owner APIs. It can be isolated without
extracting database activation/transfer or giving Core knowledge of Storage. Its runtime
reach is broad, so the task explicitly preserves consumer contracts and adds a concrete
application-regression matrix. The full wizard remains in scope; extracting only labels or
one tab would not produce a useful development loop.

Do not fold recovery into this task simply because its button appears in the catalog header.
Recovery works with original placement IDs, original projects/storage and owner continuations;
its proof is not a catalog save test. Likewise, picker membership controls what an Agent may
reference. A clean read-only adapter for this wizard does not finish those consumers.

Recommended follow-up order: this catalog slice, then audit/extract shared catalog selection,
then storage recovery, and finally the database-profile/Data Sources family. Reassess that
order against actual code/remaining risk; no later slice is authorized here. Processes and
Workbench remain outside this incremental Workspace work.
