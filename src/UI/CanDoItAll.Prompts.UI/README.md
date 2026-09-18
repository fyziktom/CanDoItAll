# Prompt Gallery rendering UI

This Razor class library owns the real Prompt Gallery rendering: the controlled search
surface, the item editor surface with its form draft, the compatibility warning surface,
their immutable presentation records, typed intents, pure text mapping, and scoped CSS.
The production page, the embedded picker and the scenario sandbox render these same
components; there is no second renderer.

The library references `CanDoItAll.Modules.Prompts.Contracts` (public gallery value
contracts and ports), `CanDoItAll.Components.BaseLib` and `Microsoft.AspNetCore.Components.Web`.
It has no dependency on the Prompts module implementation, Infrastructure, Entity
Framework, Web, AgentFramework runtime or application registration. No component injects
a service; every surface receives explicit presentation data and emits intents.

Effect ownership stays in `CanDoItAll.Modules.Prompts`: `PromptGallerySearchSession` and
`PromptGallerySearchHost` own searches, debounce and favorite writes;
`PromptGalleryEditorSession` and `PromptGalleryItemEditorHost` own the editing target,
loads, draft/version/archive writes and identity adoption; `PromptGalleryPickerDialog`,
`PromptGalleryPickerButton` and `PromptGalleryChatComposerButton` own dialogs, selection
resolution and compatibility checks; `PromptGalleryPage` owns the route, the editor dialog
and the optional Curator integration.

Build from the repository root:

    dotnet build src/UI/CanDoItAll.Prompts.UI/CanDoItAll.Prompts.UI.csproj --configuration Release /m:1

The [Prompt Gallery UI sandbox](../../Sandboxes/CanDoItAll.Prompts.UiSandbox/README.md)
exercises these surfaces with deterministic local state. The boundary decisions, behavior
matrix and validation record live in
[Prompt Gallery UI boundary](../../../docs/architecture/prompt-gallery-ui-boundary.md).
