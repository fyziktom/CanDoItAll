using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Agents.SimpleChats;

public sealed class HrSimpleChatProposalCodec : IAgentToolProposalPreparer {
    public const int SemanticVersion = 1;
    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public bool Supports(string toolName)
        => HrSimpleChatToolPolicy.Operations.Any(operation => string.Equals(operation.ToolName, toolName, StringComparison.Ordinal));

    public AgentToolPreparedPayload Prepare(string toolName, JsonElement arguments) {
        EnsureUniqueProperties(arguments);
        var operation = HrSimpleChatToolPolicy.Get(toolName);
        return operation.Operation switch {
            HrSimpleChatOperation.Search => PrepareSearch(ReadRequest<HrSimpleChatSearchRequest>(arguments)),
            HrSimpleChatOperation.Options => PrepareOptions(arguments),
            HrSimpleChatOperation.Settings => PrepareSettings(ReadRequest<HrSimpleChatDefinitionVersion>(arguments)),
            HrSimpleChatOperation.Create => PrepareCreate(ReadRequest<CreateLlmChatDefinitionCommand>(arguments)),
            HrSimpleChatOperation.Update => PrepareUpdate(ReadRequest<HrSimpleChatUpdateRequest>(arguments)),
            HrSimpleChatOperation.Status => PrepareStatus(ReadRequest<HrSimpleChatStatusRequest>(arguments)),
            HrSimpleChatOperation.Receipt => PrepareReceipt(ReadRequest<HrSimpleChatReceiptRequest>(arguments)),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName))
        };
    }

    public AgentToolPreparedPayload PrepareSearch(HrSimpleChatSearchRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        var query = request.ToOwnerQuery();
        var normalized = request with { SearchText = query.SearchText, Tags = query.Tags };
        return Create(HrSimpleChatOperation.Search, normalized, Hash(HrSimpleChatOperation.Search, normalized));
    }

    public AgentToolPreparedPayload PrepareSettings(HrSimpleChatDefinitionVersion request) {
        ArgumentNullException.ThrowIfNull(request);
        return Create(HrSimpleChatOperation.Settings, request, Hash(HrSimpleChatOperation.Settings, request));
    }

    public AgentToolPreparedPayload PrepareCreate(CreateLlmChatDefinitionCommand request) {
        var prepared = LlmChatDefinitionCreatePreparation.Prepare(request);
        RequireOwnerVersion(prepared.SemanticVersion);
        return Create(HrSimpleChatOperation.Create, prepared.Definition, new(prepared.Fingerprint.Value));
    }

    public AgentToolPreparedPayload PrepareUpdate(HrSimpleChatUpdateRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Expected);
        var prepared = LlmChatDefinitionCreatePreparation.Prepare(request.Definition);
        RequireOwnerVersion(prepared.SemanticVersion);
        var normalized = request with { Definition = prepared.Definition };
        return Create(HrSimpleChatOperation.Update, normalized,
            Hash(HrSimpleChatOperation.Update, new UpdateDigest(request.Expected, prepared.Fingerprint.Value)));
    }

    public AgentToolPreparedPayload PrepareStatus(HrSimpleChatStatusRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Expected);
        if (!Enum.IsDefined(request.Status)) {
            throw new ArgumentOutOfRangeException(nameof(request), "The definition status is invalid.");
        }

        return Create(HrSimpleChatOperation.Status, request, Hash(HrSimpleChatOperation.Status, request));
    }

    public AgentToolPreparedPayload PrepareReceipt(HrSimpleChatReceiptRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(request.IntentId, Guid.Empty);
        return Create(HrSimpleChatOperation.Receipt, request, Hash(HrSimpleChatOperation.Receipt, request));
    }

    public T Read<T>(AgentToolPreparedPayload payload) {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.SemanticVersion != SemanticVersion) {
            throw new InvalidOperationException("The stored Simple Chat proposal semantic version is unsupported.");
        }

        using var document = JsonDocument.Parse(payload.ArgumentsJson);
        var recomputed = Prepare(payload.ToolName, document.RootElement);
        if (recomputed.Digest != payload.Digest || recomputed.Effect != payload.Effect || recomputed.Recovery != payload.Recovery) {
            throw new InvalidOperationException("The stored Simple Chat proposal does not match its immutable semantic digest.");
        }

        return ReadRequest<T>(document.RootElement);
    }

    private static AgentToolPreparedPayload PrepareOptions(JsonElement arguments) {
        if (arguments.ValueKind != JsonValueKind.Object || arguments.EnumerateObject().Any()) {
            throw new JsonException("Creation options do not accept tool arguments.");
        }

        var operation = HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Options);
        return new(operation.ToolName, SemanticVersion, Hash(operation.Operation, new { }), "{}", operation.Effect, operation.Recovery);
    }

    private static AgentToolPreparedPayload Create<T>(HrSimpleChatOperation operation, T request, AgentToolSemanticDigest digest) {
        var policy = HrSimpleChatToolPolicy.Get(operation);
        return new(policy.ToolName, SemanticVersion, digest,
            JsonSerializer.Serialize(new RequestEnvelope<T>(request), SerializerOptions), policy.Effect, policy.Recovery);
    }

    private static AgentToolSemanticDigest Hash<T>(HrSimpleChatOperation operation, T value) {
        var json = JsonSerializer.Serialize(new DigestEnvelope<T>(SemanticVersion,
            HrSimpleChatToolPolicy.Get(operation).ToolName, value), SerializerOptions);
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
    }

    private static T ReadRequest<T>(JsonElement arguments) {
        if (arguments.ValueKind != JsonValueKind.Object ||
            arguments.EnumerateObject().Count() != 1 || !arguments.TryGetProperty("request", out var request)) {
            throw new JsonException("The tool requires exactly one typed request argument.");
        }

        return request.Deserialize<T>(SerializerOptions) ?? throw new JsonException("The tool request cannot be null.");
    }

    private static void EnsureUniqueProperties(JsonElement element) {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException("Duplicate properties are not accepted in a tool proposal.");
                }

                EnsureUniqueProperties(property.Value);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            foreach (var item in element.EnumerateArray()) {
                EnsureUniqueProperties(item);
            }
        }
    }

    private static void RequireOwnerVersion(int version) {
        if (version != SemanticVersion) {
            throw new InvalidOperationException("The owner create semantic version requires an adapter upgrade.");
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private sealed record RequestEnvelope<T>(T Request);
    private sealed record DigestEnvelope<T>(int SemanticVersion, string ToolName, T Payload);
    private sealed record UpdateDigest(HrSimpleChatDefinitionVersion Expected, string DefinitionFingerprint);
}
