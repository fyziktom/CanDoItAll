using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskExecutionStatePolicyTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unknown_legacy_state_is_valid_without_timestamps()
    {
        ProjectTaskExecutionStatePolicy.Validate(
            ProjectTaskExecutionState.Unknown,
            null,
            null);
    }

    [Fact]
    public void Not_started_rejects_actual_timestamps()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProjectTaskExecutionStatePolicy.Validate(
                ProjectTaskExecutionState.NotStarted,
                StartedAtUtc,
                null));

        Assert.Contains("has not started", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Started_requires_start_and_rejects_end()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProjectTaskExecutionStatePolicy.Validate(
                ProjectTaskExecutionState.Started,
                null,
                null));

        Assert.Throws<InvalidOperationException>(() =>
            ProjectTaskExecutionStatePolicy.Validate(
                ProjectTaskExecutionState.Started,
                StartedAtUtc,
                StartedAtUtc.AddHours(1)));
    }

    [Fact]
    public void Completed_requires_ordered_start_and_end()
    {
        ProjectTaskExecutionStatePolicy.Validate(
            ProjectTaskExecutionState.Completed,
            StartedAtUtc,
            StartedAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() =>
            ProjectTaskExecutionStatePolicy.Validate(
                ProjectTaskExecutionState.Completed,
                StartedAtUtc,
                StartedAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void Cancelled_can_end_before_work_started()
    {
        ProjectTaskExecutionStatePolicy.Validate(
            ProjectTaskExecutionState.Cancelled,
            null,
            StartedAtUtc);
    }

    [Theory]
    [InlineData(ProjectTaskExecutionState.NotStarted, ProjectTaskExecutionState.Started)]
    [InlineData(ProjectTaskExecutionState.NotStarted, ProjectTaskExecutionState.Completed)]
    [InlineData(ProjectTaskExecutionState.NotStarted, ProjectTaskExecutionState.Cancelled)]
    [InlineData(ProjectTaskExecutionState.Started, ProjectTaskExecutionState.Completed)]
    [InlineData(ProjectTaskExecutionState.Started, ProjectTaskExecutionState.Cancelled)]
    [InlineData(ProjectTaskExecutionState.Unknown, ProjectTaskExecutionState.NotStarted)]
    [InlineData(ProjectTaskExecutionState.Unknown, ProjectTaskExecutionState.Completed)]
    public void Forward_or_explicit_reconciliation_transitions_are_allowed(
        ProjectTaskExecutionState current,
        ProjectTaskExecutionState proposed)
    {
        ProjectTaskExecutionStatePolicy.ValidateTransition(current, proposed);
    }

    [Theory]
    [InlineData(ProjectTaskExecutionState.Started, ProjectTaskExecutionState.NotStarted)]
    [InlineData(ProjectTaskExecutionState.Completed, ProjectTaskExecutionState.Started)]
    [InlineData(ProjectTaskExecutionState.Cancelled, ProjectTaskExecutionState.NotStarted)]
    [InlineData(ProjectTaskExecutionState.NotStarted, ProjectTaskExecutionState.Unknown)]
    public void Backward_or_erasing_transitions_are_rejected(
        ProjectTaskExecutionState current,
        ProjectTaskExecutionState proposed)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProjectTaskExecutionStatePolicy.ValidateTransition(current, proposed));
    }

    [Fact]
    public void Only_explicit_not_started_state_allows_repricing()
    {
        Assert.True(ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(
            ProjectTaskExecutionState.NotStarted));
        Assert.False(ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(
            ProjectTaskExecutionState.Unknown));
        Assert.False(ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(
            ProjectTaskExecutionState.Started));
        Assert.False(ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(
            ProjectTaskExecutionState.Completed));
        Assert.False(ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(
            ProjectTaskExecutionState.Cancelled));
    }

    [Theory]
    [InlineData(
        ProjectTaskExecutionState.NotStarted,
        ProjectTaskExecutionState.Started,
        ProjectTaskExecutionState.NotStarted)]
    [InlineData(
        ProjectTaskExecutionState.NotStarted,
        ProjectTaskExecutionState.Completed,
        ProjectTaskExecutionState.NotStarted)]
    [InlineData(
        ProjectTaskExecutionState.Unknown,
        ProjectTaskExecutionState.NotStarted,
        ProjectTaskExecutionState.NotStarted)]
    [InlineData(
        ProjectTaskExecutionState.Started,
        ProjectTaskExecutionState.Completed,
        ProjectTaskExecutionState.Completed)]
    public void Pricing_state_uses_pre_execution_eligibility_when_the_update_crosses_the_boundary(
        ProjectTaskExecutionState current,
        ProjectTaskExecutionState proposed,
        ProjectTaskExecutionState expected)
    {
        var pricingState = ProjectTaskExecutionStatePolicy.ResolveAuthoritativePricingState(
            current,
            proposed);

        Assert.Equal(expected, pricingState);
        Assert.Equal(
            expected == ProjectTaskExecutionState.NotStarted,
            ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(pricingState));
    }
}
