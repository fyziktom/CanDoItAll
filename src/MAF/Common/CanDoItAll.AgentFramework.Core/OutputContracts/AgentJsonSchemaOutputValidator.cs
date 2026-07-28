using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class AgentJsonSchemaOutputValidator
{
    public static AgentJsonSchemaOutputResult Validate(
        string schemaJson,
        string? rawOutput,
        string schemaName = "json_schema_output",
        bool strict = false)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            throw new ArgumentException("A JSON Schema is required.", nameof(schemaJson));
        }

        JsonElement schema;
        try
        {
            using var document = JsonDocument.Parse(
                schemaJson,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });
            schema = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new AgentJsonSchemaOutputContractException(
                "agents.structured-output-schema-invalid-json",
                $"Structured output schema is not one complete JSON value: {exception.Message}");
        }

        var prepared = AgentJsonSchemaOutputContractProcessor.PrepareResponseFormatValidation(
            schema,
            schemaName,
            strict);

        return AgentJsonSchemaOutputContractProcessor.ValidateOutput(prepared, rawOutput);
    }
}
