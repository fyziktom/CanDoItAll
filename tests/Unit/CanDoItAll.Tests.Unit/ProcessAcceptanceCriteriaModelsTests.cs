using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAcceptanceCriteriaModelsTests
{
    [Fact]
    public void Required_criteria_excludes_delivery_planning_even_when_legacy_required_flag_is_true()
    {
        var productCriterion = new ProcessAcceptanceCriterion
        {
            Id = "AC-001",
            Summary = "The product must expose the requested behavior.",
            Kind = ProcessAcceptanceCriterionKind.ProductAcceptance,
            RequiredForAcceptance = true
        };
        var planningCriterion = new ProcessAcceptanceCriterion
        {
            Id = "AC-002",
            Summary = "Confirm the preferred delivery window.",
            Kind = ProcessAcceptanceCriterionKind.DeliveryPlanning,
            RequiredForAcceptance = true
        };
        var matrix = new ProcessAcceptanceCriteriaMatrix
        {
            Criteria = [productCriterion, planningCriterion]
        };

        var required = Assert.Single(matrix.RequiredCriteria);

        Assert.Same(productCriterion, required);
    }

    [Fact]
    public void Json_round_trip_preserves_typed_criterion_kind()
    {
        var matrix = new ProcessAcceptanceCriteriaMatrix
        {
            Criteria =
            [
                new ProcessAcceptanceCriterion
                {
                    Id = "AC-001",
                    Summary = "Capture the pending delivery decision.",
                    Kind = ProcessAcceptanceCriterionKind.DeliveryPlanning,
                    RequiredForAcceptance = false
                }
            ]
        };

        var payload = ProcessAcceptanceCriteriaMatrixJson.Serialize(matrix);

        Assert.True(
            ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(
                payload,
                out var roundTripped));
        var criterion = Assert.Single(roundTripped.Criteria);
        Assert.Equal(
            ProcessAcceptanceCriterionKind.DeliveryPlanning,
            criterion.Kind);
        Assert.Contains(
            "\"kind\":\"DeliveryPlanning\"",
            payload,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_json_without_kind_defaults_to_required_product_acceptance()
    {
        const string payload =
            """
            {
              "criteria": [
                {
                  "id": "AC-001",
                  "sourceNodeId": "custom:legacy",
                  "summary": "The legacy product behavior must remain required.",
                  "verificationMethods": ["planned-validation"],
                  "requiredForAcceptance": true
                }
              ]
            }
            """;

        Assert.True(
            ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(
                payload,
                out var matrix));
        var criterion = Assert.Single(matrix.RequiredCriteria);
        Assert.Equal(
            ProcessAcceptanceCriterionKind.ProductAcceptance,
            criterion.Kind);
    }

    [Theory]
    [InlineData("""{"criteria":null}""")]
    [InlineData("""{"criteria":[null]}""")]
    [InlineData("""{"criteria":[{"id":"AC-001","summary":"Invalid numeric kind.","verificationMethods":[],"kind":999}]}""")]
    [InlineData("""{"criteria":[{"id":"AC-001","summary":"Invalid string kind.","verificationMethods":[],"kind":"Unknown"}]}""")]
    [InlineData("""{"criteria":[{"id":"","summary":"Blank id is invalid.","verificationMethods":[]}]}""")]
    [InlineData("""{"criteria":[{"id":"AC-001","summary":"First.","verificationMethods":[]},{"id":"ac-001","summary":"Duplicate.","verificationMethods":[]}]}""")]
    public void Invalid_json_contracts_are_rejected(string payload)
    {
        Assert.False(
            ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(
                payload,
                out _));
    }
}
