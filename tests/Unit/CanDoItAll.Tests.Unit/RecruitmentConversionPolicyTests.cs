using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit;

public sealed class RecruitmentConversionPolicyTests
{
    [Theory]
    [InlineData(RecruitmentStage.Rejected)]
    [InlineData(RecruitmentStage.Withdrawn)]
    public void Rejected_or_withdrawn_application_is_ineligible_even_when_approved(
        RecruitmentStage stage)
    {
        var error = RecruitmentConversionPolicy.Evaluate(
            stage,
            RecruitmentDecision.Approved);

        Assert.NotNull(error);
        Assert.Equal(RecruitmentConversionPolicy.IneligibleStageErrorCode, error.Code);
        Assert.Contains("Reopen", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RecruitmentDecision.Pending)]
    [InlineData(RecruitmentDecision.Rejected)]
    [InlineData(RecruitmentDecision.Withdrawn)]
    public void Eligible_stage_requires_an_approved_decision(
        RecruitmentDecision decision)
    {
        var error = RecruitmentConversionPolicy.Evaluate(
            RecruitmentStage.Offer,
            decision);

        Assert.NotNull(error);
        Assert.Equal(RecruitmentConversionPolicy.DecisionNotApprovedErrorCode, error.Code);
        Assert.Contains("Approve", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Approved_application_in_an_eligible_stage_can_convert()
    {
        var error = RecruitmentConversionPolicy.Evaluate(
            RecruitmentStage.Offer,
            RecruitmentDecision.Approved);

        Assert.Null(error);
    }

    [Fact]
    public void Ai_candidate_requires_application_specific_ready_assessment()
    {
        var error = RecruitmentConversionPolicy.Evaluate(
            RecruitmentStage.Offer,
            RecruitmentDecision.Approved,
            assessmentRequired: true,
            assessmentReady: false);

        Assert.NotNull(error);
        Assert.Equal(
            RecruitmentConversionPolicy.AssessmentNotReadyErrorCode,
            error.Code);
    }

    [Fact]
    public void Ready_ai_assessment_satisfies_the_conversion_policy()
    {
        var error = RecruitmentConversionPolicy.Evaluate(
            RecruitmentStage.Offer,
            RecruitmentDecision.Approved,
            assessmentRequired: true,
            assessmentReady: true);

        Assert.Null(error);
    }
}
