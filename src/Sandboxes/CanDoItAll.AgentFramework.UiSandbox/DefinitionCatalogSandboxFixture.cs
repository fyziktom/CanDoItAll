using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum DefinitionCatalogScenario { Denied, Loading, Empty, ReadOnly, Manageable, Filtered, Paged, Adversarial }

public sealed class DefinitionCatalogSandboxFixture {
    public static readonly Guid DefinitionA = Guid.Parse("31000000-0000-0000-0000-000000000001");
    public static readonly Guid DefinitionB = Guid.Parse("31000000-0000-0000-0000-000000000002");
    public static DefinitionCatalogFilterLimits Limits { get; } = new(200, 6, 100);
    public DefinitionCatalogPresentation Presentation { get; private set; } = Create(DefinitionCatalogScenario.Manageable);
    public string IntentLog { get; private set; } = "No intent";
    public DefinitionCatalogScenario Scenario { get; private set; } = DefinitionCatalogScenario.Manageable;
    public void SetScenario(DefinitionCatalogScenario scenario) {
        Scenario = scenario;
        Presentation = Create(scenario);
    }
    public void Apply(DefinitionCatalogIntent intent) {
        IntentLog = intent.ToString();
        var filters = Presentation.Filters;
        Presentation = intent switch {
            DefinitionCatalogIntent.SearchChanged search => Presentation with { Filters = filters with { Search = search.Value } },
            DefinitionCatalogIntent.TagsChanged tags => Presentation with { Filters = filters with { Tags = tags.Value } },
            DefinitionCatalogIntent.StatusChanged status => Presentation with { Filters = filters with { Status = status.Value } },
            DefinitionCatalogIntent.ResetFilters => Presentation with { Filters = DefinitionCatalogFilters.Empty },
            DefinitionCatalogIntent.LoadMore => Presentation with { Cards = Presentation.Cards.Add(Card(DefinitionB)), HasMore = false },
            _ => Presentation
        };
    }
    public static DefinitionCatalogCard Card(Guid? id = null, bool adversarial = false) => new(id ?? DefinitionA,
        adversarial ? "<script id='definitions-injected'>unsafe()</script> " + new string('界', 240) : "Research assistant",
        adversarial ? new string('W', 500) : "Summarizes research with clear source notes.", "", LlmChatDefinitionStatusFilter.Active,
        3, new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero), ["research", "summaries"]);
    public static DefinitionCatalogPresentation Create(DefinitionCatalogScenario scenario) {
        var presentation = new DefinitionCatalogPresentation(true, true, DefinitionCatalogPhase.Ready, [Card()], DefinitionCatalogFilters.Empty);
        return scenario switch {
            DefinitionCatalogScenario.Denied => presentation with { CanRead = false, CanManage = false, Cards = [] },
            DefinitionCatalogScenario.Loading => presentation with { Phase = DefinitionCatalogPhase.Loading, Cards = [] },
            DefinitionCatalogScenario.Empty => presentation with { Cards = [] },
            DefinitionCatalogScenario.ReadOnly => presentation with { CanManage = false },
            DefinitionCatalogScenario.Manageable => presentation,
            DefinitionCatalogScenario.Filtered => presentation with { Filters = new("research", ["research"], LlmChatDefinitionStatusFilter.Active) },
            DefinitionCatalogScenario.Paged => presentation with { HasMore = true },
            DefinitionCatalogScenario.Adversarial => presentation with { Cards = [Card(adversarial: true)], Phase = DefinitionCatalogPhase.Failed,
                Failure = DefinitionCatalogFailure.Unavailable },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown definition catalog scenario.")
        };
    }
}
