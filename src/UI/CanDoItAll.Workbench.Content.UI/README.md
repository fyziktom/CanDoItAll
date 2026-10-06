# Workbench Content UI

Independent rendering for Workbench content and files. The native feature retains project
authority and effects; the leaf receives immutable input and typed results. The text form
owns its raw draft, bounded upload reading and per-opening presentation state.

The collection window renders the actual FileBrowser and read-only FileInteraction. The
direct interaction dialog receives an original authorized content source and optional native
save port, starts in View mode, and guards dirty/conflicted/pending close requests. Mermaid
file and Markdown-fence renderers share strict options. CanvasLib owns the neutral scoped
overlay; this leaf has no Structure UI or application module dependency.

Use the [independent sandbox](../../Sandboxes/CanDoItAll.Workbench.Content.UiSandbox/README.md)
and [focused tests](../../../tests/Components/CanDoItAll.Workbench.Content.UI.Tests/README.md).
The [WB4 record](../../../docs/architecture/workbench-content-wb4.md) describes current scope,
proof and remaining implementation. This library cannot establish native file authority.
