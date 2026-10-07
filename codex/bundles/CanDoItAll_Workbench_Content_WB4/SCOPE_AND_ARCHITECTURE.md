# WB4 architecture and ownership

The unit of work is the complete content family, not all of Workbench. Extract the real
text form, collection window, direct interaction overlay, generated-image setup/state,
Progress Summary and its exports, transcript confirmation/actions and real Mermaid content
presentation. Preserve the complete graph-authoring shell already delivered by WB3.

## Preferred project shape

`CanDoItAll.Workbench.Content.UI` and `CanDoItAll.Workbench.Content.UiSandbox` are the default.
Use actual neutral BaseLib, CanvasLib, FileBrowser, FileInteraction and Mermaid primitives.
A small feature-value/contracts assembly is acceptable when native adapters must share
stable pure values. A split into a pure authoring leaf and file interaction leaf is allowed
only with a clear caller/build benefit; avoid multiplying projects just for uniformity.

Do not pull CanDoItAll.AppComponents as a whole for one neutral child. Inspect existing
Projects.Files.UI and Resources/Workspace boundaries for reusable neutral mechanisms, but do
not make Workbench depend on another feature's session to borrow its lifecycle or authority.
Reuse shared FileTools components, not copies of their markup or a second file protocol.

Allowed conceptual graph:

    native Workbench route + existing owners
      -> Content UI -> narrow neutral components / shared stable values
      -> Planning UI, Insights UI, Structure UI through native composition
    independent Content sandbox -> Content UI + bounded scenario owners

Forbidden: Content UI -> Workbench implementation/EF/Infrastructure/provider/runtime;
Foundation/AppComponents/Processes -> Content UI; Planning/Insights/Structure adding a reverse
reference only to mount a new content family. An old native renderer hidden inside a slot
is not an extracted WB4 surface. A slot is legitimate for the explicitly deferred family.

## One semantic owner per responsibility

| Responsibility | Retained owner |
|---|---|
| Project, node occurrence, protected targets | Native project admission and Workbench mutation owners |
| Asset payload preparation, normalization and limits | Existing ProjectAssetCreationService and actual generators |
| Storage, known-file scope, content, save and download grants | Existing storage/FileTools native integrations |
| External generation, transcript provider execution | Existing provider execution/image services and queue |
| Placement, references, rich metadata and graph persistence | Existing Workbench owners |
| Visible input, raw validation, derived presentation, element/focus handling | Actual Content renderer or per-opening presentation session |
| Which view exists and its source/actor/lifetime | Native feature host |

No `IServiceProvider`, injected native class behind `object`, giant command dictionary or
universal service-bag view model in the leaf. Use cohesive typed intents and explicit ports.
Do not move whole mixed source files to Contracts: public values may be separated and type-
forwarded where needed. Preserve serialization and XML/API descriptions for moved contracts.

## Native composition and shared canvases

The WB3 leaf continues constructing the actual canvas. Content UI mounts through the native
composition that already owns openings. WB2 Selection actions reach the same native owners;
they do not directly obtain file grants. Preserve every current toolbar/context/action entry.
The new sandbox mounts actual controls, two independent instances, full uploads and viewers;
no database or fake production IServiceCollection gives a misleading independent pass.

Feature scoped CSS, scripts and Tailwind inputs move with the renderer. Register existing
shared assets once in the supported order. Record compression-decoded equivalence separately
from byte equality. Public compatibility wrappers/type forwards are allowed when needed by
real callers; do not retain two active rendering implementations or remove legacy behavior
without a caller audit.

## Scope limits

No new import/export formats, OCR, speech transcription, automatic task creation from model
text, cross-instance file sync, provider fallback or new replay/export/delete capability.
No DB migration or broad persistence/permission redesign. If a small native parameter or
result is necessary to carry the original target or known phase, make that targeted change
and test every actual caller. Deep new protocol issues require mapping, not speculative fixes.
