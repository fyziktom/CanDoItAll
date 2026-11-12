using System.Globalization;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionChildIdentityTests {
    private static readonly ProcessDecisionId Decision = new(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));

    [Theory]
    [InlineData(1, ProcessDecisionChildOwner.Agent, 1, "021d3273-1af1-8a33-9a02-2cb72e204efc")]
    [InlineData(1, ProcessDecisionChildOwner.Workflow, 1, "c2728e27-1368-8a1c-980c-0d409c36d832")]
    [InlineData(2, ProcessDecisionChildOwner.Agent, 1, "e41a586f-59a5-8179-8a12-bc0c29892085")]
    [InlineData(32, ProcessDecisionChildOwner.Workflow, 64, "a59ab1c6-e478-8e1a-9c4a-132b9be4bc0f")]
    public void Occurrence_identity_matches_fixed_versioned_cross_host_vectors(int generation, ProcessDecisionChildOwner owner, int ordinal, string expected) {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var actual = ProcessDecisionChildIdentity.Create(new(Decision, generation), owner, ordinal);
            Assert.Equal(Guid.Parse(expected), actual.Value);
            Assert.Equal(actual, ProcessDecisionChildIdentity.Create(new(Decision, generation), owner, ordinal));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void All_bounded_generations_owners_and_ordinals_have_distinct_occurrences() {
        var ids = new HashSet<ProcessDecisionChildId>();
        foreach (var decision in new[] { Decision, new ProcessDecisionId(Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00")) }) {
            for (var generation = 1; generation <= ProcessDecisionGeneration.MaximumGenerations; generation++) {
                foreach (var owner in Enum.GetValues<ProcessDecisionChildOwner>()) {
                    for (var ordinal = 1; ordinal <= ProcessDecisionGeneration.MaximumChildren; ordinal++) {
                        Assert.True(ids.Add(ProcessDecisionChildIdentity.Create(new(decision, generation), owner, ordinal)));
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(33, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 2, 1)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 0, 65)]
    public void Invalid_generation_owner_or_ordinal_cannot_issue_an_identity(int generation, int owner, int ordinal) {
        if (generation is < 1 or > ProcessDecisionGeneration.MaximumGenerations) {
            Assert.Throws<InvalidDataException>(() => ProcessDecisionChildIdentity.Create(new(Decision, generation), (ProcessDecisionChildOwner)owner, ordinal));
        } else {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProcessDecisionChildIdentity.Create(new(Decision, generation), (ProcessDecisionChildOwner)owner, ordinal));
        }
    }

    [Fact]
    public void Missing_decision_identity_cannot_issue_a_child() {
        Assert.Throws<InvalidDataException>(() => ProcessDecisionChildIdentity.Create(new(default, 1), ProcessDecisionChildOwner.Agent, 1));
        Assert.Throws<ArgumentNullException>(() => ProcessDecisionChildIdentity.Create(null!, ProcessDecisionChildOwner.Agent, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Admission_requires_original_scope_material_owner_and_positive_allocation(int change) {
        var admission = new ProcessDecisionChildAdmission(new(new(Guid.NewGuid()), new(Guid.NewGuid()), new(Guid.NewGuid()), new(Decision, 1),
            new(Guid.NewGuid())), new(Guid.NewGuid()), ProcessDecisionChildOwner.Agent, new(Guid.NewGuid()), new(Guid.NewGuid()), new(5, 0));
        admission.Validate();
        var invalid = change switch {
            0 => admission with { Address = admission.Address with { RootRunId = default } },
            1 => admission with { Address = admission.Address with { RunId = default } },
            2 => admission with { Address = admission.Address with { StepInstanceId = default } },
            3 => admission with { Address = admission.Address with { ChildId = default } },
            4 => admission with { Address = admission.Address with { Reference = new(default, 1) } },
            5 => admission with { MaterialId = default },
            6 => admission with { Owner = (ProcessDecisionChildOwner)2 },
            7 => admission with { BudgetId = default },
            8 => admission with { AllocationId = default },
            9 => admission with { Grant = new(0, 0) },
            _ => admission with { Grant = new(-1, 0) }
        };
        Assert.Throws<InvalidDataException>(invalid.Validate);
    }
}
