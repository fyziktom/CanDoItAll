using System.Text;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Factory;

internal static class PromptFactoryCanvasCatalog
{
    private sealed record MenuSectionDefinition(
        string Key,
        string Label,
        string Description,
        string Tone,
        string Icon,
        IReadOnlyList<string> ItemKeys);

    private sealed record InputDefinition(
        string Key,
        string Label,
        string Description,
        string Icon,
        string Tone,
        string TitleLabel,
        string TitlePlaceholder,
        string SubtitleLabel,
        string SubtitlePlaceholder,
        string NotesLabel,
        string NotesPlaceholder,
        bool RequiresFile = false,
        string AcceptedFileTypes = "",
        string FilePrompt = "Drop a file here or choose one.");

    private static readonly IReadOnlyList<MenuSectionDefinition> ComponentSections =
    [
        new(
            "foundation",
            "Foundation",
            "Role, mission framing, context loading, and guardrails.",
            "accent",
            "prompt",
            ["session-framing", "mission-scope", "context-discovery", "guardrails"]),
        new(
            "delivery",
            "Delivery",
            "Workflow, architecture, planning, and implementation execution.",
            "primary",
            "build",
            ["workflow-orchestration", "architecture-analysis", "planning-checklists", "implementation-execution"]),
        new(
            "validation",
            "Validation",
            "Evidence, review passes, and final handoff structure.",
            "warn",
            "qa",
            ["validation-review", "output-handoff"]),
        new(
            "environment",
            "Environment",
            "Stack-aware inserts and optional toolbox snippets.",
            "mint",
            "ops",
            ["stack-profiles", "toolbox-snippets"])
    ];

    private static readonly IReadOnlyList<MenuSectionDefinition> FlowSections =
    [
        new(
            "core-delivery",
            "Core Delivery",
            "General engineering flows for planning, implementation, and release work.",
            "accent",
            "flow",
            [
                "architecture-review-plan-implement-validate",
                "audit-plan-refactor-review",
                "bugfix-regression-proof",
                "release-hardening-final-audit"
            ]),
        new(
            "ui-data",
            "UI and Data",
            "Flows for canvas, full-stack, and data-layer changes.",
            "primary",
            "feature",
            [
                "ui-canvas-feature-delivery",
                "fullstack-offline-feature",
                "data-layer-change-crossdb"
            ]),
        new(
            "specialized",
            "Specialized",
            "Targeted flows for automation, migration, and embedded work.",
            "mint",
            "ops",
            [
                "playwright-automation-upgrade",
                "php-canvas-migration",
                "embedded-midi-firmware-tuning"
            ])
    ];

    private static readonly IReadOnlyList<MenuSectionDefinition> BlueprintSections =
    [
        new(
            "foundation",
            "Foundation",
            "Starting points for architecture, audits, implementation, and bugfixes.",
            "accent",
            "prompt",
            [
                "architecture-spec",
                "repository-audit",
                "implementation-plan",
                "feature-implementation",
                "safe-refactor",
                "bugfix-with-regression-lock"
            ]),
        new(
            "assurance",
            "Review and Assurance",
            "Review, test strategy, validation, performance, and security blueprints.",
            "warn",
            "qa",
            [
                "senior-code-review",
                "test-strategy-and-automation",
                "validation-audit",
                "performance-hardening",
                "security-hardening"
            ]),
        new(
            "experience-and-embedded",
            "Experience and Embedded",
            "User-facing delivery and hardware-integrated iterations.",
            "mint",
            "feature",
            [
                "ui-ux-delivery",
                "embedded-firmware-iteration"
            ])
    ];

