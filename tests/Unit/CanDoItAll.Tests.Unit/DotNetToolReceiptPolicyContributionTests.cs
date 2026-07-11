using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit;

public sealed class DotNetToolReceiptPolicyContributionTests
{
    [Theory]
    [InlineData("diagnose-quality-failure")]
    [InlineData("diagnose-persistent-failure")]
    public void Quality_diagnosis_steps_may_report_the_unresolved_state_they_diagnose(string stepKey)
    {
        Assert.True(DotNetToolReceiptPolicyContribution.IsDiagnosticStep(stepKey));
    }

    [Theory]
    [InlineData("implement-quality-repair")]
    [InlineData("validate-quality-repair")]
    public void Mutation_and_validation_steps_are_not_diagnostic_exemptions(string stepKey)
    {
        Assert.False(DotNetToolReceiptPolicyContribution.IsDiagnosticStep(stepKey));
    }
}
