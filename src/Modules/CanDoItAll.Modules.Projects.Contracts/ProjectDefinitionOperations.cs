using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

[JsonConverter(typeof(ProjectOperationEpochJsonConverter))]
public sealed record ProjectOperationEpoch {
    public ProjectOperationEpoch(long value) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }
}

[JsonConverter(typeof(ProjectCallerBindingJsonConverter))]
public sealed record ProjectCallerBinding {
    public const string VersionOnePrefix = "pc1:";

    public ProjectCallerBinding(string value) {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 68 || !value.StartsWith(VersionOnePrefix, StringComparison.Ordinal) ||
            value.AsSpan(4).ContainsAnyExcept("0123456789abcdef")) {
            throw new ArgumentException("A caller binding requires the supported version and a lowercase SHA-256 digest.", nameof(value));
        }
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

[JsonConverter(typeof(ProjectDefinitionOperationJsonConverter))]
public sealed record ProjectDefinitionOperation {
    public ProjectDefinitionOperation(DatasetNamespaceId @namespace, ProjectOperationEpoch epoch,
        Guid id, ProjectCallerBinding callerBinding) {
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(epoch);
        ArgumentNullException.ThrowIfNull(callerBinding);
        if (id == Guid.Empty) {
            throw new ArgumentException("An operation identifier must be nonempty.", nameof(id));
        }
        Namespace = @namespace;
        Epoch = epoch;
        Id = id;
        CallerBinding = callerBinding;
    }

    public DatasetNamespaceId Namespace { get; }
    public ProjectOperationEpoch Epoch { get; }
    public Guid Id { get; }
    public ProjectCallerBinding CallerBinding { get; }
}

[JsonConverter(typeof(ProjectDefinitionRequestVersionJsonConverter))]
public sealed record ProjectDefinitionRequestVersion {
    public static ProjectDefinitionRequestVersion VersionOne { get; } = new(1, 1);

    public ProjectDefinitionRequestVersion(int schema, int normalization) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schema);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(normalization);
        Schema = schema;
        Normalization = normalization;
    }

    public int Schema { get; }
    public int Normalization { get; }
}

public enum ProjectDefinitionCommandKind { Create = 1, Replace = 2, UpdateScalars = 3 }

[JsonConverter(typeof(ProjectDefinitionRequestJsonConverterFactory))]
public sealed record ProjectDefinitionRequest<TCommand> where TCommand : class {
    public ProjectDefinitionRequest(ProjectDefinitionOperation operation, ProjectDefinitionRequestVersion version, TCommand command) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(command);
        if (command is not CreateProjectDefinitionCommand and not ReplaceProjectDefinitionCommand and not UpdateProjectDefinitionScalarsCommand) {
            throw new ArgumentException("The request requires a supported definition command.", nameof(command));
        }
        Operation = operation;
        Version = version;
        Command = command;
    }

    public ProjectDefinitionOperation Operation { get; }
    public ProjectDefinitionRequestVersion Version { get; }
    public TCommand Command { get; }
}

public sealed record ProjectDefinitionResultVersion {
    public static ProjectDefinitionResultVersion VersionOne { get; } = new(1);

    public ProjectDefinitionResultVersion(int value) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public int Value { get; }
}

public sealed record ProjectDefinitionLimits {
    public static ProjectDefinitionLimits VersionOneMaximum { get; } = new(1024, 1024, 65_536, 2_097_152, 1_048_576, 2_097_152);

    public ProjectDefinitionLimits(int maxPhaseCount, int maxOptionCount, int maxFieldUtf8Bytes,
        int maxRawRequestBytes, int maxCanonicalCommandBytes, int maxAcceptedResultBytes) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPhaseCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOptionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFieldUtf8Bytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRawRequestBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCanonicalCommandBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAcceptedResultBytes);
        MaxPhaseCount = maxPhaseCount;
        MaxOptionCount = maxOptionCount;
        MaxFieldUtf8Bytes = maxFieldUtf8Bytes;
        MaxRawRequestBytes = maxRawRequestBytes;
        MaxCanonicalCommandBytes = maxCanonicalCommandBytes;
        MaxAcceptedResultBytes = maxAcceptedResultBytes;
    }

    public int MaxPhaseCount { get; }
    public int MaxOptionCount { get; }
    public int MaxFieldUtf8Bytes { get; }
    public int MaxRawRequestBytes { get; }
    public int MaxCanonicalCommandBytes { get; }
    public int MaxAcceptedResultBytes { get; }
}

