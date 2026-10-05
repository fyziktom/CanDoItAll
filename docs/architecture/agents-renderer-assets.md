# Agents renderer asset and activation closure

This supplements the [current renderer/caller census](agents-renderer-census.csv).
The CSV records source hashes, route and symbol locations, descendant components,
scoped assets, rendering destinations and native owners. Symbol locations include
generic dialog calls and registration sites; they are evidence to follow, rather
than a claim that every lexical reference independently renders a component.
The [AC1 record](agents-completion-ac1.md) distinguishes native execution from
controlled sandbox presentation.

## Shared asset ownership

| Asset family | Actual entry and consumers | Owner and destination | Validation |
|---|---|---|---|
| Theme, Material Symbols and font | Web `Components/App.razor`; UiSandbox `Components/App.razor`; shell, forms, cards and all dialogs | Components BaseLib `wwwroot/css/output.css`, `material-symbols.css` and `fonts/material-symbols/material-symbols-rounded.woff2`; host theme remains host-owned | Source and independent published Parity/Fast at 1920×1080 scale 1; three native hosts serve identical source bytes for shared assets |
| Charts | `AddCanDoItAllCharts`, `ChartsHeadAssets` and actual Usage/Overview chart components | Components.Charts and its Blazor-ApexCharts dependency; chart JS/CSS remain shared static assets | Actual chart SVGs in all three Usage views; Apex JS fetched successfully in source/published hosts |
| Modal and focus | `Dialog`, `DialogHost` and `DialogInterop` import `Components/Modals/Dialog.razor.js` | Components BaseLib; owned native `DialogReference` remains with each opening host | Stacked native-owner tests; source/published modal footer and focus traversal. HTML `dialog` has an implicit role; a CSS `[role=dialog]` selector alone is insufficient |
| Safe copy | `AgentExecutionLogSurface` uses the real BaseLib `CopyButton` | BaseLib `wwwroot/js/copyButton.js`; native adapter supplies sanitized display text | Exact displayed text copied in an independent browser context; native log identity checked separately |
| Floating window | Neutral `ConversationShellHost`, `ConversationFloatingWindow`, catalog and active-handle contributions | Components.OverlayLib `wwwroot/js/runtime/overlay-window.js`; shell owns placement, contributors own durable operations | Native contextual handles; exact owner tests and unchanged shared JS bytes on all three apps |
| Voice and downloads | Web explicitly includes `agent-framework-voice.js` and `agent-framework-download.js`; native Voice/chat adapters call their APIs | `CanDoItAll.AgentFramework.Components/wwwroot/js`; no runtime/voice JS was copied into the light UI library | Production voice bytes exercised with real Audio objects, independent playback owners, URL revocation and explicit browser denial; native file journey validates download bytes |
| Bundled avatars | `AgentAvatarImageCatalog.BundledAvatarUrls`, `AvatarPickerSurface`, catalog and neutral participant cards | BaseLib `wwwroot/assets/identity/avatars/avatar-01.jpg` through `avatar-08.jpg`; Models owns only the safe URL catalog | Eight bundled images decode in the published picker; an owned upload decodes at 32×32; all eight source files match all three native hosts |
| Canvas and Tooltip | Existing Workflow and Project Structure surfaces; Web `CanvasLibHeadAssets`/`CanvasLibBodyAssets`; BaseLib Tooltip | Published Components CanvasLib/BaseLib; retained WF1 canvas and read-only guards | Current Components commit includes Tooltip/read-only repairs; native canvas consumer gestures, final image DLL hashes and source-equivalent canvas runtime |
| Scoped CSS | CSV `assets` column, including Runtime, Context, Avatar, History, catalog, teams, SimpleChats and retained host CSS | Original owning RCL static web asset bundle; Web imports its generated CSS and sandbox imports its own bundle | Source/published theme and scoped bundle requests; large-desktop scrolling, footer and no horizontal overflow |

Seventeen concrete source assets (Voice/download, Dialog/copy, theme/font,
eight avatars, floating window and canvas runtime) were hashed against all three
native hosts. Seven relevant native DLL hashes and the immutable image ID also
agree across those hosts. This is byte identity evidence; it does not substitute
for the browser and native-effect journeys.

## Indirect activation and retained hosts

`AgentsHomePage` owns the supported tab query, selected agent/team, header/overview
read lanes, HR readiness, defaults and Usage dialog lifetime. The light shell has
typed presentation and commands plus a native-content slot. Its tab changes replace
the current history entry; direct supported routes participate in browser Back/Forward.

`ConversationShellHost` receives registered `IConversationShellContributor`
implementations. The Agent contributor supplies `AgentFloatingConversationContent`;
SimpleChats supplies `LlmChatFloatingConversationContent`. These are current product
paths even though they are created through contribution records. Their context leases,
operation scopes, active handles and approvals remain native. Opening history or a
close-choice dialog carries the original contributor lifetime and exact dialog reference.

Generic `OpenAsync<T>` calls connect the Agent editor, teams, switch/thread dialogs,
runtime/log, Usage, native confirmations and SimpleChat editor/history/archive to their
actual callers. Workflow executor settings additionally use the native settings renderer
registry and `WorkflowNativeSettingsSlot`; the shared Configuration renderer does not
acquire executor authority. Provider History keeps its native authorization adapter,
and provider Sharing keeps its source/refresh operation owners.

The CSV names each retained host's effect responsibility and rendered destination.
The native shell, runtime, context, policy/redaction, settings persistence, grants,
admission and approval owners are intentional boundaries, not unfinished full forms.
No renderer references a new all-Agents runtime service bag.

Fourteen retained legacy components have no current product route, Razor caller,
generic dialog call or registry activation. In particular, `ScenarioHarnessPanel`
is separate from the active ScenarioHarness backend APIs; `AgentOverviewUsageList`
has test consumers only. `ContextualAgentWorkspaceWindows` is superseded by the
current shell contribution. They remain intact. A backend test-chat/model operation
alone does not establish an active provider editor screen.
