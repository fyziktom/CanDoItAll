using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowExternalRequestBoundaryEntity : IHasConcurrencyToken
{
    public Guid RequestId { get; set; }

    public long RequestVersion { get; set; }

    public int State { get; set; }

    public string ResponseContractJson { get; set; } = string.Empty;

    public string ContinuationJson { get; set; } = string.Empty;

    public string RequestPayloadHash { get; set; } = string.Empty;

    public string? AuthorizationPolicyJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
