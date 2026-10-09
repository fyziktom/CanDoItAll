using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessAuthoringSession(ProcessWorkspaceShellScope Scope, ProcessAuthoringAddress Address, long Revision,
    ProcessAuthoringLifecycle Lifecycle, ProcessAuthoringContent Content, Guid? PublishedId, ProcessAuthoringObservation Observation, string Token) {
    public DateTimeOffset? CommittedAtUtc { get; init; }
    public ProcessDefinitionAuthoringStatus Status => Lifecycle switch {
        ProcessAuthoringLifecycle.Archived => ProcessDefinitionAuthoringStatus.Archived,
        ProcessAuthoringLifecycle.Published => ProcessDefinitionAuthoringStatus.Published,
        _ when Revision == 0 || Lifecycle == ProcessAuthoringLifecycle.Deleted => ProcessDefinitionAuthoringStatus.TemplateDefault,
        _ => ProcessDefinitionAuthoringStatus.Draft
    };
}

public sealed class ProcessAuthoringWorkspace(IProcessAuthoringStore store, IProcessAuthoringContext context, ProcessTemplatePackLoader templates) {
    private readonly Dictionary<(ProcessWorkspaceShellScope Scope, ProcessDefinitionCatalogItemKey Key), Task<ProcessAuthoringSession>> reads = [];

