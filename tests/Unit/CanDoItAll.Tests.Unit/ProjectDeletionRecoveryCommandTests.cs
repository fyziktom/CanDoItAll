using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Infrastructure;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDeletionRecoveryCommandTests {
    private const string Profile = "11111111-1111-4111-8111-111111111111";
    private const string Dataset = "22222222-2222-4222-8222-222222222222";
    private const string Completion = "33333333-3333-4333-8333-333333333333";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Commands_retain_only_the_exact_original_address_plan_and_explicit_precondition(bool reconcile) {
        string[] options = ["--profile-id", Profile, "--expected-namespace", Dataset, "--completion-id", Completion];
        string[] extra = reconcile ? ["--expected-plan-digest", new string('a', 64), "--writers-stopped"] : [];
        var command = ProjectDeletionRecoveryCommand.Parse([ProjectDeletionRecoveryCommand.CommandName,
            reconcile ? "reconcile" : "inspect", .. options, .. extra]);
        Assert.NotNull(command);
        Assert.Equal(Guid.Parse(Profile), command.Address.DatabaseProfileId);
        Assert.Equal(Guid.Parse(Dataset), command.Address.Namespace.Value);
        Assert.Equal(Guid.Parse(Completion), command.Address.Id.Value);
        Assert.Equal(reconcile ? new string('a', 64) : null, command.ExpectedPlan?.Hex);
        Assert.Equal(reconcile ? ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped : null, command.Precondition);
    }

    [Theory]
    [InlineData("inspect", "--writers-stopped")]
    [InlineData("reconcile", "")]
    [InlineData("reconcile", "--writers-stopped")]
    [InlineData("inspect", "--force true")]
    [InlineData("inspect", "--profile-id " + Profile)]
    [InlineData("inspect", "--expected-plan-digest malformed")]
    [InlineData("install-legacy", "--writers-stopped")]
    public void Incomplete_ambiguous_or_unsupported_commands_are_refused_before_host_configuration(string action, string extra) {
        string command = $"{ProjectDeletionRecoveryCommand.CommandName} {action} --profile-id {Profile} --expected-namespace {Dataset} --completion-id {Completion} {extra}";
        Assert.ThrowsAny<ArgumentException>(() => ProjectDeletionRecoveryCommand.Parse(command.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public void Legacy_installation_requires_both_host_attestations_and_the_exact_inspected_subject() {
        string project = Guid.NewGuid().ToString("D");
        string lifetime = Guid.NewGuid().ToString("D");
        string[] address = ["--profile-id", Profile, "--expected-namespace", Dataset, "--completion-id", Completion,
            "--project-id", project, "--lifetime-id", lifetime];
        var inspected = ProjectDeletionRecoveryCommand.Parse([ProjectDeletionRecoveryCommand.CommandName, "inspect-legacy", .. address])!;
        Assert.Equal(ProjectDeletionRecoveryCommandKind.InspectLegacy, inspected.Kind);
        Assert.Equal(new(Guid.Parse(Profile), Guid.Parse(project), Guid.Parse(lifetime)), inspected.LegacyProject);
        Assert.Null(inspected.LegacyPrecondition);
        string[] write = [ProjectDeletionRecoveryCommand.CommandName, "install-legacy", .. address,
            "--expected-plan-digest", new string('a', 64), "--writers-stopped", "--former-source-hosts-stopped"];
        var install = ProjectDeletionRecoveryCommand.Parse(write)!;
        Assert.Equal(ProjectDeletionRecoveryCommandKind.InstallLegacy, install.Kind);
        Assert.Equal(inspected.LegacyProject, install.LegacyProject);
        Assert.Equal(ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped, install.LegacyPrecondition);
        Assert.Throws<ArgumentException>(() => ProjectDeletionRecoveryCommand.Parse(write[..^1]));
        Assert.Throws<ArgumentException>(() => ProjectDeletionRecoveryCommand.Parse(write.Where(value => value != "--writers-stopped").ToArray()));
        Assert.Throws<ArgumentException>(() => ProjectDeletionRecoveryCommand.Parse([ProjectDeletionRecoveryCommand.CommandName, "reconcile",
            .. address, "--expected-plan-digest", new string('a', 64), "--writers-stopped"]));
        Assert.Throws<ArgumentException>(() => ProjectDeletionRecoveryCommand.Parse([ProjectDeletionRecoveryCommand.CommandName, "inspect-legacy",
            .. address, "--former-source-hosts-stopped"]));
    }

    [Fact]
    public void Ordinary_host_arguments_are_not_offline_mutations() {
        Assert.Null(ProjectDeletionRecoveryCommand.Parse([]));
        Assert.Null(ProjectDeletionRecoveryCommand.Parse(["--urls", "http://127.0.0.1:5032"]));
    }
}
