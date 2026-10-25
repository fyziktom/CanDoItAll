using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class DatasetActivationRestorationTests {
    [Theory]
    [InlineData(DatasetActivationFileRecord.LegacyVersion)]
    [InlineData(DatasetActivationFileRecord.CurrentVersion)]
    public void Ordinary_records_remain_readable_without_a_restoration_descriptor(int version) {
        var record = new DatasetActivationFileRecord(version, Guid.NewGuid(), DatasetActivationFileState.Active,
            new(Guid.NewGuid(), DatasetActivationKind.Bootstrap, Binding(), null));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, DatasetActivationRecordCodec.Options);
        using var json = JsonDocument.Parse(bytes);
        Assert.False(json.RootElement.TryGetProperty("restoration", out _));
        Assert.Equal(record, DatasetActivationRecordCodec.Decode(bytes, record.ProfileId));
    }

    [Theory]
    [InlineData(DatasetActivationFileState.Prepared)]
    [InlineData(DatasetActivationFileState.Active)]
    public void Restoration_retains_the_complete_abandoned_plan_and_reuses_the_exact_replacement(DatasetActivationFileState state) {
        var pending = Pending();
        var selected = DatasetActivationRestoration.Select(pending, pending.Plan.PlanId, pending.Plan.ExpectedPreviousSqlBinding!.Namespace);
        var retained = new DatasetActivationFileRecord(selected.Version, selected.ProfileId, state, selected.Plan, selected.Restoration);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(retained, DatasetActivationRecordCodec.Options);
        var decoded = DatasetActivationRecordCodec.Decode(bytes, pending.ProfileId);
        Assert.Equal(retained, decoded);
        Assert.Equal(pending.Plan, decoded.Restoration!.AbandonedPlan);
        Assert.Equal(pending.Plan.ExpectedPreviousSqlBinding, decoded.Plan.Binding);
        Assert.NotEqual(pending.Plan.PlanId, decoded.Plan.PlanId);
        Assert.Equal(DatasetActivationKind.Attach, decoded.Plan.Kind);
        Assert.Same(decoded, DatasetActivationRestoration.Select(decoded, pending.Plan.PlanId, pending.Plan.ExpectedPreviousSqlBinding.Namespace));
    }

    [Theory]
    [InlineData(InvalidDescriptor.LegacyVersion)]
    [InlineData(InvalidDescriptor.WrongBinding)]
    [InlineData(InvalidDescriptor.ReusedPlan)]
    [InlineData(InvalidDescriptor.WrongKind)]
    public void Unsupported_or_inconsistent_restoration_metadata_is_rejected(InvalidDescriptor fault) {
        var pending = Pending();
        var previous = pending.Plan.ExpectedPreviousSqlBinding!;
        var binding = fault == InvalidDescriptor.WrongBinding ? Binding() : previous;
        var plan = fault == InvalidDescriptor.WrongKind ? pending.Plan : new DatasetActivationPlan(
            fault == InvalidDescriptor.ReusedPlan ? pending.Plan.PlanId : Guid.NewGuid(), DatasetActivationKind.Attach, binding, binding);
        Assert.Throws<ArgumentException>(() => new DatasetActivationFileRecord(
            fault == InvalidDescriptor.LegacyVersion ? 1 : 2, pending.ProfileId, DatasetActivationFileState.Prepared, plan, new(pending.Plan)));
        Assert.Throws<ArgumentException>(() => new DatasetActivationRestoration(new(Guid.NewGuid(), DatasetActivationKind.Attach, previous, previous)));
    }

    [Theory]
    [InlineData(InvalidSelection.WrongPlan)]
    [InlineData(InvalidSelection.WrongNamespace)]
    [InlineData(InvalidSelection.ActiveReactivation)]
    [InlineData(InvalidSelection.Bootstrap)]
    [InlineData(InvalidSelection.RestoredOtherPlan)]
    public void A_restore_request_cannot_reinterpret_another_selection(InvalidSelection fault) {
        var original = Pending();
        var pending = original;
        if (fault == InvalidSelection.ActiveReactivation) {
            pending = new(2, original.ProfileId, DatasetActivationFileState.Active, original.Plan);
        } else if (fault == InvalidSelection.Bootstrap) {
            pending = new(2, original.ProfileId, DatasetActivationFileState.Prepared,
                new(original.Plan.PlanId, DatasetActivationKind.Bootstrap, Binding(), null));
        } else if (fault == InvalidSelection.RestoredOtherPlan) {
            pending = DatasetActivationRestoration.Select(original, original.Plan.PlanId, original.Plan.ExpectedPreviousSqlBinding!.Namespace);
        }
        Guid expected = fault is InvalidSelection.WrongPlan or InvalidSelection.RestoredOtherPlan ? Guid.NewGuid() : original.Plan.PlanId;
        var expectedNamespace = fault == InvalidSelection.WrongNamespace ? new DatasetNamespaceId(Guid.NewGuid()) : original.Plan.ExpectedPreviousSqlBinding!.Namespace;
        Assert.Throws<InvalidOperationException>(() => DatasetActivationRestoration.Select(pending, expected, expectedNamespace));
    }

    private static DatasetActivationFileRecord Pending()
        => new(2, Guid.NewGuid(), DatasetActivationFileState.Prepared, new(Guid.NewGuid(), DatasetActivationKind.Reactivate, Binding(), Binding()));

    private static DatasetActivationBinding Binding()
        => new(new(Guid.NewGuid()), Guid.NewGuid(), DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows);

    public enum InvalidDescriptor { LegacyVersion, WrongBinding, ReusedPlan, WrongKind }
    public enum InvalidSelection { WrongPlan, WrongNamespace, ActiveReactivation, Bootstrap, RestoredOtherPlan }
}
