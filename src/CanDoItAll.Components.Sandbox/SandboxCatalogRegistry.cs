using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Components.Sandbox;

public enum SandboxGroupKey
{
    Foundations,
    Inputs,
    Actions,
    Navigation,
    Feedback,
    Layout,
    DataDisplay,
    Charts,
    Mermaid,
    Overlays,
    Canvas
}

public enum SandboxScenarioKey
{
    HappyPath,
    DenseContent,
    EmptyState,
    LoadingState,
    DisabledState,
    LongText
}

public enum SandboxFramePreset
{
    LiveViewport,
    Desktop,
    Mobile
}

public sealed record SandboxGroupDefinition(
    SandboxGroupKey Key,
    string Title,
    string Route,
    string Summary,
    IReadOnlyList<string> FocusAreas,
    IReadOnlyList<string> ProofNotes);

public sealed record SandboxExampleDefinition(
    string Id,
    SandboxGroupKey GroupKey,
    string Title,
    string Route,
    SandboxScenarioKey Scenario,
    string Summary,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> ComponentNames);

public static class SandboxCatalogRegistry
{
    public static IReadOnlyList<SandboxGroupDefinition> Groups { get; } =
    [
        new(
            SandboxGroupKey.Foundations,
            "Foundations",
            "/groups/foundations",
            "Typography, spacing, surfaces, and icon rhythm for the shared component language.",
            ["typography", "spacing", "surfaces", "icons"],
            ["Readability first.", "Spacing must hold at dense and mobile widths.", "Icons should support scanning instead of decoration."]),
        new(
            SandboxGroupKey.Inputs,
            "Inputs",
            "/groups/inputs",
            "Field-level components for fast data entry, review, and configuration flows.",
            ["text inputs", "numeric", "dropdown", "checkbox", "switch", "password", "text area", "editable"],
            ["Form state stays explicit.", "Disabled and long-text states must remain legible.", "Mobile stacking should still scan quickly."]),
        new(
            SandboxGroupKey.Actions,
            "Actions",
            "/groups/actions",
            "Primary and secondary action treatments for inline and sectional flows.",
            ["buttons", "button variants", "inline actions"],
            ["Primary action hierarchy must be obvious.", "Dense toolbars cannot collapse into visual noise.", "Disabled actions should still explain intent."]),
        new(
            SandboxGroupKey.Navigation,
            "Navigation",
            "/groups/navigation",
            "Shared navigation primitives for segmenting dense workflows into focused views so pages do not turn into long vertical scrolls.",
            ["tabs", "secondary tabs", "steps", "list detail shells", "progressive disclosure", "reduce scrolling"],
            ["Keyboard navigation should remain coherent.", "The active state must be obvious at a glance.", "Prefer tabs or split shells when users switch between modes instead of reading one tall page top-to-bottom."]),
        new(
            SandboxGroupKey.Feedback,
            "Feedback",
            "/groups/feedback",
            "Status, alerts, loading, and empty-state surfaces that explain system state clearly.",
            ["alerts", "notifications", "tooltips", "badges", "status", "empty", "loading"],
            ["Status language must be calm and actionable.", "Loading should not feel like a blank failure.", "Empty states need orientation and a next step."]),
        new(
            SandboxGroupKey.Layout,
            "Layout",
            "/groups/layout",
            "Page-level composition patterns for headers, scaffolds, sections, and split regions.",
            ["page header", "page scaffold", "form section", "section card", "split layouts"],
            ["Desktop layouts should use width intentionally.", "Mobile should orient the user inside the first viewport.", "Shared shells must avoid ad-hoc structure."]),
        new(
            SandboxGroupKey.DataDisplay,
            "Data Display",
            "/groups/data-display",
            "Reusable display surfaces for cards, lists, summaries, and metadata-heavy views.",
            ["cards", "lists", "fact and meta summaries", "chips and pills"],
            ["Long labels cannot destroy hierarchy.", "Dense displays still need calm grouping.", "Empty views should reuse shared empty surfaces."]),
        new(
            SandboxGroupKey.Charts,
            "Charts",
            "/groups/charts",
            "Apex-backed chart wrappers for operational trends, shares, fills, labels, and color tuning.",
            ["pie", "line", "area", "multi-series", "labels"],
            ["Charts must render nonblank browser output.", "Legends, labels, and toolbars cannot crowd the plot.", "Consumers should use CanDoItAll chart models instead of Apex component markup."]),
        new(
            SandboxGroupKey.Mermaid,
            "Mermaid",
            "/groups/mermaid",
            "First-party Mermaid.js wrapper for diagrams, syntax diagnostics, click events, and pan/zoom inspection.",
            ["flowchart", "architecture-beta", "node clicks", "pan zoom", "syntax errors"],
            ["Diagrams must render from the vendored official Mermaid resource.", "Clickable nodes should raise .NET events with useful node text and id context.", "Syntax failures must show line, column, excerpt, and expected-token hints when Mermaid provides them."]),
        new(
            SandboxGroupKey.Overlays,
            "Overlays",
            "/groups/overlays",
            "Contextual and modal interaction layers for help, confirmation, and sticky actions.",
            ["dialog host", "help popover", "sticky footers", "modals"],
            ["Overlay triggers must read clearly.", "The page should keep context when overlays open.", "Sticky actions should not dominate the viewport."]),
        new(
            SandboxGroupKey.Canvas,
            "Canvas",
            "/groups/canvas",
            "Typed workbench and calendar surfaces plus runtime primitives and sandbox-only preview assets.",
            ["workbench", "calendar", "floating windows", "primitives", "overlays"],
            ["Canvas contracts stay typed.", "Runtime workbench stays distinct from sandbox previews.", "Desktop width should be used aggressively without losing orientation."])
    ];