    private static readonly IReadOnlyList<InputDefinition> InputDefinitions =
    [
    new(
        "file",
        "File",
        "Attach any file and describe what the AI should extract, compare, or verify from it.",
        "file",
        "mint",
        "File title",
        "Architecture notes",
        "Focus or usage",
        "Extract acceptance criteria",
        "AI task",
        "What should the AI read or extract from this file?",
        true,
        "*/*",
        "Drop a file here or choose one."),
        new(
            "image",
        "Image",
        "Attach an image and specify what visual evidence the AI should inspect.",
        "image",
        "danger",
        "Image title",
        "Canvas capture",
        "Focus or usage",
        "Read layout defects",
        "AI task",
        "What should the AI inspect or infer from this image?",
        true,
        "image/*",
        "Drop an image here or choose one."),
        new(
            "video",
        "Video",
        "Attach a video and explain what sequence, regression, or behavior the AI should inspect.",
        "video",
        "accent",
        "Video title",
        "Demo recording",
        "Focus or usage",
        "Find the regression sequence",
        "AI task",
        "What should the AI watch for in this video?",
        true,
        "video/*",
        "Drop a video here or choose one."),
        new(
            "link",
        "Link",
        "Attach a link and state how the AI should use it.",
        "link",
        "sky",
        "Link label",
        "API reference",
        "URL",
        "https://...",
        "AI task",
        "What should the AI extract from this link?"),
    new(
        "note",
        "Note",
        "Capture free-form prompt context or operator instructions.",
        "note",
        "neutral",
        "Note title",
        "Current blocker",
        "Context",
        "Where this note applies",
        "AI task",
        "How should the AI use this note?")
    ];

    public static IReadOnlyList<CanvasWorkbenchAction> BuildSessionContextActions(PromptLibraryCatalogSummary catalog)
        =>
        [
            BuildComponentsAction(catalog),
            BuildBlueprintsAction(catalog),
            BuildFlowsAction(catalog),
            BuildInputsAction(),
            new CanvasWorkbenchAction
            {
                ActionId = "reset:session",
                Label = "Reset",
                MenuLabel = "Reset",
                Description = "Clear the blueprint, flow, components, and inputs from the current session.",
                Icon = "clear",
                Tone = "warn"
            },
            new CanvasWorkbenchAction
            {
                ActionId = "apply-recommendations",
                Label = "Recommend",
                MenuLabel = "Recommend",
                Description = "Apply the current blueprint and flow recommendations.",
                Icon = "audit",
                Tone = "accent"
            },
            new CanvasWorkbenchAction
            {
                ActionId = "build-flow",
                Label = "Build",
                MenuLabel = "Build",
                Description = "Build the prompt from the current selections.",
                Icon = "flow",
                Tone = "mint"
            },
            new CanvasWorkbenchAction
            {
                ActionId = "save-session",
                Label = "Save",
                MenuLabel = "Save",
                Description = "Persist the prompt session state.",
                Icon = "support",
                Tone = "primary"
            }
        ];

    public static IReadOnlyList<CanvasWorkbenchAction> BuildSelectionContextActions(PromptLibraryCatalogSummary catalog, string selectionKind)
        => selectionKind switch
        {
            "components-root" => [BuildComponentsAction(catalog), BuildClearAction("components")],
            "component-section" => [BuildComponentsAction(catalog)],
            "component-group" => [BuildComponentsAction(catalog)],
            "flow-root" => [BuildFlowsAction(catalog), BuildClearAction("flow")],
            "blueprint-root" => [BuildBlueprintsAction(catalog), BuildClearAction("blueprint")],
            "inputs-root" => [BuildInputsAction(), BuildClearAction("inputs")],
            _ => BuildSessionContextActions(catalog)
        };

    public static IReadOnlyList<CanvasWorkbenchAction> BuildComponentNodeActions(PromptBlockSummary block)
    {
        var actions = new List<CanvasWorkbenchAction>();
        if (block.TemplateTokens.Count > 0)
        {
            var configure = BuildComponentLeafAction(block);
            configure.Label = "Configure";
            configure.MenuLabel = "Configure";
            configure.Description = $"Update the specification for {block.Name}.";
            configure.SubmitLabel = "Update";
            actions.Add(configure);
        }

        actions.Add(new CanvasWorkbenchAction
        {
            ActionId = $"component:remove:{block.Key}",
            Label = "Remove",
            MenuLabel = "Remove",
            Description = $"Remove {block.Name} from the prompt session.",
            Icon = "clear",
            Tone = "warn"
        });

        return actions;
    }

    public static IReadOnlyList<CanvasWorkbenchAction> BuildFlowNodeActions(PromptLibraryCatalogSummary catalog)
        => [BuildFlowsAction(catalog), BuildClearAction("flow")];

