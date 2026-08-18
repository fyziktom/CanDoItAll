using Bunit;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using WebMainLayout = CanDoItAll.Web.Components.Layout.MainLayout;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class MainLayoutCollaborationTests
{
    [Fact]
    public async Task Main_layout_shows_collaboration_unread_badge_when_items_exist()
    {
        await using var harness = await CreateHarnessAsync();
        var collaborationService = harness.Context.Services.GetRequiredService<CollaborationService>();
        var createResult = await collaborationService.CreateThreadAsync(
            new CollaborationThreadCreateRequest(
                "Unread collaboration item",
                CollaborationContextKind.Manual,
                ContextId: null,
                ProjectId: null,
                "Manual thread",
                ContextRoute: null,
                CollaborationInboxItemKind.Notification,
                "user:local-operator",
                "Local operator",
                CollaborationParticipantKind.User,
                "The collaboration badge should surface this unread item.",
                CollaborationMessageKind.Standard));

        Assert.True(createResult.IsSuccess, string.Join(" | ", createResult.Errors.Select(error => error.Message)));

        harness.Context.JSInterop.Setup<bool>("CanDoItAll.browserState.isDatabaseStartupPromptDismissed")
            .SetResult(true);

        var cut = harness.Context.Render<WebMainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Body</div>"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("shell-nav-badge-collaboration", cut.Markup);
            Assert.Contains(">1<", cut.Markup);
        });
    }

    private static async Task<ComponentTestHarness> CreateHarnessAsync()
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-layout-collaboration-tests");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("bootstrap");

        return await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile,
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
                ["Database:Provider"] = null,
                ["Database:ConnectionString"] = null
            }
        });
    }
}
