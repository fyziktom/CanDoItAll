using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentJsonSchemaOutputContractException : InvalidOperationException
{
    public AgentJsonSchemaOutputContractException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

internal sealed record PreparedAgentJsonSchemaOutputContract(
    string Kind,
    string Version,
    string Name,
    string SchemaJson,
    string SchemaHash,
    bool Strict)
{
    public bool EvaluateInstanceSchema { get; init; } = true;
}

internal static partial class AgentJsonSchemaOutputContractProcessor
{
    internal const int MaximumSchemaBytes = 64 * 1024;
    internal const int MaximumSchemaDepth = 16;
    internal const int MaximumSchemaNodes = 512;
    internal const int MaximumPropertyCount = 128;
    internal const int MaximumOutputBytes = 1024 * 1024;
    internal const int MaximumValidationErrors = 64;

    private static readonly HashSet<string> SupportedKeywords = new(StringComparer.Ordinal)
    {
        "$schema",
        "$id",
        "title",
        "description",
        "type",
        "properties",
        "required",
        "additionalProperties",
        "items",
        "enum",
        "const",
        "minimum",
        "maximum",
        "exclusiveMinimum",
        "exclusiveMaximum",
        "minLength",
        "maxLength",
        "pattern",
        "minItems",
        "maxItems",
        "uniqueItems",
        "minProperties",
        "maxProperties"
    };

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
    {
        "object",
        "array",
        "string",
        "number",
        "integer",
        "boolean",
        "null"
    };

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SchemaNamePattern();

    public static PreparedAgentJsonSchemaOutputContract? Prepare(AgentJsonSchemaOutputContract? contract)
    {
        if (contract is null)
        {
            return null;
        }

        var kind = contract.Kind?.Trim() ?? string.Empty;
        if (!string.Equals(kind, AgentJsonSchemaOutputContractVersions.Kind, StringComparison.Ordinal))
        {
            throw ContractError(
                "agents.structured-output-kind-unsupported",
                $"Structured output kind must be '{AgentJsonSchemaOutputContractVersions.Kind}'.");
        }

        var version = contract.Version?.Trim() ?? string.Empty;
        if (!string.Equals(version, AgentJsonSchemaOutputContractVersions.Current, StringComparison.Ordinal))
        {
            throw ContractError(
                "agents.structured-output-version-unsupported",
                $"Structured output contract version must be '{AgentJsonSchemaOutputContractVersions.Current}'.");
        }

        var name = contract.Name?.Trim() ?? string.Empty;
        if (!SchemaNamePattern().IsMatch(name))
        {
            throw ContractError(
                "agents.structured-output-name-invalid",
                "Structured output name must start with a letter and contain at most 64 ASCII letters, digits, underscores, or hyphens.");
        }

        if (contract.Schema.ValueKind != JsonValueKind.Object)
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                "Structured output schema must be a JSON object.");
        }

        var schemaJson = contract.Schema.GetRawText();
        if (Encoding.UTF8.GetByteCount(schemaJson) > MaximumSchemaBytes)
        {
            throw ContractError(
                "agents.structured-output-schema-too-large",
                $"Structured output schema cannot exceed {MaximumSchemaBytes:N0} UTF-8 bytes.");
        }

        var budget = new SchemaBudget();
        InspectSchema(contract.Schema, "$", depth: 1, contract.Strict, budget);
        EnsureRootObjectSchema(contract.Schema);

        var schemaHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schemaJson))).ToLowerInvariant();
        return new PreparedAgentJsonSchemaOutputContract(kind, version, name, schemaJson, schemaHash, contract.Strict);
    }

    public static PreparedAgentJsonSchemaOutputContract PrepareResponseFormatValidation(
        JsonElement schema,
        string name,
        bool strict)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                "Structured output schema must be a JSON object.");
        }

        var normalizedName = name?.Trim() ?? string.Empty;
        if (!SchemaNamePattern().IsMatch(normalizedName))
        {
            throw ContractError(
                "agents.structured-output-name-invalid",
                "Structured output name must start with a letter and contain at most 64 ASCII letters, digits, underscores, or hyphens.");
        }

        var schemaJson = schema.GetRawText();
        var schemaHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schemaJson))).ToLowerInvariant();
        return new PreparedAgentJsonSchemaOutputContract(
            AgentJsonSchemaOutputContractVersions.Kind,
            AgentJsonSchemaOutputContractVersions.Current,
            normalizedName,
            schemaJson,
            schemaHash,
            strict)
        {
            EvaluateInstanceSchema = CanEvaluateResponseFormatSchema(schema)
        };
    }

    public static PreparedAgentJsonSchemaOutputContract? Restore(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (string.IsNullOrWhiteSpace(run.StructuredOutputJsonSchema))
        {
            return null;
        }

        using var document = JsonDocument.Parse(
            run.StructuredOutputJsonSchema,
            new JsonDocumentOptions { MaxDepth = MaximumSchemaDepth + 2 });
        var prepared = Prepare(new AgentJsonSchemaOutputContract(
            AgentJsonSchemaOutputContractVersions.Kind,
            run.StructuredOutputSchemaVersion,
            run.StructuredOutputSchemaName,
            document.RootElement.Clone(),
            run.StructuredOutputSchemaStrict))!;

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(prepared.SchemaHash),
                Encoding.ASCII.GetBytes(run.StructuredOutputSchemaHash)))
        {
            throw ContractError(
                "agents.structured-output-schema-integrity-failed",
                $"The persisted JSON Schema hash for execution run '{run.Id:N}' does not match its schema evidence.");
        }

        return prepared;
    }

    public static AgentJsonSchemaOutputResult ValidateOutput(
        PreparedAgentJsonSchemaOutputContract contract,
        string? rawOutput)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var raw = rawOutput ?? string.Empty;
        if (Encoding.UTF8.GetByteCount(raw) > MaximumOutputBytes)
        {
            return Failure(
                contract,
                raw,
                AgentJsonSchemaOutputValidationStatus.MalformedJson,
                "output-too-large",
                $"Provider output exceeds the {MaximumOutputBytes:N0}-byte validation limit.");
        }

        JsonDocument outputDocument;
        try
        {
            outputDocument = JsonDocument.Parse(
                raw,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumSchemaDepth + 16
                });
        }
        catch (JsonException exception)
        {
            var status = LooksLikeTextRefusal(raw)
                ? AgentJsonSchemaOutputValidationStatus.ProviderRefusal
                : AgentJsonSchemaOutputValidationStatus.MalformedJson;
            return Failure(
                contract,
                raw,
                status,
                status == AgentJsonSchemaOutputValidationStatus.ProviderRefusal
                    ? "provider-refusal"
                    : "malformed-json",
                status == AgentJsonSchemaOutputValidationStatus.ProviderRefusal
                    ? "The provider refused to produce output for this contract."
                    : $"Provider output is not one complete JSON value: {exception.Message}");
        }

        using (outputDocument)
        using (var schemaDocument = JsonDocument.Parse(contract.SchemaJson))
        {
            var output = outputDocument.RootElement;
            if (contract.EvaluateInstanceSchema &&
                IsStructuredRefusal(output, schemaDocument.RootElement))
            {
                return new AgentJsonSchemaOutputResult(
                    output.Clone(),
                    raw,
                    contract.SchemaJson,
                    contract.SchemaHash,
                    AgentJsonSchemaOutputValidationStatus.ProviderRefusal,
                    [new AgentJsonSchemaOutputValidationError(
                        "provider-refusal",
                        "The provider refused to produce output for this contract.")]);
            }

            var errors = new List<AgentJsonSchemaOutputValidationError>();
            if (contract.EvaluateInstanceSchema)
            {
                ValidateInstance(schemaDocument.RootElement, output, "$", errors);
            }

            return new AgentJsonSchemaOutputResult(
                output.Clone(),
                raw,
                contract.SchemaJson,
                contract.SchemaHash,
                errors.Count == 0
                    ? AgentJsonSchemaOutputValidationStatus.Valid
                    : AgentJsonSchemaOutputValidationStatus.SchemaValidationFailed,
                errors);
        }
    }

    private static void InspectSchema(
        JsonElement schema,
        string path,
        int depth,
        bool strict,
        SchemaBudget budget)
    {
        if (depth > MaximumSchemaDepth)
        {
            throw ContractError(
                "agents.structured-output-schema-too-complex",
                $"Structured output schema cannot exceed {MaximumSchemaDepth} nested schema levels.");
        }

        if (++budget.NodeCount > MaximumSchemaNodes)
        {
            throw ContractError(
                "agents.structured-output-schema-too-complex",
                $"Structured output schema cannot contain more than {MaximumSchemaNodes} schema nodes.");
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"Schema at '{path}' must be a JSON object.");
        }

        foreach (var keyword in schema.EnumerateObject())
        {
            if (!SupportedKeywords.Contains(keyword.Name))
            {
                throw ContractError(
                    "agents.structured-output-keyword-unsupported",
                    $"JSON Schema keyword '{keyword.Name}' at '{path}' is not supported by the portable output contract.");
            }
        }

        ValidateTypeKeyword(schema, path);
        ValidateIntegerKeyword(schema, "minLength", path);
        ValidateIntegerKeyword(schema, "maxLength", path);
        ValidateIntegerKeyword(schema, "minItems", path);
        ValidateIntegerKeyword(schema, "maxItems", path);
        ValidateIntegerKeyword(schema, "minProperties", path);
        ValidateIntegerKeyword(schema, "maxProperties", path);
        ValidateNumberKeyword(schema, "minimum", path);
        ValidateNumberKeyword(schema, "maximum", path);
        ValidateNumberKeyword(schema, "exclusiveMinimum", path);
        ValidateNumberKeyword(schema, "exclusiveMaximum", path);

        if (schema.TryGetProperty("pattern", out var pattern) &&
            (pattern.ValueKind != JsonValueKind.String || (pattern.GetString()?.Length ?? 0) > 256))
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'pattern' at '{path}' must be a string no longer than 256 characters.");
        }

        if (schema.TryGetProperty("enum", out var enumElement) &&
            (enumElement.ValueKind != JsonValueKind.Array || enumElement.GetArrayLength() == 0 || enumElement.GetArrayLength() > 128))
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'enum' at '{path}' must contain between 1 and 128 values.");
        }

        var properties = Array.Empty<JsonProperty>();
        if (schema.TryGetProperty("properties", out var propertiesElement))
        {
            if (propertiesElement.ValueKind != JsonValueKind.Object)
            {
                throw ContractError(
                    "agents.structured-output-schema-invalid",
                    $"'properties' at '{path}' must be a JSON object.");
            }

            properties = propertiesElement.EnumerateObject().ToArray();
            if (properties.Length > MaximumPropertyCount)
            {
                throw ContractError(
                    "agents.structured-output-schema-too-complex",
                    $"An object schema cannot define more than {MaximumPropertyCount} properties.");
            }

            foreach (var property in properties)
            {
                InspectSchema(property.Value, AppendPropertyPath(path, property.Name), depth + 1, strict, budget);
            }
        }

        var required = ValidateRequiredKeyword(schema, path);
        if (required.Except(properties.Select(property => property.Name), StringComparer.Ordinal).Any())
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'required' at '{path}' contains a name that is not declared in 'properties'.");
        }

        if (schema.TryGetProperty("additionalProperties", out var additionalProperties))
        {
            if (additionalProperties.ValueKind == JsonValueKind.Object)
            {
                InspectSchema(additionalProperties, path + ".*", depth + 1, strict, budget);
            }
            else if (additionalProperties.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                throw ContractError(
                    "agents.structured-output-schema-invalid",
                    $"'additionalProperties' at '{path}' must be a boolean or schema object.");
            }
        }

        if (schema.TryGetProperty("items", out var items))
        {
            InspectSchema(items, path + "[*]", depth + 1, strict, budget);
        }

        if (schema.TryGetProperty("uniqueItems", out var uniqueItems) &&
            uniqueItems.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'uniqueItems' at '{path}' must be a boolean.");
        }

        if (strict && SchemaAllowsType(schema, "object"))
        {
            if (!schema.TryGetProperty("additionalProperties", out var additional) ||
                additional.ValueKind != JsonValueKind.False)
            {
                throw ContractError(
                    "agents.structured-output-strict-object-invalid",
                    $"Strict object schema at '{path}' must set 'additionalProperties' to false.");
            }

            if (properties.Any(property => !required.Contains(property.Name, StringComparer.Ordinal)))
            {
                throw ContractError(
                    "agents.structured-output-strict-object-invalid",
                    $"Strict object schema at '{path}' must list every declared property in 'required'. Use a type including 'null' for optional values.");
            }
        }
    }

    private static bool CanEvaluateResponseFormatSchema(JsonElement schema)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return true;
        }

        if (schema.ValueKind != JsonValueKind.Object ||
            schema.EnumerateObject().Any(property => !SupportedKeywords.Contains(property.Name)))
        {
            return false;
        }

        if (schema.TryGetProperty("properties", out var properties))
        {
            if (properties.ValueKind != JsonValueKind.Object ||
                properties.EnumerateObject().Any(property => !CanEvaluateResponseFormatSchema(property.Value)))
            {
                return false;
            }
        }

        if (schema.TryGetProperty("additionalProperties", out var additionalProperties) &&
            additionalProperties.ValueKind == JsonValueKind.Object &&
            !CanEvaluateResponseFormatSchema(additionalProperties))
        {
            return false;
        }

        return !schema.TryGetProperty("items", out var items) ||
               CanEvaluateResponseFormatSchema(items);
    }

    private static void EnsureRootObjectSchema(JsonElement schema)
    {
        if (!SchemaAllowsType(schema, "object"))
        {
            throw ContractError(
                "agents.structured-output-root-invalid",
                "The top-level structured output schema must declare type 'object'.");
        }
    }

    private static void ValidateTypeKeyword(JsonElement schema, string path)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            EnsureSupportedType(type.GetString(), path);
            return;
        }

        if (type.ValueKind != JsonValueKind.Array ||
            type.GetArrayLength() == 0 ||
            type.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'type' at '{path}' must be a type name or a non-empty array of type names.");
        }

        var names = type.EnumerateArray().Select(item => item.GetString()!).ToArray();
        foreach (var name in names)
        {
            EnsureSupportedType(name, path);
        }

        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'type' at '{path}' cannot contain duplicate type names.");
        }
    }

    private static void EnsureSupportedType(string? type, string path)
    {
        if (string.IsNullOrWhiteSpace(type) || !SupportedTypes.Contains(type))
        {
            throw ContractError(
                "agents.structured-output-type-unsupported",
                $"JSON Schema type '{type}' at '{path}' is not supported.");
        }
    }

    private static HashSet<string> ValidateRequiredKeyword(JsonElement schema, string path)
    {
        if (!schema.TryGetProperty("required", out var required))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (required.ValueKind != JsonValueKind.Array ||
            required.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'required' at '{path}' must be an array of property names.");
        }

        var values = required.EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'required' at '{path}' cannot contain duplicate property names.");
        }

        return values.ToHashSet(StringComparer.Ordinal);
    }

    private static void ValidateIntegerKeyword(JsonElement schema, string keyword, string path)
    {
        if (schema.TryGetProperty(keyword, out var value) &&
            (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var integer) || integer < 0))
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'{keyword}' at '{path}' must be a non-negative 32-bit integer.");
        }
    }

    private static void ValidateNumberKeyword(JsonElement schema, string keyword, string path)
    {
        if (schema.TryGetProperty(keyword, out var value) &&
            (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number)))
        {
            throw ContractError(
                "agents.structured-output-schema-invalid",
                $"'{keyword}' at '{path}' must be a finite JSON number.");
        }
    }

    private static void ValidateInstance(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<AgentJsonSchemaOutputValidationError> errors)
    {
        if (errors.Count >= MaximumValidationErrors)
        {
            return;
        }

        if (schema.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (schema.ValueKind == JsonValueKind.False)
        {
            AddError(errors, "false-schema", "Value is rejected by the false JSON Schema.", path);
            return;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!MatchesDeclaredType(schema, instance))
        {
            AddError(errors, "type-mismatch", $"Value does not match schema type {DescribeTypes(schema)}.", path);
            return;
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(constant, instance))
        {
            AddError(errors, "const-mismatch", "Value does not match the schema constant.", path);
        }

        if (schema.TryGetProperty("enum", out var enumValues) &&
            !enumValues.EnumerateArray().Any(candidate => JsonElement.DeepEquals(candidate, instance)))
        {
            AddError(errors, "enum-mismatch", "Value is not one of the allowed schema values.", path);
        }

        switch (instance.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(schema, instance, path, errors);
                break;
            case JsonValueKind.Array:
                ValidateArray(schema, instance, path, errors);
                break;
            case JsonValueKind.String:
                ValidateString(schema, instance.GetString() ?? string.Empty, path, errors);
                break;
            case JsonValueKind.Number:
                ValidateNumber(schema, instance, path, errors);
                break;
        }
    }

    private static void ValidateObject(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<AgentJsonSchemaOutputValidationError> errors)
    {
        var instanceProperties = instance.EnumerateObject().ToArray();
        ValidateCount(schema, "minProperties", "maxProperties", instanceProperties.Length, "property-count", path, errors);

        var required = schema.TryGetProperty("required", out var requiredElement)
            ? requiredElement.EnumerateArray().Select(item => item.GetString()!).ToArray()
            : [];
        foreach (var name in required)
        {
            if (!instance.TryGetProperty(name, out _))
            {
                AddError(errors, "required-property-missing", $"Required property '{name}' is missing.", path);
            }
        }

        var declared = schema.TryGetProperty("properties", out var properties)
            ? properties.EnumerateObject().ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        schema.TryGetProperty("additionalProperties", out var additional);

        foreach (var property in instanceProperties)
        {
            if (declared.TryGetValue(property.Name, out var propertySchema))
            {
                ValidateInstance(propertySchema, property.Value, AppendPropertyPath(path, property.Name), errors);
            }
            else if (additional.ValueKind == JsonValueKind.False)
            {
                AddError(
                    errors,
                    "additional-property-not-allowed",
                    $"Property '{property.Name}' is not declared by the schema.",
                    AppendPropertyPath(path, property.Name));
            }
            else if (additional.ValueKind == JsonValueKind.Object)
            {
                ValidateInstance(additional, property.Value, AppendPropertyPath(path, property.Name), errors);
            }
        }
    }

    private static void ValidateArray(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<AgentJsonSchemaOutputValidationError> errors)
    {
        var values = instance.EnumerateArray().ToArray();
        ValidateCount(schema, "minItems", "maxItems", values.Length, "item-count", path, errors);
        if (schema.TryGetProperty("uniqueItems", out var uniqueItems) && uniqueItems.ValueKind == JsonValueKind.True)
        {
            for (var left = 0; left < values.Length; left++)
            {
                if (values.Skip(left + 1).Any(right => JsonElement.DeepEquals(values[left], right)))
                {
                    AddError(errors, "array-items-not-unique", "Array values must be unique.", path);
                    break;
                }
            }
        }

        if (schema.TryGetProperty("items", out var itemSchema))
        {
            for (var index = 0; index < values.Length; index++)
            {
                ValidateInstance(itemSchema, values[index], $"{path}[{index}]", errors);
            }
        }
    }

    private static void ValidateString(
        JsonElement schema,
        string value,
        string path,
        List<AgentJsonSchemaOutputValidationError> errors)
    {
        ValidateCount(schema, "minLength", "maxLength", value.Length, "string-length", path, errors);
        if (schema.TryGetProperty("pattern", out var pattern))
        {
            try
            {
                if (!Regex.IsMatch(
                        value,
                        pattern.GetString()!,
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100)))
                {
                    AddError(errors, "pattern-mismatch", "String does not match the schema pattern.", path);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                AddError(errors, "pattern-timeout", "String pattern validation exceeded its time limit.", path);
            }
            catch (ArgumentException)
            {
                AddError(errors, "pattern-invalid", "Schema contains an invalid regular expression.", path);
            }
        }
    }

    private static void ValidateNumber(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<AgentJsonSchemaOutputValidationError> errors)
    {
        if (!instance.TryGetDouble(out var value) || !double.IsFinite(value))
        {
            AddError(errors, "number-invalid", "Value is not a finite JSON number.", path);
            return;
        }

        CompareBound(schema, "minimum", value, inclusive: true, path, errors);
        CompareBound(schema, "maximum", value, inclusive: true, path, errors);
        CompareBound(schema, "exclusiveMinimum", value, inclusive: false, path, errors);
        CompareBound(schema, "exclusiveMaximum", value, inclusive: false, path, errors);
    }

    private static void CompareBound(
        JsonElement schema,
        string keyword,
        double value,
        bool inclusive,
        string path,
        List<AgentJsonSchemaOutputValidationError> errors)
    {
        if (!schema.TryGetProperty(keyword, out var boundElement))
        {
            return;
        }

        var bound = boundElement.GetDouble();
        var isMinimum = keyword is "minimum" or "exclusiveMinimum";
        var valid = isMinimum
            ? inclusive ? value >= bound : value > bound
            : inclusive ? value <= bound : value < bound;
        if (!valid)
        {
            AddError(errors, "number-bound-failed", $"Value violates schema keyword '{keyword}'.", path);
        }
    }

    private static void ValidateCount(
        JsonElement schema,
        string minimumKeyword,
        string maximumKeyword,
        int count,
        string code,
        string path,
        List<AgentJsonSchemaOutputValidationError> errors)
    {
        if (schema.TryGetProperty(minimumKeyword, out var minimum) && count < minimum.GetInt32())
        {
            AddError(errors, code, $"Count is below schema keyword '{minimumKeyword}'.", path);
        }

        if (schema.TryGetProperty(maximumKeyword, out var maximum) && count > maximum.GetInt32())
        {
            AddError(errors, code, $"Count exceeds schema keyword '{maximumKeyword}'.", path);
        }
    }

    private static bool MatchesDeclaredType(JsonElement schema, JsonElement instance)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return true;
        }

        return type.ValueKind == JsonValueKind.String
            ? MatchesType(type.GetString()!, instance)
            : type.EnumerateArray().Any(item => MatchesType(item.GetString()!, instance));
    }

    private static bool MatchesType(string type, JsonElement value)
        => type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "number" => value.ValueKind == JsonValueKind.Number,
            "integer" => value.ValueKind == JsonValueKind.Number &&
                         value.TryGetDecimal(out var number) &&
                         decimal.Truncate(number) == number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false
        };

    private static bool SchemaAllowsType(JsonElement schema, string expected)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }

        return type.ValueKind == JsonValueKind.String
            ? string.Equals(type.GetString(), expected, StringComparison.Ordinal)
            : type.ValueKind == JsonValueKind.Array &&
              type.EnumerateArray().Any(item => string.Equals(item.GetString(), expected, StringComparison.Ordinal));
    }

    private static bool IsStructuredRefusal(JsonElement output, JsonElement schema)
    {
        if (output.ValueKind != JsonValueKind.Object ||
            !output.TryGetProperty("refusal", out var refusal) ||
            refusal.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(refusal.GetString()))
        {
            return false;
        }

        return !schema.TryGetProperty("properties", out var properties) ||
               !properties.TryGetProperty("refusal", out _);
    }

    private static bool LooksLikeTextRefusal(string output)
    {
        var normalized = output.TrimStart();
        return normalized.StartsWith("refusal:", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("refused:", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeTypes(JsonElement schema)
    {
        var type = schema.GetProperty("type");
        return type.ValueKind == JsonValueKind.String
            ? $"'{type.GetString()}'"
            : string.Join(" or ", type.EnumerateArray().Select(item => $"'{item.GetString()}'"));
    }

    private static string AppendPropertyPath(string path, string propertyName)
        => $"{path}['{propertyName.Replace("'", "\\'", StringComparison.Ordinal)}']";

    private static void AddError(
        List<AgentJsonSchemaOutputValidationError> errors,
        string code,
        string message,
        string path)
    {
        if (errors.Count < MaximumValidationErrors)
        {
            errors.Add(new AgentJsonSchemaOutputValidationError(code, message, path));
        }
    }

    private static AgentJsonSchemaOutputResult Failure(
        PreparedAgentJsonSchemaOutputContract contract,
        string raw,
        AgentJsonSchemaOutputValidationStatus status,
        string code,
        string message)
        => new(
            Data: null,
            RawOutput: raw,
            Schema: contract.SchemaJson,
            SchemaHash: contract.SchemaHash,
            ValidationStatus: status,
            ValidationErrors: [new AgentJsonSchemaOutputValidationError(code, message)]);

    private static AgentJsonSchemaOutputContractException ContractError(string code, string message)
        => new(code, message);

    private sealed class SchemaBudget
    {
        public int NodeCount { get; set; }
    }
}