    public static IReadOnlyList<CanvasWorkbenchAction> BuildBlueprintNodeActions(PromptLibraryCatalogSummary catalog)
        => [BuildBlueprintsAction(catalog), BuildClearAction("blueprint")];

    public static IReadOnlyList<CanvasWorkbenchAction> BuildInputNodeActions(string attachmentId)
        => [
            BuildInputsAction(),
            new CanvasWorkbenchAction
            {
                ActionId = $"input:remove:{attachmentId}",
                Label = "Remove",
                MenuLabel = "Remove",
                Description = "Remove this prompt-session input.",
                Icon = "clear",
                Tone = "warn"
            }
        ];

private static CanvasWorkbenchAction BuildComponentsAction(PromptLibraryCatalogSummary catalog)
    => new()
    {
        ActionId = "catalog-components",
        Label = "Components",
        MenuLabel = "Components",
        Description = "Add prompt components from the shared library.",
        Icon = "prompt",
        Tone = "accent",
        SubmenuLayout = "toolbox-panel",
        Children = ComponentSections
                .Select(section => new CanvasWorkbenchAction
                {
                    ActionId = $"catalog-components:{section.Key}",
                    Label = section.Label,
                    MenuLabel = section.Label,
                    Description = section.Description,
                    Icon = section.Icon,
                    Tone = section.Tone,
                    Children = section.ItemKeys
                        .Select(groupKey => catalog.Groups.FirstOrDefault(group => string.Equals(group.Key, groupKey, StringComparison.OrdinalIgnoreCase)))
                        .Where(group => group is not null)
                        .Select(group => BuildComponentGroupAction(group!))
                        .ToList()
                })
                .ToList()
        };

    private static CanvasWorkbenchAction BuildComponentGroupAction(PromptLibraryGroupSummary group)
        => new()
        {
            ActionId = $"catalog-group:{group.Key}",
            Label = ResolveGroupMenuLabel(group.Name),
            MenuLabel = ResolveGroupMenuLabel(group.Name),
            Description = group.Purpose,
            Icon = ResolveGroupIcon(group.Key),
            Tone = ResolveGroupTone(group.Key),
            Children = group.Components
                .OrderBy(item => item.OrderIndex)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(BuildComponentLeafAction)
                .ToList()
        };

private static CanvasWorkbenchAction BuildComponentLeafAction(PromptBlockSummary block)
    => new()
    {
        ActionId = $"component:add:{block.Key}",
        Label = ResolveComponentMenuLabel(block.Name),
        MenuLabel = ResolveComponentMenuLabel(block.Name),
        Description = string.IsNullOrWhiteSpace(block.ContentPreview) ? block.Summary : block.ContentPreview,
        Icon = ResolveBlockIcon(block.BlockKind),
        Tone = ResolveGroupTone(block.GroupKey),
        RequiresInput = block.TemplateTokens.Count > 0,
            CreateMode = block.TemplateTokens.Count > 0 ? "dialog" : "create",
            ShowDefaultTextFields = false,
            SubmitLabel = block.TemplateTokens.Count > 0 ? "Add component" : "Add",
            InputFields = block.TemplateTokens
                .Select(BuildTokenField)
                .ToList()
        };

    private static CanvasWorkbenchAction BuildFlowsAction(PromptLibraryCatalogSummary catalog)
        => new()
        {
            ActionId = "catalog-flows",
            Label = "Flows",
            MenuLabel = "Flows",
            Description = "Choose a flow template for the session.",
            Icon = "flow",
            Tone = "primary",
            Children = FlowSections
                .Select(section => new CanvasWorkbenchAction
                {
                    ActionId = $"catalog-flows:{section.Key}",
                    Label = section.Label,
                    MenuLabel = section.Label,
                    Description = section.Description,
                    Icon = section.Icon,
                    Tone = section.Tone,
                    Children = section.ItemKeys
                        .Select(flowKey => catalog.FlowTemplates.FirstOrDefault(item => string.Equals(item.Key, flowKey, StringComparison.OrdinalIgnoreCase)))
                        .Where(flow => flow is not null)
                        .Select(flow => new CanvasWorkbenchAction
                        {
                            ActionId = $"flow:set:{flow!.Key}",
                            Label = flow.Name,
                            MenuLabel = ResolveFlowMenuLabel(flow.Name),
                            Description = flow.Summary,
                            Icon = "flow",
                            Tone = section.Tone
                        })
                        .ToList()
                })
                .ToList()
        };

