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
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly WorkflowValueShape ResultShape = new(WorkflowValueShapeKind.Json, "{}", "Office365 email message batch JSON.");
    private static readonly WorkflowExecutorSourceDescriptor PluginSource = WorkflowExecutorSourceDescriptor.BundledPlugin(
        Office365PluginConstants.PluginId.Value,
        "1.0.0");

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
        Simulation = Office365WorkflowSimulationTemplates.DownloadByCategory
    };

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = JsonSerializer.Deserialize<Office365WorkflowExecutorSettings>(context.SettingsJson, JsonOptions)
                       ?? throw new InvalidOperationException("Office365 executor settings are invalid.");
        if (!Guid.TryParse(settings.ConnectionId, out var connectionGuid) || connectionGuid == Guid.Empty)
        {
            throw new InvalidOperationException("Office365 executor requires a valid connectionId setting.");
        }

        var token = await oauthService.GetAccessTokenAsync(
            Office365PluginConstants.PluginId,
            new PluginConnectionId(connectionGuid),
            [Office365PluginConstants.MailReadScope],
            cancellationToken);
        var batch = await graphClient.DownloadMessagesByCategoryAsync(
            token.AccessToken,
            settings.Category,
            settings.MaxMessages,
            cancellationToken);

        return new WorkflowNodeExecutionResult(
            context.Node.Id,
            CreatePayload(input, batch),
            ResultShape);
    }

    private static string CreatePayload(
        WorkflowNodeInput input,
        PluginEmailMessageBatch batch)
    {
        var payload = new JsonObject
        {
            ["provider"] = batch.Provider,
            ["filterKind"] = batch.FilterKind,
            ["filterValue"] = batch.FilterValue,
            ["count"] = batch.Count,
            ["messages"] = JsonSerializer.SerializeToNode(batch.Messages, JsonOptions)
        };

        if (TryParseObject(input.PayloadJson, out var workflowInput))
        {
            CopyIfPresent(workflowInput, payload, "projectId");
            CopyIfPresent(workflowInput, payload, "nodeId");
            CopyIfPresent(workflowInput, payload, "project");
            CopyIfPresent(workflowInput, payload, "runContext");
            payload["workflowInput"] = workflowInput.DeepClone();
        }

        return payload.ToJsonString(JsonOptions);
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
          "projectId": "{{inputPath:$.projectId}}",
          "nodeId": "{{inputPath:$.nodeId}}",
          "project": "{{inputPath:$.project}}",
          "runContext": "{{inputPath:$.runContext}}",
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
}
