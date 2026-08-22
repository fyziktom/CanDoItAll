using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class MafJsonCheckpointStoreAdapter(
    IWorkflowBackendCheckpointPayloadStore store,
    WorkflowBackendCheckpointSession session) : ICheckpointStore<JsonElement>
{
    public async ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(
        string sessionId,
        CheckpointInfo? withParent = null)
    {
        ValidateSession(sessionId);
        var parent = withParent is null ? null : MapLink(withParent);
        var result = await store.ListIndexAsync(session.Id, CancellationToken.None);
        if (result.Outcome == WorkflowBackendCheckpointListOutcome.SessionNotFound)
        {
            return [];
        }

        return result.Checkpoints
            .Where(checkpoint => parent is null || checkpoint.Parent == parent)
            .OrderBy(checkpoint => checkpoint.CommitOrdinal.Value)
            .Select(checkpoint => new CheckpointInfo(
                checkpoint.Link.SessionId.Value,
                checkpoint.Link.CheckpointId.Value))
            .ToArray();
    }

    public async ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId,
        JsonElement value,
        CheckpointInfo? parent = null)
    {
        ValidateSession(sessionId);
        var result = await store.CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                session,
                parent is null ? null : MapLink(parent),
                WorkflowBackendCheckpointPayload.Create(value.GetRawText())),
            CancellationToken.None);
        if (!result.Succeeded || result.Checkpoint is null)
        {
            throw new InvalidOperationException(
                $"MAF checkpoint creation failed with outcome '{result.Outcome}'.");
        }

        var link = result.Checkpoint.Index.Link;
        return new CheckpointInfo(link.SessionId.Value, link.CheckpointId.Value);
    }

    public async ValueTask<JsonElement> RetrieveCheckpointAsync(
        string sessionId,
        CheckpointInfo key)
    {
        ValidateSession(sessionId);
        var result = await store.ReadAsync(MapLink(key), CancellationToken.None);
        if (!result.Succeeded || result.Checkpoint is null)
        {
            throw new InvalidOperationException(
                $"MAF checkpoint retrieval failed with outcome '{result.Outcome}'.");
        }

        using var document = JsonDocument.Parse(result.Checkpoint.Payload.Json);
        return document.RootElement.Clone();
    }

    private WorkflowBackendCheckpointLink MapLink(CheckpointInfo checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ValidateSession(checkpoint.SessionId);
        return new WorkflowBackendCheckpointLink(
            session.Id,
            new WorkflowBackendCheckpointId(checkpoint.CheckpointId));
    }

    private void ValidateSession(string sessionId)
    {
        var requested = new WorkflowBackendSessionId(sessionId);
        if (requested != session.Id)
        {
            throw new InvalidOperationException(
                $"MAF checkpoint session '{requested}' does not match configured session '{session.Id}'.");
        }
    }
}

internal static class MafWorkflowCheckpointProtocol
{
    public static WorkflowBackendCheckpointFormat Format { get; } = new("maf-json");

    public static WorkflowBackendCheckpointFormatVersion FormatVersion { get; } = new(1);

    public static WorkflowCompilerContractVersion CompilerContractVersion { get; } = new(1);

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static CheckpointManager CreateManager(
        IWorkflowBackendCheckpointPayloadStore store,
        WorkflowBackendCheckpointSession session)
        => CheckpointManager.CreateJson(
            new MafJsonCheckpointStoreAdapter(store, session),
            JsonOptions);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            AllowOutOfOrderMetadataProperties = true
        };
        options.MakeReadOnly();
        return options;
    }
}