    private static CanvasWorkbenchAction BuildBlueprintsAction(PromptLibraryCatalogSummary catalog)
        => new()
        {
            ActionId = "catalog-blueprints",
            Label = "Blueprints",
            MenuLabel = "Blueprints",
            Description = "Choose a prompt blueprint for the session.",
            Icon = "feature",
            Tone = "accent",
            Children = BlueprintSections
                .Select(section => new CanvasWorkbenchAction
                {
                    ActionId = $"catalog-blueprints:{section.Key}",
                    Label = section.Label,
                    MenuLabel = section.Label,
                    Description = section.Description,
                    Icon = section.Icon,
                    Tone = section.Tone,
                    Children = section.ItemKeys
                        .Select(blueprintKey => catalog.Blueprints.FirstOrDefault(item => string.Equals(item.Key, blueprintKey, StringComparison.OrdinalIgnoreCase)))
                        .Where(blueprint => blueprint is not null)
                        .Select(blueprint => new CanvasWorkbenchAction
                        {
                            ActionId = $"blueprint:set:{blueprint!.Key}",
                            Label = blueprint.Name,
                            MenuLabel = ResolveBlueprintMenuLabel(blueprint.Name),
                            Description = blueprint.Summary,
                            Icon = "prompt",
                            Tone = section.Tone
                        })
                        .ToList()
                })
                .ToList()
        };

    private static CanvasWorkbenchAction BuildInputsAction()
        => new()
        {
            ActionId = "catalog-inputs",
            Label = "Inputs",
            MenuLabel = "Inputs",
            Description = "Attach files, media, links, and notes to the prompt session.",
            Icon = "file",
            Tone = "mint",
            Children = InputDefinitions
                .Select(definition => new CanvasWorkbenchAction
                {
                    ActionId = $"input:add:{definition.Key}",
                    Label = definition.Label,
                    MenuLabel = definition.Label,
                    Description = definition.Description,
                    Icon = definition.Icon,
                    Tone = definition.Tone,
                    RequiresInput = true,
                    CreateMode = "dialog",
                    ObjectSubtype = definition.Key,
                    TitleLabel = definition.TitleLabel,
                    TitlePlaceholder = definition.TitlePlaceholder,
                    SubtitleLabel = definition.SubtitleLabel,
                    SubtitlePlaceholder = definition.SubtitlePlaceholder,
                    NotesLabel = definition.NotesLabel,
                    NotesPlaceholder = definition.NotesPlaceholder,
                    SubmitLabel = definition.RequiresFile ? "Attach" : "Add input",
                    RequiresFile = definition.RequiresFile,
                    AcceptedFileTypes = definition.AcceptedFileTypes,
                    FilePrompt = definition.FilePrompt,
                    SupportsDragDrop = definition.RequiresFile
                })
                .ToList()
        };

    private static CanvasWorkbenchAction BuildClearAction(string target)
        => new()
        {
            ActionId = $"clear:{target}",
            Label = "Clear",
            MenuLabel = "Clear",
            Description = $"Clear the current {target.Replace('-', ' ')} selection.",
            Icon = "clear",
            Tone = "warn"
        };

    private static CanvasWorkbenchInputField BuildTokenField(string token)
    {
        var normalized = token?.Trim() ?? string.Empty;
        return new CanvasWorkbenchInputField
        {
            Key = normalized,
            Label = HumanizeToken(normalized),
            Placeholder = $"Enter {HumanizeToken(normalized).ToLowerInvariant()}",
            InputMode = ResolveTokenInputMode(normalized),
            IsRequired = true
        };
    }