    public static IReadOnlyList<SandboxExampleDefinition> Examples { get; } =
    [
        CreateExample("foundations-happy", SandboxGroupKey.Foundations, "Type scale", SandboxScenarioKey.HappyPath, "Baseline typography, surfaces, and icon alignment.", ["typography", "spacing"], "TextBlock", "Icon", "SummaryTiles"),
        CreateExample("foundations-dense", SandboxGroupKey.Foundations, "Dense editorial stack", SandboxScenarioKey.DenseContent, "Denser heading, copy, and icon combinations.", ["dense", "copy"], "TextBlock", "Card"),
        CreateExample("foundations-empty", SandboxGroupKey.Foundations, "Empty orientation", SandboxScenarioKey.EmptyState, "Foundation-only empty orientation surface.", ["empty"], "EmptyState"),
        CreateExample("inputs-happy", SandboxGroupKey.Inputs, "Form entry", SandboxScenarioKey.HappyPath, "Standard field layout with validation and helper context.", ["forms"], "FormField", "TextBox", "DropDown", "Numeric"),
        CreateExample("inputs-editable", SandboxGroupKey.Inputs, "Inline property editing", SandboxScenarioKey.HappyPath, "Editable property rows switch from displayed values to shared text and checkbox inputs.", ["forms", "inline-edit"], "Editable", "Stack", "TextBox", "CheckBox"),
        CreateExample("inputs-dense", SandboxGroupKey.Inputs, "Dense intake form", SandboxScenarioKey.DenseContent, "High-density entry form with stacked fields.", ["dense", "forms"], "FormSection", "TextArea", "Switch"),
        CreateExample("inputs-empty", SandboxGroupKey.Inputs, "No draft selected", SandboxScenarioKey.EmptyState, "Entry workflow before a draft is created.", ["empty"], "EmptyState"),
        CreateExample("actions-happy", SandboxGroupKey.Actions, "Primary action hierarchy", SandboxScenarioKey.HappyPath, "Primary, secondary, ghost, and copy action contrast.", ["buttons", "copy"], "Button", "CopyButton"),
        CreateExample("actions-dense", SandboxGroupKey.Actions, "Dense inline actions", SandboxScenarioKey.DenseContent, "Action rows inside a compact review surface with copy affordances.", ["dense", "inline", "copy"], "Button", "CopyButton", "StatusBadge"),
        CreateExample("actions-empty", SandboxGroupKey.Actions, "No actions available", SandboxScenarioKey.EmptyState, "Action surface when prerequisites are missing.", ["empty"], "EmptyState", "Alert"),
        CreateExample("navigation-happy", SandboxGroupKey.Navigation, "Workspace navigation", SandboxScenarioKey.HappyPath, "Tabs, steps, and list-detail shell in a standard workflow that keeps dense content segmented instead of vertically stacked.", ["keyboard", "tabs", "progressive-disclosure", "reduce-scroll"], "Tabs", "Steps", "ListDetailShell"),
        CreateExample("navigation-dense", SandboxGroupKey.Navigation, "Dense workspace routing", SandboxScenarioKey.DenseContent, "High-information navigation shell with active detail.", ["dense"], "SecondaryTabs", "SelectionListItem"),
        CreateExample("navigation-empty", SandboxGroupKey.Navigation, "Nothing selected", SandboxScenarioKey.EmptyState, "Navigation shell before an item is selected.", ["empty"], "ListDetailShell", "EmptyState"),
        CreateCustomExample("navigation-tabs-lab", SandboxGroupKey.Navigation, "Tabs lab", "/groups/navigation/tabs", SandboxScenarioKey.HappyPath, "Dedicated tabs page covering the readable default setup, advanced accent styling, shell customization, overflow strategies, and edge cases that prove tabs can replace long stacked pages.", ["tabs", "responsive", "edge-cases", "customization", "progressive-disclosure", "reduce-scroll", "overflow"], "Tabs"),
        CreateExample("feedback-happy", SandboxGroupKey.Feedback, "Status surfaces", SandboxScenarioKey.HappyPath, "Alerts, badges, notifications, and load states.", ["status"], "Alert", "Notification", "NotificationService", "StatusBadge"),
        CreateExample("feedback-dense", SandboxGroupKey.Feedback, "Operational feedback", SandboxScenarioKey.DenseContent, "Mixed severity feedback inside a busy operational surface.", ["dense"], "Alert", "StatusBadge", "SummaryTiles"),
        CreateExample("feedback-empty", SandboxGroupKey.Feedback, "No activity yet", SandboxScenarioKey.EmptyState, "Shared empty and loading transitions.", ["empty"], "EmptyState", "LoadingState"),
        CreateExample("layout-happy", SandboxGroupKey.Layout, "Page scaffold", SandboxScenarioKey.HappyPath, "Page-level composition with header, lead, rail, and section blocks.", ["page"], "PageScaffold", "PageHeader", "SectionCard"),
        CreateExample("layout-dense", SandboxGroupKey.Layout, "Dense workspace layout", SandboxScenarioKey.DenseContent, "Split layout that uses wider desktop space deliberately.", ["dense"], "PageScaffold", "FormSection", "StickyActionFooter"),
        CreateExample("layout-empty", SandboxGroupKey.Layout, "No records in view", SandboxScenarioKey.EmptyState, "Layout shell with an empty workspace region.", ["empty"], "PageScaffold", "EmptyState"),
        CreateCustomExample("layout-composition", SandboxGroupKey.Layout, "Layout composition lab", "/groups/layout/composition", SandboxScenarioKey.HappyPath, "Compare Stack, Grid, and Row/Column versions of the same analytics control panel.", ["comparison", "responsive", "layout"], "Stack", "Grid", "Row", "Column", "FormRow"),
        CreateExample("data-display-happy", SandboxGroupKey.DataDisplay, "Summary and list views", SandboxScenarioKey.HappyPath, "Cards, summaries, and list rows for stable data display.", ["lists"], "Card", "SelectionListItem", "SummaryTiles"),
        CreateExample("data-display-dense", SandboxGroupKey.DataDisplay, "Dense data review", SandboxScenarioKey.DenseContent, "Metadata-heavy summaries and compact item rows.", ["dense", "metadata"], "SelectionListItem", "StatusBadge"),
        CreateExample("data-display-empty", SandboxGroupKey.DataDisplay, "No results", SandboxScenarioKey.EmptyState, "Shared empty display state for list-style data.", ["empty"], "EmptyState"),
        CreateExample("charts-happy", SandboxGroupKey.Charts, "Trend and share charts", SandboxScenarioKey.HappyPath, "Area, line, multi-line, and pie examples using the CanDoItAll chart wrapper.", ["charts", "apex", "wrapper"], "CdaChart"),
        CreateExample("charts-dense", SandboxGroupKey.Charts, "Dense operational charts", SandboxScenarioKey.DenseContent, "Higher-density multi-series charts with legends, labels, and color tuning.", ["dense", "multi-series"], "CdaChart"),
        CreateExample("charts-empty", SandboxGroupKey.Charts, "No chart data", SandboxScenarioKey.EmptyState, "Wrapper empty-state behavior before any chart series is available.", ["empty"], "CdaChart", "EmptyState"),
        CreateExample("mermaid-flowchart", SandboxGroupKey.Mermaid, "Interactive flowchart", SandboxScenarioKey.HappyPath, "Flowchart proof with .NET node-click events and pan/zoom controls.", ["mermaid", "flowchart", "click", "pan-zoom"], "MermaidDiagram"),
        CreateExample("mermaid-architecture", SandboxGroupKey.Mermaid, "Architecture beta", SandboxScenarioKey.DenseContent, "architecture-beta sample using groups, services, junctions, icons, labels, and directional ports.", ["mermaid", "architecture-beta", "services"], "MermaidDiagram"),
        CreateExample("mermaid-error", SandboxGroupKey.Mermaid, "Syntax diagnostics", SandboxScenarioKey.EmptyState, "Intentional Mermaid syntax failure proving line, column, excerpt, and expected-token rendering.", ["mermaid", "syntax-error", "diagnostics"], "MermaidDiagram", "Alert"),
        CreateExample("overlays-happy", SandboxGroupKey.Overlays, "Overlay services", SandboxScenarioKey.HappyPath, "DialogService, TooltipService, and NotificationService around contextual help and sticky actions.", ["overlay", "services"], "DialogService", "DialogHost", "TooltipService", "Tooltip", "NotificationService", "HelpPopover", "StickyActionFooter"),
        CreateExample("overlays-dense", SandboxGroupKey.Overlays, "Dense approval flow", SandboxScenarioKey.DenseContent, "Inline approval surface with secondary actions, modal affordance, and service-hosted overlays.", ["dense", "approval"], "DialogService", "TooltipService", "NotificationService", "StickyActionFooter", "Alert"),
        CreateExample("overlays-empty", SandboxGroupKey.Overlays, "No modal context", SandboxScenarioKey.EmptyState, "Overlay surface before the user selects a record.", ["empty"], "EmptyState", "HelpPopover", "DialogHost"),
        CreateExample("canvas-happy", SandboxGroupKey.Canvas, "Workbench surface", SandboxScenarioKey.HappyPath, "Shared workbench, floating windows, and calendar surface.", ["canvas", "workbench"], "CanvasWorkbench", "CanvasFloatingWindow", "CanvasCalendar"),
        CreateExample("canvas-dense", SandboxGroupKey.Canvas, "Dense workbench review", SandboxScenarioKey.DenseContent, "Deeper workbench, preview, and calendar density.", ["dense", "canvas"], "CanvasWorkbench", "CanvasSceneHostPreview", "LayerStackPreview"),
        CreateExample("canvas-empty", SandboxGroupKey.Canvas, "Empty canvas state", SandboxScenarioKey.EmptyState, "Canvas empty-state coverage across workbench and calendar.", ["empty", "canvas"], "CanvasWorkbench", "CanvasCalendar", "EmptyStateOverlay")
    ];

