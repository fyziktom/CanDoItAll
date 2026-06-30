using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public sealed class Office365DownloadByCategoryWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    PluginOAuthService oauthService,
    Office365GraphClient graphClient) : IWorkflowExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = Office365WorkflowJson.Options;
    private static readonly WorkflowValueShape ResultShape = new(WorkflowValueShapeKind.Json, "{}", "Office365 email message batch JSON.");
    private static readonly WorkflowExecutorSourceDescriptor PluginSource = WorkflowExecutorSourceDescriptor.BundledPlugin(
        Office365PluginConstants.PluginId.Value,
        "1.0.0",
        "Office365 Mail",
        Office365PluginConstants.Icon);

    public WorkflowExecutorDescriptor Descriptor => new(
        Office365PluginConstants.DownloadByCategoryExecutorId,
        "Office365 messages by category",
        "Downloads a bounded batch of Microsoft Graph mail messages that have the configured Outlook category.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        Office365PluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        ResultShape,
        "{\"type\":\"object\"}",
        JsonSerializer.Serialize(new Office365WorkflowExecutorSettings(), JsonOptions),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = PluginSource,
        Availability = ResolveAvailability(),
        Simulation = Office365WorkflowSimulationTemplates.DownloadByCategory,
        PermissionPolicy = new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead(EmailWorkflowSideEffectConstants.ExternalReadReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Run Preview uses simulated Microsoft Graph messages without calling Office365.")
    };

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = JsonSerializer.Deserialize<Office365WorkflowExecutorSettings>(context.SettingsJson, JsonOptions)
                       ?? throw new InvalidOperationException("Office365 executor settings are invalid.");

        var connectionId = await oauthService.ResolveWorkflowConnectionIdAsync(
            Office365PluginConstants.PluginId,
            Office365PluginConstants.ConnectionKey,
            settings.ConnectionId,
            [Office365PluginConstants.MailReadScope],
            cancellationToken);
        var token = await oauthService.GetAccessTokenAsync(
            Office365PluginConstants.PluginId,
            connectionId,
            [Office365PluginConstants.MailReadScope],
            cancellationToken);
        var batch = await graphClient.DownloadMessagesByCategoryAsync(
            token.AccessToken,
            settings.Category,
            settings.MaxMessages,
            cancellationToken);
        if (batch.Count == 0)
        {
            throw new InvalidOperationException($"No Office365 messages were found with category '{settings.Category}'.");
        }

        return new WorkflowNodeExecutionResult(
            context.Node.Id,
            CreatePayload(input, settings, connectionId, batch),
            ResultShape);
    }

    private static string CreatePayload(
        WorkflowNodeInput input,
        Office365WorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
    {
        var payload = new JsonObject
        {
            ["provider"] = batch.Provider,
            ["filterKind"] = batch.FilterKind,
            ["filterValue"] = batch.FilterValue,
            ["processedCategory"] = settings.ProcessedCategory,
            ["count"] = batch.Count,
            ["messages"] = JsonSerializer.SerializeToNode(batch.Messages, JsonOptions)
        };
        var processing = CreateProcessingPayload(settings, connectionId, batch);
        payload["office365Processing"] = processing.DeepClone();

        if (TryParseObject(input.PayloadJson, out var workflowInput))
        {
            CopyIfPresent(workflowInput, payload, "projectId");
            CopyIfPresent(workflowInput, payload, "nodeId");
            CopyIfPresent(workflowInput, payload, "project");
            payload["runContext"] = CreateRunContext(workflowInput, processing);
            payload["workflowInput"] = workflowInput.DeepClone();
        }
        else
        {
            payload["runContext"] = new JsonObject
            {
                ["office365Processing"] = processing.DeepClone()
            };
        }

        return payload.ToJsonString(JsonOptions);
    }

    private static JsonObject CreateProcessingPayload(
        Office365WorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
        => new()
        {
            ["connectionId"] = connectionId.ToString(),
            ["sourceCategory"] = settings.Category,
            ["processedCategory"] = settings.ProcessedCategory,
            ["messageIds"] = JsonSerializer.SerializeToNode(batch.Messages.Select(message => message.Id).ToArray(), JsonOptions),
            ["idempotencyKeys"] = JsonSerializer.SerializeToNode(
                batch.Messages.Select(message => CreateOffice365IdempotencyKey(message.Id)).ToArray(),
                JsonOptions)
        };

    private static JsonObject CreateRunContext(
        JsonObject workflowInput,
        JsonObject processing)
    {
        var runContext = workflowInput.TryGetPropertyValue("runContext", out var value) && value is JsonObject context
            ? (JsonObject)context.DeepClone()
            : new JsonObject();
        runContext["office365Processing"] = processing.DeepClone();
        return runContext;
    }

    private static bool TryParseObject(
        string json,
        out JsonObject value)
    {
        try
        {
            value = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            return value.Count > 0;
        }
        catch (JsonException)
        {
            value = new JsonObject();
            return false;
        }
    }

    private static void CopyIfPresent(
        JsonObject source,
        JsonObject target,
        string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out var value) && value is not null)
        {
            target[propertyName] = value.DeepClone();
        }
    }

    private static string CreateOffice365IdempotencyKey(string messageId)
        => string.IsNullOrWhiteSpace(messageId)
            ? string.Empty
            : $"{EmailWorkflowSideEffectConstants.Office365IdempotencyPrefix}{messageId.Trim()}";

    private WorkflowExecutorAvailabilityDescriptor ResolveAvailability()
    {
        var workflowGrant = grantEvaluator.Evaluate(Office365PluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
        if (!workflowGrant.Allowed)
        {
            return WorkflowExecutorAvailabilityDescriptor.Unavailable(workflowGrant.Kind.ToString(), workflowGrant.Message);
        }

        var oauthGrant = grantEvaluator.Evaluate(Office365PluginConstants.PluginId, PluginCapabilityKind.OAuth2);
        return oauthGrant.Allowed
            ? WorkflowExecutorAvailabilityDescriptor.Available()
            : WorkflowExecutorAvailabilityDescriptor.Unavailable(oauthGrant.Kind.ToString(), oauthGrant.Message);
    }

}

