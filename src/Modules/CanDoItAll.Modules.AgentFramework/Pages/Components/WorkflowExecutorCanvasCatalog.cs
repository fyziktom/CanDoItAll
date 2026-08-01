using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public static class WorkflowExecutorCanvasCatalog
{
    private const string CreateExecutorActionPrefix = "workflow-executor:create:";
    private const string PluginExecutorsActionId = "workflow-executor:plugins";
    public static IReadOnlyList<CanvasWorkbenchAction> BuildQuickCreateActions(
        IReadOnlyList<WorkflowExecutorDescriptor> executors,
        IReadOnlyList<SecretListItem> secrets)
    {
        var implemented = executors
            .Where(executor => executor.CanExecute)
            .OrderBy(executor => executor.Category)
            .ThenBy(executor => executor.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (implemented.Count == 0)
        {
            return [];
        }

        var builtInActions = implemented
            .Where(executor => !IsPluginExecutor(executor))
            .Select(executor => BuildCreateAction(executor, secrets))
            .ToList();
        var pluginAction = BuildPluginExecutorsAction(implemented, secrets);
        var children = pluginAction is null
            ? builtInActions
            : builtInActions.Append(pluginAction).ToList();

        return
        [
            new CanvasWorkbenchAction
            {
                ActionId = "workflow-executor:menu",
                Label = "Executors",
                MenuLabel = "Executors",
                Description = "Run typed tools with explicit settings, timeout, retry, and result contracts.",
                Icon = "bolt",
                Tone = "info",
                Children = children
            }
        ];
    }

    public static CanvasWorkbenchAction BuildCreateAction(
        WorkflowExecutorDescriptor descriptor,
        IReadOnlyList<SecretListItem>? secrets = null)
    {
        var defaultInputValues = BuildDefaultInputValues(descriptor);
        var inputFields = BuildInputFields(descriptor, secrets ?? []);
        return new CanvasWorkbenchAction
        {
            ActionId = BuildCreateActionId(descriptor.Id),
            Label = descriptor.Name,
            MenuLabel = TrimMenuLabel(descriptor.Name),
            Description = BuildExecutorSummary(descriptor),
            Icon = descriptor.IconName,
            Tone = ResolveTone(descriptor.Category),
            SetupRendererKey = descriptor.SetupRendererKey,
            RequiresInput = descriptor.SettingsPresentationMode != WorkflowExecutorSettingsPresentationMode.CustomRenderer,
            CreateMode = "dialog",
            TitlePlaceholder = descriptor.Name,
            NotesPlaceholder = descriptor.Description,
            SubmitLabel = "Add executor",
            ObjectSubtype = descriptor.Id.Value,
            DefaultInputValues = defaultInputValues,
            InputFields = inputFields
        };
    }

    public static string BuildCreateActionId(WorkflowExecutorId executorId)
        => $"{CreateExecutorActionPrefix}{executorId.Value}";

    public static bool TryParseCreateActionId(string actionId, out WorkflowExecutorId executorId)
    {
        if (actionId.StartsWith(CreateExecutorActionPrefix, StringComparison.Ordinal) &&
            actionId.Length > CreateExecutorActionPrefix.Length)
        {
            executorId = new WorkflowExecutorId(actionId[CreateExecutorActionPrefix.Length..]);
            return true;
        }

        executorId = default;
        return false;
    }

    public static string ResolveTone(WorkflowExecutorCategoryKind category)
        => category switch
        {
            WorkflowExecutorCategoryKind.Storage => "success",
            WorkflowExecutorCategoryKind.ProjectStructure => "accent",
            WorkflowExecutorCategoryKind.Http => "info",
            WorkflowExecutorCategoryKind.Image => "danger",
            WorkflowExecutorCategoryKind.Spreadsheet => "warning",
            WorkflowExecutorCategoryKind.Data => "accent",
            WorkflowExecutorCategoryKind.Markdown => "info",
            WorkflowExecutorCategoryKind.Human => "warning",
            WorkflowExecutorCategoryKind.Utility => "neutral",
            WorkflowExecutorCategoryKind.Command => "danger",
            _ => "neutral"
        };

    public static string ResolveCategoryLabel(WorkflowExecutorCategoryKind category)
        => category switch
        {
            WorkflowExecutorCategoryKind.ProjectStructure => "Project structure",
            WorkflowExecutorCategoryKind.Http => "HTTP",
            WorkflowExecutorCategoryKind.Spreadsheet => "Spreadsheets",
            WorkflowExecutorCategoryKind.Data => "Data",
            WorkflowExecutorCategoryKind.Markdown => "Markdown",
            WorkflowExecutorCategoryKind.Human => "Human",
            WorkflowExecutorCategoryKind.Utility => "Utility",
            WorkflowExecutorCategoryKind.Command => "Commands",
            WorkflowExecutorCategoryKind.Image => "Images",
            WorkflowExecutorCategoryKind.Storage => "Storage",
            _ => category.ToString()
        };

    public static string ResolveCategoryDescription(WorkflowExecutorCategoryKind category)
        => category switch
        {
            WorkflowExecutorCategoryKind.Storage => "Workspace storage reads, writes, searches, stats, and diffs.",
            WorkflowExecutorCategoryKind.ProjectStructure => "Project tree reads and typed asset creation.",
            WorkflowExecutorCategoryKind.Http => "Bounded HTTP and HTTPS calls.",
            WorkflowExecutorCategoryKind.Image => "Image generation and image-provider output.",
            WorkflowExecutorCategoryKind.Spreadsheet => "XLSX inspection, reading, writing, and Markdown extraction.",
            WorkflowExecutorCategoryKind.Data => "Structured payload transformations.",
            WorkflowExecutorCategoryKind.Markdown => "Markdown rendering and report assembly.",
            WorkflowExecutorCategoryKind.Human => "Human approvals and workflow pauses.",
            WorkflowExecutorCategoryKind.Utility => "Bounded helper executors for local control flow.",
            WorkflowExecutorCategoryKind.Command => "Bounded local process execution.",
            _ => "Workflow executor tools."
        };

    public static string ResolveCategoryIcon(WorkflowExecutorCategoryKind category)
        => category switch
        {
            WorkflowExecutorCategoryKind.Storage => "folder_open",
            WorkflowExecutorCategoryKind.ProjectStructure => "account_tree",
            WorkflowExecutorCategoryKind.Http => "public",
            WorkflowExecutorCategoryKind.Image => "image",
            WorkflowExecutorCategoryKind.Spreadsheet => "table_chart",
            WorkflowExecutorCategoryKind.Data => "data_object",
            WorkflowExecutorCategoryKind.Markdown => "article",
            WorkflowExecutorCategoryKind.Human => "approval",
            WorkflowExecutorCategoryKind.Utility => "timer",
            WorkflowExecutorCategoryKind.Command => "terminal",
            _ => "bolt"
        };

    public static string BuildExecutorSummary(WorkflowExecutorDescriptor descriptor)
    {
        var badges = WorkflowExecutorDisplayAdapter.BuildSummaryBadges(descriptor);
        return $"{descriptor.Description} {string.Join(" · ", badges)}.";
    }

    private static CanvasWorkbenchAction? BuildPluginExecutorsAction(
        IReadOnlyList<WorkflowExecutorDescriptor> executors,
        IReadOnlyList<SecretListItem> secrets)
    {
        var pluginGroups = executors
            .Where(IsPluginExecutor)
            .GroupBy(executor => executor.Source.PluginId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => ResolvePluginDisplayName(group), StringComparer.OrdinalIgnoreCase)
            .Select(group => new CanvasWorkbenchAction
            {
                ActionId = $"{PluginExecutorsActionId}:{group.Key}",
                Label = ResolvePluginDisplayName(group),
                MenuLabel = ResolvePluginDisplayName(group),
                Description = $"Create workflow executors contributed by {ResolvePluginDisplayName(group)}.",
                Icon = ResolveIconName(group.First().Source.Icon),
                Tone = "accent",
                Children = group
                    .OrderBy(executor => executor.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(executor => BuildCreateAction(executor, secrets))
                    .ToList()
            })
            .ToList();

        if (pluginGroups.Count == 0)
        {
            return null;
        }

        return new CanvasWorkbenchAction
        {
            ActionId = PluginExecutorsActionId,
            Label = "Plugins",
            MenuLabel = "Plugins",
            Description = "Create executors contributed by installed or bundled plugins.",
            Icon = "extension",
            Tone = "accent",
            Children = pluginGroups
        };
    }

    public static bool IsPluginExecutor(WorkflowExecutorDescriptor executor)
        => executor.Source.Kind != WorkflowExecutorSourceKind.BuiltIn &&
           !string.IsNullOrWhiteSpace(executor.Source.PluginId);

    public static string ResolvePluginDisplayName(IEnumerable<WorkflowExecutorDescriptor> executors)
    {
        var materialized = executors.ToList();
        var displayName = materialized
            .Select(executor => executor.Source.DisplayName)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return materialized
            .Select(executor => executor.Source.PluginId)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Plugin";
    }

    public static string ResolvePluginIconName(IEnumerable<WorkflowExecutorDescriptor> executors)
        => ResolveIconName(executors.Select(executor => executor.Source.Icon).FirstOrDefault(icon => icon is not null));

    private static string ResolvePluginDisplayName(IGrouping<string, WorkflowExecutorDescriptor> group)
        => ResolvePluginDisplayName(group.AsEnumerable());

    private static string ResolveIconName(UiIconDescriptor? icon)
        => icon?.Kind == UiIconKind.MaterialIcon && !string.IsNullOrWhiteSpace(icon.Value)
            ? icon.Value
            : "extension";

    private static string TrimMenuLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "Tool";
        }

        var parts = label.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length <= 1 ? label.Trim() : parts[0];
    }

    private static List<CanvasWorkbenchInputValue> BuildDefaultInputValues(WorkflowExecutorDescriptor descriptor)
    {
        var values = new List<CanvasWorkbenchInputValue>
        {
            Value("timeoutSeconds", descriptor.DefaultPolicy.TimeoutSeconds.ToString()),
            Value("retryAttempts", descriptor.DefaultPolicy.MaxRetryAttempts.ToString()),
            Value("captureOutput", descriptor.DefaultPolicy.CaptureOutputArtifact.ToString().ToLowerInvariant())
        };

        if (descriptor.ConfigurationSchema.Fields.Count > 0)
        {
            var state = WorkflowExecutorConfigurationMapper.ReadState(
                descriptor.DefaultSettingsJson,
                descriptor.ConfigurationSchema);
            values.AddRange(descriptor.ConfigurationSchema.Fields.Select(field => Value(
                WorkflowExecutorConfigurationMapper.BuildInputKey(field.Key),
                state.GetText(field.Key))));
            return values;
        }

        return values;
    }

    private static List<CanvasWorkbenchInputField> BuildInputFields(
        WorkflowExecutorDescriptor descriptor,
        IReadOnlyList<SecretListItem> secrets)
    {
        var fields = new List<CanvasWorkbenchInputField>();
        if (descriptor.ConfigurationSchema.Fields.Count > 0)
        {
            fields.AddRange(descriptor.ConfigurationSchema.Fields.Select(field =>
                BuildConfigurationField(field, secrets)));
            fields.AddRange(BuildPolicyFields(descriptor));
            return fields;
        }

        fields.AddRange(BuildPolicyFields(descriptor));

        return fields;
    }

    private static IReadOnlyList<CanvasWorkbenchInputField> BuildPolicyFields(
        WorkflowExecutorDescriptor descriptor)
        =>
        [
            NumberField("timeoutSeconds", "Execution policy", "Timeout seconds", descriptor.DefaultPolicy.TimeoutSeconds.ToString()),
            NumberField("retryAttempts", "Execution policy", "Retries", descriptor.DefaultPolicy.MaxRetryAttempts.ToString()),
            BoolField("captureOutput", "Execution policy", "Capture output")
        ];

    private static CanvasWorkbenchInputField BuildConfigurationField(
        ConfigurationFieldDescriptor field,
        IReadOnlyList<SecretListItem> secrets)
    {
        var key = WorkflowExecutorConfigurationMapper.BuildInputKey(field.Key);
        return new CanvasWorkbenchInputField
        {
            Key = key,
            SectionKey = "settings",
            SectionTitle = "Settings",
            SectionDescription = string.IsNullOrWhiteSpace(field.HelpText)
                ? "Configure the typed settings required by this executor."
                : field.HelpText,
            Label = field.Label,
            InputMode = field.FieldType switch
            {
                ConfigurationFieldType.Url => "url",
                ConfigurationFieldType.Number => "number",
                ConfigurationFieldType.Json or ConfigurationFieldType.MultilineText => "textarea",
                ConfigurationFieldType.Boolean or
                ConfigurationFieldType.SecretReference or
                ConfigurationFieldType.Select => "select",
                _ => "text"
            },
            IsRequired = field.IsRequired,
            Options = BuildConfigurationFieldOptions(field, secrets)
        };
    }

    private static List<CanvasWorkbenchInputOption> BuildConfigurationFieldOptions(
        ConfigurationFieldDescriptor field,
        IReadOnlyList<SecretListItem> secrets)
        => field.FieldType switch
        {
            ConfigurationFieldType.Boolean =>
            [
                new CanvasWorkbenchInputOption { Value = "true", Label = "Yes" },
                new CanvasWorkbenchInputOption { Value = "false", Label = "No" }
            ],
            ConfigurationFieldType.SecretReference => secrets
                .OrderBy(secret => secret.Name, StringComparer.OrdinalIgnoreCase)
                .Select(secret => new CanvasWorkbenchInputOption
                {
                    Value = secret.Id.ToString("D"),
                    Label = $"{secret.Name} ({secret.Kind})"
                })
                .ToList(),
            _ => field.Options
                .Select(option => new CanvasWorkbenchInputOption
                {
                    Value = option.Value,
                    Label = option.Label
                })
                .ToList()
        };

    private static CanvasWorkbenchInputValue Value(string key, string value)
        => new() { Key = key, Value = value };

    private static CanvasWorkbenchInputField TextField(
        string key,
        string section,
        string label,
        string placeholder = "",
        string inputMode = "text",
        bool required = false)
        => new()
        {
            Key = key,
            SectionKey = Slug(section),
            SectionTitle = section,
            SectionDescription = SectionDescription(section),
            Label = label,
            Placeholder = placeholder,
            InputMode = inputMode,
            IsRequired = required
        };

    private static CanvasWorkbenchInputField NumberField(
        string key,
        string section,
        string label,
        string placeholder)
        => TextField(key, section, label, placeholder, "number");

    private static CanvasWorkbenchInputField BoolField(
        string key,
        string section,
        string label)
        => new()
        {
            Key = key,
            SectionKey = Slug(section),
            SectionTitle = section,
            SectionDescription = SectionDescription(section),
            Label = label,
            InputMode = "select",
            Options =
            [
                new CanvasWorkbenchInputOption { Value = "true", Label = "Yes" },
                new CanvasWorkbenchInputOption { Value = "false", Label = "No" }
            ]
        };

    private static string Slug(string value)
        => value.Replace(' ', '-').ToLowerInvariant();

    private static string SectionDescription(string section)
        => section switch
        {
            "Execution policy" => "Bound the executor before it is added to the canvas.",
            "Range" => "Limit reads and writes so preview runs stay predictable.",
            "Write" => "Configure write behavior before the node is created.",
            "Asset" => "Configure the project-structure asset payload.",
            _ => "Configure the typed settings required by this executor."
        };

}
