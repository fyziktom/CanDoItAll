using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages;

namespace CanDoItAll.Tests.Components;

internal static class AgentsHomePageTestExtensions {
    public static void WaitForDashboardLoaded(this IRenderedComponent<AgentsHomePage> page) {
        page.WaitForAssertion(() => {
            var statistics = page.FindComponent<PageHeader>().FindComponents<CompactStat>();
            Assert.NotEmpty(statistics);
            Assert.All(statistics, statistic => Assert.NotEqual("...", statistic.Instance.Value));
        });
    }
}
