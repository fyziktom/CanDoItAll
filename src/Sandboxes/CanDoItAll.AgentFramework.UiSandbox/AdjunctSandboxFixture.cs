using CanDoItAll.AgentFramework.UI.Governance;
using CanDoItAll.AgentFramework.UI.Runtime;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum AdjunctScenario { Waiting, Streaming, Completed, Failure, Unknown, Partial, Long }

public static class AdjunctSandboxFixture {
    public static readonly Guid RunId = new("ac100000-0000-4000-8000-000000000031");
    public static readonly Guid EntryId = new("ac100000-0000-4000-8000-000000000032");

    public static AgentRuntimeDetailsState Runtime(AdjunctScenario scenario) => new(
        RunId, "Owned sample conversation", scenario.ToString(), Tone(scenario),
        scenario == AdjunctScenario.Partial ? "Partial observations" : null, "2026-10-04 12:00:00 UTC",
        "Source catalog / opaque/model:revision-1", scenario == AdjunctScenario.Failure ? "Source catalog / same display label" : null,
        scenario == AdjunctScenario.Unknown ? null : new("ChatCompletions", "Accepted", "High", "High", "opaque/model:revision-1", "opaque/model:revision-1", "None"),
        [new("Read scoped artifact", "Tool", "2026-10-04 12:00:00 UTC", "Safe sample evidence; credential=[REDACTED]", scenario.ToString(), GovernanceTone.Info)],
        new(new(1, scenario == AdjunctScenario.Completed ? 1 : 0, scenario == AdjunctScenario.Failure ? 1 : 0, 120, 40, 1),
            [new("Source catalog", "opaque/model:revision-1", "2026-10-04 12:00:00 UTC", "120 input / 40 output tokens")]));

    public static AgentExecutionLogState Log(AdjunctScenario scenario) => new("Owned sample conversation",
        "Persisted sample entries / UTC", "Source catalog", scenario.ToString(), Tone(scenario), "2s", EntryId,
        [new(EntryId, RunId, scenario.ToString(), Tone(scenario), "Scoped artifact",
            "2026-10-04 12:00:00 UTC", scenario == AdjunctScenario.Long
                ? string.Concat(Enumerable.Repeat("Long safe detail / ", 100))
                : "Read the exact granted file. credential=[REDACTED] / <encoded markup>")]);

    public static string Tone(AdjunctScenario scenario) => scenario switch {
        AdjunctScenario.Completed => "success",
        AdjunctScenario.Failure => "danger",
        AdjunctScenario.Unknown or AdjunctScenario.Partial => "warning",
        _ => "info"
    };
}
