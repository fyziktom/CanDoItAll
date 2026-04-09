using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.Sandbox;
using CanDoItAll.Mcp.Components.Configuration;
using CanDoItAll.Mcp.Core.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Components.Catalog;

public sealed class ComponentCatalogService
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
        ["TreeView"] = "Shared BaseLib tree navigation component for hierarchical exploration and selection.",
        ["EmptyState"] = "Shared BaseLib empty-state component for zero-data, no-selection, and first-run orientation surfaces.",
        ["LoadingState"] = "Shared BaseLib loading-state component for progress and transition surfaces that should feel intentional instead of blank.",
        ["Alert"] = "Shared BaseLib alert component for actionable status and inline system feedback.",
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

    private readonly Lazy<ComponentCatalogIndex> index;
    private readonly McpServerOptions options;

    public ComponentCatalogService(IOptions<McpServerOptions> optionsAccessor)
    {
        options = optionsAccessor.Value;
        index = new Lazy<ComponentCatalogIndex>(BuildIndex, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ComponentsSearchData Search(string? query, string? library = null, string? group = null, int limit = 10)
    {
        var catalog = index.Value;
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var groupLookup = catalog.Groups.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var normalizedLibrary = library?.Trim();
        var normalizedGroup = group?.Trim();

        var componentHits = catalog.Components
            .Where(component => normalizedLibrary is null || string.Equals(component.Library, normalizedLibrary, StringComparison.OrdinalIgnoreCase))
            .Where(component => normalizedGroup is null || component.GroupKeys.Contains(normalizedGroup, StringComparer.OrdinalIgnoreCase))
            .Select(component =>
            {
                var score = ScoreComponent(component, normalizedQuery, groupLookup);
                return new
                {
                    Component = component,
                    Score = score.Score,
                    score.MatchedParameters
                };
            })
            .Where(result => string.IsNullOrWhiteSpace(normalizedQuery) || result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Component.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(result => new ComponentSearchHit(
                result.Component.Name,
                result.Component.Library,
                result.Component.Summary,
                result.Score,
                result.Component.GroupKeys
                    .Select(key => groupLookup.TryGetValue(key, out var groupDocument) ? groupDocument.Title : key)
                    .ToArray(),
                result.MatchedParameters))
            .ToArray();

        var exampleHits = catalog.Examples
            .Where(example => normalizedGroup is null || string.Equals(example.GroupKey, normalizedGroup, StringComparison.OrdinalIgnoreCase))
            .Where(example => normalizedLibrary is null || ExampleMatchesLibrary(example, normalizedLibrary, catalog.Components))
            .Where(example => string.IsNullOrWhiteSpace(normalizedQuery) || MatchesExample(example, normalizedQuery))
            .OrderBy(example => example.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 50))
            .ToArray();

        var groupHits = catalog.Groups
            .Where(item => string.IsNullOrWhiteSpace(normalizedQuery) || MatchesGroup(item, normalizedQuery))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 50))
            .ToArray();

        return new ComponentsSearchData(normalizedQuery, componentHits, exampleHits, groupHits);
    }

    public ComponentDocument GetComponent(string component)
    {
        return ResolveComponent(component);
    }

    public ComponentExamplesData GetExamples(string component)
    {
        var resolvedComponent = ResolveComponent(component);
        var examples = index.Value.Examples
            .Where(example => example.ComponentNames.Contains(resolvedComponent.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(example => example.GroupTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(example => example.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ComponentExamplesData(resolvedComponent.Name, examples);
    }

    public ComponentUsageExamplesData GetUsageExamples(string component, int limit = 10)
    {
        var resolvedComponent = ResolveComponent(component);
        var usageExamples = index.Value.UsageExamplesByComponent.TryGetValue(resolvedComponent.Name, out var matches)
            ? matches
            : [];

        return new ComponentUsageExamplesData(
            resolvedComponent.Name,
            usageExamples.Count,
            usageExamples
                .Take(Math.Clamp(limit, 1, 50))
                .ToArray());
    }

    public IReadOnlyList<ComponentGroupDocument> GetGroups()
    {
        return index.Value.Groups
            .OrderBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ComponentCssTokensData GetCssTokens(string component)
    {
        var resolvedComponent = ResolveComponent(component);
        var stylesheets = resolvedComponent.Library switch
        {
            "CanvasLib" => CanvasLibStylesheets,
            _ => new[] { "_content/CanDoItAll.Components.BaseLib/css/output.css" }
        };
        var sourceFiles = ResolveCssSourceFiles(resolvedComponent);

        return new ComponentCssTokensData(resolvedComponent.Name, resolvedComponent.Library, stylesheets, sourceFiles, resolvedComponent.CssNotes);
    }

    private IReadOnlyList<string> ResolveCssSourceFiles(ComponentDocument component)
    {
        return ResolveCssSourceFileHints(component.Library, component.Name, component.SourcePath)
            .Select(path => Path.GetFullPath(Path.Combine(options.Server.WorkspaceRoot, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public CanvasContractsData GetCanvasContracts(string? query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var matches = index.Value.CanvasContracts
            .Where(contract => string.IsNullOrWhiteSpace(normalizedQuery) || MatchesCanvasContract(contract, normalizedQuery))
            .OrderBy(contract => contract.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            throw new ToolInvocationException("ContractNotFound", $"No canvas contract matched '{normalizedQuery}'.");
        }

        return new CanvasContractsData(normalizedQuery, matches);
    }

    public ComponentCatalogIndex GetIndex()
    {
        return index.Value;
    }

    private ComponentDocument ResolveComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ToolInvocationException("ValidationFailed", "A component name is required.");
        }

        var matches = index.Value.Components
            .Where(candidate =>
                string.Equals(candidate.Name, component, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.FullName, component, StringComparison.OrdinalIgnoreCase) ||
                candidate.Name.Contains(component, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            throw new ToolInvocationException("ComponentNotFound", $"No shared component matched '{component}'.");
        }

        var exact = matches.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, component, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.FullName, component, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        if (matches.Length == 1)
        {
            return matches[0];
        }

        throw new ToolInvocationException(
            "AmbiguousComponent",
            $"Component query '{component}' matched multiple shared components.",
            matches.Select(candidate => candidate.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private ComponentCatalogIndex BuildIndex()
    {
        var workspaceRoot = Path.GetFullPath(options.Server.WorkspaceRoot);
        var baseLibRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.BaseLibRoot));
        var canvasLibRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.CanvasLibRoot));
        var sandboxRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.SandboxRoot));

        var groups = BuildGroups();
        var groupLookup = groups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var examples = BuildExamples(groupLookup);

        var libraries = new[]
        {
            new LibraryDescriptor("BaseLib", typeof(Button).Assembly, baseLibRoot),
            new LibraryDescriptor("CanvasLib", typeof(CanvasWorkbench).Assembly, canvasLibRoot)
        };

        var componentTypes = libraries
            .SelectMany(library => DiscoverComponentTypes(library).Select(type => (Library: library, Type: type)))
            .OrderBy(item => GetComponentName(item.Type), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var componentNames = componentTypes
            .Select(item => GetComponentName(item.Type))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usageExamplesByComponent = BuildConsumerUsageExamples(workspaceRoot, componentNames);

        var components = componentTypes
            .Select(item => BuildComponentDocument(item.Library, item.Type, examples, groupLookup, componentNames, usageExamplesByComponent))
            .ToArray();

        var canvasContracts = BuildCanvasContracts();

        _ = sandboxRoot;

        return new ComponentCatalogIndex(components, examples, groups, canvasContracts, usageExamplesByComponent);
    }

    private static IReadOnlyList<ComponentGroupDocument> BuildGroups()
    {
        var examplesByGroup = SandboxCatalogRegistry.Examples
            .GroupBy(example => GetGroupKeyFromRoute(SandboxCatalogRegistry.GetGroup(example.GroupKey).Route), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return SandboxCatalogRegistry.Groups
            .Select(group =>
            {
                var key = GetGroupKeyFromRoute(group.Route);
                return new ComponentGroupDocument(
                    key,
                    group.Title,
                    group.Route,
                    group.Summary,
                    group.FocusAreas,
                    group.ProofNotes,
                    examplesByGroup.TryGetValue(key, out var exampleCount) ? exampleCount : 0);
            })
            .ToArray();
    }

    private static IReadOnlyList<ComponentExampleDocument> BuildExamples(IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup)
    {
        return SandboxCatalogRegistry.Examples
            .Select(example =>
            {
                var group = SandboxCatalogRegistry.GetGroup(example.GroupKey);
                var groupKey = GetGroupKeyFromRoute(group.Route);
                return new ComponentExampleDocument(
                    example.Id,
                    example.Title,
                    example.Route,
                    groupKey,
                    groupLookup[groupKey].Title,
                    example.Scenario.ToLabel(),
                    example.Summary,
                    example.Tags,
                    example.ComponentNames);
            })
            .ToArray();
    }

    private static IEnumerable<Type> DiscoverComponentTypes(LibraryDescriptor library)
    {
        return library.Assembly
            .GetExportedTypes()
            .Where(type =>
                typeof(IComponent).IsAssignableFrom(type) &&
                type.IsClass &&
                !type.IsAbstract &&
                string.Equals(type.Namespace, type.Assembly == typeof(Button).Assembly
                    ? "CanDoItAll.Components.BaseLib"
                    : "CanDoItAll.Components.CanvasLib", StringComparison.Ordinal));
    }

    private static ComponentDocument BuildComponentDocument(
        LibraryDescriptor library,
        Type type,
        IReadOnlyList<ComponentExampleDocument> examples,
        IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup,
        IReadOnlySet<string> componentNames,
        IReadOnlyDictionary<string, IReadOnlyList<ComponentUsageExampleDocument>> usageExamplesByComponent)
    {
        var componentName = GetComponentName(type);
        var sourcePath = ResolveSourcePath(library.SourceRoot, componentName);
        var sourceText = File.Exists(sourcePath) ? File.ReadAllText(sourcePath) : string.Empty;

        var relatedExamples = examples
            .Where(example => example.ComponentNames.Contains(componentName, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var groupKeys = relatedExamples
            .Select(example => example.GroupKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tags = BuildTags(componentName, relatedExamples, groupKeys);
        var dependencyNames = DiscoverDependencies(sourceText, componentNames, componentName);
        var (parameters, events) = BuildParameterDocuments(componentName, type);
        var cssNotes = BuildCssNotes(componentName, library.Name, sourcePath);
        var guidance = BuildGuidance(componentName, library.Name, sourcePath);
        var usageExamples = usageExamplesByComponent.TryGetValue(componentName, out var matches)
            ? matches
            : [];

        return new ComponentDocument(
            componentName,
            type.FullName ?? componentName,
            type.Namespace ?? string.Empty,
            library.Name,
            BuildSummary(componentName, library.Name, sourcePath, groupKeys, groupLookup, relatedExamples),
            sourcePath,
            tags,
            groupKeys,
            dependencyNames,
            parameters,
            events,
            cssNotes,
            guidance,
            usageExamples.Count,
            usageExamples.Take(5).ToArray());
    }

    private static IReadOnlyList<CanvasContractDocument> BuildCanvasContracts()
    {
        var assembly = typeof(CanvasWorkbenchSurface).Assembly;
        return assembly
            .GetExportedTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                !typeof(IComponent).IsAssignableFrom(type) &&
                (type.Name.StartsWith("CanvasWorkbench", StringComparison.Ordinal) ||
                 type.Name.StartsWith("CanvasCalendar", StringComparison.Ordinal)) &&
                !type.Name.EndsWith("Snapshot", StringComparison.Ordinal) &&
                !type.Name.EndsWith("Factory", StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .Select(type => new CanvasContractDocument(
                type.Name,
                type.FullName ?? type.Name,
                ResolveCanvasContractKind(type.Name),
                BuildCanvasContractSummary(type.Name),
                type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.GetMethod is not null)
                    .Select(property => new CanvasContractPropertyDocument(property.Name, FormatTypeName(property.PropertyType)))
                    .ToArray()))
            .ToArray();
    }

    private static (IReadOnlyList<ComponentParameterDocument> Parameters, IReadOnlyList<ComponentEventDocument> Events) BuildParameterDocuments(string componentName, Type type)
    {
        var defaultInstance = TryCreateDefaultInstance(type);
        var parameterProperties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.GetCustomAttribute<ParameterAttribute>() is not null ||
                property.GetCustomAttribute<CascadingParameterAttribute>() is not null)
            .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parameters = parameterProperties
            .Where(property => !IsEventCallback(property.PropertyType))
            .Select(property => new ComponentParameterDocument(
                property.Name,
                FormatTypeName(property.PropertyType),
                property.GetCustomAttribute<EditorRequiredAttribute>() is not null,
                property.GetCustomAttribute<CascadingParameterAttribute>() is not null,
                IsChildContent(property.PropertyType),
                BuildParameterSummary(componentName, property.Name),
                ResolveDefaultValue(property, defaultInstance),
                ResolveAllowedValues(property.PropertyType)))
            .ToArray();

        var events = parameterProperties
            .Where(property => IsEventCallback(property.PropertyType))
            .Select(property => new ComponentEventDocument(property.Name, FormatTypeName(property.PropertyType)))
            .ToArray();

        return (parameters, events);
    }

    private static IReadOnlyList<string> DiscoverDependencies(string sourceText, IReadOnlySet<string> componentNames, string componentName)
    {
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(sourceText))
        {
            foreach (var dependency in ComponentReferenceRegex
                         .Matches(sourceText)
                         .Select(match => match.Groups["name"].Value)
                         .Where(name => componentNames.Contains(name) && !string.Equals(name, componentName, StringComparison.OrdinalIgnoreCase)))
            {
                dependencies.Add(dependency);
            }
        }

        if (AdditionalDependenciesByComponent.TryGetValue(componentName, out var additionalDependencies))
        {
            foreach (var dependency in additionalDependencies.Where(componentNames.Contains))
            {
                dependencies.Add(dependency);
            }
        }

        return dependencies
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildSummary(
        string componentName,
        string library,
        string sourcePath,
        IReadOnlyList<string> groupKeys,
        IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup,
        IReadOnlyList<ComponentExampleDocument> examples)
    {
        if (SummaryByComponent.TryGetValue(componentName, out var summary))
        {
            return summary;
        }

        var family = ResolveComponentFamily(library, sourcePath);

        if (string.Equals(library, "BaseLib", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(family))
        {
            var displayName = HumanizeComponentName(componentName);
            return family switch
            {
                "Badges" => $"Shared BaseLib {displayName} component for compact status, counts, and categorical emphasis.",
                "Buttons" => $"Shared BaseLib {displayName} component for primary, secondary, and inline actions.",
                "Cards" => $"Shared BaseLib {displayName} surface for grouped content, summaries, metrics, or action clusters.",
                "DataVisualization" => $"Shared BaseLib {displayName} component for charts, data grids, axes, or quantitative readouts.",
                "Feedback" => $"Shared BaseLib {displayName} component for alerts, loading, empty, notification, or contextual-help states.",
                "Forms" => $"Shared BaseLib {displayName} component for data entry, configuration, and field-level workflows.",
                "Identity" => $"Shared BaseLib {displayName} component for identity, presence, icons, and attribution details.",
                "Layout" => $"Shared BaseLib {displayName} component for responsive structure, spacing rhythm, and workspace composition.",
                "Lists" => $"Shared BaseLib {displayName} component for list, metadata, and selection-driven detail flows.",
                "Modals" => $"Shared BaseLib {displayName} component for modal, dialog, or transient overlay interactions.",
                "Navigation" => $"Shared BaseLib {displayName} component for navigation, orientation, and dense workspace movement.",
                "Storage" => $"Shared BaseLib {displayName} component for storage-oriented summaries, status, and capacity display.",
                "Typography" => $"Shared BaseLib {displayName} component for consistent text hierarchy, rhythm, and emphasis.",
                _ => $"Shared BaseLib {displayName} component for reusable app UI composition."
            };
        }

        if (string.Equals(library, "CanvasLib", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(family))
        {
            var displayName = HumanizeComponentName(componentName);
            return family switch
            {
                "Calendar" => $"Shared CanvasLib {displayName} component for calendar-specific runtime, editing, and selection surfaces.",
                "Core" => $"Shared CanvasLib {displayName} component for core workbench runtime infrastructure.",
                "Diagnostics" => $"Shared CanvasLib {displayName} component for diagnostics and runtime debugging overlays.",
                "Graph" => $"Shared CanvasLib {displayName} component for graph composition, interaction, overlays, or primitives.",
                "Shared" => $"Shared CanvasLib {displayName} component for reusable canvas runtime assets and shared support surfaces.",
                "Workbench" => $"Shared CanvasLib {displayName} component for workbench shells, stages, and desktop-first canvas workflows.",
                _ => $"Shared CanvasLib {displayName} component for reusable canvas runtime composition."
            };
        }

        if (groupKeys.Count > 0)
        {
            var primaryGroup = groupLookup[groupKeys[0]];
            return $"Shared {library} component commonly used in the sandbox {primaryGroup.Title} group. {primaryGroup.Summary}";
        }

        if (componentName.Contains("Calendar", StringComparison.Ordinal))
        {
            return "Shared CanvasLib component for calendar runtime or calendar boundary preview surfaces.";
        }

        if (componentName.Contains("Canvas", StringComparison.Ordinal))
        {
            return "Shared CanvasLib component for the workbench runtime, boundary previews, or canvas-specific documentation surfaces.";
        }

        if (examples.Count > 0)
        {
            return $"Shared {library} component with curated sandbox coverage through {examples[0].Title}.";
        }

        return $"Shared {library} component in the extracted component libraries.";
    }

    private static IReadOnlyList<string> BuildCssNotes(string componentName, string library, string sourcePath)
    {
        var noteSet = new List<string>();

        if (CssNotesByComponent.TryGetValue(componentName, out var notes))
        {
            noteSet.AddRange(notes);
        }
        else if (DefaultCssNotesByLibrary.TryGetValue(library, out var defaultNotes))
        {
            noteSet.AddRange(defaultNotes);
        }

        var sourceFiles = ResolveCssSourceFileHints(library, componentName, sourcePath);
        if (sourceFiles.Count > 0)
        {
            noteSet.Add($"Relevant source styling files: {string.Join(", ", sourceFiles.Select(path => $"`{path}`"))}.");
        }
        else if (string.Equals(library, "BaseLib", StringComparison.OrdinalIgnoreCase))
        {
            noteSet.Add("This component currently relies mostly on inline Tailwind utility classes inside the Razor source, so inspect the component source path when no dedicated Tailwind source file is listed.");
        }

        return noteSet
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ComponentGuidanceDocument BuildGuidance(string componentName, string library, string sourcePath)
    {
        if (GuidanceByComponent.TryGetValue(componentName, out var guidance))
        {
            return guidance;
        }

        var family = ResolveComponentFamily(library, sourcePath);
        if (!string.IsNullOrWhiteSpace(family) && GuidanceByFamily.TryGetValue(family, out var familyGuidance))
        {
            return familyGuidance;
        }

        return DefaultGuidanceByLibrary.TryGetValue(library, out var defaultGuidance)
            ? defaultGuidance
            : new ComponentGuidanceDocument([], [], []);
    }

    private static IReadOnlyList<string> BuildTags(
        string componentName,
        IReadOnlyList<ComponentExampleDocument> relatedExamples,
        IReadOnlyList<string> groupKeys)
    {
        var tags = relatedExamples
            .SelectMany(example => example.Tags)
            .Concat(groupKeys)
            .ToList();

        if (TagsByComponent.TryGetValue(componentName, out var componentTags))
        {
            tags.AddRange(componentTags);
        }

        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ComponentUsageExampleDocument>> BuildConsumerUsageExamples(
        string workspaceRoot,
        IReadOnlySet<string> componentNames)
    {
        var usageLookup = new Dictionary<string, List<ComponentUsageExampleDocument>>(StringComparer.OrdinalIgnoreCase);

        foreach (var consumerRoot in DiscoverConsumerRoots(workspaceRoot))
        {
            foreach (var filePath in Directory.EnumerateFiles(consumerRoot.RootPath, "*.razor", SearchOption.AllDirectories))
            {
                if (IsGeneratedPath(filePath))
                {
                    continue;
                }

                var lines = File.ReadAllLines(filePath);
                var route = ResolveRoute(lines);

                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var line = lines[lineIndex];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    foreach (Match match in ComponentReferenceRegex.Matches(line))
                    {
                        var componentName = match.Groups["name"].Value;
                        if (!componentNames.Contains(componentName))
                        {
                            continue;
                        }

                        if (!usageLookup.TryGetValue(componentName, out var examples))
                        {
                            examples = [];
                            usageLookup[componentName] = examples;
                        }

                        examples.Add(new ComponentUsageExampleDocument(
                            consumerRoot.SourceKind,
                            consumerRoot.ProjectName,
                            filePath,
                            lineIndex + 1,
                            TruncateSnippet(line.Trim()),
                            route));
                    }
                }
            }
        }

        return usageLookup.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ComponentUsageExampleDocument>)pair.Value
                .GroupBy(
                    example => $"{example.Project}|{example.FilePath}|{example.LineNumber}|{example.Snippet}|{example.Route}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(example => GetSourcePriority(example.SourceKind))
                .ThenBy(example => example.Project, StringComparer.OrdinalIgnoreCase)
                .ThenBy(example => example.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(example => example.LineNumber)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveSourcePath(string sourceRoot, string componentName)
    {
        var componentsRoot = Path.Combine(sourceRoot, "Components");
        var directPath = Path.Combine(componentsRoot, $"{componentName}.razor");
        if (File.Exists(directPath) || !Directory.Exists(componentsRoot))
        {
            return directPath;
        }

        var candidates = Directory.EnumerateFiles(componentsRoot, $"{componentName}.razor", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .OrderBy(path => path.Contains($"{Path.DirectorySeparatorChar}Compatibility{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.FirstOrDefault() ?? directPath;
    }

    private static string GetComponentName(Type type)
    {
        var rawName = type.Name;
        var tickIndex = rawName.IndexOf('`');
        return tickIndex >= 0 ? rawName[..tickIndex] : rawName;
    }

    private static string ResolveCanvasContractKind(string typeName)
    {
        if (typeName.EndsWith("Surface", StringComparison.Ordinal))
        {
            return "surface";
        }

        if (typeName.EndsWith("EventArgs", StringComparison.Ordinal))
        {
            return "event";
        }

        if (typeName.EndsWith("Request", StringComparison.Ordinal))
        {
            return "request";
        }

        if (typeName.EndsWith("State", StringComparison.Ordinal))
        {
            return "state";
        }

        if (typeName.EndsWith("Options", StringComparison.Ordinal))
        {
            return "options";
        }

        return "model";
    }

    private static string BuildCanvasContractSummary(string typeName)
    {
        return ResolveCanvasContractKind(typeName) switch
        {
            "surface" => "Top-level typed surface passed into the shared canvas runtime.",
            "event" => "Event payload emitted by the shared canvas runtime back into .NET.",
            "request" => "Typed request emitted by the shared canvas runtime or expected by its callbacks.",
            "state" => "Persisted or computed state used by the shared canvas runtime.",
            "options" => "Options object that configures a reusable canvas subsystem.",
            _ => "Shared canvas contract model used by the extracted canvas libraries."
        };
    }

    private static bool IsEventCallback(Type type)
    {
        return type == typeof(EventCallback) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));
    }

    private static bool IsChildContent(Type type)
    {
        return type == typeof(RenderFragment) ||
               (type.IsGenericType && string.Equals(type.GetGenericTypeDefinition().Name, "RenderFragment`1", StringComparison.Ordinal));
    }

    private static string FormatTypeName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            return $"{FormatTypeName(nullableType)}?";
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        if (type == typeof(object))
        {
            return "object";
        }

        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            var typeName = GetComponentName(type);
            var genericArguments = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{typeName}<{genericArguments}>";
        }

        return type.Name;
    }

    private static SearchScore ScoreComponent(
        ComponentDocument component,
        string query,
        IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchScore(1, []);
        }

        var score = 0d;
        var matchedParameters = new List<string>();
        score += ScoreText(component.Name, query, exactBoost: 100, containsBoost: 60, tokenBoost: 12);
        score += ScoreText(component.FullName, query, exactBoost: 85, containsBoost: 35, tokenBoost: 8);
        score += ScoreText(component.Summary, query, exactBoost: 20, containsBoost: 18, tokenBoost: 6);
        score += ScoreText(string.Join(' ', component.Tags), query, exactBoost: 15, containsBoost: 12, tokenBoost: 5);
        score += ScoreText(string.Join(' ', component.Guidance.UseFor), query, exactBoost: 18, containsBoost: 14, tokenBoost: 5);
        score += ScoreText(string.Join(' ', component.Guidance.AvoidFor), query, exactBoost: 10, containsBoost: 8, tokenBoost: 3);
        score += ScoreText(string.Join(' ', component.Guidance.CompositionRules), query, exactBoost: 18, containsBoost: 14, tokenBoost: 5);

        foreach (var parameter in component.Parameters)
        {
            var parameterScore = ScoreText(parameter.Name, query, exactBoost: 40, containsBoost: 25, tokenBoost: 8) +
                                 ScoreText(parameter.Type, query, exactBoost: 15, containsBoost: 10, tokenBoost: 4) +
                                 ScoreText(parameter.Summary ?? string.Empty, query, exactBoost: 12, containsBoost: 10, tokenBoost: 4) +
                                 ScoreText(parameter.DefaultValue ?? string.Empty, query, exactBoost: 12, containsBoost: 8, tokenBoost: 3) +
                                 ScoreText(string.Join(' ', parameter.AllowedValues), query, exactBoost: 18, containsBoost: 12, tokenBoost: 4);
            if (parameterScore > 0)
            {
                matchedParameters.Add(parameter.Name);
                score += parameterScore;
            }
        }

        foreach (var eventDocument in component.Events)
        {
            score += ScoreText(eventDocument.Name, query, exactBoost: 24, containsBoost: 16, tokenBoost: 6);
        }

        foreach (var groupKey in component.GroupKeys)
        {
            if (groupLookup.TryGetValue(groupKey, out var group))
            {
                score += ScoreText(group.Title, query, exactBoost: 18, containsBoost: 12, tokenBoost: 5);
                score += ScoreText(group.Summary, query, exactBoost: 8, containsBoost: 6, tokenBoost: 2);
            }
        }

        score += ScoreText(string.Join(' ', component.CssNotes), query, exactBoost: 10, containsBoost: 8, tokenBoost: 3);

        return new SearchScore(score, matchedParameters.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static double ScoreText(string text, string query, double exactBoost, double containsBoost, double tokenBoost)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        var normalizedText = text.Trim();
        var normalizedQuery = query.Trim();

        if (string.Equals(normalizedText, normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return exactBoost;
        }

        var score = normalizedText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
            ? containsBoost
            : 0;

        foreach (var token in normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length < 2)
            {
                continue;
            }

            if (normalizedText.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += tokenBoost;
            }
        }

        return score;
    }

    private static bool MatchesExample(ComponentExampleDocument example, string query)
    {
        return example.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               example.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               example.Scenario.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               example.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               example.ComponentNames.Any(name => name.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesGroup(ComponentGroupDocument group, string query)
    {
        return group.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               group.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               group.FocusAreas.Any(area => area.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCanvasContract(CanvasContractDocument contract, string query)
    {
        return contract.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               contract.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               contract.Properties.Any(property =>
                   property.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   property.Type.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ExampleMatchesLibrary(ComponentExampleDocument example, string library, IReadOnlyList<ComponentDocument> components)
    {
        var componentLookup = components.ToDictionary(component => component.Name, StringComparer.OrdinalIgnoreCase);
        return example.ComponentNames
            .Any(componentName =>
                componentLookup.TryGetValue(componentName, out var component) &&
                string.Equals(component.Library, library, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetGroupKeyFromRoute(string route)
    {
        return route.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? route.Trim('/');
    }

    private static IReadOnlyList<ConsumerSourceDescriptor> DiscoverConsumerRoots(string workspaceRoot)
    {
        var srcRoot = Path.Combine(workspaceRoot, "src");
        if (!Directory.Exists(srcRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(srcRoot)
            .Select(rootPath => new ConsumerSourceDescriptor(
                Path.GetFileName(rootPath),
                ResolveSourceKind(Path.GetFileName(rootPath)),
                rootPath))
            .Where(descriptor => !ConsumerProjectExclusions.Contains(descriptor.ProjectName))
            .Where(descriptor => Directory.EnumerateFiles(descriptor.RootPath, "*.razor", SearchOption.AllDirectories).Any(filePath => !IsGeneratedPath(filePath)))
            .OrderBy(descriptor => GetSourcePriority(descriptor.SourceKind))
            .ThenBy(descriptor => descriptor.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveSourceKind(string projectName)
    {
        return projectName switch
        {
            "CanDoItAll.Web" => "product",
            "CanDoItAll.Components.Sandbox" => "sandbox",
            _ when projectName.StartsWith("CanDoItAll.Modules.", StringComparison.OrdinalIgnoreCase) => "module",
            _ when projectName.StartsWith("CanDoItAll.Components", StringComparison.OrdinalIgnoreCase) => "shared",
            _ => "consumer"
        };
    }

    private static int GetSourcePriority(string sourceKind)
    {
        return sourceKind switch
        {
            "product" => 0,
            "module" => 1,
            "sandbox" => 2,
            "shared" => 3,
            _ => 4
        };
    }

    private static string? ResolveRoute(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = RouteDirectiveRegex.Match(line);
            if (match.Success)
            {
                return match.Groups["route"].Value;
            }
        }

        return null;
    }

    private static bool IsGeneratedPath(string filePath)
    {
        return filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveComponentFamily(string library, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        var directoryPath = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return null;
        }

        var directory = new DirectoryInfo(directoryPath);
        if (string.Equals(directory.Name, "Compatibility", StringComparison.OrdinalIgnoreCase) &&
            directory.Parent is not null)
        {
            directory = directory.Parent;
        }

        if (string.Equals(library, "CanvasLib", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(directory.Name, "Composition", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(directory.Name, "Interaction", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(directory.Name, "Overlays", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(directory.Name, "Primitives", StringComparison.OrdinalIgnoreCase)))
        {
            return "Graph";
        }

        return directory.Name;
    }

    private static string HumanizeComponentName(string componentName)
    {
        return Regex.Replace(componentName, "(?<!^)([A-Z])", " $1").ToLowerInvariant();
    }

    private static IReadOnlyList<string> ResolveCssSourceFileHints(string library, string componentName, string sourcePath)
    {
        if (string.Equals(library, "CanvasLib", StringComparison.OrdinalIgnoreCase))
        {
            return CanvasLibStylesheets
                .Select(path => $@"src\CanDoItAll.Components.CanvasLib\wwwroot\{path.Replace("_content/CanDoItAll.Components.CanvasLib/", string.Empty).Replace('/', Path.DirectorySeparatorChar)}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (!string.Equals(library, "BaseLib", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (BaseLibCssSourceFilesByComponent.TryGetValue(componentName, out var explicitFiles))
        {
            return explicitFiles;
        }

        var family = ResolveComponentFamily(library, sourcePath);
        if (string.IsNullOrWhiteSpace(family))
        {
            return [];
        }

        return family switch
        {
            "Badges" => [@"Tailwind\controls\badges.css"],
            "Buttons" => [@"Tailwind\controls\buttons.css"],
            "Cards" => componentName.Contains("Stat", StringComparison.OrdinalIgnoreCase) ||
                       componentName.Contains("Summary", StringComparison.OrdinalIgnoreCase)
                ? [@"Tailwind\surfaces\cards.css", @"Tailwind\layout\stats.css"]
                : [@"Tailwind\surfaces\cards.css"],
            "DataVisualization" => [@"Tailwind\foundation\radzen-layout.css"],
            "Feedback" => componentName.Contains("Popover", StringComparison.OrdinalIgnoreCase) ||
                          componentName.Contains("Tooltip", StringComparison.OrdinalIgnoreCase)
                ? [@"Tailwind\surfaces\overlays.css"]
                : [@"Tailwind\feedback\alerts.css"],
            "Forms" => [@"Tailwind\forms\fields.css"],
            "Identity" => [@"Tailwind\typography\text.css"],
            "Layout" => componentName.Contains("Stat", StringComparison.OrdinalIgnoreCase)
                ? [@"Tailwind\layout\stats.css"]
                : [@"Tailwind\layout\stacks.css", @"Tailwind\layout\sheets.css"],
            "Lists" => [@"Tailwind\layout\sheets.css", @"Tailwind\surfaces\cards.css"],
            "Modals" => [@"Tailwind\surfaces\overlays.css"],
            "Navigation" => componentName.Contains("Tree", StringComparison.OrdinalIgnoreCase)
                ? [@"Tailwind\navigation\treeview.css"]
                : componentName.Contains("Tab", StringComparison.OrdinalIgnoreCase)
                    ? [@"Tailwind\navigation\tabs.css"]
                    : componentName.Contains("Header", StringComparison.OrdinalIgnoreCase) ||
                      componentName.Contains("Toolbar", StringComparison.OrdinalIgnoreCase) ||
                      componentName.Contains("Filter", StringComparison.OrdinalIgnoreCase) ||
                      componentName.Contains("Toc", StringComparison.OrdinalIgnoreCase)
                        ? [@"Tailwind\navigation\page-header.css"]
                        : [],
            "Storage" => [@"Tailwind\surfaces\cards.css", @"Tailwind\controls\badges.css"],
            "Typography" => [@"Tailwind\typography\text.css"],
            _ => []
        };
    }

    private static string? BuildParameterSummary(string componentName, string parameterName)
    {
        if (ParameterDescriptionsByComponent.TryGetValue(componentName, out var componentDescriptions) &&
            componentDescriptions.TryGetValue(parameterName, out var parameterSummary))
        {
            return parameterSummary;
        }

        return DefaultParameterDescriptionsByName.TryGetValue(parameterName, out var defaultSummary)
            ? defaultSummary
            : null;
    }

    private static object? TryCreateDefaultInstance(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveDefaultValue(PropertyInfo property, object? defaultInstance)
    {
        if (defaultInstance is null ||
            property.GetMethod is null ||
            property.GetIndexParameters().Length > 0 ||
            property.PropertyType == typeof(RenderFragment) ||
            (property.PropertyType.IsGenericType && string.Equals(property.PropertyType.GetGenericTypeDefinition().Name, "RenderFragment`1", StringComparison.Ordinal)) ||
            typeof(Delegate).IsAssignableFrom(property.PropertyType))
        {
            return null;
        }

        try
        {
            var value = property.GetValue(defaultInstance);
            return FormatDefaultValue(value, property.PropertyType);
        }
        catch
        {
            return null;
        }
    }

    private static string? FormatDefaultValue(object? value, Type propertyType)
    {
        if (value is null)
        {
            return null;
        }

        var normalizedType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (normalizedType == typeof(string))
        {
            return $"\"{value}\"";
        }

        if (normalizedType == typeof(bool))
        {
            return (bool)value ? "true" : "false";
        }

        if (normalizedType.IsEnum)
        {
            return Enum.GetName(normalizedType, value);
        }

        if (normalizedType == typeof(int) ||
            normalizedType == typeof(long) ||
            normalizedType == typeof(double) ||
            normalizedType == typeof(float) ||
            normalizedType == typeof(decimal))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveAllowedValues(Type type)
    {
        var normalizedType = Nullable.GetUnderlyingType(type) ?? type;
        if (!normalizedType.IsEnum)
        {
            return [];
        }

        return Enum.GetNames(normalizedType)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string TruncateSnippet(string snippet)
    {
        return snippet.Length <= 220
            ? snippet
            : $"{snippet[..217]}...";
    }

    private sealed record LibraryDescriptor(string Name, Assembly Assembly, string SourceRoot);

    private sealed record ConsumerSourceDescriptor(string ProjectName, string SourceKind, string RootPath);

    private sealed record SearchScore(double Score, IReadOnlyList<string> MatchedParameters);
}