public sealed record ProjectDefinitionWriteContext {
    public ProjectDefinitionWriteContext(DatasetNamespaceId @namespace, ProjectCallerBinding callerBinding,
        ProjectOperationEpoch epoch, DateTimeOffset admissionDeadlineUtc, DateTimeOffset replayUntilUtc,
        IReadOnlyList<ProjectDefinitionRequestVersion> requestVersions,
        IReadOnlyList<ProjectDefinitionResultVersion> resultVersions, ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(callerBinding);
        ArgumentNullException.ThrowIfNull(epoch);
        ArgumentNullException.ThrowIfNull(limits);
        if (admissionDeadlineUtc.Offset != TimeSpan.Zero || replayUntilUtc.Offset != TimeSpan.Zero || replayUntilUtc <= admissionDeadlineUtc) {
            throw new ArgumentException("Write context requires ordered UTC admission and replay deadlines.");
        }
        Namespace = @namespace;
        CallerBinding = callerBinding;
        Epoch = epoch;
        AdmissionDeadlineUtc = admissionDeadlineUtc;
        ReplayUntilUtc = replayUntilUtc;
        RequestVersions = ProjectDefinitionCollections.Snapshot(requestVersions, nameof(requestVersions));
        ResultVersions = ProjectDefinitionCollections.Snapshot(resultVersions, nameof(resultVersions));
        if (RequestVersions.Count == 0 || ResultVersions.Count == 0) {
            throw new ArgumentException("Write context requires supported request and result versions.");
        }
        Limits = limits;
    }

    public DatasetNamespaceId Namespace { get; }
    public ProjectCallerBinding CallerBinding { get; }
    public ProjectOperationEpoch Epoch { get; }
    public DateTimeOffset AdmissionDeadlineUtc { get; }
    public DateTimeOffset ReplayUntilUtc { get; }
    public IReadOnlyList<ProjectDefinitionRequestVersion> RequestVersions { get; }
    public IReadOnlyList<ProjectDefinitionResultVersion> ResultVersions { get; }
    public ProjectDefinitionLimits Limits { get; }
}

internal sealed class ProjectOperationEpochJsonConverter : JsonConverter<ProjectOperationEpoch> {
    public override ProjectOperationEpoch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var value) && value > 0
            ? new(value) : throw new JsonException("An operation epoch must be a positive integer.");

    public override void Write(Utf8JsonWriter writer, ProjectOperationEpoch value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);
}

internal sealed class ProjectCallerBindingJsonConverter : JsonConverter<ProjectCallerBinding> {
    public override ProjectCallerBinding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        try {
            return reader.TokenType == JsonTokenType.String ? new(reader.GetString()!)
                : throw new JsonException("A caller binding must be a string.");
        } catch (ArgumentException exception) {
            throw new JsonException("The caller binding is invalid.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ProjectCallerBinding value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

internal sealed class ProjectDefinitionOperationJsonConverter : JsonConverter<ProjectDefinitionOperation> {
    public override ProjectDefinitionOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var value = JsonSerializer.Deserialize<SerializedOperation>(ref reader, options)
            ?? throw new JsonException("An operation must be an object.");
        try {
            return new(value.Namespace!, value.Epoch!, value.Id, value.CallerBinding!);
        } catch (ArgumentException exception) {
            throw new JsonException("An operation requires a namespace, epoch, nonempty identifier and caller binding.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ProjectDefinitionOperation value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new SerializedOperation(value.Namespace, value.Epoch, value.Id, value.CallerBinding), options);

    private sealed record SerializedOperation(DatasetNamespaceId? Namespace, ProjectOperationEpoch? Epoch, Guid Id, ProjectCallerBinding? CallerBinding);
}

internal sealed class ProjectDefinitionRequestVersionJsonConverter : JsonConverter<ProjectDefinitionRequestVersion> {
    public override ProjectDefinitionRequestVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var value = JsonSerializer.Deserialize<SerializedVersion>(ref reader, options)
            ?? throw new JsonException("A request interpretation version must be an object.");
        try {
            return new(value.Schema, value.Normalization);
        } catch (ArgumentException exception) {
            throw new JsonException("Request interpretation versions must be positive integers.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ProjectDefinitionRequestVersion value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new SerializedVersion(value.Schema, value.Normalization), options);

    private sealed record SerializedVersion(int Schema, int Normalization);
}

internal sealed class ProjectDefinitionRequestJsonConverterFactory : JsonConverterFactory {
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ProjectDefinitionRequest<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(RequestConverter<>).MakeGenericType(typeToConvert.GenericTypeArguments))!;

    private sealed class RequestConverter<TCommand> : JsonConverter<ProjectDefinitionRequest<TCommand>> where TCommand : class {
        public RequestConverter() {
        }

        public override ProjectDefinitionRequest<TCommand> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            try {
                var value = JsonSerializer.Deserialize<SerializedRequest<TCommand>>(ref reader, options)
                    ?? throw new JsonException("A definition request must be an object.");
                return new(value.Operation!, value.Version!, value.Command!);
            } catch (ArgumentException exception) {
                throw new JsonException("A definition request requires an operation, version and command.", exception);
            }
        }

        public override void Write(Utf8JsonWriter writer, ProjectDefinitionRequest<TCommand> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, new SerializedRequest<TCommand>(value.Operation, value.Version, value.Command), options);
    }

    private sealed record SerializedRequest<TCommand>(ProjectDefinitionOperation? Operation,
        ProjectDefinitionRequestVersion? Version, TCommand? Command) where TCommand : class;
}