    private static string ResolveTokenInputMode(string token)
    {
        if (token.Contains("url", StringComparison.OrdinalIgnoreCase) ||
            token.Contains("link", StringComparison.OrdinalIgnoreCase))
        {
            return "url";
        }

        return token.Contains("notes", StringComparison.OrdinalIgnoreCase) ||
               token.Contains("summary", StringComparison.OrdinalIgnoreCase) ||
               token.Contains("description", StringComparison.OrdinalIgnoreCase) ||
               token.Contains("context", StringComparison.OrdinalIgnoreCase) ||
               token.Contains("commands", StringComparison.OrdinalIgnoreCase) ||
               token.Contains("artifacts", StringComparison.OrdinalIgnoreCase) ||
               token.Contains("plan", StringComparison.OrdinalIgnoreCase)
            ? "textarea"
            : "text";
    }

    private static string ResolveGroupMenuLabel(string value)
        => value switch
        {
            "Session Framing and Role" => "Session Framing",
            "Mission, Scope, and Success" => "Mission & Scope",
            "Context Loading and Discovery" => "Context Discovery",
            "Guardrails and Constraints" => "Guardrails",
            "Workflow Orchestration and Continuity" => "Workflow",
            "Architecture and Analysis" => "Architecture",
            "Planning and Checklists" => "Planning",
            "Implementation Execution" => "Implementation",
            "Validation, Testing, and Review" => "Validation",
            "Output, Delivery, and Handoff" => "Output & Handoff",
            _ => value
        };

    private static string ResolveComponentMenuLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Component";
        }

        if (value.Contains(": ", StringComparison.Ordinal))
        {
            return value[(value.IndexOf(": ", StringComparison.Ordinal) + 2)..];
        }

        return value;
    }

    private static string ResolveFlowMenuLabel(string value)
        => value
            .Replace("Architecture -> Review -> Plan -> Implement -> Validate", "Architecture Flow", StringComparison.Ordinal)
            .Replace("Audit -> Plan -> Refactor -> Review", "Audit Flow", StringComparison.Ordinal)
            .Replace("Bugfix -> Regression Proof -> Close", "Bugfix Flow", StringComparison.Ordinal)
            .Replace("Release Hardening and Final Audit", "Release Audit", StringComparison.Ordinal);

    private static string ResolveBlueprintMenuLabel(string value)
        => value switch
        {
            "Implementation Plan" => "Plan",
            "Feature Implementation" => "Implementation",
            "Safe Refactor" => "Refactor",
            "Senior Code Review" => "Review",
            "Validation Audit" => "Validation",
            "Performance Hardening" => "Performance",
            "Security Hardening" => "Security",
            _ => value
        };

    private static string ResolveGroupTone(string groupKey)
        => groupKey switch
        {
            "session-framing" or "mission-scope" or "context-discovery" or "guardrails" => "accent",
            "workflow-orchestration" or "architecture-analysis" or "planning-checklists" or "implementation-execution" => "primary",
            "validation-review" or "output-handoff" => "warn",
            "stack-profiles" or "toolbox-snippets" => "mint",
            _ => "neutral"
        };

    private static string ResolveGroupIcon(string groupKey)
        => groupKey switch
        {
            "session-framing" => "session",
            "mission-scope" => "prompt",
            "context-discovery" => "research",
            "guardrails" => "shield",
            "workflow-orchestration" => "flow",
            "architecture-analysis" => "arch",
            "planning-checklists" => "phase",
            "implementation-execution" => "build",
            "validation-review" => "qa",
            "output-handoff" => "ship",
            "stack-profiles" => "ops",
            "toolbox-snippets" => "support",
            _ => "prompt"
        };

    private static string ResolveBlockIcon(PromptBlockKind kind)
        => kind switch
        {
            PromptBlockKind.Constraint => "shield",
            PromptBlockKind.Validation => "qa",
            PromptBlockKind.Delivery => "ship",
            PromptBlockKind.Security => "shield",
            PromptBlockKind.Testing => "test",
            _ => "prompt"
        };

    private static string HumanizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "Value";
        }

        var words = token
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => int.TryParse(word, out _) ? word : char.ToUpperInvariant(word[0]) + word[1..])
            .ToList();

        var builder = new StringBuilder();
        for (var index = 0; index < words.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(words[index]);
        }

        return builder.ToString();
    }
}