public sealed class Office365DownloadByAddressWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    PluginOAuthService oauthService,
    Office365GraphClient graphClient) : IWorkflowExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = Office365WorkflowJson.Options;
    private static readonly WorkflowValueShape ResultShape = new(WorkflowValueShapeKind.Json, "{}", "Office365 address-matched email message JSON.");
    private static readonly WorkflowExecutorSourceDescriptor PluginSource = WorkflowExecutorSourceDescriptor.BundledPlugin(
        Office365PluginConstants.PluginId.Value,
        "1.0.0",
        "Office365 Mail",
        Office365PluginConstants.Icon);

    public WorkflowExecutorDescriptor Descriptor => new(
        Office365PluginConstants.DownloadByAddressExecutorId,
        "Office365 unprocessed message by address",
        "Downloads at most one newest Microsoft 365 message from or sent by a configured email address, excluding messages already carrying the processed category.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        Office365PluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        ResultShape,
        "{\"type\":\"object\"}",
        JsonSerializer.Serialize(new Office365MessageAddressWorkflowExecutorSettings(), JsonOptions),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = PluginSource,
        Availability = ResolveAvailability(),
        Simulation = Office365WorkflowSimulationTemplates.DownloadByAddress,
        PermissionPolicy = new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead(EmailWorkflowSideEffectConstants.ExternalReadReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Run Preview uses simulated Microsoft Graph messages without calling Office365.")
    };

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = JsonSerializer.Deserialize<Office365MessageAddressWorkflowExecutorSettings>(context.SettingsJson, JsonOptions)
                       ?? throw new InvalidOperationException("Office365 address executor settings are invalid.");
        var filterSettings = CreateFilterSettings(settings, input);
        var configuredConnectionId = EmailWorkflowPayloadResolver.ResolveOptionalInputJsonString(
            input,
            settings.ConnectionId,
            settings.ConnectionIdJsonPath,
            nameof(settings.ConnectionIdJsonPath),
            "Office365 address executor");
        var connectionId = await oauthService.ResolveWorkflowConnectionIdAsync(
            Office365PluginConstants.PluginId,
            Office365PluginConstants.ConnectionKey,
            configuredConnectionId,
            [Office365PluginConstants.MailReadScope],
            cancellationToken);
        var token = await oauthService.GetAccessTokenAsync(
            Office365PluginConstants.PluginId,
            connectionId,
            [Office365PluginConstants.MailReadScope],
            cancellationToken);
        var batch = await graphClient.DownloadOneUnprocessedMessageByAddressAsync(
            token.AccessToken,
            filterSettings,
            cancellationToken);
        if (batch.Count == 0 && settings.NoMessageBehavior == Office365NoMessageBehavior.Fail)
        {
            throw new InvalidOperationException($"No unprocessed Office365 messages matched '{filterSettings.EmailAddress}'.");
        }

        var resolvedPayloadSettings = settings with
        {
            ProcessedCategory = filterSettings.ProcessedCategory
        };
        return new WorkflowNodeExecutionResult(
            context.Node.Id,
            CreatePayload(input, resolvedPayloadSettings, connectionId, batch),
            ResultShape);
    }

    private static Office365MessageAddressFilterSettings CreateFilterSettings(
        Office365MessageAddressWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
    {
        var emailAddress = !string.IsNullOrWhiteSpace(settings.EmailAddress)
            ? settings.EmailAddress
            : EmailWorkflowPayloadResolver.ResolveInputJsonString(
                input,
                settings.EmailAddressJsonPath,
                nameof(settings.EmailAddressJsonPath),
                "Office365 address executor");

        return new Office365MessageAddressFilterSettings
        {
            EmailAddress = emailAddress,
            ProcessedCategory = EmailWorkflowPayloadResolver.ResolveOptionalInputJsonString(
                input,
                settings.ProcessedCategory,
                settings.ProcessedCategoryJsonPath,
                nameof(settings.ProcessedCategoryJsonPath),
                "Office365 address executor"),
            MailFolderId = settings.MailFolderId,
            MatchMode = settings.MatchMode,
            MaxCandidateMessages = settings.MaxCandidateMessages,
            LookbackHours = EmailWorkflowPayloadResolver.ResolveOptionalInputJsonInt32(
                input,
                settings.LookbackHours,
                settings.LookbackHoursJsonPath,
                nameof(settings.LookbackHoursJsonPath),
                "Office365 address executor"),
            MaxBodyCharacters = settings.MaxBodyCharacters,
            IncludeBody = settings.IncludeBody
        };
    }

    private static string CreatePayload(
        WorkflowNodeInput input,
        Office365MessageAddressWorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
    {
        var payload = new JsonObject
        {
            ["provider"] = batch.Provider,
            ["filterKind"] = batch.FilterKind,
            ["filterValue"] = batch.FilterValue,
            ["processedCategory"] = settings.ProcessedCategory,
            ["count"] = batch.Count,
            ["messages"] = JsonSerializer.SerializeToNode(batch.Messages, JsonOptions)
        };
        if (batch.Count == 0)
        {
            payload["noMessages"] = true;
            payload["route"] = "no_messages";
            payload["summary"] = "No unprocessed Office365 email matched the configured address.";
        }

        var processing = CreateProcessingPayload(settings, connectionId, batch);
        payload["office365Processing"] = processing.DeepClone();

        if (TryParseObject(input.PayloadJson, out var workflowInput))
        {
            CopyIfPresent(workflowInput, payload, "projectId");
            CopyIfPresent(workflowInput, payload, "nodeId");
            CopyIfPresent(workflowInput, payload, "project");
            payload["runContext"] = CreateRunContext(workflowInput, processing);
            payload["workflowInput"] = workflowInput.DeepClone();
        }
        else
        {
            payload["runContext"] = new JsonObject
            {
                ["office365Processing"] = processing.DeepClone()
            };
        }

        return payload.ToJsonString(JsonOptions);
    }

    private static JsonObject CreateProcessingPayload(
        Office365MessageAddressWorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
    {
        var selectedMessageId = batch.Messages.FirstOrDefault()?.Id ?? string.Empty;
        return new JsonObject
        {
            ["connectionId"] = connectionId.ToString(),
            ["processedCategory"] = settings.ProcessedCategory,
            ["messageIds"] = JsonSerializer.SerializeToNode(batch.Messages.Select(message => message.Id).ToArray(), JsonOptions),
            ["selectedMessageId"] = selectedMessageId,
            ["idempotencyKey"] = CreateOffice365IdempotencyKey(selectedMessageId),
            ["idempotencyKeys"] = JsonSerializer.SerializeToNode(
                batch.Messages.Select(message => CreateOffice365IdempotencyKey(message.Id)).ToArray(),
                JsonOptions)
        };
    }

    private static JsonObject CreateRunContext(
        JsonObject workflowInput,
        JsonObject processing)
    {
        var runContext = workflowInput.TryGetPropertyValue("runContext", out var value) && value is JsonObject context
            ? (JsonObject)context.DeepClone()
            : new JsonObject();
        runContext["office365Processing"] = processing.DeepClone();
        return runContext;
    }

    private static bool TryParseObject(
        string json,
        out JsonObject value)
    {
        try
        {
            value = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            return value.Count > 0;
        }
        catch (JsonException)
        {
            value = new JsonObject();
            return false;
        }
    }

    private static void CopyIfPresent(
        JsonObject source,
        JsonObject target,
        string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out var value) && value is not null)
        {
            target[propertyName] = value.DeepClone();
        }
    }

    private static string CreateOffice365IdempotencyKey(string messageId)
        => string.IsNullOrWhiteSpace(messageId)
            ? string.Empty
            : $"{EmailWorkflowSideEffectConstants.Office365IdempotencyPrefix}{messageId.Trim()}";

    private WorkflowExecutorAvailabilityDescriptor ResolveAvailability()
    {
        var workflowGrant = grantEvaluator.Evaluate(Office365PluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
        if (!workflowGrant.Allowed)
        {
            return WorkflowExecutorAvailabilityDescriptor.Unavailable(workflowGrant.Kind.ToString(), workflowGrant.Message);
        }

        var oauthGrant = grantEvaluator.Evaluate(Office365PluginConstants.PluginId, PluginCapabilityKind.OAuth2);
        return oauthGrant.Allowed
            ? WorkflowExecutorAvailabilityDescriptor.Available()
            : WorkflowExecutorAvailabilityDescriptor.Unavailable(oauthGrant.Kind.ToString(), oauthGrant.Message);
    }

}

public sealed class Office365MarkProcessedWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    PluginOAuthService oauthService,
    Office365GraphClient graphClient) : IWorkflowExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = Office365WorkflowJson.Options;
    private static readonly WorkflowValueShape ResultShape = new(WorkflowValueShapeKind.Json, "{}", "Office365 category mutation JSON.");
    private static readonly WorkflowExecutorSourceDescriptor PluginSource = WorkflowExecutorSourceDescriptor.BundledPlugin(
        Office365PluginConstants.PluginId.Value,
        "1.0.0",
        "Office365 Mail",
        Office365PluginConstants.Icon);

    public WorkflowExecutorDescriptor Descriptor => new(
        Office365PluginConstants.MarkProcessedExecutorId,
        "Office365 mark processed",
        "Adds the processed Outlook category to a Microsoft 365 message and optionally removes the source category.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        Office365PluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        ResultShape,
        "{\"type\":\"object\"}",
        JsonSerializer.Serialize(new Office365MarkProcessedWorkflowExecutorSettings(), JsonOptions),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = PluginSource,
        Availability = ResolveAvailability(),
        Simulation = Office365WorkflowSimulationTemplates.MarkProcessed,
        PermissionPolicy = new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.WritesExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets |
            WorkflowExecutorCapabilityFlags.IdempotentExternalMarker |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
            "$.externalSideEffectReceipt.idempotencyKey",
            EmailWorkflowSideEffectConstants.ProcessedMarkerReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Run Preview simulates the Office365 category mutation without changing Microsoft Graph.")
    };

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = JsonSerializer.Deserialize<Office365MarkProcessedWorkflowExecutorSettings>(context.SettingsJson, JsonOptions)
                       ?? throw new InvalidOperationException("Office365 mark-processed executor settings are invalid.");
        var configuredConnectionId = EmailWorkflowPayloadResolver.ResolveOptionalInputJsonString(
            input,
            settings.ConnectionId,
            settings.ConnectionIdJsonPath,
            nameof(settings.ConnectionIdJsonPath),
            "Office365 mark-processed executor");
        var processedCategory = EmailWorkflowPayloadResolver.ResolveOptionalInputJsonString(
            input,
            settings.ProcessedCategory,
            settings.ProcessedCategoryJsonPath,
            nameof(settings.ProcessedCategoryJsonPath),
            "Office365 mark-processed executor");

        var messageId = EmailWorkflowPayloadResolver.ResolveInputJsonString(
            input,
            settings.MessageIdJsonPath,
            nameof(settings.MessageIdJsonPath),
            "Office365 mark-processed executor");
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("Office365 mark-processed executor resolved an empty message id.");
        }

        var connectionId = await oauthService.ResolveWorkflowConnectionIdAsync(
            Office365PluginConstants.PluginId,
            Office365PluginConstants.ConnectionKey,
            configuredConnectionId,
            [Office365PluginConstants.MailReadWriteScope, Office365PluginConstants.MailboxSettingsReadWriteScope],
            cancellationToken);
        var token = await oauthService.GetAccessTokenAsync(
            Office365PluginConstants.PluginId,
            connectionId,
            [Office365PluginConstants.MailReadWriteScope, Office365PluginConstants.MailboxSettingsReadWriteScope],
            cancellationToken);
        var result = await graphClient.MarkMessageProcessedAsync(
            token.AccessToken,
            messageId,
            settings.SourceCategory,
            processedCategory,
            cancellationToken);

        return new WorkflowNodeExecutionResult(
            context.Node.Id,
            CreatePayload(input, result),
            ResultShape);
    }

    private static string CreatePayload(
        WorkflowNodeInput input,
        Office365MessageCategoryMutationResult result)
    {
        var payload = new JsonObject
        {
            ["provider"] = result.Provider,
            ["messageId"] = result.MessageId,
            ["sourceCategory"] = result.SourceCategory,
            ["processedCategory"] = result.ProcessedCategory,
            ["sourceCategoryRemoved"] = result.SourceCategoryRemoved,
            ["processedCategoryAdded"] = result.ProcessedCategoryAdded,
            ["processedCategoryCreated"] = result.ProcessedCategoryCreated,
            ["categories"] = JsonSerializer.SerializeToNode(result.Categories, JsonOptions),
            ["sideEffectMode"] = EmailWorkflowSideEffectConstants.CommitMode,
            ["dryRun"] = false
        };
        var receipt = CreateProcessedMarkerReceipt(result);
        payload["committed"] = receipt["mutationApplied"]?.GetValue<bool>() ?? false;
        payload["idempotencyRecord"] = CreateIdempotencyRecord(result.Provider, result.MessageId);
        payload["processedMarker"] = CreateProcessedMarkerRecord(result);
        payload["externalSideEffectReceipt"] = receipt;

        if (TryParseObject(input.PayloadJson, out var workflowInput))
        {
            payload["inputPayload"] = workflowInput.DeepClone();
        }

        return payload.ToJsonString(JsonOptions);
    }

    private static JsonObject CreateProcessedMarkerReceipt(Office365MessageCategoryMutationResult result)
        => new()
        {
            ["schemaVersion"] = EmailWorkflowSideEffectConstants.ProcessedMarkerReceiptSchema,
            ["provider"] = result.Provider,
            ["operation"] = EmailWorkflowSideEffectConstants.Operation,
            ["mode"] = EmailWorkflowSideEffectConstants.CommitMode,
            ["dryRun"] = false,
            ["committed"] = result.SourceCategoryRemoved || result.ProcessedCategoryAdded || result.ProcessedCategoryCreated,
            ["mutationApplied"] = result.SourceCategoryRemoved || result.ProcessedCategoryAdded || result.ProcessedCategoryCreated,
            ["idempotencyKey"] = CreateOffice365IdempotencyKey(result.MessageId),
            ["messageId"] = result.MessageId,
            ["sourceMarkerName"] = result.SourceCategory,
            ["processedMarkerName"] = result.ProcessedCategory,
            ["sourceMarkerRemoved"] = result.SourceCategoryRemoved,
            ["processedMarkerAdded"] = result.ProcessedCategoryAdded,
            ["processedMarkerCreated"] = result.ProcessedCategoryCreated,
            ["retrySafe"] = true
        };

    private static JsonObject CreateIdempotencyRecord(
        string provider,
        string messageId)
        => new()
        {
            ["provider"] = provider,
            ["operation"] = EmailWorkflowSideEffectConstants.Operation,
            ["key"] = CreateOffice365IdempotencyKey(messageId),
            ["messageId"] = messageId,
            ["retrySafe"] = true
        };

    private static JsonObject CreateProcessedMarkerRecord(Office365MessageCategoryMutationResult result)
        => new()
        {
            ["provider"] = result.Provider,
            ["messageId"] = result.MessageId,
            ["sourceMarkerName"] = result.SourceCategory,
            ["processedMarkerName"] = result.ProcessedCategory,
            ["sourceMarkerRemoved"] = result.SourceCategoryRemoved,
            ["processedMarkerAdded"] = result.ProcessedCategoryAdded,
            ["processedMarkerCreated"] = result.ProcessedCategoryCreated
        };

    private static string CreateOffice365IdempotencyKey(string messageId)
        => string.IsNullOrWhiteSpace(messageId)
            ? string.Empty
            : $"{EmailWorkflowSideEffectConstants.Office365IdempotencyPrefix}{messageId.Trim()}";

    private static bool TryParseObject(
        string json,
        out JsonObject value)
    {
        try
        {
            value = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            return value.Count > 0;
        }
        catch (JsonException)
        {
            value = new JsonObject();
            return false;
        }
    }

    private WorkflowExecutorAvailabilityDescriptor ResolveAvailability()
    {
        var workflowGrant = grantEvaluator.Evaluate(Office365PluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
        if (!workflowGrant.Allowed)
        {
            return WorkflowExecutorAvailabilityDescriptor.Unavailable(workflowGrant.Kind.ToString(), workflowGrant.Message);
        }

        var oauthGrant = grantEvaluator.Evaluate(Office365PluginConstants.PluginId, PluginCapabilityKind.OAuth2);
        return oauthGrant.Allowed
            ? WorkflowExecutorAvailabilityDescriptor.Available()
            : WorkflowExecutorAvailabilityDescriptor.Unavailable(oauthGrant.Kind.ToString(), oauthGrant.Message);
    }

}

