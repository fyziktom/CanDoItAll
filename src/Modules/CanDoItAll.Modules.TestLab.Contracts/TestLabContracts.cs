using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.TestLab;

public enum TestCaseStatus {
    Planned,
    Implemented,
    Passed,
    Failed,
    Blocked
}

public sealed record TestPlanSummary(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string Phase,
    int CaseCount,
    int EvidenceCount,
    TestCaseStatus? LatestResult,
    DateTimeOffset UpdatedAtUtc) {
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? ProjectLifetimeId { get; init; }
}

public sealed class TestCaseEditorModel : ITestPlanChildEditor {
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string StoryOrFeature { get; set; } = string.Empty;

    public TestCaseStatus Status { get; set; } = TestCaseStatus.Planned;

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestEvidenceEditorModel : ITestPlanChildEditor {
    public Guid? Id { get; set; }

    public string EvidenceLabel { get; set; } = string.Empty;

    public string ArtifactPath { get; set; } = string.Empty;

    public string EvidenceKind { get; set; } = "Screenshot";

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestRunEditorModel : ITestPlanChildEditor {
    public Guid? Id { get; set; }

    public DateTimeOffset ExecutedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Runner { get; set; } = "Playwright";

    public TestCaseStatus Result { get; set; } = TestCaseStatus.Planned;

    public string Summary { get; set; } = string.Empty;
}

public sealed class TestPlanEditorModel {
    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public ProjectWriteAdmission? ExpectedProjectAdmission { get; set; }

    public Guid? ResponsiblePartyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string CoverageGoal { get; set; } = string.Empty;

    public string PlaywrightSpecPath { get; set; } = string.Empty;

    public List<TestCaseEditorModel> Cases { get; set; } = [];

    public List<TestEvidenceEditorModel> Evidence { get; set; } = [];

    public List<TestRunEditorModel> Runs { get; set; } = [];
}

public interface ITestPlanChildEditor {
    Guid? Id { get; set; }
}
