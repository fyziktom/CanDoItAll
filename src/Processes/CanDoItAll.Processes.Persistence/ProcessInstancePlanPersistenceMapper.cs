using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessInstancePlanPersistenceMapper
{
    internal static readonly DateTimeOffset HostCapabilityHashMigrationBoundaryUtc =
        new(2026, 8, 11, 18, 53, 52, TimeSpan.Zero);

    private static readonly HashSet<string> HostCapabilitySealPropertyNames = new(
        [
            "hostProfileId",
            "hostCapabilities",
            "requiredHostCapabilities",
            "requiredRuntimeToolNames"
        ],
        StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static ProcessInstancePlanEntity ToEntity(ProcessInstancePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        EnsureCanonicalPlanHash(plan);

        return new ProcessInstancePlanEntity
        {
            PlanId = plan.Header.PlanId.Value,
            RootPlanId = plan.Header.RootPlanId.Value,
            ParentPlanId = plan.Header.ParentPlanId?.Value,
            ParentStepId = plan.Header.ParentStepId?.Value,
            DefinitionId = plan.Definition.DefinitionId.Value,
            DefinitionVersionId = plan.Definition.VersionId.Value,
            PlanHash = plan.PlanHash,
            PlanHashAlgorithmVersion = ProcessPlanHasher.CurrentAlgorithmVersion,
            ExecutionState = PersistedProcessPlanExecutionState.Executable,
            PlanSchemaVersion = plan.Header.PlanSchemaVersion,
            DefinitionContentHash = plan.Definition.DefinitionContentHash,
            PayloadJson = JsonSerializer.Serialize(plan, SerializerOptions),
            CreatedAtUtc = plan.Header.CreatedAtUtc
        };
    }

    public static ProcessInstancePlan ToPlan(ProcessInstancePlanEntity entity)
        => Read(entity).RequireExecutablePlan();

    public static ProcessInstancePlanReadResult Read(ProcessInstancePlanEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var planId = new ProcessInstancePlanId(entity.PlanId);
        var plan = JsonSerializer.Deserialize<ProcessInstancePlan>(entity.PayloadJson, SerializerOptions)
            ?? throw new InvalidOperationException($"Process instance plan '{planId}' payload deserialized to null.");
        if (plan.Header.PlanId != planId ||
            !string.Equals(plan.PlanHash, entity.PlanHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Process instance plan '{planId}' payload identity or hash does not match the persisted metadata.");
        }

        var algorithmVersion = ResolveAlgorithmVersion(entity);
        if (!string.Equals(
                ProcessPlanHasher.Compute(plan, algorithmVersion),
                entity.PlanHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Process instance plan '{planId}' payload identity or hash does not match the persisted metadata.");
        }

        return algorithmVersion switch
        {
            ProcessPlanHashAlgorithmVersion.LegacyV1 => ReadLegacyPlan(entity, plan),
            ProcessPlanHashAlgorithmVersion.HostCapabilitiesV2 => ReadCurrentPlan(entity, plan),
            _ => throw new InvalidOperationException(
                $"Process instance plan '{planId}' uses an unsupported hash algorithm version.")
        };
    }

    public static void EnsureSameIdentityAndHash(
        ProcessInstancePlanEntity entity,
        ProcessInstancePlan plan)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(plan);

        var persistedPlan = Read(entity).RequireExecutablePlan();
        if (persistedPlan.Header.PlanId != plan.Header.PlanId ||
            !string.Equals(entity.PlanHash, plan.PlanHash, StringComparison.Ordinal) ||
            !string.Equals(ProcessPlanHasher.Compute(plan), plan.PlanHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Process instance plan '{plan.Header.PlanId}' already exists with a different identity or hash.");
        }
    }

    private static void EnsureCanonicalPlanHash(ProcessInstancePlan plan)
    {
        if (!string.Equals(ProcessPlanHasher.Compute(plan), plan.PlanHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Process instance plan '{plan.Header.PlanId}' does not carry its canonical content hash.");
        }
    }

    private static ProcessPlanHashAlgorithmVersion ResolveAlgorithmVersion(
        ProcessInstancePlanEntity entity)
    {
        if (entity.PlanHashAlgorithmVersion.HasValue)
        {
            return entity.PlanHashAlgorithmVersion.Value;
        }

        if (entity.CreatedAtUtc >= HostCapabilityHashMigrationBoundaryUtc ||
            ContainsHostCapabilitySealProperty(entity.PayloadJson))
        {
            throw new InvalidOperationException(
                $"Process instance plan '{new ProcessInstancePlanId(entity.PlanId)}' is missing a bounded hash algorithm version.");
        }

        return ProcessPlanHashAlgorithmVersion.LegacyV1;
    }

    private static ProcessInstancePlanReadResult ReadLegacyPlan(
        ProcessInstancePlanEntity entity,
        ProcessInstancePlan plan)
    {
        if (entity.ExecutionState == PersistedProcessPlanExecutionState.Executable)
        {
            throw new InvalidOperationException(
                $"Legacy process instance plan '{plan.Header.PlanId}' cannot be marked executable without sealed host capabilities.");
        }

        bool metadataChanged =
            entity.PlanHashAlgorithmVersion != ProcessPlanHashAlgorithmVersion.LegacyV1 ||
            entity.ExecutionState != PersistedProcessPlanExecutionState.NeedsRecompile ||
            entity.MigrationReason != ProcessPlanMigrationReason.HostCapabilitiesWereNotSealed;
        entity.PlanHashAlgorithmVersion = ProcessPlanHashAlgorithmVersion.LegacyV1;
        entity.ExecutionState = PersistedProcessPlanExecutionState.NeedsRecompile;
        entity.MigrationReason = ProcessPlanMigrationReason.HostCapabilitiesWereNotSealed;

        return new ProcessInstancePlanReadResult(
            plan,
            metadataChanged,
            PersistedProcessPlanExecutionState.NeedsRecompile,
            ProcessPlanMigrationReason.HostCapabilitiesWereNotSealed,
            ProcessPlanHashAlgorithmVersion.LegacyV1);
    }

    private static ProcessInstancePlanReadResult ReadCurrentPlan(
        ProcessInstancePlanEntity entity,
        ProcessInstancePlan plan)
    {
        if (entity.ExecutionState != PersistedProcessPlanExecutionState.Executable ||
            entity.MigrationReason.HasValue)
        {
            throw new InvalidOperationException(
                $"Current process instance plan '{plan.Header.PlanId}' has inconsistent execution metadata.");
        }

        return new ProcessInstancePlanReadResult(
            plan,
            false,
            PersistedProcessPlanExecutionState.Executable,
            null,
            ProcessPlanHashAlgorithmVersion.HostCapabilitiesV2);
    }

    private static bool ContainsHostCapabilitySealProperty(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return ContainsHostCapabilitySealProperty(document.RootElement);
    }

    private static bool ContainsHostCapabilitySealProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (HostCapabilitySealPropertyNames.Contains(property.Name) ||
                    ContainsHostCapabilitySealProperty(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsHostCapabilitySealProperty(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };
        options.Converters.Add(new SingleValueProcessStructJsonConverterFactory());
        options.Converters.Add(new ReadOnlySetJsonConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class SingleValueProcessStructJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsValueType ||
                typeToConvert.IsEnum ||
                typeToConvert.Namespace?.StartsWith("CanDoItAll.Processes.", StringComparison.Ordinal) != true)
            {
                return false;
            }

            return TryResolve(typeToConvert, out _, out var valueType) &&
                (valueType == typeof(Guid) || valueType == typeof(string));
        }

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (!TryResolve(typeToConvert, out var constructor, out var valueType))
            {
                throw new InvalidOperationException($"Process value struct '{typeToConvert}' is not supported by the plan serializer.");
            }

            return (JsonConverter)Activator.CreateInstance(
                typeof(SingleValueProcessStructJsonConverter<,>).MakeGenericType(typeToConvert, valueType),
                constructor)!;
        }

        private static bool TryResolve(
            Type typeToConvert,
            out ConstructorInfo? constructor,
            out Type valueType)
        {
            var valueProperty = typeToConvert.GetProperty(
                "Value",
                BindingFlags.Instance | BindingFlags.Public);
            valueType = valueProperty?.PropertyType ?? typeof(object);
            constructor = valueProperty is null
                ? null
                : typeToConvert.GetConstructor([valueType]);

            return constructor is not null;
        }
    }

    private sealed class SingleValueProcessStructJsonConverter<TWrapper, TValue>(ConstructorInfo constructor) : JsonConverter<TWrapper>
        where TWrapper : struct
    {
        private readonly ConstructorInfo constructor = constructor;

        public override TWrapper Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var value = reader.TokenType == JsonTokenType.StartObject
                ? ReadObjectValue(ref reader, options)
                : JsonSerializer.Deserialize<TValue>(ref reader, options);

            if (value is null)
            {
                throw new JsonException($"Process value struct '{typeToConvert}' cannot be deserialized from a null value.");
            }

            return (TWrapper)this.constructor.Invoke([value]);
        }

        public override void Write(
            Utf8JsonWriter writer,
            TWrapper value,
            JsonSerializerOptions options)
        {
            var valueProperty = typeof(TWrapper).GetProperty(
                "Value",
                BindingFlags.Instance | BindingFlags.Public)
                ?? throw new JsonException($"Process value struct '{typeof(TWrapper)}' does not expose a Value property.");

            writer.WriteStartObject();
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, (TValue)valueProperty.GetValue(value)!, options);
            writer.WriteEndObject();
        }

        private static TValue? ReadObjectValue(
            ref Utf8JsonReader reader,
            JsonSerializerOptions options)
        {
            TValue? value = default;
            var found = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Process value struct payload must contain object properties.");
                }

                var propertyName = reader.GetString();
                reader.Read();
                if (string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase))
                {
                    value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                    found = true;
                    continue;
                }

                reader.Skip();
            }

            if (!found)
            {
                throw new JsonException("Process value struct payload is missing the Value property.");
            }

            return value;
        }
    }

    private sealed class ReadOnlySetJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlySet<>);

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var elementType = typeToConvert.GetGenericArguments()[0];
            return (JsonConverter)Activator.CreateInstance(
                typeof(ReadOnlySetJsonConverter<>).MakeGenericType(elementType))!;
        }
    }

    private sealed class ReadOnlySetJsonConverter<T> : JsonConverter<IReadOnlySet<T>>
        where T : notnull
    {
        public override IReadOnlySet<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                throw new JsonException("Process plan set payload cannot be null.");
            }

            var values = JsonSerializer.Deserialize<List<T>>(ref reader, options)
                ?? throw new JsonException("Process plan set payload must be an array.");
            var result = new HashSet<T>();
            foreach (var value in values)
            {
                if (!result.Add(value))
                {
                    throw new JsonException("Process plan set payload cannot contain duplicate values.");
                }
            }

            return result;
        }

        public override void Write(
            Utf8JsonWriter writer,
            IReadOnlySet<T> value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.ToArray(), options);
        }
    }
}