internal static class Office365WorkflowJson
{
    public static JsonSerializerOptions Options { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal static class Office365WorkflowSimulationTemplates
{
    public static WorkflowExecutorSimulationDescriptor DownloadByCategory { get; } = WorkflowExecutorSimulationDescriptor.JsonTemplate(
        """
        {
          "provider": "office365",
          "filterKind": "category",
          "filterValue": "simulated-preview-category",
          "processedCategory": "simulated-preview-processed",
          "count": 1,
          "messages": [
            {
              "id": "simulated-office365-message-1",
              "threadId": "simulated-office365-thread-1",
              "subject": "Simulated workflow preview message",
              "from": "preview@example.test",
              "receivedAt": "{{utcNow}}",
              "snippet": "Preview-only Office365 message generated for workflow simulation.",
              "bodyText": "This is simulated Microsoft 365 message content for a Run Preview flow. It is not fetched from Microsoft Graph.",
              "labels": [
                "simulated-preview-category"
              ],
              "webLink": "https://outlook.office.com/mail/inbox/id/simulated-office365-message-1"
            }
          ],
          "office365Processing": {
            "connectionId": "preview",
            "sourceCategory": "simulated-preview-category",
            "processedCategory": "simulated-preview-processed",
            "messageIds": [
              "simulated-office365-message-1"
            ],
            "idempotencyKeys": [
              "office365:simulated-office365-message-1"
            ]
          },
          "projectId": "{{inputPath:$.projectId}}",
          "nodeId": "{{inputPath:$.nodeId}}",
          "project": "{{inputPath:$.project}}",
          "runContext": {
            "workflowNodeId": "{{inputPath:$.runContext.workflowNodeId}}",
            "office365Processing": {
              "connectionId": "preview",
              "sourceCategory": "simulated-preview-category",
              "processedCategory": "simulated-preview-processed",
              "messageIds": [
                "simulated-office365-message-1"
              ],
              "idempotencyKeys": [
                "office365:simulated-office365-message-1"
              ]
            }
          },
          "workflowInput": "{{inputPayload}}",
          "simulation": {
            "nodeId": "{{node.id}}",
            "nodeName": "{{node.name}}",
            "sourceExecutorId": "{{source.executor.id}}",
            "reason": "{{simulation.reason}}",
            "generatedAtUtc": "{{utcNow}}"
          }
        }
        """,
        "Simulate a one-message Microsoft 365 category download without calling Graph.");

    public static WorkflowExecutorSimulationDescriptor DownloadByAddress { get; } = WorkflowExecutorSimulationDescriptor.JsonTemplate(
        """
        {
          "provider": "office365",
          "filterKind": "emailAddress",
          "filterValue": "{{inputPath:$.emailAddress}}",
          "processedCategory": "{{inputPath:$.processedCategory}}",
          "count": 1,
          "messages": [
            {
              "id": "simulated-office365-message-1",
              "threadId": "simulated-office365-conversation-1",
              "subject": "Simulated email-watch message",
              "from": "{{inputPath:$.emailAddress}}",
              "receivedAt": "{{utcNow}}",
              "snippet": "Preview-only Office365 message generated for workflow simulation.",
              "bodyText": "This is simulated Microsoft 365 message content for an Office365 email-watch Run Preview flow. It is not fetched from Microsoft Graph.",
              "labels": [],
              "webLink": "https://outlook.office.com/mail/inbox/id/simulated-office365-message-1"
            }
          ],
          "office365Processing": {
            "connectionId": "preview",
            "processedCategory": "{{inputPath:$.processedCategory}}",
            "messageIds": [
              "simulated-office365-message-1"
            ],
            "selectedMessageId": "simulated-office365-message-1",
            "idempotencyKey": "office365:simulated-office365-message-1",
            "idempotencyKeys": [
              "office365:simulated-office365-message-1"
            ]
          },
          "projectId": "{{inputPath:$.projectId}}",
          "nodeId": "{{inputPath:$.nodeId}}",
          "project": "{{inputPath:$.project}}",
          "runContext": {
            "workflowNodeId": "{{inputPath:$.runContext.workflowNodeId}}",
            "office365Processing": {
              "connectionId": "preview",
              "processedCategory": "{{inputPath:$.processedCategory}}",
              "messageIds": [
                "simulated-office365-message-1"
              ],
              "selectedMessageId": "simulated-office365-message-1",
              "idempotencyKey": "office365:simulated-office365-message-1",
              "idempotencyKeys": [
                "office365:simulated-office365-message-1"
              ]
            }
          },
          "workflowInput": "{{inputPayload}}",
          "simulation": {
            "nodeId": "{{node.id}}",
            "nodeName": "{{node.name}}",
            "sourceExecutorId": "{{source.executor.id}}",
            "reason": "{{simulation.reason}}",
            "generatedAtUtc": "{{utcNow}}"
          }
        }
        """,
        "Simulate one unprocessed Microsoft 365 message matched by address without calling Graph.");

    public static WorkflowExecutorSimulationDescriptor MarkProcessed { get; } = WorkflowExecutorSimulationDescriptor.JsonTemplate(
        """
        {
          "provider": "office365",
          "messageId": "simulated-office365-message-1",
          "sourceCategory": "simulated-preview-category",
          "processedCategory": "simulated-preview-processed",
          "sourceCategoryRemoved": true,
          "processedCategoryAdded": true,
          "processedCategoryCreated": true,
          "categories": [
            "simulated-preview-processed"
          ],
          "sideEffectMode": "Preview",
          "dryRun": true,
          "committed": false,
          "idempotencyRecord": {
            "provider": "office365",
            "operation": "processed-marker",
            "key": "office365:simulated-office365-message-1",
            "messageId": "simulated-office365-message-1",
            "retrySafe": true
          },
          "processedMarker": {
            "provider": "office365",
            "messageId": "simulated-office365-message-1",
            "sourceMarkerName": "simulated-preview-category",
            "processedMarkerName": "simulated-preview-processed",
            "sourceMarkerRemoved": true,
            "processedMarkerAdded": true,
            "processedMarkerCreated": true
          },
          "externalSideEffectReceipt": {
            "schemaVersion": "workflow-email-processed-marker/v1",
            "provider": "office365",
            "operation": "processed-marker",
            "mode": "Preview",
            "dryRun": true,
            "committed": false,
            "mutationApplied": false,
            "idempotencyKey": "office365:simulated-office365-message-1",
            "messageId": "simulated-office365-message-1",
            "sourceMarkerName": "simulated-preview-category",
            "processedMarkerName": "simulated-preview-processed",
            "sourceMarkerRemoved": true,
            "processedMarkerAdded": true,
            "processedMarkerCreated": true,
            "retrySafe": true
          },
          "inputPayload": "{{inputPayload}}",
          "simulation": {
            "nodeId": "{{node.id}}",
            "nodeName": "{{node.name}}",
            "sourceExecutorId": "{{source.executor.id}}",
            "reason": "{{simulation.reason}}",
            "generatedAtUtc": "{{utcNow}}"
          }
        }
        """,
        "Simulate the Office365 category mutation without modifying Microsoft Graph.");
}
