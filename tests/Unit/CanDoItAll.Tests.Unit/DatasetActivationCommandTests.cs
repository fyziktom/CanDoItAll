using CanDoItAll.Web.Infrastructure;

namespace CanDoItAll.Tests.Unit;

public sealed class DatasetActivationCommandTests {
    private const string Profile = "11111111-1111-1111-1111-111111111111";
    private const string Previous = "22222222-2222-2222-2222-222222222222";
    private const string Maintenance = "33333333-3333-3333-3333-333333333333";

    [Theory]
    [InlineData("bootstrap", DatasetActivationCommandKind.Bootstrap)]
    [InlineData("attach", DatasetActivationCommandKind.Attach)]
    [InlineData("resume", DatasetActivationCommandKind.Resume)]
    [InlineData("reactivate", DatasetActivationCommandKind.Reactivate)]
    [InlineData("attach-superseded-bootstrap", DatasetActivationCommandKind.AttachSupersededBootstrap)]
    [InlineData("restore-previous", DatasetActivationCommandKind.RestorePrevious)]
    public void Offline_commands_require_the_exact_typed_target_and_recovery_identity(string action, DatasetActivationCommandKind expected) {
        var args = new List<string> { "dataset-activation", action, "--profile-id", Profile, "--writers-stopped" };
        if (expected is DatasetActivationCommandKind.Reactivate or DatasetActivationCommandKind.AttachSupersededBootstrap or DatasetActivationCommandKind.RestorePrevious) {
            args.AddRange(["--expected-namespace", Previous,
                expected == DatasetActivationCommandKind.Reactivate ? "--maintenance-id" : "--expected-plan-id", Maintenance]);
        }

        var parsed = DatasetActivationCommand.Parse(args.ToArray());

        Assert.NotNull(parsed);
        Assert.Equal(expected, parsed.Kind);
        Assert.Equal(Guid.Parse(Profile), parsed.ProfileId);
        if (expected == DatasetActivationCommandKind.Reactivate) {
            Assert.Equal(Guid.Parse(Previous), parsed.ExpectedNamespace!.Value);
            Assert.Equal(Guid.Parse(Maintenance), parsed.MaintenanceId);
            Assert.Null(parsed.ExpectedPlanId);
        } else if (expected is DatasetActivationCommandKind.AttachSupersededBootstrap or DatasetActivationCommandKind.RestorePrevious) {
            Assert.Equal(Guid.Parse(Previous), parsed.ExpectedNamespace!.Value);
            Assert.Equal(Guid.Parse(Maintenance), parsed.ExpectedPlanId);
            Assert.Null(parsed.MaintenanceId);
        } else {
            Assert.Null(parsed.ExpectedNamespace);
            Assert.Null(parsed.ExpectedPlanId);
            Assert.Null(parsed.MaintenanceId);
        }
    }

    [Theory]
    [InlineData("bootstrap --profile-id " + Profile)]
    [InlineData("bootstrap --profile-id 00000000-0000-0000-0000-000000000000 --writers-stopped")]
    [InlineData("reactivate --profile-id " + Profile + " --writers-stopped")]
    [InlineData("attach --profile-id " + Profile + " --writers-stopped --writers-stopped")]
    [InlineData("unknown --profile-id " + Profile + " --writers-stopped")]
    [InlineData("bootstrap --profile-id " + Profile + " --writers-stopped --maintenance-id " + Maintenance)]
    [InlineData("reactivate --profile-id " + Profile + " --writers-stopped --new-namespace " + Previous)]
    [InlineData("restore-previous --profile-id " + Profile + " --writers-stopped")]
    [InlineData("restore-previous --profile-id " + Profile + " --expected-namespace " + Previous + " --expected-plan-id " + Maintenance)]
    [InlineData("restore-previous --profile-id " + Profile + " --writers-stopped --expected-namespace " + Previous + " --maintenance-id " + Maintenance)]
    public void Invalid_or_ambiguous_maintenance_arguments_cannot_start_an_activation(string arguments)
        => Assert.Throws<ArgumentException>(() => DatasetActivationCommand.Parse(
            ("dataset-activation " + arguments).Split(' ', StringSplitOptions.RemoveEmptyEntries)));

    [Fact]
    public void Ordinary_host_arguments_are_not_interpreted_as_offline_mutations() {
        Assert.Null(DatasetActivationCommand.Parse([]));
        Assert.Null(DatasetActivationCommand.Parse(["--urls", "http://127.0.0.1:5032"]));
    }
}