    public Task<ProcessAuthoringSession> ReadAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken) {
        var cacheKey = (scope, key);
        if (!reads.TryGetValue(cacheKey, out var read)) {
            read = ReadCoreAsync(scope, key, cancellationToken);
            reads.Add(cacheKey, read);
        }
        return read;
    }

    private async Task<ProcessAuthoringSession> ReadCoreAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken) {
        var address = await context.CaptureReadAddressAsync(scope, key, cancellationToken);
        var head = await store.ReadAsync(address, cancellationToken);
        var content = head is { Lifecycle: not ProcessAuthoringLifecycle.Deleted } ? head.Content : null;
        long inheritedRevision = 0;
        if (content is null && address.ProjectId != Guid.Empty) {
            var inherited = await store.ReadAsync(address with { ProjectId = Guid.Empty, ProjectLifetimeId = Guid.Empty }, cancellationToken);
            inheritedRevision = inherited?.Revision ?? 0;
            if (inherited is { Lifecycle: not ProcessAuthoringLifecycle.Deleted }) {
                content = inherited.Content;
            }
        }
        content ??= ReadTemplate(key.Value);
        return CreateSession(scope, address, head?.Revision ?? 0, head?.Lifecycle ?? ProcessAuthoringLifecycle.Deleted, content, head?.PublishedId, inheritedRevision);
    }

    public ProcessAuthoringContent ReadTemplate(string key) {
        var definition = templates.LoadDefinition(key);
        if (definition.Key != key) {
            throw new InvalidOperationException("Use the exact canonical definition key from the catalog.");
        }
        var canonical = JsonSerializer.Serialize(definition, ProcessTemplateJsonContext.Default.ProcessTemplateDefinitionDocument);
        return new(ProcessAuthoringContent.CurrentSchemaVersion, definition,
            definition.Steps.ToDictionary(step => step.Key, step => step.ResolvedExecutionGuidance), templates.LoadRoleResources(key),
            new("distributed-template", templates.Load().Manifest.Version, ProcessAuthoringCodec.Hash(canonical)), [], []);
    }

    public async Task<ProcessAuthoringReceipt?> RecoverAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key,
        string? token, Guid operationId, string fingerprint, CancellationToken cancellationToken) {
        var address = token is null ? await context.CaptureReadAddressAsync(scope, key, cancellationToken) : AddressFromToken(scope, key, token);
        return await store.RecoverAsync(address, context.CallerId, operationId, fingerprint, cancellationToken);
    }

    public async Task<ProcessAuthoringOperationStatus> GetOperationAsync(ProcessAuthoringOperationQuery query, CancellationToken cancellationToken) {
        var address = AddressFromToken(query.Scope, query.DefinitionKey, query.VersionToken);
        var receipt = await store.GetOperationAsync(address, context.CallerId, query.OperationId, cancellationToken);
        return new(receipt is null ? ProcessAuthoringOperationState.NotRecorded : receipt.Outcome == ProcessAuthoringOutcome.Accepted
            ? ProcessAuthoringOperationState.Committed : ProcessAuthoringOperationState.Rejected,
            receipt?.Snapshot is null ? null : FromReceipt(query.Scope, receipt).Observation);
    }

    public bool Matches(ProcessAuthoringSession current, string? expectedToken) {
        if (expectedToken is null) {
            return current.Revision == 0;
        }
        var expected = ParseToken(expectedToken);
        return current.Observation == expected;
    }

    public async Task<ProcessAuthoringReceipt> CommitAsync(ProcessAuthoringSession baseline, Guid operationId, string fingerprint,
        ProcessAuthoringContent content, ProcessAuthoringLifecycle lifecycle, bool publish, ProcessAuthoringSelection? selection,
        CancellationToken cancellationToken) {
        var saved = await store.CommitAsync(new(baseline.Address, context.CallerId, operationId, fingerprint,
            baseline.Revision, content, lifecycle, publish) {
                Selection = selection,
                ExpectedInheritedRevision = baseline.Address.ProjectId != Guid.Empty && baseline.Lifecycle == ProcessAuthoringLifecycle.Deleted
                    ? baseline.Observation.InheritedRevision : null
            }, cancellationToken);
        reads.Clear();
        return saved;
    }

    public ProcessAuthoringSession FromReceipt(ProcessWorkspaceShellScope scope, ProcessAuthoringReceipt receipt) {
        var snapshot = receipt.Snapshot ?? throw new InvalidOperationException("The authoring receipt has no committed definition.");
        var session = CreateSession(scope, snapshot.Address, snapshot.Revision, snapshot.Lifecycle, snapshot.Content, snapshot.PublishedId, snapshot.InheritedRevision)
            with { CommittedAtUtc = snapshot.UpdatedAtUtc };
        reads[(scope, new(snapshot.Address.DefinitionKey))] = Task.FromResult(session);
        return session;
    }

    public async Task<ProcessAuthoringContent> InheritedContentAsync(ProcessAuthoringSession session, CancellationToken cancellationToken) {
        if (session.Address.ProjectId != Guid.Empty) {
            var global = await store.ReadAsync(session.Address with { ProjectId = Guid.Empty, ProjectLifetimeId = Guid.Empty }, cancellationToken);
            if (global is { Lifecycle: not ProcessAuthoringLifecycle.Deleted }) {
                return global.Content;
            }
        }
        return ReadTemplate(session.Address.DefinitionKey);
    }

    public async Task<IReadOnlyList<ProcessAuthoringCatalogEntry>> CatalogAsync(ProcessWorkspaceShellScope scope, CancellationToken cancellationToken) {
        var address = await context.CaptureReadAddressAsync(scope, new("catalog"), cancellationToken);
        return await store.ListAsync(address.DatabaseProfileId, address.ProjectId, address.ProjectLifetimeId, cancellationToken);
    }

    private ProcessAuthoringAddress AddressFromToken(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, string token) {
        var observed = ParseToken(token);
        if (observed.DatabaseProfileId != context.DatabaseProfileId || observed.DefinitionKey != key ||
                observed.ProjectId != (scope.ProjectId ?? Guid.Empty)) {
            throw new InvalidOperationException("The submitted authoring origin differs from the current profile or workspace.");
        }
        return new(observed.DatabaseProfileId, observed.ProjectId, observed.ProjectLifetimeId, key.Value);
    }

    private static ProcessAuthoringSession CreateSession(ProcessWorkspaceShellScope scope, ProcessAuthoringAddress address, long revision,
        ProcessAuthoringLifecycle lifecycle, ProcessAuthoringContent content, Guid? publishedId, long inheritedRevision = 0) {
        ProcessAuthoringObservation observation = new(address.DatabaseProfileId, address.ProjectId, address.ProjectLifetimeId,
            new(address.DefinitionKey), revision, ProcessAuthoringCodec.Hash(ProcessAuthoringCodec.Write(content)), publishedId) { InheritedRevision = inheritedRevision };
        var token = TokenPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(observation,
            ProcessAuthoringCommandJsonContext.Default.ProcessAuthoringObservation)));
        return new(scope, address, revision, lifecycle, content, publishedId, observation, token);
    }

    private const string TokenPrefix = "authoring.v1.";

    private static ProcessAuthoringObservation ParseToken(string token) {
        if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal) || token.Length > 4096) {
            throw new InvalidOperationException("Reload the definition before submitting an older authoring token.");
        }
        try {
            return JsonSerializer.Deserialize(Convert.FromBase64String(token[TokenPrefix.Length..]),
                ProcessAuthoringCommandJsonContext.Default.ProcessAuthoringObservation)
                ?? throw new InvalidOperationException("The authoring origin is absent.");
        } catch (Exception exception) when (exception is FormatException or JsonException) {
            throw new InvalidOperationException("The authoring origin is invalid; reload the definition.", exception);
        }
    }
}

internal static class ProcessAuthoringRequests {
    public static (T Command, string Fingerprint) Capture<T>(T command) {
        var info = ProcessAuthoringCommandJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new NotSupportedException("The authoring command has no registered codec.");
        var json = JsonSerializer.Serialize(command, info);
        return (JsonSerializer.Deserialize(json, info)!, ProcessAuthoringCodec.Hash(json));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProcessAuthoringObservation))]
[JsonSerializable(typeof(ProcessDefinitionEditorCommand))]
[JsonSerializable(typeof(ProcessDefinitionRoleEditorCommand))]
[JsonSerializable(typeof(ProcessDefinitionStepEditorCommand))]
[JsonSerializable(typeof(ProcessDefinitionCanvasCommand))]
[JsonSerializable(typeof(ProcessTemplateImportCommand))]
internal sealed partial class ProcessAuthoringCommandJsonContext : JsonSerializerContext;
