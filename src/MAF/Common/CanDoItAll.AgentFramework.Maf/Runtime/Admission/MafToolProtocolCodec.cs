using System.ClientModel.Primitives;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafToolProtocolCodec {
    internal const string Format = "maf-tool-protocol";
    internal const int Version = 1;
    internal static JsonSerializerOptions SerializationOptions => Options;
    private static readonly ModelReaderWriterOptions ModelOptions = new("J");
    private static readonly IReadOnlyDictionary<string, Type> OpenAiTypes = ModelTypes(typeof(OpenAIClient).Assembly,
        type => typeof(IPersistableModel<object>).IsAssignableFrom(type));
    private static readonly IReadOnlyDictionary<string, Type> OllamaTypes = ModelTypes(typeof(OllamaApiClient).Assembly,
        type => type.Namespace?.StartsWith("OllamaSharp.Models", StringComparison.Ordinal) == true);
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static readonly string PackageFingerprint = string.Join("|",
        typeof(ChatResponse).Assembly.GetName().Version,
        typeof(OpenAIClient).Assembly.GetName().Version,
        typeof(OllamaApiClient).Assembly.GetName().Version);

    internal static AgentToolProtocolEnvelope Encode<T>(T value) {
        try {
            var payload = JsonSerializer.SerializeToElement(value, Options);
            var json = JsonSerializer.Serialize(new SavedProtocol(PackageFingerprint, typeof(T).FullName!, payload));
            var envelope = AgentToolProtocolEnvelope.Create(Format, Version, json);
            var restored = Decode<T>(envelope);
            var original = Canonicalize(payload);
            var roundTrip = Canonicalize(JsonSerializer.SerializeToElement(restored, Options));
            if (!string.Equals(original, roundTrip, StringComparison.Ordinal)) {
                throw Unsupported("The installed SDK cannot round-trip this complete protocol envelope.");
            }

            return envelope;
        } catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException) {
            throw new AgentToolAdmissionException("tool-admission.unsupported-protocol",
                $"The protocol checkpoint cannot be saved safely ({exception.GetType().Name}). Recovery must inspect the persisted dispatch state before any retry.");
        }
    }

    internal static T Decode<T>(AgentToolProtocolEnvelope envelope) {
        if (envelope.Format != Format || envelope.Version != Version) {
            throw Unsupported("The saved protocol format is unsupported; explicit recovery is required.");
        }

        var saved = JsonSerializer.Deserialize<SavedProtocol>(envelope.PayloadJson)
            ?? throw Unsupported("The saved protocol envelope is empty.");
        if (saved.Packages != PackageFingerprint || saved.Shape != typeof(T).FullName) {
            throw Unsupported("The saved protocol requires a different installed SDK or shape; it cannot be replayed implicitly.");
        }

        return saved.Value.Deserialize<T>(Options) ?? throw Unsupported("The saved protocol value is empty.");
    }

    internal static AgentToolSemanticDigest Digest<T>(T value)
        => AgentToolProtocolEnvelope.ComputeDigest(Canonicalize(JsonSerializer.SerializeToElement(value, Options)));

    internal static string Canonicalize(JsonElement value) {
        return value.ValueKind == JsonValueKind.Null ? "null" : Normalize(value).ToJsonString();

        static JsonNode Normalize(JsonElement item) {
            if (item.ValueKind == JsonValueKind.Object) {
                var properties = item.EnumerateObject().ToArray();
                if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) {
                    throw Unsupported("Duplicate object properties are not supported in admitted protocol data.");
                }

                var result = new JsonObject();
                foreach (var property in properties.OrderBy(property => property.Name, StringComparer.Ordinal)) {
                    result.Add(property.Name, property.Value.ValueKind == JsonValueKind.Null ? null : Normalize(property.Value));
                }

                return result;
            }

            if (item.ValueKind == JsonValueKind.Array) {
                var result = new JsonArray();
                foreach (var child in item.EnumerateArray()) {
                    result.Add(child.ValueKind == JsonValueKind.Null ? null : Normalize(child));
                }

                return result;
            }

            return JsonNode.Parse(item.GetRawText())!;
        }
    }

    private static JsonSerializerOptions CreateOptions() {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo => {
            if (typeInfo.Kind != JsonTypeInfoKind.Object || typeInfo.Type.Assembly != typeof(AIContent).Assembly) {
                return;
            }

            var rawProperty = typeInfo.Type.GetProperty(nameof(AIContent.RawRepresentation));
            if (rawProperty is not { CanRead: true, CanWrite: true }) {
                return;
            }

            var property = typeInfo.CreateJsonPropertyInfo(typeof(SavedRawModel), "providerRaw");
            property.Get = target => SaveRaw(rawProperty.GetValue(target));
            property.Set = (target, value) => rawProperty.SetValue(target, RestoreRaw((SavedRawModel?)value));
            typeInfo.Properties.Add(property);
        });
        return new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) {
            TypeInfoResolver = resolver,
            MaxDepth = 64
        };
    }

    private static SavedRawModel? SaveRaw(object? raw) {
        if (raw is null) {
            return null;
        }

        Type? type = raw.GetType();
        if (type.Assembly == typeof(OpenAIClient).Assembly && raw is IPersistableModel<object>) {
            while (type is not null && !OpenAiTypes.ContainsKey(type.FullName!)) {
                type = type.BaseType;
            }

            if (type is not null) {
                var json = ModelReaderWriter.Write(raw, ModelOptions).ToString();
                return new(NativeProtocolKind.OpenAi, type.FullName!, json);
            }
        }

        if (type is not null && OllamaTypes.ContainsKey(type.FullName!)) {
            return new(NativeProtocolKind.Ollama, type.FullName!, JsonSerializer.Serialize(raw, type));
        }

        throw Unsupported("An opaque provider item has no supported installed-package serializer.");
    }

    private static object? RestoreRaw(SavedRawModel? saved) {
        if (saved is null) {
            return null;
        }

        if (saved.Kind == NativeProtocolKind.OpenAi && OpenAiTypes.TryGetValue(saved.Model, out var openAiType)) {
            return ModelReaderWriter.Read(BinaryData.FromString(saved.Json), openAiType, ModelOptions);
        }

        if (saved.Kind == NativeProtocolKind.Ollama && OllamaTypes.TryGetValue(saved.Model, out var ollamaType)) {
            return JsonSerializer.Deserialize(saved.Json, ollamaType)
                ?? throw Unsupported("The saved Ollama protocol item is empty.");
        }

        throw Unsupported("The saved opaque provider item is outside the installed-package allowlist.");
    }

    private static IReadOnlyDictionary<string, Type> ModelTypes(Assembly assembly, Func<Type, bool> include)
        => assembly.GetTypes().Where(type => type.IsVisible && !type.IsGenericTypeDefinition && include(type))
            .ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    private static AgentToolAdmissionException Unsupported(string message)
        => new("tool-admission.unsupported-protocol", message);

    private enum NativeProtocolKind { OpenAi, Ollama }
    private sealed record SavedRawModel(NativeProtocolKind Kind, string Model, string Json);
    private sealed record SavedProtocol(string Packages, string Shape, JsonElement Value);
}
