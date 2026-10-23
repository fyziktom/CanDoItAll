using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionChildMaterialBinding(ProcessDecisionChildAdmission Child, ProcessDecisionMaterialScope Scope,
    ProcessDecisionMaterialStamp Material, ProcessDecisionExecutionContract ExecutionContract) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Child);
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(Material);
        ArgumentNullException.ThrowIfNull(ExecutionContract);
        Child.Validate();
        Scope.Validate();
        Material.Validate();
        ExecutionContract.Validate();
        if (Child.Owner != ExecutionContract.Executor || Child.MaterialId != Material.Reference.MaterialId ||
            Scope.Id != Material.Reference.ScopeId || Scope.RootRunId != Child.Address.RootRunId ||
            Scope.OwnerRunId != Child.Address.RunId && Scope.OwnerRunId != Child.Address.RootRunId ||
            Scope.StepInstanceId is { } step && (step != Child.Address.StepInstanceId || Scope.OwnerRunId != Child.Address.RunId)) {
            throw new InvalidDataException("The original Process child and material binding do not share their owner and scope.");
        }
    }
}

public static class ProcessDecisionChildMaterialBindingCodec {
    public const string Format = "process-decision-child-material";
    public const int Version = 1;
    public const int MaximumUtf8Bytes = 16_384;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, NumberHandling = JsonNumberHandling.Strict, MaxDepth = 8
    };

    public static string Fingerprint(ProcessDecisionChildMaterialBinding binding)
        => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(binding))));

    public static string Serialize(ProcessDecisionChildMaterialBinding binding) {
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        var child = binding.Child;
        var address = child.Address;
        var scope = binding.Scope;
        var contract = binding.ExecutionContract;
        var value = new Wire(Version, address.RootRunId.Value, address.RunId.Value, address.StepInstanceId.Value,
            address.Reference.DecisionId.Value, address.Reference.Generation, address.ChildId.Value,
            child.BudgetId.Value, child.AllocationId.Value, child.Grant.WorkTicks, child.Grant.Replans,
            scope.Id.Value, scope.DatabaseProfileId, scope.WorkspaceNamespaceId, scope.OwnerRunId.Value, scope.StepInstanceId?.Value,
            binding.Material.Reference.Generation, child.MaterialId.Value, binding.Material.Fingerprint,
            contract.ContractId, contract.Version, contract.Executor);
        var json = JsonSerializer.Serialize(value, Options);
        RequireSize(json);
        return json;
    }

    public static ProcessDecisionChildMaterialBinding Deserialize(string json) {
        ArgumentNullException.ThrowIfNull(json);
        RequireSize(json);
        try {
            var wire = JsonSerializer.Deserialize<Wire>(json, Options)
                ?? throw new InvalidDataException("The original Process child material binding is absent.");
            if (wire.Version != Version) {
                throw new InvalidDataException("The original Process child material binding version is unsupported.");
            }
            var child = new ProcessDecisionChildAdmission(new(new(wire.RootRunId), new(wire.RunId), new(wire.StepId),
                new(new(wire.DecisionId), wire.Generation), new(wire.ChildId)), new(wire.MaterialId), wire.Executor,
                new(wire.BudgetId), new(wire.AllocationId), new(wire.GrantedWorkTicks, wire.GrantedReplans));
            ProcessDecisionChildMaterialBinding binding = new(child,
                new(new(wire.ScopeId), wire.DatabaseProfileId, wire.WorkspaceNamespaceId, new(wire.RootRunId), new(wire.OwnerRunId),
                    wire.MaterialStepId is { } step ? new(step) : null),
                new(new(new(wire.ScopeId), wire.MaterialGeneration, new(wire.MaterialId)), wire.Fingerprint),
                new(wire.ContractId, wire.ContractVersion, wire.Executor));
            if (Serialize(binding) != json) {
                throw new InvalidDataException("The original Process child material binding is not canonical.");
            }
            return binding;
        } catch (Exception failure) when (failure is JsonException or ArgumentException) {
            throw new InvalidDataException("The original Process child material binding is malformed.", failure);
        }
    }

    private static void RequireSize(string json) {
        if (json.Length > MaximumUtf8Bytes || Encoding.UTF8.GetByteCount(json) > MaximumUtf8Bytes) {
            throw new InvalidDataException("The original Process child material binding exceeds its byte bound.");
        }
    }

    private sealed record Wire(int Version, Guid RootRunId, Guid RunId, Guid StepId, Guid DecisionId, int Generation, Guid ChildId,
        Guid BudgetId, Guid AllocationId, long GrantedWorkTicks, int GrantedReplans, Guid ScopeId, Guid DatabaseProfileId,
        Guid WorkspaceNamespaceId, Guid OwnerRunId, Guid? MaterialStepId, int MaterialGeneration, Guid MaterialId, string Fingerprint,
        Guid ContractId, int ContractVersion, ProcessDecisionChildOwner Executor);
}
