using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class WorkflowExecutorCanvasCatalogTests
{
    [Fact]
    public void BuildQuickCreateActions_groups_plugin_executors_under_plugin_menu()
    {
        var builtIn = CreateExecutor(
            "builtin.utility",
            "Built-in utility",
            WorkflowExecutorSourceDescriptor.BuiltIn("1.0.0"));
        var officeDownload = CreateExecutor(
            "office365.download",
            "Office365 download",
            WorkflowExecutorSourceDescriptor.BundledPlugin(
                "office365.mail",
                "1.0.0",
                "Office365 Mail",
                UiIconDescriptor.MaterialIcon("business_center", "Office365 Mail")));
        var officeMark = CreateExecutor(
            "office365.mark",
            "Office365 mark processed",
            WorkflowExecutorSourceDescriptor.BundledPlugin(
                "office365.mail",
                "1.0.0",
                "Office365 Mail",
                UiIconDescriptor.MaterialIcon("business_center", "Office365 Mail")));

        var actions = WorkflowExecutorCanvasCatalog.BuildQuickCreateActions(
            [officeMark, builtIn, officeDownload],
            []);

        var executorsMenu = Assert.Single(actions);
        var builtInActionId = WorkflowExecutorCanvasCatalog.BuildCreateActionId(builtIn.Id);
        var officeDownloadActionId = WorkflowExecutorCanvasCatalog.BuildCreateActionId(officeDownload.Id);
        var pluginMenu = Assert.Single(executorsMenu.Children, item => item.ActionId == "workflow-executor:plugins");
        var officeGroup = Assert.Single(pluginMenu.Children, item => item.ActionId == "workflow-executor:plugins:office365.mail");

        Assert.Contains(executorsMenu.Children, item => item.ActionId == builtInActionId);
        Assert.DoesNotContain(executorsMenu.Children, item => item.ActionId == officeDownloadActionId);
        Assert.Equal("Office365 Mail", officeGroup.Label);
        Assert.Equal("business_center", officeGroup.Icon);
        Assert.Contains(officeGroup.Children, item => item.ActionId == officeDownloadActionId);
        Assert.Contains(officeGroup.Children, item => item.ActionId == WorkflowExecutorCanvasCatalog.BuildCreateActionId(officeMark.Id));
    }

    [Fact]
    public void BuildCreateAction_includes_side_effect_and_retry_safety_metadata()
    {
        var executor = CreateExecutor(
            "external.write",
            "External write",
            WorkflowExecutorSourceDescriptor.BuiltIn("1.0.0"),
            WorkflowExecutorSideEffectDescriptor.ExternalWrite(
                WorkflowExecutorExternalMutationKind.None,
                requiresCommitIdempotencyKey: false,
                allowsIdempotentRetry: false,
                idempotencyKeyJsonPath: string.Empty,
                receiptSchema: "{}"),
            new WorkflowExecutorExecutionPolicy(
                TimeoutSeconds: 30,
                MaxRetryAttempts: 2,
                RetryDelayMilliseconds: 250,
                CaptureOutputArtifact: false));

        var action = WorkflowExecutorCanvasCatalog.BuildCreateAction(executor);

        Assert.Contains("Available", action.Description, StringComparison.Ordinal);
        Assert.Contains("External write", action.Description, StringComparison.Ordinal);
        Assert.Contains("Unsafe retries", action.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCreateAction_projects_any_executor_configuration_schema()
    {
        var schema = new ConfigurationSchema(
            "2.0",
            [
                new ConfigurationFieldDescriptor(
                    "sourcePath",
                    "Source path",
                    ConfigurationFieldType.Text,
                    IsRequired: true,
                    "Workspace source path."),
                new ConfigurationFieldDescriptor(
                    "maxBytes",
                    "Max bytes",
                    ConfigurationFieldType.Number,
                    IsRequired: false,
                    "Maximum source bytes."),
                new ConfigurationFieldDescriptor(
                    "options",
                    "Options",
                    ConfigurationFieldType.Json,
                    IsRequired: false,
                    "Executor-specific JSON options.")
            ]);
        var executor = CreateExecutor(
            "plugin.document.convert",
            "Plugin document converter",
            WorkflowExecutorSourceDescriptor.BundledPlugin(
                "document.plugin",
                "2.0.0",
                "Document plugin")) with
        {
            ConfigurationSchema = schema,
            DefaultSettingsJson = """{"sourcePath":"in/report.docx","maxBytes":10485760,"options":{"format":"markdown"}}"""
        };

        var action = WorkflowExecutorCanvasCatalog.BuildCreateAction(executor);

        Assert.Equal("test-renderer", action.SetupRendererKey);
        Assert.Contains(action.InputFields, field =>
            field.Key == WorkflowExecutorConfigurationMapper.BuildInputKey("sourcePath") &&
            field.IsRequired);
        Assert.Contains(action.InputFields, field =>
            field.Key == WorkflowExecutorConfigurationMapper.BuildInputKey("maxBytes") &&
            field.InputMode == "number");
        Assert.Contains(action.InputFields, field =>
            field.Key == WorkflowExecutorConfigurationMapper.BuildInputKey("options") &&
            field.InputMode == "textarea");
        Assert.Contains(action.DefaultInputValues, value =>
            value.Key == WorkflowExecutorConfigurationMapper.BuildInputKey("sourcePath") &&
            value.Value == "in/report.docx");
        Assert.Contains(action.DefaultInputValues, value =>
            value.Key == WorkflowExecutorConfigurationMapper.BuildInputKey("maxBytes") &&
            value.Value == "10485760");
    }

    [Fact]
    public void BuildCreateAction_projects_every_field_for_new_document_and_image_executors()
    {
        var descriptors = new[]
        {
            BuiltInWorkflowExecutorDescriptors.DocumentToMarkdown,
            BuiltInWorkflowExecutorDescriptors.ImageInspect,
            BuiltInWorkflowExecutorDescriptors.ImageAnalyze,
            BuiltInWorkflowExecutorDescriptors.StorageFile,
            BuiltInWorkflowExecutorDescriptors.Spreadsheet
        };

        foreach (var descriptor in descriptors)
        {
            var action = WorkflowExecutorCanvasCatalog.BuildCreateAction(descriptor);
            var actionFieldKeys = action.InputFields
                .Select(field => field.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.All(
                descriptor.ConfigurationSchema.Fields,
                field => Assert.Contains(
                    WorkflowExecutorConfigurationMapper.BuildInputKey(field.Key),
                    actionFieldKeys));
        }

        var imageAnalyzeFields = BuiltInWorkflowExecutorDescriptors.ImageAnalyze.ConfigurationSchema.Fields
            .ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(ConfigurationNumberKind.Int64, imageAnalyzeFields["maxBytes"].NumberKind);
        Assert.Equal(ConfigurationFieldType.Guid, imageAnalyzeFields["providerProfileId"].FieldType);
    }

    [Fact]
    public void BuildCreateAction_routes_custom_settings_to_the_trusted_renderer_instead_of_the_generic_dialog()
    {
        var customAction = WorkflowExecutorCanvasCatalog.BuildCreateAction(
            BuiltInWorkflowExecutorDescriptors.ImageGeneration);
        var schemaAction = WorkflowExecutorCanvasCatalog.BuildCreateAction(
            BuiltInWorkflowExecutorDescriptors.ImageInspect);

        Assert.False(customAction.RequiresInput);
        Assert.True(schemaAction.RequiresInput);
        Assert.Equal(
            BuiltInWorkflowExecutorDescriptors.ImageGeneration.SetupRendererKey,
            customAction.SetupRendererKey);
    }

    [Fact]
    public void ConfigurationMapper_round_trips_typed_values_without_int_truncation()
    {
        var schema = new ConfigurationSchema(
            "1.0",
            [
                new ConfigurationFieldDescriptor("maxBytes", "Max bytes", ConfigurationFieldType.Number, false, string.Empty)
                {
                    NumberKind = ConfigurationNumberKind.Int64
                },
                new ConfigurationFieldDescriptor("enabled", "Enabled", ConfigurationFieldType.Boolean, false, string.Empty),
                new ConfigurationFieldDescriptor("options", "Options", ConfigurationFieldType.Json, false, string.Empty)
            ]);
        var state = WorkflowExecutorConfigurationMapper.ReadState(
            """{"maxBytes":1099511627776,"enabled":true,"options":{"mode":"strict"}}""",
            schema);

        var json = WorkflowExecutorConfigurationMapper.SerializeState(schema, state);
        using var document = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(1099511627776L, document.RootElement.GetProperty("maxBytes").GetInt64());
        Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal("strict", document.RootElement.GetProperty("options").GetProperty("mode").GetString());
    }

    [Fact]
    public void ConfigurationMapper_rejects_fractional_integer_and_invalid_guid_settings()
    {
        var schema = new ConfigurationSchema(
            "1.0",
            [
                new ConfigurationFieldDescriptor("maxRows", "Max rows", ConfigurationFieldType.Number, false, string.Empty),
                new ConfigurationFieldDescriptor("providerId", "Provider id", ConfigurationFieldType.Guid, false, string.Empty)
            ]);
        var state = new ConfigurationState(new Dictionary<string, string>
        {
            ["maxRows"] = "1.5",
            ["providerId"] = "not-a-guid"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            WorkflowExecutorConfigurationMapper.SerializeState(schema, state));

        Assert.Contains("Int32", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GUID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationMapper_rejects_invalid_settings_instead_of_hiding_them()
    {
        var schema = ConfigurationSchema.Empty();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            WorkflowExecutorConfigurationMapper.ReadState("{", schema));

        Assert.Contains("invalid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfigurationMapper_allows_partial_editor_state_but_rejects_incomplete_create_state()
    {
        var schema = new ConfigurationSchema(
            "1.0",
            [
                new ConfigurationFieldDescriptor(
                    "requiredName",
                    "Required name",
                    ConfigurationFieldType.Text,
                    IsRequired: true,
                    string.Empty)
            ]);
        var state = new ConfigurationState();

        var partialJson = WorkflowExecutorConfigurationMapper.SerializeState(schema, state);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            WorkflowExecutorConfigurationMapper.SerializeCompleteState(schema, state));

        Assert.Equal("{}", partialJson);
        Assert.Contains("requiredName", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Workflow_canvas_editor_has_no_executor_id_specific_settings_branches()
    {
        var root = FindRepositoryRoot();
        var razorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "WorkflowCanvasEditor.razor"));
        var codeBehindSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "WorkflowCanvasEditor.razor.cs"));
        var forbiddenExecutorIds = new[]
        {
            nameof(WorkflowExecutorIds.StorageFile),
            nameof(WorkflowExecutorIds.HttpFetch),
            nameof(WorkflowExecutorIds.Spreadsheet),
            nameof(WorkflowExecutorIds.ProjectStructure),
            nameof(WorkflowExecutorIds.ImageGeneration)
        };

        Assert.Contains(nameof(SettingsRendererHost), razorSource, StringComparison.Ordinal);
        Assert.Contains("ShouldRenderSettingsRenderer(descriptor)", razorSource, StringComparison.Ordinal);
        Assert.Contains(nameof(WorkflowExecutorSettingsPresentationMode.CustomRenderer), codeBehindSource, StringComparison.Ordinal);
        foreach (var executorId in forbiddenExecutorIds)
        {
            Assert.DoesNotContain(
                $"WorkflowExecutorIds.{executorId}",
                razorSource,
                StringComparison.Ordinal);
        }

        Assert.DoesNotContain("ReadExecutorSettings<", codeBehindSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateExecutorSettings<", codeBehindSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateEnumExecutorSettings<", codeBehindSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateHttp", codeBehindSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSpreadsheet", codeBehindSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_canvas_llm_editor_uses_gallery_identity_and_node_execution_settings()
    {
        var root = FindRepositoryRoot();
        var razorSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "WorkflowCanvasEditor.razor"));
        var codeBehindSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "WorkflowCanvasEditor.razor.cs"));

        Assert.DoesNotContain("data-testid=\"workflow-canvas-node-component\"", razorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("data-testid=\"workflow-canvas-node-modal-component\"", razorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleSelectedComponentChanged", codeBehindSource, StringComparison.Ordinal);
        Assert.DoesNotContain("componentOptions.FirstOrDefault() ??", codeBehindSource, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"workflow-canvas-node-prompt-identity\"", razorSource, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"workflow-canvas-node-provider\"", razorSource, StringComparison.Ordinal);
        Assert.Contains("workflow-canvas-node-model-selector", razorSource, StringComparison.Ordinal);
        Assert.Contains("readonly=\"@(selected.Kind == WorkflowNodeKind.LlmCall)\"", razorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_canvas_gallery_selection_prefers_declared_pair_then_preserves_compatible_pair()
    {
        var currentProvider = CreateProviderOption(
            "Current provider",
            ProviderKind.OpenAi,
            "current-model");
        var preferredProvider = CreateProviderOption(
            "Preferred provider",
            ProviderKind.AzureOpenAi,
            "preferred-model");
        var editor = new WorkflowCanvasEditor();
        typeof(WorkflowCanvasEditor)
            .GetProperty(nameof(WorkflowCanvasEditor.ProviderOptions))!
            .SetValue(editor, new WorkflowProviderOption[] { currentProvider, preferredProvider });
        SetPrivateField(editor, "newComponentProviderProfileId", currentProvider.ProviderProfileId.ToString("D"));
        SetPrivateField(editor, "newComponentModel", currentProvider.DefaultModel);

        var preferredSelection = CreateSelection(
        [
            new PromptProviderModel(ProviderKind.OpenAi.ToString(), currentProvider.DefaultModel),
            new PromptProviderModel(ProviderKind.AzureOpenAi.ToString(), preferredProvider.DefaultModel, IsPreferred: true)
        ]);
        var preferredPair = ResolveExecutionPair(editor, preferredSelection);

        Assert.Equal(preferredProvider.ProviderProfileId, preferredPair.Provider.ProviderProfileId);
        Assert.Equal(preferredProvider.DefaultModel, preferredPair.Model);

        var compatibleSelection = CreateSelection(
        [
            new PromptProviderModel(ProviderKind.OpenAi.ToString(), currentProvider.DefaultModel),
            new PromptProviderModel(ProviderKind.AzureOpenAi.ToString(), preferredProvider.DefaultModel)
        ]);
        var compatiblePair = ResolveExecutionPair(editor, compatibleSelection);

        Assert.Equal(currentProvider.ProviderProfileId, compatiblePair.Provider.ProviderProfileId);
        Assert.Equal(currentProvider.DefaultModel, compatiblePair.Model);
    }

    private static PromptGallerySelection CreateSelection(IReadOnlyList<PromptProviderModel> supportedModels)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            VersionNumber: 1,
            "Gallery prompt",
            "Prompt summary",
            PromptGalleryItemKind.FullPrompt,
            "Pinned prompt snapshot.",
            Tags: [],
            supportedModels,
            new PromptModelRecommendations());

    private static WorkflowProviderOption CreateProviderOption(
        string name,
        ProviderKind kind,
        string model)
        => new(
            Guid.NewGuid(),
            name,
            kind,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            model,
            [model],
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            SupportsStructuredOutput: true,
            SupportsVision: true,
            SupportsBackgroundResponses: true);

    private static (WorkflowProviderOption Provider, string Model) ResolveExecutionPair(
        WorkflowCanvasEditor editor,
        PromptGallerySelection selection)
    {
        var method = typeof(WorkflowCanvasEditor).GetMethod(
            "ResolvePromptBindingExecutionPair",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Workflow Gallery execution-pair resolver was not found.");
        var pair = method.Invoke(editor, [selection, null])
            ?? throw new InvalidOperationException("Workflow Gallery execution-pair resolver returned no pair.");
        var pairType = pair.GetType();
        var provider = pairType.GetProperty("Provider")?.GetValue(pair) as WorkflowProviderOption
            ?? throw new InvalidOperationException("Workflow Gallery execution pair has no provider.");
        var model = pairType.GetProperty("Model")?.GetValue(pair) as string
            ?? throw new InvalidOperationException("Workflow Gallery execution pair has no model.");
        return (provider, model);
    }

    private static void SetPrivateField(WorkflowCanvasEditor editor, string name, object value)
    {
        var field = typeof(WorkflowCanvasEditor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Workflow canvas field '{name}' was not found.");
        field.SetValue(editor, value);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static WorkflowExecutorDescriptor CreateExecutor(
        string id,
        string name,
        WorkflowExecutorSourceDescriptor source,
        WorkflowExecutorSideEffectDescriptor? sideEffects = null,
        WorkflowExecutorExecutionPolicy? defaultPolicy = null)
        => new(
            new WorkflowExecutorId(id),
            name,
            $"{name} description.",
            WorkflowExecutorCategoryKind.Utility,
            "bolt",
            "test-renderer",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            "{}",
            "{}",
            defaultPolicy ?? WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            Source = source,
            SideEffects = sideEffects ?? WorkflowExecutorSideEffectDescriptor.None
        };
}
