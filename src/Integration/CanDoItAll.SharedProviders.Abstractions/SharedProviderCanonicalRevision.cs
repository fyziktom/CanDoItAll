using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace CanDoItAll.SharedProviders.Abstractions;

public static class SharedProviderCanonicalRevision
{
    public static SharedProviderPublicRevision ComputeCatalog(SharedProviderCatalogDocument catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(catalog.Protocols);
        ArgumentNullException.ThrowIfNull(catalog.Providers);
        SharedProviderProtocolJson.ValidateCatalogShape(catalog);

        return Compute(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", catalog.SchemaVersion.Value);
            writer.WriteString("sourceInstanceId", catalog.SourceInstanceId.ToString());
            writer.WriteStartObject("protocols");
            writer.WriteString("openAiCompatibleBasePath", catalog.Protocols.OpenAiCompatibleBasePath);
            writer.WriteEndObject();
            writer.WriteStartArray("providers");
            foreach (var publication in catalog.Providers.OrderBy(
                item => item.PublicationId.ToString(),
                StringComparer.Ordinal))
            {
                WritePublication(writer, publication);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static SharedProviderPublicRevision ComputePublication(
        SharedProviderCatalogPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        SharedProviderProtocolJson.ValidatePublication(publication);
        return Compute(writer => WritePublication(writer, publication));
    }

    private static SharedProviderPublicRevision Compute(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        var hash = SHA256.HashData(buffer.WrittenSpan);
        return new SharedProviderPublicRevision(
            $"{SharedProviderPublicRevision.Prefix}{Convert.ToHexStringLower(hash)}");
    }

    private static void WritePublication(
        Utf8JsonWriter writer,
        SharedProviderCatalogPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(publication.Models);

        writer.WriteStartObject();
        writer.WriteString("publicationId", publication.PublicationId.ToString());
        writer.WriteString("displayName", publication.DisplayName);
        writer.WriteString("purpose", SharedProviderPurposeJsonConverter.GetToken(publication.Purpose));
        writer.WriteString("transport", SharedProviderTransportJsonConverter.GetToken(publication.Transport));
        writer.WriteString("defaultModelId", publication.DefaultModelId.Value);
        writer.WriteStartArray("models");
        foreach (var model in publication.Models.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(model.Capabilities);

            writer.WriteStartObject();
            writer.WriteString("id", model.Id.Value);
            writer.WriteString("displayName", model.DisplayName);
            writer.WriteStartArray("capabilities");
            foreach (var capability in model.Capabilities
                .Select(SharedProviderCapabilityJsonConverter.GetToken)
                .Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(capability);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartObject("health");
        writer.WriteString("state", SharedProviderHealthStateJsonConverter.GetToken(publication.Health.State));
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
