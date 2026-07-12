using CanDoItAll.Memory.SourceGateway;
using System.Text.RegularExpressions;
using CanDoItAll.Memory.Abstractions;
using MafMemorySourceKind = CanDoItAll.Memory.SourceGateway.MemorySourceKind;

namespace CanDoItAll.Memory.Application;

public enum MemorySourceGatewayStatus
{
    Succeeded = 0,
    MissingAdapter = 1,
    DeniedSourceScope = 2,
    UnsupportedSourceKind = 3,
    RedactionRequired = 4,
    InvalidSnapshot = 5
}

public enum MemorySourcePayloadForm
{
    TextSection = 0,
    StructuredJsonFacts = 1,
    FileReference = 2,
    ArtifactReference = 3,
    LinkReference = 4,
    BinaryOrExternalReference = 5
}

public readonly record struct MemorySourceModuleId
{
    private static readonly Regex ModuleIdPattern = new(
        "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public MemorySourceModuleId(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Memory source module id must not be empty.", nameof(value))
            : value.Trim();
        if (!ModuleIdPattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "Memory source module ids must use dotted lowercase tokens such as 'workbench.project-structure'.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public static MemorySourceModuleId Parse(string value) => new(value);

    public override string ToString() => Value;
}

public sealed record MemorySourceGatewayPolicy(
    IReadOnlyList<MafMemorySourceKind> AllowedSourceKinds,
    bool RequireRedactionForSensitivePayload)
{
    private static readonly MemorySourceScope[] AllSourceScopes = Enum.GetValues<MemorySourceScope>();

    public IReadOnlyList<MemorySourceScope> AllowedScopes { get; init; } = AllSourceScopes;

    public static MemorySourceGatewayPolicy Allow(IReadOnlyList<MafMemorySourceKind> sourceKinds) =>
        new(sourceKinds.ToArray(), RequireRedactionForSensitivePayload: true)
        {
            AllowedScopes = AllSourceScopes
        };

    public static MemorySourceGatewayPolicy AllowScopes(
        IReadOnlyList<MafMemorySourceKind> sourceKinds,
        IReadOnlyList<MemorySourceScope> sourceScopes) =>
        new(sourceKinds.ToArray(), RequireRedactionForSensitivePayload: true)
        {
            AllowedScopes = sourceScopes.ToArray()
        };

    public bool Allows(MafMemorySourceKind sourceKind) => AllowedSourceKinds.Contains(sourceKind);

    public bool AllowsScope(MemorySourceScope sourceScope) => AllowedScopes.Contains(sourceScope);
}

public sealed record MemorySourceGatewayRequest(
    MafMemorySourceKind SourceKind,
    Guid ScopeId,
    MemorySourceScope RequestedScope,
    MemorySourceSnapshotCursor? Cursor,
    int? Take,
    MemorySourceGatewayPolicy Policy,
    string RequesterId)
{
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record MemorySourceGatewayResult(
    MemorySourceGatewayStatus Status,
    bool DispatchAllowed,
    MemorySourceSnapshot? Snapshot,
    IReadOnlyList<MemorySourcePayloadForm> PayloadForms,
    MemorySourceModuleId? AdapterModuleId,
    string Diagnostic)
{
    public static MemorySourceGatewayResult Succeeded(
        MemorySourceSnapshot snapshot,
        IReadOnlyList<MemorySourcePayloadForm> payloadForms,
        MemorySourceModuleId? adapterModuleId = null) =>
        new(
            MemorySourceGatewayStatus.Succeeded,
            DispatchAllowed: true,
            snapshot,
            payloadForms,
            adapterModuleId,
            "Memory source snapshot returned.");

    public static MemorySourceGatewayResult Rejected(
        MemorySourceGatewayStatus status,
        string diagnostic) =>
        new(
            status,
            DispatchAllowed: false,
            Snapshot: null,
            PayloadForms: [],
            AdapterModuleId: null,
            diagnostic);
}

public sealed record MemorySourceGatewayAdapterDescriptor(
    MemorySourceModuleId ModuleId,
    MafMemorySourceKind SourceKind,
    string ProviderVersion,
    MemorySourceScope RequiredScope,
    bool RequiresPermissionCheck);

public interface IMemorySourceGatewayAdapter
{
    MemorySourceGatewayAdapterDescriptor Descriptor { get; }

    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        MemorySourceGatewayRequest request,
        CancellationToken cancellationToken = default);
}

public interface IMemorySourceGateway
{
    Task<MemorySourceGatewayResult> ReadSnapshotAsync(
        MemorySourceGatewayRequest request,
        CancellationToken cancellationToken = default);
}
