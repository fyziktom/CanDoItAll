using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed record AiAgentDiagnosticParty(Guid Id, string DisplayName);

public sealed record AiAgentDiagnosticBinding(
    Guid PartyId,
    Guid? TechnicalAgentId,
    AiResourceBindingStatus BindingStatus,
    string BindingReason);
