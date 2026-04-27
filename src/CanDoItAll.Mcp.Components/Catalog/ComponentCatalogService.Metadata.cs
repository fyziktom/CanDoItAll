using System.Text.RegularExpressions;

namespace CanDoItAll.Mcp.Components.Catalog;

public sealed partial class ComponentCatalogService
{
    private static readonly Regex ComponentReferenceRegex = new(@"<(?<name>[A-Z][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
    private static readonly Regex RouteDirectiveRegex = new(@"^\s*@page\s+""(?<route>[^""]+)""", RegexOptions.Compiled);
    private static readonly IReadOnlyList<string> CanvasLibStylesheets =
    [
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/shell/01-layout-and-shell.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/chrome/02-toolbar-and-windows.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/panels/03-help-settings-and-preview.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/scene/04-scene-and-nodes.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/overlays/05-overlays-and-composer.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/responsive/06-motion-and-responsive.css"
    ];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CssNotesByComponent = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] =
        [
            "Uses the shared BaseLib button variants, sizes, and tones from `_content/CanDoItAll.Components.BaseLib/css/output.css`.",
            "Prefer `ButtonStyle`, `Variant`, and `Size` before adding page-local button styling."
        ],
        ["CopyButton"] =
        [
            "Uses the shared BaseLib button and inline icon-copy styles from `_content/CanDoItAll.Components.BaseLib/css/output.css`.",
            "Prefer `DisplayMode`, `Size`, `ButtonStyle`, and `Variant` before adding page-local copy affordances or custom clipboard handlers."
        ],
        ["PageScaffold"] =
        [
            "Owns page-level spacing and max-width conventions through the shared BaseLib output CSS.",
            "Use scaffold slots before introducing page-local wrapper structure."
        ],
        ["Tabs"] =
        [
            "Uses the shared Tailwind-owned `cad-tabs` styles from `_content/CanDoItAll.Components.BaseLib/css/output.css`; it should not depend on legacy `zy-*` selectors.",
            "Prefer `Variant`, `Tone`, `BorderMode`, and `OverflowMode` before page-local styling, then use the root `Class` parameter only for shell-level atmosphere."
        ],
        ["SecondaryTabs"] =
        [
            "Secondary tabs stay lighter than full `Tabs` and should read as a compact route or scenario switch instead of a content container.",
            "Use shared variants and text states first; do not restyle them into a second full tabs system."
        ],
        ["StatusBadge"] =
        [
            "Maps semantic tones to the shared status surface palette in the generated BaseLib stylesheet.",
            "Status chips should communicate state, not replace headings or summaries."
        ],
        ["Notification"] =
        [
            "Renders fixed viewport toast stacks from one mounted host using Tailwind utility classes in `_content/CanDoItAll.Components.BaseLib/css/output.css`.",
            "`NotificationMessage.Position` and the `Notify(..., position: ...)` overload override the host `Position` per message; use that instead of page-local fixed wrappers.",
            "The close affordance is intentionally the compact X control to preserve notification width for summary and detail copy."
        ],
        ["Tooltip"] =
        [
            "Renders the active `TooltipService` state through one mounted host using Tailwind utility classes in `_content/CanDoItAll.Components.BaseLib/css/output.css`.",
            "`TooltipOptions.Position` controls service-opened tooltip placement; choose a `TooltipPosition` side, corner, or edge alignment instead of custom absolute-positioning CSS."
        ],
        ["TooltipTarget"] =
        [
            "Wraps trigger content and forwards hover/focus coordinates to `TooltipService` with shared Tailwind tooltip rendering.",
            "`TooltipTarget Position` uses the same `TooltipPosition` enum as service-opened tooltips, so declarative and imperative tooltip placement rules stay aligned."
        ],
        ["CanvasWorkbench"] =
        [
            "Uses the shared CanvasLib workbench stylesheets exposed by `<CanvasLibHeadAssets />` under `_content/CanDoItAll.Components.CanvasLib/css/workbench/...` plus the typed `CanvasThemeTokenPack` theme vocabulary.",
            "Toolbar, floating windows, preview cards, and diagnostics share the same `cw-*` token space."
        ],
        ["CanvasCalendar"] =
        [
            "Uses the shared canvas stylesheet and the same theme token pack as the workbench surfaces.",
            "Calendar boundary previews should stay aligned with the runtime token vocabulary rather than inventing parallel styles."
        ],
        ["CanvasBoundaryCard"] =
        [
            "Boundary cards share the same canvas preview card treatment and `cw-*` token space as the runtime previews.",
            "Use boundary cards for proof and documentation surfaces, not for runtime authoring content."
        ]
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultCssNotesByLibrary = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["BaseLib"] =
        [
            "BaseLib components render against `_content/CanDoItAll.Components.BaseLib/css/output.css`.",
            "Typography, spacing, surfaces, and status tones are shared through the BaseLib token system."
        ],
        ["CanvasLib"] =
        [
            "CanvasLib components render against the shared workbench stylesheets exposed by `<CanvasLibHeadAssets />`.",
            "Canvas surfaces also use the typed `CanvasThemeTokenPack` so runtime and preview assets stay aligned."
        ]
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BaseLibCssSourceFilesByComponent = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Tabs"] = [@"Tailwind\navigation\tabs.css"],
        ["TabsItem"] = [@"Tailwind\navigation\tabs.css"],
        ["SecondaryTabs"] = [@"Tailwind\navigation\tabs.css"],
        ["RibbonTabs"] = [@"Tailwind\navigation\tabs.css"],
        ["PageHeader"] = [@"Tailwind\navigation\page-header.css"],
        ["FilterBar"] = [@"Tailwind\navigation\page-header.css"],
        ["TreeView"] = [@"Tailwind\navigation\treeview.css"],
        ["TreeViewNodeRow"] = [@"Tailwind\navigation\treeview.css"],
        ["TagEditor"] = [@"Tailwind\forms\tag-editor.css"],
        ["Button"] = [@"Tailwind\controls\buttons.css"],
        ["CopyButton"] = [@"Tailwind\controls\buttons.css"],
        ["Badge"] = [@"Tailwind\controls\badges.css"],
        ["BadgesGroup"] = [@"Tailwind\controls\badges.css"],
        ["Chip"] = [@"Tailwind\controls\badges.css"],
        ["ChipRow"] = [@"Tailwind\controls\badges.css"],
        ["Pill"] = [@"Tailwind\controls\badges.css"],
        ["PillList"] = [@"Tailwind\controls\badges.css"],
        ["StatusBadge"] = [@"Tailwind\controls\badges.css"],
        ["Alert"] = [@"Tailwind\feedback\alerts.css"],
        ["Callout"] = [@"Tailwind\feedback\alerts.css"],
        ["EmptyState"] = [@"Tailwind\feedback\alerts.css"],
        ["LoadingState"] = [@"Tailwind\feedback\alerts.css"],
        ["Notification"] = [@"Tailwind\feedback\alerts.css"],
        ["HelpPopover"] = [@"Tailwind\surfaces\overlays.css"],
        ["Tooltip"] = [@"Tailwind\surfaces\overlays.css"],
        ["TooltipTarget"] = [@"Tailwind\surfaces\overlays.css"],
        ["Dialog"] = [@"Tailwind\surfaces\overlays.css"]
    };
    private static readonly IReadOnlyDictionary<string, ComponentGuidanceDocument> GuidanceByFamily = new Dictionary<string, ComponentGuidanceDocument>(StringComparer.OrdinalIgnoreCase)
    {
        ["Badges"] = new(
            [
                "Compact status, counts, labels, and short categorical emphasis."
            ],
            [
                "Replacing headings, summaries, or full explanation copy."
            ],
            [
                "Keep badge copy short and scannable.",
                "Use semantic tone first and avoid turning badges into miniature cards."
            ]),
        ["Buttons"] = new(
            [
                "Primary, secondary, and inline actions with shared emphasis rules."
            ],
            [
                "Using button styling as generic layout chrome."
            ],
            [
                "Use shared variants and sizes before page-local classes.",
                "If many actions compete, reduce the set or move secondary actions into lighter navigation."
            ]),
        ["Cards"] = new(
            [
                "Grouped surfaces for summaries, actions, metrics, and section-level content."
            ],
            [
                "Building very long pages by stacking many cards that users consume one mode at a time."
            ],
            [
                "Use cards when the surface itself is the interaction or grouping unit.",
                "If adjacent cards represent alternate views of the same object, promote the pattern into tabs or a split shell."
            ]),
        ["DataVisualization"] = new(
            [
                "Charts, data grids, axes, and quantitative progress display."
            ],
            [
                "Decorative charts that do not help someone operate or decide."
            ],
            [
                "Keep the chart or grid tied to a decision or workflow.",
                "Prefer shared display components before inventing local chart shells."
            ]),
        ["Feedback"] = new(
            [
                "Alerts, empty states, loading surfaces, notifications, and contextual help."
            ],
            [
                "Primary page structure or decorative marketing copy."
            ],
            [
                "Use feedback components to orient, explain state, and suggest the next step.",
                "Keep tone calm and utility-first; if the state needs a real workflow branch, switch layout instead of adding louder alert chrome."
            ]),
        ["Forms"] = new(
            [
                "Data entry, configuration, validation, and small editing workflows."
            ],
            [
                "Hand-built field wrappers when shared field, row, and section components already exist."
            ],
            [
                "Prefer `FormField`, `FormRow`, and shared input parameters before raw input markup.",
                "Use inline help and validation to reduce scanning friction instead of adding extra layout wrappers."
            ]),
        ["Identity"] = new(
            [
                "Presence, avatars, icons, and attribution details that improve scanning."
            ],
            [
                "Decorative icon usage with no orientation value."
            ],
            [
                "Use identity components to help recognition and hierarchy.",
                "If an icon does not improve scanning, remove it."
            ]),
        ["Layout"] = new(
            [
                "Page structure, responsive regions, spacing rhythm, and workspace composition."
            ],
            [
                "Using layout primitives to simulate semantic components that already exist."
            ],
            [
                "Start with layout intent, not wrappers.",
                "If content splits into modes, use tabs, list-detail, or rails before accepting a long single-column page."
            ]),
        ["Lists"] = new(
            [
                "Rows, grouped metadata, list/detail workspaces, and selection-driven review surfaces."
            ],
            [
                "Using list shells when the user is not actually selecting between records or views."
            ],
            [
                "Keep list density readable and let the detail area stay focused.",
                "When the detail area becomes tall and mode-heavy, segment it with tabs instead of stacking more sections."
            ]),
        ["Modals"] = new(
            [
                "Dialogs and transient overlays that keep the user in context."
            ],
            [
                "Permanent page structure or long-form navigation."
            ],
            [
                "Use overlays for focused interruption, not as a substitute for page composition.",
                "Keep modal flows short and explicit."
            ]),
        ["Navigation"] = new(
            [
                "Tabs, headers, trees, steps, toolbars, and route-level orientation."
            ],
            [
                "Passive content layout that should live in cards, lists, or scaffold regions instead."
            ],
            [
                "Choose the lightest navigation that still makes state obvious.",
                "Use tabs aggressively when it reduces vertical sprawl and keeps one focused working surface visible."
            ]),
        ["Storage"] = new(
            [
                "Storage-oriented summaries, badges, and capacity surfaces."
            ],
            [
                "General-purpose layout or status surfaces outside storage workflows."
            ],
            [
                "Keep storage displays quantitative and utility-first.",
                "Reuse shared summary surfaces before introducing storage-specific local wrappers."
            ]),
        ["Typography"] = new(
            [
                "Heading rhythm, copy hierarchy, captions, dividers, and text emphasis."
            ],
            [
                "Replacing semantic structure with arbitrary font-size utility classes."
            ],
            [
                "Use typography components to keep scanning and hierarchy consistent.",
                "Prefer shared text styles before page-local text tuning."
            ]),
        ["Calendar"] = new(
            [
                "Calendar-specific runtime surfaces, editors, and selection workflows."
            ],
            [
                "Generic app layout that should stay in BaseLib."
            ],
            [
                "Keep calendar interactions tied to the shared canvas model and workbench surfaces."
            ]),
        ["Core"] = new(
            [
                "Canvas runtime infrastructure such as floating windows and accessibility layers."
            ],
            [
                "General product UI outside the workbench runtime."
            ],
            [
                "Treat core canvas components as runtime infrastructure, not page-level decoration."
            ]),
        ["Diagnostics"] = new(
            [
                "Canvas diagnostics and runtime debugging overlays."
            ],
            [
                "Routine operator surfaces that should stay calm and minimal."
            ],
            [
                "Keep diagnostics discoverable but secondary to the main work surface."
            ]),
        ["Graph"] = new(
            [
                "Canvas graph composition, overlays, interaction, and primitives."
            ],
            [
                "General layout outside graph or scene authoring flows."
            ],
            [
                "Align graph surfaces with the shared canvas contract and interaction model."
            ]),
        ["Workbench"] = new(
            [
                "Canvas workbench shells and stage-level runtime surfaces."
            ],
            [
                "Lightweight page regions that should use BaseLib layout."
            ],
            [
                "Use workbench components when the page is truly a canvas workspace, not just a dense form."
            ])
    };
    private static readonly IReadOnlyDictionary<string, string> SummaryByComponent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Tabs"] = "Shared BaseLib tabs for segmenting dense content into focused panels so workspace pages do not become one long scroll.",
        ["TabsItem"] = "Child panel definition for `Tabs` that supplies the tab label, optional icon or badge, and the pane content for the selected view.",
        ["SecondaryTabs"] = "Shared BaseLib secondary tab strip for lighter scenario switches, route toggles, and inline subnavigation.",
        ["Steps"] = "Shared BaseLib sequential progress navigation for wizard-like flows where order matters more than free switching.",
        ["PageHeader"] = "Shared BaseLib page-header component for titles, lead copy, and action context at the top of a workspace.",
        ["Toolbar"] = "Shared BaseLib toolbar component for dense inline actions, filters, and control clusters around a working surface.",
        ["CopyButton"] = "Shared BaseLib clipboard action for copying a selected input slice or the full nearby value, with text, icon-button, and tiny inline icon variants for obvious copy affordances.",
        ["TreeView"] = "Shared BaseLib tree navigation component for hierarchical exploration and selection.",
        ["EmptyState"] = "Shared BaseLib empty-state component for zero-data, no-selection, and first-run orientation surfaces.",
        ["LoadingState"] = "Shared BaseLib loading-state component for progress and transition surfaces that should feel intentional instead of blank.",
        ["Alert"] = "Shared BaseLib alert component for actionable status and inline system feedback.",
        ["Notification"] = "Shared BaseLib notification host for service-triggered toast stacks with per-message viewport positioning and compact X dismiss controls.",
        ["Tooltip"] = "Shared BaseLib tooltip host for service-triggered contextual help with side, corner, and edge-aligned placement options.",
        ["TooltipTarget"] = "Shared BaseLib tooltip trigger wrapper for hover and focus help that can choose a local TooltipPosition without page-level tooltip plumbing.",
        ["FormField"] = "Shared BaseLib form-field wrapper for label, helper, validation, and control alignment.",
        ["TextBox"] = "Shared BaseLib text input component for standard single-line entry flows.",
        ["DropDown"] = "Shared BaseLib selection input component for shared option picking flows.",
        ["SectionCard"] = "Shared BaseLib section surface for grouped content blocks, often inside a scaffold, grid, or tabs panel.",
        ["PageScaffold"] = "Shared BaseLib page shell that coordinates header, lead, and workspace regions so dense pages can use width intentionally.",
        ["ListDetailShell"] = "Shared BaseLib master-detail shell for selection-driven workspaces with a stable list region and a focused detail region.",
        ["CanvasWorkbench"] = "Shared CanvasLib workbench surface for graph-based authoring, tool windows, and dense desktop-first runtime composition.",
        ["CanvasCalendar"] = "Shared CanvasLib calendar surface for schedule navigation, selection, and event-oriented workspace views.",
        ["CanvasFloatingWindow"] = "Shared CanvasLib floating window surface for movable auxiliary panels inside the workbench runtime.",
        ["EmptyStateOverlay"] = "Shared CanvasLib empty-state overlay for graph and canvas surfaces with no active content."
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> TagsByComponent = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Tabs"] =
        [
            "progressive-disclosure",
            "reduce-scroll",
            "segmented-workspace",
            "panel-switching",
            "settings-rail",
            "overflow"
        ],
        ["TabsItem"] =
        [
            "tab-panel",
            "panel-content",
            "badge"
        ],
        ["SecondaryTabs"] =
        [
            "subnavigation",
            "filters",
            "scenario-switch"
        ],
        ["Steps"] =
        [
            "wizard",
            "progress",
            "sequential-navigation"
        ],
        ["CopyButton"] =
        [
            "clipboard",
            "copy-action",
            "inline-action",
            "link",
            "path",
            "hash",
            "token"
        ],
        ["Notification"] =
        [
            "feedback",
            "toast",
            "notification-position",
            "overlay-service",
            "dismiss"
        ],
        ["Tooltip"] =
        [
            "contextual-help",
            "placement",
            "tooltip",
            "overlay-service",
            "hover"
        ],
        ["TooltipTarget"] =
        [
            "contextual-help",
            "placement",
            "tooltip-trigger",
            "hover",
            "focus"
        ],
        ["PageScaffold"] =
        [
            "page-shell",
            "layout",
            "workspace"
        ],
        ["SectionCard"] =
        [
            "panel",
            "surface",
            "section"
        ],
        ["ListDetailShell"] =
        [
            "master-detail",
            "detail-pane",
            "split-view"
        ]
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AdditionalDependenciesByComponent = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Tabs"] = ["TabsItem"],
        ["Steps"] = ["StepsItem"]
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ParameterDescriptionsByComponent = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Tabs"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AriaLabel"] = "Accessible label announced for the whole tab list when the surrounding heading is not enough.",
            ["BorderMode"] = "Controls whether the tab buttons render with the default border, a softer border treatment, or no border at all.",
            ["Change"] = "Raised after a user-initiated tab switch so flows can react to the new selected panel.",
            ["ChildContent"] = "Optional fallback child content path when the tab items are declared directly instead of inside `TabItems`.",
            ["Class"] = "Adds extra Tailwind classes to the tabs root for shell-level atmosphere without replacing the shared internal structure.",
            ["OverflowMode"] = "Chooses whether long tab strips auto-decide, wrap into multiple rows, or stay single-row and scroll horizontally.",
            ["RenderMode"] = "Chooses server rendering of only the active panel or client rendering of every panel for faster instant switches.",
            ["SelectedIndex"] = "Zero-based selected tab index among the visible tabs.",
            ["SelectedIndexChanged"] = "Two-way binding callback for the selected tab index.",
            ["Style"] = "Appends inline styles to the tabs root when a one-off shell tweak is unavoidable.",
            ["TabItems"] = "Slot that contains the `TabsItem` children defining labels and panel content.",
            ["TabPosition"] = "Moves the tab strip to the top, bottom, left, or right of the panel surface.",
            ["Tone"] = "Advanced accent color for the selected state. Use it to shift emphasis, not to rescue readability.",
            ["Variant"] = "Shared shape and density preset for the tabs surface, such as workspace, modal, or workstation styling."
        },
        ["TabsItem"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdditionalAttributes"] = "Passes extra attributes and classes to the rendered tab button.",
            ["BadgeText"] = "Optional supporting badge shown inside the tab button for counts or short status markers.",
            ["ChildContent"] = "Panel content rendered when the tab becomes active.",
            ["Disabled"] = "Keeps the tab visible but unavailable for selection.",
            ["Icon"] = "Optional icon token rendered before the tab text.",
            ["Text"] = "Primary tab label. If omitted, the component falls back to a stable generic title.",
            ["Visible"] = "Removes the tab from the rendered strip when false."
        },
        ["SecondaryTabs"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header"] = "Optional content rendered above the secondary tab strip for local orientation.",
            ["Items"] = "Collection of lightweight tab items used for compact scenario switching or route toggles.",
            ["SelectedKey"] = "Currently selected item key.",
            ["SelectedKeyChanged"] = "Two-way binding callback for the selected item key."
        },
        ["SectionCard"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Description"] = "Supporting copy for the section header.",
            ["Title"] = "Section heading rendered by the shared panel shell."
        },
        ["PageScaffold"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ChildContent"] = "Main page body region where tabs, split views, grids, and sections should live.",
            ["Header"] = "Top page header region for orientation and primary actions.",
            ["Lead"] = "Optional lead content that introduces the workspace before the main body.",
            ["SecondaryRail"] = "Optional side rail for supporting context, shortcuts, or meta information."
        },
        ["Notification"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Position"] = "Default viewport stack used for messages that do not set `NotificationMessage.Position`; prefer per-message positions when the toast belongs near a specific action region."
        },
        ["TooltipTarget"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ChildContent"] = "Trigger content that opens the tooltip on hover and focus.",
            ["CloseOnMouseLeave"] = "Closes the tooltip when the pointer leaves the trigger; keep enabled for short hover help.",
            ["Delay"] = "Optional wait before showing the tooltip so dense toolbars do not flicker during quick pointer movement.",
            ["Duration"] = "Optional lifetime after opening; set null or a longer value only for tooltip content that needs extra reading time.",
            ["Position"] = "Tooltip placement relative to the pointer; choose the side or corner that keeps the bubble inside the viewport and away from the next likely action.",
            ["TabIndex"] = "Keyboard focus order for the trigger wrapper.",
            ["TestId"] = "Optional stable selector for Playwright checks of non-default tooltip placements.",
            ["Text"] = "Short plain-text tooltip content for compact help.",
            ["TooltipClass"] = "Additional Tailwind classes for the tooltip surface, without replacing the shared overlay structure.",
            ["TooltipContent"] = "Rich tooltip content rendered with access to the shared TooltipService.",
            ["TriggerClass"] = "Additional Tailwind classes for the inline trigger wrapper."
        }
    };
    private static readonly IReadOnlyDictionary<string, string> DefaultParameterDescriptionsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AdditionalAttributes"] = "Passes unmatched HTML attributes through to the rendered element.",
        ["ChildContent"] = "Slot for the component body content.",
        ["Class"] = "Adds extra Tailwind classes to the component root without replacing the shared structure.",
        ["Style"] = "Appends inline styles to the component root."
    };
    private static readonly IReadOnlyDictionary<string, ComponentGuidanceDocument> GuidanceByComponent = new Dictionary<string, ComponentGuidanceDocument>(StringComparer.OrdinalIgnoreCase)
    {
        ["Grid"] = new(
            [
                "Page and section shells that need explicit tracks.",
                "Responsive layouts that should be driven by `Columns*`, `Rows*`, or `ColumnTemplate*` parameters."
            ],
            [
                "Simple one-dimensional flows that `Stack` already expresses cleanly."
            ],
            [
                "Prefer `Columns*`, `Rows*`, and `ColumnTemplate*` before raw CSS grid utilities.",
                "Use `Gap`, `ColumnGap`, `RowGap`, `AlignItems`, and `JustifyContent` before adding wrapper markup.",
                "Let child `Row` components inherit the parent track variables when the same-row content should collapse with the parent grid."
            ]),
        ["Row"] = new(
            [
                "Nested layout bands inside a `Grid`.",
                "Same-row groups that need their own track count or need to collapse responsively."
            ],
            [
                "Simple horizontal button groups or inline content that `Stack` already handles."
            ],
            [
                "`Row` spans the available grid width and can inherit the parent grid track model automatically.",
                "Use `Columns*` or `ColumnTemplate*` on the row only when that row needs a different track model than its parent.",
                "Use responsive `ColumnsSm` and `ColumnsMd` on the row when sibling columns should wrap under each other."
            ]),
        ["Column"] = new(
            [
                "Content cells inside a `Row`.",
                "Leaf layout containers that need local flex alignment plus a grid placement."
            ],
            [
                "Standalone page shells where `Stack`, `SectionCard`, or `Grid` would express the intent more clearly."
            ],
            [
                "Treat `Column` as both a grid item and a local flex container.",
                "Use `Orientation`, `Gap`, `AlignItems`, and `JustifyContent` instead of ad-hoc flex wrappers.",
                "Use `Size*` only when the row is using span-based sizing; otherwise let inherited track definitions place the column."
            ]),
        ["Stack"] = new(
            [
                "One-dimensional vertical or horizontal flows.",
                "Grouped copy, pill rows, action clusters, and compact sidebars."
            ],
            [
                "Two-dimensional layouts that need explicit tracks across breakpoints."
            ],
            [
                "Reach for `Stack` first when the structure is linear.",
                "When a panel starts needing left/right regions or breakpoint-driven track changes, move to `Grid` or `Row` plus `Column`.",
                "Use `GapScale`, `Wrap`, `AlignItems`, and `JustifyContent` before custom flex classes."
            ]),
        ["FormRow"] = new(
            [
                "Standard paired or evenly split form fields.",
                "Form surfaces that should follow the shared label and spacing rhythm."
            ],
            [
                "Complex asymmetric layouts where `Grid` or `Row` provides clearer control."
            ],
            [
                "Use `FormRow` for common field pairings before creating a custom form wrapper.",
                "Combine `FormRow` with `FormField` so label, helper text, and control spacing stay inside BaseLib."
            ]),
        ["Tabs"] = new(
            [
                "Workspace pages whose sections are mutually exclusive and would otherwise become a long vertical scroll.",
                "Settings, review, and case-management flows that need progressive disclosure inside one stable shell.",
                "Horizontal or vertical panel switching where one selected pane should dominate the viewport."
            ],
            [
                "Short inline toggles or route chips where `SecondaryTabs` is the lighter fit.",
                "Strictly sequential flows where `Steps` should communicate order and progress.",
                "Comparison layouts where users need multiple panes visible at the same time."
            ],
            [
                "Prefer `Tabs` before stacking several `SectionCard` blocks that users read one at a time.",
                "Start with the readable default surface; use `Tone` as an advanced accent and keep shell customization on the root `Class` only.",
                "Use `OverflowMode.Wrap` or `OverflowMode.Scroll` intentionally for longer label sets, and move to `TabPosition.Left` for settings rails.",
                "Keep the strip focused. If the labels stop scanning quickly, split the workflow instead of adding more tabs."
            ]),
        ["TabsItem"] = new(
            [
                "A focused pane inside `Tabs` with a task-oriented label and one coherent content region."
            ],
            [
                "Standalone section surfaces that should just be cards or layout blocks.",
                "Decorative labels with no associated panel content."
            ],
            [
                "Keep labels short and specific, then use `BadgeText` sparingly for counts or short status hints.",
                "If a pane needs a unique shell, customize the parent `Tabs` root instead of rebuilding the inner tab button structure."
            ]),
        ["SecondaryTabs"] = new(
            [
                "Compact scenario switches, route segments, and inline mode changes.",
                "Lightweight navigation where the surrounding page or panel already owns the content shell."
            ],
            [
                "Dense content panels that need a visible selected seam and a shared panel surface; use `Tabs` there instead.",
                "Sequential progress indicators that should use `Steps`."
            ],
            [
                "Reach for `SecondaryTabs` when users are changing context inside an already established shell.",
                "If switching the tab also switches a substantial content panel and reduces page scroll, promote the pattern to `Tabs`."
            ]),
        ["Steps"] = new(
            [
                "Wizard-like flows, checklists, and ordered progress where completion state matters.",
                "Navigation that should teach the user what comes next."
            ],
            [
                "Free workspace switching between independent panels where tabs would be clearer."
            ],
            [
                "Use `Steps` when sequence is the message. If users need to hop between peer sections, use `Tabs` instead."
            ]),
        ["PageScaffold"] = new(
            [
                "Full-page shells that need a stable header and a deliberate content region.",
                "Dense pages that should use width intentionally instead of defaulting to a narrow single-column stack."
            ],
            [
                "Tiny inline content regions where a local `SectionCard`, `Grid`, or `Stack` is enough."
            ],
            [
                "Use tabs, split views, or grid-based regions inside the scaffold body when content naturally breaks into modes or panes.",
                "Avoid turning the scaffold body into a long stack of unrelated cards when navigation or segmentation would scan better."
            ]),
        ["SectionCard"] = new(
            [
                "Grouped content blocks inside a page, grid cell, or tabs panel."
            ],
            [
                "Using many stacked cards to simulate navigation between mutually exclusive content modes."
            ],
            [
                "If several section cards represent alternate views of the same object, promote them into `Tabs` so the page does not grow vertically without limit."
            ]),
        ["CopyButton"] = new(
            [
                "Copying links, file paths, hashes, tokens, invite codes, commands, and other obvious clipboard targets.",
                "Inline copy affordances next to a nearby link, input, path, hash, or read-only value where a one-click copy action is expected."
            ],
            [
                "Primary page actions or ambiguous icon-only controls where the copied target is not visually obvious.",
                "Custom copy wrappers when the shared component can target the nearby element with `TargetElementId` or `TargetSelector`."
            ],
            [
                "Use `CopyButton` automatically for obvious copyable values such as links, paths, hashes, IDs, and tokens instead of building local clipboard logic.",
                "Default to `DisplayMode=IconOnly` with `Size=ExtraSmall` or `Size=Small` when the copy target is adjacent and self-explanatory.",
                "Use `DisplayMode=Text`, `TextWithIcon`, or `IconButton` when the control stands alone or the copy target needs more context.",
                "Let the built-in copied-state check icon provide lightweight confirmation instead of adding extra status chrome for simple copy flows."
            ]),
        ["Notification"] = new(
            [
                "Transient confirmations, save results, background task outcomes, and non-blocking feedback that should not interrupt the current workflow.",
                "Short service-driven messages rendered from a single mounted `<Notification />` host."
            ],
            [
                "Field validation, long instructions, or content that must remain visible until the user acts.",
                "Confirmation, destructive, or decision-making flows where `DialogService` or an inline `Alert` is clearer."
            ],
            [
                "Default to `TopRight` for ordinary desktop product toasts because it stays near global chrome and avoids the main reading column.",
                "Use `TopCenter` only for global high-importance but still non-blocking status; if it requires a decision, use `DialogService` instead.",
                "Use `BottomCenter` when the page header or top navigation is dense, when mobile reach matters, or when the toast should not cover top actions.",
                "Use `BottomLeft`, `BottomRight`, `CenterLeft`, or `CenterRight` when feedback belongs near a side rail, list pane, or action region; keep the center of the working surface unobscured.",
                "Avoid central notification stacks for routine messages; reserve `Center` or center-side positions for urgent transient feedback that still does not need a modal.",
                "Keep notification copy short, keep the close affordance as the compact X control, and set `NotificationMessage.Position` or `Notify(..., position: ...)` when a message needs a different placement from the host default."
            ]),
        ["Tooltip"] = new(
            [
                "Short contextual help for icons, dense toolbar actions, small controls, and terms that need quick clarification.",
                "Service-triggered tooltip content rendered from the single mounted `<Tooltip />` host."
            ],
            [
                "Required instructions, validation errors, long explanatory copy, or content users need to compare while working.",
                "Replacing visible labels on controls that are not already obvious from context."
            ],
            [
                "Default to `Top` or `Right` when there is enough room, choosing the side that keeps the bubble out of the main reading path.",
                "Use `Bottom` for triggers close to the top edge and `Top` for triggers near the bottom edge, sticky footers, or lower toolbars.",
                "Use `Left` or `Right` for inline controls and icon clusters where vertical placement would cover neighboring rows.",
                "Use corner and edge placements such as `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`, `LeftTop`, and `RightBottom` near viewport, card, toolbar, or panel corners so the tooltip stays visible and avoids adjacent controls.",
                "Prefer `TooltipTarget Position` for declarative hover and focus help; use `TooltipOptions.Position` when opening through `TooltipService` from custom pointer logic.",
                "Validate non-default placements with Playwright at the sandbox viewport sizes that match the target workflow, especially mobile or dense toolbar cases."
            ]),
        ["TooltipTarget"] = new(
            [
                "Declarative hover and focus help around a single trigger element.",
                "Icon-only or compact controls where the trigger should stay local to the markup."
            ],
            [
                "Complex help content that needs persistent reading or multiple interactions.",
                "Cases where visible helper text, a `HelpPopover`, or an inline `Alert` would be more discoverable."
            ],
            [
                "Pick the same `TooltipPosition` rules as the tooltip service: choose the side or corner that keeps the bubble inside the viewport and away from the next likely click target.",
                "Keep `Text` short; use `TooltipContent` only for compact rich content.",
                "Use `Delay` for crowded controls to avoid flicker, and keep `CloseOnMouseLeave` true for normal hover help."
            ]),
        ["ListDetailShell"] = new(
            [
                "Selection-driven workspaces with a stable list region and a detail pane.",
                "Review flows where the detail pane should stay focused while the selected record changes."
            ],
            [
                "Simple pages without a persistent selection model."
            ],
            [
                "When the detail pane starts stacking multiple dense regions, place `Tabs` inside the detail pane instead of growing one very tall view."
            ]),
        ["SectionHead"] = new(
            [
                "Section titles, lead copy, and compact header blocks above a panel."
            ],
            [
                "Decorative hero shells that should be handled by page-level layout components."
            ],
            [
                "Use `SectionHead` to keep heading rhythm consistent before adding panel-local typography markup.",
                "Let `SectionHead` own the section introduction so the panel body can focus on content."
            ]),
        ["StatsGrid"] = new(
            [
                "Dashboard-style metric rows and summary tiles."
            ],
            [
                "Arbitrary card mosaics with mixed content density."
            ],
            [
                "Use `StatsGrid` when the content is truly metric-first.",
                "If the cards become multi-purpose sections, switch back to `Grid` plus panel components."
            ])
    };
    private static readonly IReadOnlyDictionary<string, ComponentGuidanceDocument> DefaultGuidanceByLibrary = new Dictionary<string, ComponentGuidanceDocument>(StringComparer.OrdinalIgnoreCase)
    {
        ["BaseLib"] = new(
            [
                "Shared app structure and interactive UI surfaces."
            ],
            [
                "Page-local structural CSS when the shared component parameters already express the intent."
            ],
            [
                "Prefer component parameters, variants, and layout primitives before custom structural classes.",
                "When content naturally breaks into modes or stages, use tabs, split layouts, or list-detail shells before stacking one long page.",
                "Use sandbox and product examples to mirror established composition patterns."
            ]),
        ["CanvasLib"] = new(
            [
                "Workbench, canvas, and runtime preview surfaces."
            ],
            [
                "Generic page shells that should stay in BaseLib."
            ],
            [
                "Keep runtime and preview surfaces aligned with the shared canvas contracts and token pack."
            ])
    };
    private static readonly IReadOnlySet<string> ConsumerProjectExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CanDoItAll.ComponentKit",
        "CanDoItAll.Components.BaseLib",
        "CanDoItAll.Components.CanvasLib",
        "CanDoItAll.Components.Common",
        "CanDoItAll.Mcp.Components",
        "CanDoItAll.Mcp.Core",
        "CanDoItAll.Mcp.DotNetWatch",
        "CanDoItAll.Mcp.LocalRuntime",
        "CanDoItAll.Mcp.ProjectStructure",
        "CanDoItAll.Mcp.SshOps"
    };
}