internal sealed record ProcessInstancePlanReadResult(
    ProcessInstancePlan Plan,
    bool MetadataChanged,
    PersistedProcessPlanExecutionState ExecutionState,
    ProcessPlanMigrationReason? MigrationReason,
    ProcessPlanHashAlgorithmVersion HashAlgorithmVersion)
{
    public ProcessInstancePlan RequireExecutablePlan()
    {
        if (ExecutionState == PersistedProcessPlanExecutionState.Executable)
        {
            return Plan;
        }

        throw new ProcessPlanMigrationRequiredException(
            Plan.Header.PlanId,
            HashAlgorithmVersion,
            MigrationReason ?? ProcessPlanMigrationReason.HostCapabilitiesWereNotSealed);
    }
}

public sealed class ProcessPlanMigrationRequiredException : InvalidOperationException
{
    public ProcessPlanMigrationRequiredException(
        ProcessInstancePlanId planId,
        ProcessPlanHashAlgorithmVersion hashAlgorithmVersion,
        ProcessPlanMigrationReason reason)
        : base($"Process instance plan '{planId}' is not executable on this version. Recompile the plan before retrying.")
    {
        PlanId = planId;
        HashAlgorithmVersion = hashAlgorithmVersion;
        Reason = reason;
    }

    public ProcessInstancePlanId PlanId { get; }

    public ProcessPlanHashAlgorithmVersion HashAlgorithmVersion { get; }

    public ProcessPlanMigrationReason Reason { get; }
}
