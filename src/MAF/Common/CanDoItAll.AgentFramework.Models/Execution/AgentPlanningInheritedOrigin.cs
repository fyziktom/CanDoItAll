using System.Text;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentPlanningInheritedOrigin(
    [property: JsonRequired] AgentToolProfileBinding Profile,
    [property: JsonRequired] Guid WorkspaceNamespaceId,
    [property: JsonRequired] Guid AgentId,
    [property: JsonRequired] AgentToolBackgroundSourceBinding Source,
    [property: JsonRequired] AgentToolProtocolEnvelope ParentBinding,
    [property: JsonRequired] Guid BasisId,
    [property: JsonRequired] DateTimeOffset CapturedAtUtc,
    [property: JsonRequired] AgentToolSemanticDigest BasisDigest,
    [property: JsonRequired] DecisionDataPolicy ParentPolicy,
    [property: JsonRequired] DecisionBudgetAllocation Allocation) {
    public const int MaximumBindingUtf8Bytes = 16_384;

    public DecisionDataPolicy ResolvePolicy() {
        if (Profile is null || WorkspaceNamespaceId == Guid.Empty || AgentId == Guid.Empty || Source is null ||
            ParentBinding is null || BasisId == Guid.Empty || (CapturedAtUtc == default || CapturedAtUtc.Offset != TimeSpan.Zero) ||
            string.IsNullOrEmpty(BasisDigest.Value) || ParentPolicy is null || Allocation is null ||
            Encoding.UTF8.GetByteCount(ParentBinding.PayloadJson) > MaximumBindingUtf8Bytes) {
            throw new InvalidDataException("The inherited planning origin or grant is invalid.");
        }
        Allocation.Validate();
        _ = DecisionDataPolicyCodec.Serialize(ParentPolicy);
        if (Allocation.Settlement is not null || Allocation.Grant.WorkTicks <= 0 || Allocation.Grant.Replans != 0 ||
            Allocation.Grant.WorkTicks > ParentPolicy.Budget.MaximumWorkDuration.Ticks) {
            throw new InvalidDataException("The inherited planning origin or grant is invalid.");
        }
        return new(ReadBasisMode.RunSnapshot, ParentPolicy.PublicationFreshness, OnBasisChange.PauseForReview,
            new(0, TimeSpan.FromTicks(Allocation.Grant.WorkTicks), ParentPolicy.Budget.MaximumBasisAge));
    }

    internal void Validate(AgentToolSessionReference session, DateTimeOffset startedAtUtc) {
        _ = ResolvePolicy();
        if (session.BackgroundSource != Source || CapturedAtUtc > startedAtUtc) {
            throw new InvalidDataException("The inherited planning origin or grant is invalid.");
        }
    }

    public void RequireOriginalBasis(AgentPlanningBasisRecord basis) {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.Id != BasisId || basis.CapturedAtUtc != CapturedAtUtc || basis.Value?.Digest != BasisDigest) {
            throw new InvalidDataException("The inherited planning origin or grant is invalid.");
        }
    }
}
