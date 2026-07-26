using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentJsonSchemaOutputContractProcessorTests
{
    private const string ValidSchemaJson =
        """
        {
          "type": "object",
          "properties": {
            "status": {
              "type": "string",
              "enum": [ "ready", "blocked" ]
            },
            "count": {
              "type": "integer",
              "minimum": 1
            }
          },
          "required": [ "status", "count" ],
          "additionalProperties": false
        }
        """;

    [Fact]
    public void Prepare_normalizes_contract_and_computes_deterministic_schema_hash()
    {
        var schema = ParseElement(ValidSchemaJson);
        var contract = new AgentJsonSchemaOutputContract(
            Kind: $" {AgentJsonSchemaOutputContractVersions.Kind} ",
            Version: $" {AgentJsonSchemaOutputContractVersions.Current} ",
            Name: " portable_result ",
            Schema: schema,
            Strict: true);

        var first = Assert.IsType<PreparedAgentJsonSchemaOutputContract>(
            AgentJsonSchemaOutputContractProcessor.Prepare(contract));
        var second = Assert.IsType<PreparedAgentJsonSchemaOutputContract>(
            AgentJsonSchemaOutputContractProcessor.Prepare(contract));
        var expectedHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(schema.GetRawText())))
            .ToLowerInvariant();

        Assert.Equal(AgentJsonSchemaOutputContractVersions.Kind, first.Kind);
        Assert.Equal(AgentJsonSchemaOutputContractVersions.Current, first.Version);
        Assert.Equal("portable_result", first.Name);
        Assert.Equal(schema.GetRawText(), first.SchemaJson);
        Assert.Equal(expectedHash, first.SchemaHash);
        Assert.Equal(first.SchemaHash, second.SchemaHash);
        Assert.True(first.Strict);
    }

    [Fact]
    public void Prepare_rejects_unsupported_kind()
    {
        var exception = Assert.Throws<AgentJsonSchemaOutputContractException>(
            () => AgentJsonSchemaOutputContractProcessor.Prepare(
                CreateContract(ParseElement(ValidSchemaJson), kind: "provider-native")));

        Assert.Equal("agents.structured-output-kind-unsupported", exception.Code);
    }

    [Fact]
    public void Prepare_rejects_unsupported_version()
    {
        var exception = Assert.Throws<AgentJsonSchemaOutputContractException>(
            () => AgentJsonSchemaOutputContractProcessor.Prepare(
                CreateContract(ParseElement(ValidSchemaJson), version: "2.0")));

        Assert.Equal("agents.structured-output-version-unsupported", exception.Code);
    }

    [Fact]
    public void Prepare_rejects_strict_object_with_optional_declared_property()
    {
        var schema = ParseElement(
            """
            {
              "type": "object",
              "properties": {
                "status": { "type": "string" }
              },
              "required": [],
              "additionalProperties": false
            }
            """);

        var exception = Assert.Throws<AgentJsonSchemaOutputContractException>(
            () => AgentJsonSchemaOutputContractProcessor.Prepare(CreateContract(schema)));

        Assert.Equal("agents.structured-output-strict-object-invalid", exception.Code);
    }

    [Fact]
    public void Prepare_rejects_unsupported_schema_keyword()
    {
        var schema = ParseElement(
            """
            {
              "type": "object",
              "properties": {},
              "required": [],
              "additionalProperties": false,
              "oneOf": [
                { "type": "object" }
              ]
            }
            """);

        var exception = Assert.Throws<AgentJsonSchemaOutputContractException>(
            () => AgentJsonSchemaOutputContractProcessor.Prepare(CreateContract(schema)));

        Assert.Equal("agents.structured-output-keyword-unsupported", exception.Code);
        Assert.Contains("oneOf", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Prepare_rejects_schema_that_exceeds_utf8_size_limit()
    {
        var oversizedDescription = new string('x', AgentJsonSchemaOutputContractProcessor.MaximumSchemaBytes);
        var schema = ParseElement(
            $$"""
            {
              "type": "object",
              "description": "{{oversizedDescription}}",
              "properties": {},
              "required": [],
              "additionalProperties": false
            }
            """);

        var exception = Assert.Throws<AgentJsonSchemaOutputContractException>(
            () => AgentJsonSchemaOutputContractProcessor.Prepare(CreateContract(schema)));

        Assert.Equal("agents.structured-output-schema-too-large", exception.Code);
    }

    [Fact]
    public void Prepare_rejects_schema_that_exceeds_property_count_limit()
    {
        var properties = string.Join(
            ',',
            Enumerable.Range(0, AgentJsonSchemaOutputContractProcessor.MaximumPropertyCount + 1)
                .Select(index => $"\"p{index}\":{{\"type\":\"string\"}}"));
        var required = string.Join(
            ',',
            Enumerable.Range(0, AgentJsonSchemaOutputContractProcessor.MaximumPropertyCount + 1)
                .Select(index => $"\"p{index}\""));
        var schema = ParseElement(
            $$"""
            {
              "type": "object",
              "properties": { {{properties}} },
              "required": [ {{required}} ],
              "additionalProperties": false
            }
            """);

        var exception = Assert.Throws<AgentJsonSchemaOutputContractException>(
            () => AgentJsonSchemaOutputContractProcessor.Prepare(CreateContract(schema)));

        Assert.Equal("agents.structured-output-schema-too-complex", exception.Code);
    }

    [Fact]
    public void ValidateOutput_returns_valid_result_with_parsed_data_and_evidence()
    {
        var prepared = PrepareValidContract();
        const string rawOutput = """{"status":"ready","count":2}""";

        var result = AgentJsonSchemaOutputContractProcessor.ValidateOutput(prepared, rawOutput);

        Assert.Equal(AgentJsonSchemaOutputValidationStatus.Valid, result.ValidationStatus);
        var data = AssertJsonData(result);
        Assert.Equal("ready", data.GetProperty("status").GetString());
        Assert.Equal(2, data.GetProperty("count").GetInt32());
        Assert.Equal(rawOutput, result.RawOutput);
        Assert.Equal(prepared.SchemaJson, result.Schema);
        Assert.Equal(prepared.SchemaHash, result.SchemaHash);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public void ValidateOutput_returns_provider_refusal_with_parsed_data_and_evidence()
    {
        var prepared = PrepareValidContract();
        const string rawOutput = """{"refusal":"Policy prevents this response."}""";

        var result = AgentJsonSchemaOutputContractProcessor.ValidateOutput(prepared, rawOutput);

        Assert.Equal(AgentJsonSchemaOutputValidationStatus.ProviderRefusal, result.ValidationStatus);
        var data = AssertJsonData(result);
        Assert.Equal("Policy prevents this response.", data.GetProperty("refusal").GetString());
        Assert.Equal(rawOutput, result.RawOutput);
        Assert.Equal(prepared.SchemaJson, result.Schema);
        Assert.Equal(prepared.SchemaHash, result.SchemaHash);
        var error = Assert.Single(result.ValidationErrors);
        Assert.Equal("provider-refusal", error.Code);
        Assert.Equal("$", error.Path);
    }

    [Fact]
    public void ValidateOutput_returns_malformed_json_with_null_data_and_evidence()
    {
        var prepared = PrepareValidContract();
        const string rawOutput = """{"status":"ready","count":""";

        var result = AgentJsonSchemaOutputContractProcessor.ValidateOutput(prepared, rawOutput);

        Assert.Equal(AgentJsonSchemaOutputValidationStatus.MalformedJson, result.ValidationStatus);
        Assert.Null(result.Data);
        Assert.Equal(rawOutput, result.RawOutput);
        Assert.Equal(prepared.SchemaJson, result.Schema);
        Assert.Equal(prepared.SchemaHash, result.SchemaHash);
        var error = Assert.Single(result.ValidationErrors);
        Assert.Equal("malformed-json", error.Code);
        Assert.Equal("$", error.Path);
    }

    [Fact]
    public void ValidateOutput_returns_schema_validation_failure_with_parsed_data_and_errors()
    {
        var prepared = PrepareValidContract();
        const string rawOutput = """{"status":"unknown","extra":true}""";

        var result = AgentJsonSchemaOutputContractProcessor.ValidateOutput(prepared, rawOutput);

        Assert.Equal(AgentJsonSchemaOutputValidationStatus.SchemaValidationFailed, result.ValidationStatus);
        var data = AssertJsonData(result);
        Assert.Equal("unknown", data.GetProperty("status").GetString());
        Assert.True(data.GetProperty("extra").GetBoolean());
        Assert.Equal(rawOutput, result.RawOutput);
        Assert.Equal(prepared.SchemaJson, result.Schema);
        Assert.Equal(prepared.SchemaHash, result.SchemaHash);
        Assert.Collection(
            result.ValidationErrors,
            error =>
            {
                Assert.Equal("required-property-missing", error.Code);
                Assert.Equal("$", error.Path);
            },
            error =>
            {
                Assert.Equal("enum-mismatch", error.Code);
                Assert.Equal("$['status']", error.Path);
            },
            error =>
            {
                Assert.Equal("additional-property-not-allowed", error.Code);
                Assert.Equal("$['extra']", error.Path);
            });
    }

    private static PreparedAgentJsonSchemaOutputContract PrepareValidContract()
        => Assert.IsType<PreparedAgentJsonSchemaOutputContract>(
            AgentJsonSchemaOutputContractProcessor.Prepare(
                CreateContract(ParseElement(ValidSchemaJson))));

    private static AgentJsonSchemaOutputContract CreateContract(
        JsonElement schema,
        string kind = AgentJsonSchemaOutputContractVersions.Kind,
        string version = AgentJsonSchemaOutputContractVersions.Current)
        => new(kind, version, "portable_result", schema, Strict: true);

    private static JsonElement ParseElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement AssertJsonData(AgentJsonSchemaOutputResult result)
    {
        Assert.True(result.Data.HasValue);
        return result.Data.Value;
    }
}