    public static IReadOnlyList<string> ValidationQuestions { get; } =
    [
        "Can I read all texts properly?",
        "Will I like and understand this UI or layout as a new user?",
        "Is there any too large component, gap, or visual disruption?",
        "Do we use proper shared components instead of ad-hoc markup?",
        "Do we use available space properly?",
        "Can the page be understood by scanning headings only?",
        "Is the hierarchy clear without decorative styling?",
        "Do focus, hover, disabled, loading, and empty states read clearly?",
        "On mobile, does the first viewport orient the user quickly?",
        "On desktop, are we avoiding dead horizontal space and accidental narrow columns?"
    ];

    public static SandboxGroupDefinition GetGroup(SandboxGroupKey key)
        => Groups.First(group => group.Key == key);

    public static IReadOnlyList<SandboxExampleDefinition> GetExamples(SandboxGroupKey key)
        => Examples.Where(example => example.GroupKey == key).ToArray();

    private static SandboxExampleDefinition CreateExample(
        string id,
        SandboxGroupKey groupKey,
        string title,
        SandboxScenarioKey scenario,
        string summary,
        IReadOnlyList<string> tags,
        params string[] componentNames)
    {
        var group = GetGroup(groupKey);
        var route = $"{group.Route}?scenario={scenario.ToSlug()}";

        return new SandboxExampleDefinition(
            id,
            groupKey,
            title,
            route,
            scenario,
            summary,
            tags,
            componentNames);
    }

