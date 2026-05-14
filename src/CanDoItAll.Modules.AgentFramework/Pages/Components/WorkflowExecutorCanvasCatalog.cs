using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal static class WorkflowExecutorCanvasCatalog
{
    private const string CreateExecutorActionPrefix = "workflow-executor:create:";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static IReadOnlyList<CanvasWorkbenchAction> BuildQuickCreateActions(
        IReadOnlyList<WorkflowExecutorDescriptor> executors,
        IReadOnlyList<SecretListItem> secrets)
    {
        var implemented = executors
            .Where(executor => executor.CanExecute)
            .OrderBy(executor => executor.Category)
            .ThenBy(executor => executor.Name, StringComparer.OrdinalIgnoreCase)
            .Select(executor => BuildCreateAction(executor, secrets))
            .ToList();
        if (implemented.Count == 0)
        {
            return [];
        }

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
                Children = implemented
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
            Description = descriptor.Description,
            Icon = descriptor.IconName,
            Tone = ResolveTone(descriptor.Category),
            SetupRendererKey = $"workflow-executor-{descriptor.Category.ToString().ToLowerInvariant()}",
            RequiresInput = true,
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
            WorkflowExecutorCategoryKind.Command => "terminal",
            _ => "bolt"
        };

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

        if (descriptor.Id == WorkflowExecutorIds.StorageFile)
        {
            var settings = DeserializeSettings<WorkflowStorageFileExecutorSettings>(descriptor);
            values.AddRange(
            [
                Value("storageOperation", settings.Operation.ToString()),
                Value("storagePath", settings.Path),
                Value("storageDestinationPath", settings.DestinationPath),
                Value("storageContent", settings.Content),
                Value("storageContentFromInput", settings.ContentFromInput.ToString().ToLowerInvariant()),
                Value("storageQuery", settings.Query),
                Value("storageSearchPattern", settings.SearchPattern),
                Value("storageMaxResults", settings.MaxResults.ToString()),
                Value("storageMaxCharacters", settings.MaxCharacters.ToString()),
                Value("storageOverwrite", settings.Overwrite.ToString().ToLowerInvariant())
            ]);
        }
        else if (descriptor.Id == WorkflowExecutorIds.HttpFetch)
        {
            var settings = DeserializeSettings<WorkflowHttpExecutorSettings>(descriptor);
            values.AddRange(
            [
                Value("httpMethod", settings.Method.ToString()),
                Value("httpUrl", settings.Url),
                Value("httpUrlJsonPath", settings.UrlJsonPath),
                Value("httpHeadersJson", JsonSerializer.Serialize(settings.Headers, JsonOptions)),
                Value("httpSecretId", settings.SecretHeader.SecretId?.ToString("D") ?? string.Empty),
                Value("httpSecretHeaderName", settings.SecretHeader.HeaderName),
                Value("httpSecretValueFormat", settings.SecretHeader.ValueFormat.ToString()),
                Value("httpSecretCustomPrefix", settings.SecretHeader.CustomPrefix),
                Value("httpBody", settings.Body),
                Value("httpMaxResponseBytes", settings.MaxResponseBytes.ToString()),
                Value("httpIncludeInputPayload", settings.IncludeInputPayload.ToString().ToLowerInvariant())
            ]);
        }
        else if (descriptor.Id == WorkflowExecutorIds.Spreadsheet)
        {
            var settings = DeserializeSettings<WorkflowSpreadsheetExecutorSettings>(descriptor);
            values.AddRange(
            [
                Value("spreadsheetOperation", settings.Operation.ToString()),
                Value("spreadsheetWorkbookPath", settings.WorkbookPath),
                Value("spreadsheetOutputWorkbookPath", settings.OutputWorkbookPath),
                Value("spreadsheetWorksheetName", settings.WorksheetName),
                Value("spreadsheetCellAddress", settings.CellAddress),
                Value("spreadsheetRangeAddress", settings.RangeAddress),
                Value("spreadsheetValue", settings.Value),
                Value("spreadsheetCreateWorkbookIfMissing", settings.CreateWorkbookIfMissing.ToString().ToLowerInvariant()),
                Value("spreadsheetOverwrite", settings.Overwrite.ToString().ToLowerInvariant()),
                Value("spreadsheetMaxRows", settings.MaxRows.ToString()),
                Value("spreadsheetMaxColumns", settings.MaxColumns.ToString())
            ]);
        }
        else if (descriptor.Id == WorkflowExecutorIds.ProjectStructure)
        {
            var settings = DeserializeSettings<WorkflowProjectStructureExecutorSettings>(descriptor);
            values.AddRange(
            [
                Value("projectStructureOperation", settings.Operation.ToString()),
                Value("projectStructureProjectId", settings.ProjectId?.ToString("D") ?? string.Empty),
                Value("projectStructureProjectIdJsonPath", settings.ProjectIdJsonPath),
                Value("projectStructureNodeId", settings.NodeId),
                Value("projectStructureNodeIdJsonPath", settings.NodeIdJsonPath),
                Value("projectStructureAssetKind", settings.AssetKind),
                Value("projectStructureTitle", settings.Title),
                Value("projectStructureContent", settings.Content),
                Value("projectStructureContentFromInput", settings.ContentFromInput.ToString().ToLowerInvariant()),
                Value("projectStructureSourceWorkspacePath", settings.SourceWorkspacePath),
                Value("projectStructureContentType", settings.ContentType)
            ]);
        }
        else if (descriptor.Id == WorkflowExecutorIds.ImageGeneration)
        {
            var settings = DeserializeSettings<WorkflowImageGenerationExecutorSettings>(descriptor);
            values.AddRange(
            [
                Value("imageOperation", settings.Operation.ToString()),
                Value("imagePrompt", settings.Prompt),
                Value("imageProviderProfileId", settings.ProviderProfileId?.ToString("D") ?? string.Empty),
                Value("imageModel", settings.Model),
                Value("imageSize", settings.Size),
                Value("imageQuality", settings.Quality),
                Value("imageOutputFormat", settings.OutputFormat),
                Value("imageOutputWorkspacePath", settings.OutputWorkspacePath)
            ]);
        }

        return values;
    }

    private static List<CanvasWorkbenchInputField> BuildInputFields(
        WorkflowExecutorDescriptor descriptor,
        IReadOnlyList<SecretListItem> secrets)
    {
        var fields = new List<CanvasWorkbenchInputField>();
        if (descriptor.Id == WorkflowExecutorIds.StorageFile)
        {
            fields.AddRange(
            [
                SelectField<WorkflowStorageFileOperation>("storageOperation", "Settings", "Choose the workspace file operation and its bounded inputs.", "Operation"),
                TextField("storagePath", "Settings", "Path", "samples/workflows/input.md"),
                TextField("storageDestinationPath", "Settings", "Destination path", "samples/workflows/output.md"),
                TextAreaField("storageContent", "Settings", "Content"),
                BoolField("storageContentFromInput", "Settings", "Content from workflow input"),
                TextField("storageQuery", "Settings", "Search query", "renewal"),
                TextField("storageSearchPattern", "Settings", "Search pattern", "*.md"),
                NumberField("storageMaxResults", "Settings", "Max results", "100"),
                NumberField("storageMaxCharacters", "Settings", "Max characters", "12000"),
                BoolField("storageOverwrite", "Settings", "Overwrite")
            ]);
        }
        else if (descriptor.Id == WorkflowExecutorIds.HttpFetch)
        {
            fields.AddRange(
            [
                SelectField<WorkflowHttpMethodKind>("httpMethod", "Request", "Configure the bounded HTTP request before placing it.", "Method"),
                TextField("httpUrl", "Request", "URL", "https://example.com/feed.json", inputMode: "url"),
                TextField("httpUrlJsonPath", "Request", "URL JSON path", "$.url"),
                TextAreaField("httpHeadersJson", "Request", "Headers JSON", "{\"Accept\":\"application/json\"}"),
                SecretSelectField("httpSecretId", "Secret header", "Stored secret", secrets),
                TextField("httpSecretHeaderName", "Secret header", "Header", "Authorization"),
                SelectField<WorkflowHttpSecretValueFormat>("httpSecretValueFormat", "Secret header", "Choose how the secret value is written into the request header.", "Format"),
                TextField("httpSecretCustomPrefix", "Secret header", "Custom prefix", "Token"),
                TextAreaField("httpBody", "Request", "Body"),
                NumberField("httpMaxResponseBytes", "Request", "Max response bytes", "262144"),
                BoolField("httpIncludeInputPayload", "Request", "Carry input payload")
            ]);
        }
        else if (descriptor.Id == WorkflowExecutorIds.Spreadsheet)
        {
            fields.AddRange(
            [
                SelectField<WorkflowSpreadsheetOperation>("spreadsheetOperation", "Workbook", "Configure workbook IO and bounded range limits.", "Operation"),
                TextField("spreadsheetWorkbookPath", "Workbook", "Workbook path", "samples/workflows/invoices.xlsx", required: true),
                TextField("spreadsheetOutputWorkbookPath", "Workbook", "Output path", "samples/workflows/invoices-reviewed.xlsx"),
                TextField("spreadsheetWorksheetName", "Workbook", "Worksheet", "Invoices", required: true),
                TextField("spreadsheetCellAddress", "Range", "Cell", "G2"),
                TextField("spreadsheetRangeAddress", "Range", "Range", "A1:F20"),
                TextAreaField("spreadsheetValue", "Write", "Value"),
                BoolField("spreadsheetCreateWorkbookIfMissing", "Write", "Create workbook if missing"),
                BoolField("spreadsheetOverwrite", "Write", "Overwrite"),
                NumberField("spreadsheetMaxRows", "Range", "Max rows", "100"),
                NumberField("spreadsheetMaxColumns", "Range", "Max columns", "40")
            ]);
        }
        else if (descriptor.Id == WorkflowExecutorIds.ProjectStructure)
        {
            fields.AddRange(
            [
                SelectField<WorkflowProjectStructureOperation>("projectStructureOperation", "Project structure", "Bind project and asset details explicitly or from JSON payload paths.", "Operation"),
                TextField("projectStructureProjectId", "Project structure", "Project id", "00000000-0000-0000-0000-000000000000"),
                TextField("projectStructureProjectIdJsonPath", "Project structure", "Project id JSON path", "$.projectId"),
                TextField("projectStructureNodeId", "Project structure", "Parent node id"),
                TextField("projectStructureNodeIdJsonPath", "Project structure", "Parent node JSON path", "$.nodeId"),
                TextField("projectStructureTitle", "Asset", "Asset title", "Workflow result"),
                TextField("projectStructureAssetKind", "Asset", "Asset kind", "md"),
                TextAreaField("projectStructureContent", "Asset", "Content"),
                BoolField("projectStructureContentFromInput", "Asset", "Content from workflow input"),
                TextField("projectStructureSourceWorkspacePath", "Asset", "Source workspace path"),
                TextField("projectStructureContentType", "Asset", "Content type", "text/markdown")
            ]);
        }
        else if (descriptor.Id == WorkflowExecutorIds.ImageGeneration)
        {
            fields.AddRange(
            [
                SelectField<WorkflowImageGenerationOperation>("imageOperation", "Image", "Prepare image generation settings before adding the node.", "Operation"),
                TextAreaField("imagePrompt", "Image", "Prompt", required: true),
                TextField("imageProviderProfileId", "Image", "Provider profile id"),
                TextField("imageModel", "Image", "Model", "gpt-image-2"),
                TextField("imageSize", "Image", "Size", "1024x1024"),
                TextField("imageQuality", "Image", "Quality", "low"),
                TextField("imageOutputFormat", "Image", "Output format", "png"),
                TextField("imageOutputWorkspacePath", "Image", "Output workspace path", "generated/workflows/image.png")
            ]);
        }

        fields.AddRange(
        [
            NumberField("timeoutSeconds", "Execution policy", "Timeout seconds", descriptor.DefaultPolicy.TimeoutSeconds.ToString()),
            NumberField("retryAttempts", "Execution policy", "Retries", descriptor.DefaultPolicy.MaxRetryAttempts.ToString()),
            BoolField("captureOutput", "Execution policy", "Capture output")
        ]);

        return fields;
    }

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

    private static CanvasWorkbenchInputField TextAreaField(
        string key,
        string section,
        string label,
        string placeholder = "",
        bool required = false)
        => TextField(key, section, label, placeholder, "textarea", required);

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

    private static CanvasWorkbenchInputField SecretSelectField(
        string key,
        string section,
        string label,
        IReadOnlyList<SecretListItem> secrets)
    {
        var options = secrets
            .OrderBy(secret => secret.Name, StringComparer.OrdinalIgnoreCase)
            .Select(secret => new CanvasWorkbenchInputOption
            {
                Value = secret.Id.ToString("D"),
                Label = $"{secret.Name} ({secret.Kind})"
            })
            .ToList();

        return new CanvasWorkbenchInputField
        {
            Key = key,
            SectionKey = Slug(section),
            SectionTitle = section,
            SectionDescription = "Select a stored secret. The workflow stores only the secret id and resolves it at request time.",
            Label = label,
            Placeholder = "No secret header",
            InputMode = "select",
            IsRequired = false,
            Options = options
        };
    }

    private static CanvasWorkbenchInputField SelectField<TEnum>(
        string key,
        string section,
        string description,
        string label)
        where TEnum : struct, Enum
        => new()
        {
            Key = key,
            SectionKey = Slug(section),
            SectionTitle = section,
            SectionDescription = description,
            Label = label,
            InputMode = "select",
            IsRequired = true,
            Options = Enum.GetValues<TEnum>()
                .Select(value => new CanvasWorkbenchInputOption
                {
                    Value = value.ToString(),
                    Label = value.ToString()
                })
                .ToList()
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

    private static TSettings DeserializeSettings<TSettings>(WorkflowExecutorDescriptor descriptor)
        where TSettings : new()
    {
        if (string.IsNullOrWhiteSpace(descriptor.DefaultSettingsJson))
        {
            return new TSettings();
        }

        return JsonSerializer.Deserialize<TSettings>(descriptor.DefaultSettingsJson, JsonOptions) ?? new TSettings();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