    private static SandboxExampleDefinition CreateCustomExample(
        string id,
        SandboxGroupKey groupKey,
        string title,
        string route,
        SandboxScenarioKey scenario,
        string summary,
        IReadOnlyList<string> tags,
        params string[] componentNames)
    {
        return new SandboxExampleDefinition(
            id,
            groupKey,
            title,
            route,
            scenario,
            summary,
            tags,
            componentNames);
    }
}

public static class SandboxScenarioKeyExtensions
{
    public static SandboxScenarioKey Parse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "dense" or "dense-content" => SandboxScenarioKey.DenseContent,
            "empty" or "empty-state" => SandboxScenarioKey.EmptyState,
            "loading" or "loading-state" => SandboxScenarioKey.LoadingState,
            "disabled" or "disabled-state" => SandboxScenarioKey.DisabledState,
            "long-text" => SandboxScenarioKey.LongText,
            _ => SandboxScenarioKey.HappyPath
        };
    }

    public static string ToSlug(this SandboxScenarioKey value)
    {
        return value switch
        {
            SandboxScenarioKey.DenseContent => "dense-content",
            SandboxScenarioKey.EmptyState => "empty-state",
            SandboxScenarioKey.LoadingState => "loading-state",
            SandboxScenarioKey.DisabledState => "disabled-state",
            SandboxScenarioKey.LongText => "long-text",
            _ => "happy-path"
        };
    }

    public static string ToLabel(this SandboxScenarioKey value)
    {
        return value switch
        {
            SandboxScenarioKey.DenseContent => "Dense",
            SandboxScenarioKey.EmptyState => "Empty",
            SandboxScenarioKey.LoadingState => "Loading",
            SandboxScenarioKey.DisabledState => "Disabled",
            SandboxScenarioKey.LongText => "Long Text",
            _ => "Happy Path"
        };
    }

    public static string ToSummary(this SandboxScenarioKey value)
    {
        return value switch
        {
            SandboxScenarioKey.DenseContent => "Exercises compact spacing, stacked metadata, and busier review content.",
            SandboxScenarioKey.EmptyState => "Confirms the group has a calm and oriented zero-data state.",
            SandboxScenarioKey.LoadingState => "Confirms transition surfaces communicate progress instead of failure.",
            SandboxScenarioKey.DisabledState => "Confirms affordances remain understandable when unavailable.",
            SandboxScenarioKey.LongText => "Confirms long labels and descriptions do not break layout or hierarchy.",
            _ => "Shows the baseline scenario used for most product flows."
        };
    }

    public static IReadOnlyList<SecondaryTabItem> BuildTabs()
    {
        return Enum.GetValues<SandboxScenarioKey>()
            .Select(value => new SecondaryTabItem(value.ToSlug(), value.ToLabel()))
            .ToArray();
    }
}

public static class SandboxFramePresetExtensions
{
    public static SandboxFramePreset Parse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "desktop" => SandboxFramePreset.Desktop,
            "mobile" => SandboxFramePreset.Mobile,
            _ => SandboxFramePreset.LiveViewport
        };
    }

    public static string ToSlug(this SandboxFramePreset value)
    {
        return value switch
        {
            SandboxFramePreset.Desktop => "desktop",
            SandboxFramePreset.Mobile => "mobile",
            _ => "live"
        };
    }

    public static string ToLabel(this SandboxFramePreset value)
    {
        return value switch
        {
            SandboxFramePreset.Desktop => "Desktop Frame",
            SandboxFramePreset.Mobile => "Mobile Frame",
            _ => "Live Viewport"
        };
    }

    public static string ToSurfaceClass(this SandboxFramePreset value)
    {
        return value switch
        {
            SandboxFramePreset.Desktop => "sandbox-demo-frame--desktop",
            SandboxFramePreset.Mobile => "sandbox-demo-frame--mobile",
            _ => "sandbox-demo-frame--live"
        };
    }
}
