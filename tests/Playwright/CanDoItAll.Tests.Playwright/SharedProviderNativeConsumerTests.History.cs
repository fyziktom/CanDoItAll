using System.Diagnostics;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    private static async Task AssertCanonicalHistoryAsync(SharedProviderConsumerFixture fixture, ProviderProfile profile,
        string model, HistorySourceKind kind, Guid owner, string expectedContent, string name) {
        var page = fixture.Page;
        await OpenHistoryAsync(page, fixture, profile, model, kind, false);
        var global = await FindCanonicalHistoryAsync(page, profile, model, kind, owner);
        var requiresOperator = kind is HistorySourceKind.AgentConversation or HistorySourceKind.Workflow;
        if (requiresOperator) {
            await OwnerButton(page, kind, owner).ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-detail-error")).ToHaveTextAsync(HistoryPublicErrors.Message(HistoryFailure.Denied));
            await Assertions.Expect(page.GetByTestId("history-content-text")).ToHaveCountAsync(0);
            await page.GetByTestId("history-detail-close").ClickAsync();
            page = await fixture.OpenLocalOperatorPageAsync();
        } else {
            await page.GetByTestId("history-detail-close").ClickAsync();
        }
        try {
            await OpenHistoryAsync(page, fixture, profile, model, kind, true);
            await page.GetByLabel("Request ID", new() { Exact = true }).FillAsync(global.RequestId.ToString("D"));
            await page.GetByLabel("Attempt ID", new() { Exact = true }).FillAsync(global.AttemptId.ToString("D"));
            var single = await FindCanonicalHistoryAsync(page, profile, model, kind, owner);
            Assert.Equal(global, single);
            await Assertions.Expect(page.GetByTestId("history-details")).ToHaveCountAsync(1);
            await OwnerButton(page, kind, owner).ClickAsync();
            var content = page.GetByTestId("history-content-dialog");
            await Assertions.Expect(content.GetByTestId("history-content-state")).ToHaveTextAsync("Content: Canonical");
            var values = await content.GetByTestId("history-content-text")
                .EvaluateAllAsync<string[]>("elements => elements.map(element => element.value)");
            Assert.Contains(values, value => value.Contains(expectedContent, StringComparison.Ordinal));
            await Assertions.Expect(content.GetByTestId("history-content-close")).ToBeInViewportAsync(new() { Ratio = 1 });
            await page.ScreenshotAsync(new() { Path = Path.Combine(fixture.Settings.Evidence, "history-canonical-" + name + ".png") });
            await fixture.EvidenceAsync("history-canonical-" + name, new {
                Source = kind, Owner = owner, ProviderId = profile.Id, Model = model, Global = global, SingleProvider = single,
                ManagedOwnerDenied = requiresOperator, AuthorizedContent = true,
                ContentSha256 = values.Select(SharedProviderConsumerFixture.Hash).ToArray()
            });
            await content.GetByTestId("history-content-close").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-content-text")).ToHaveCountAsync(0);
            await page.GetByTestId("history-detail-close").ClickAsync();
        } finally {
            if (requiresOperator) {
                await page.Context.CloseAsync();
            }
        }
    }

    private static async Task OpenHistoryAsync(IPage page, SharedProviderConsumerFixture fixture, ProviderProfile profile,
        string model, HistorySourceKind kind, bool singleProvider) {
        if (singleProvider) {
            await SharedProviderMetadataUiChecks.OpenProviderAsync(page, fixture.Settings.Clients[0], profile.Name);
            await page.GetByTestId("provider-editor-tab-history").ClickAsync();
        } else {
            await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, fixture.Settings.Clients[0] + "/agents?tab=request-history");
        }
        await page.GetByText("History not requested", new() { Exact = true }).WaitForAsync();
        await Assertions.Expect(page.GetByTestId("history-results")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("history-content-text")).ToHaveCountAsync(0);
        if (!singleProvider) {
            await page.GetByTestId("history-provider").FillAsync(profile.Id.ToString("D"));
        }
        await page.GetByTestId("history-model").FillAsync(model);
        await page.GetByTestId("history-more-filters").ClickAsync();
        var workload = kind switch {
            HistorySourceKind.SimpleChat => HistoryWorkload.SimpleChat,
            HistorySourceKind.AgentConversation => HistoryWorkload.Agent,
            HistorySourceKind.Workflow => HistoryWorkload.Workflow,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        await page.GetByLabel("Workload", new() { Exact = true }).SelectOptionAsync(workload.ToString());
        await page.GetByTestId("history-page-size").FillAsync("200");
        await page.GetByTestId("history-page-size").PressAsync("Tab");
        await Assertions.Expect(page.GetByTestId("history-results")).ToHaveCountAsync(0);
    }

    private static async Task<NativeHistoryIdentity> FindCanonicalHistoryAsync(IPage page, ProviderProfile profile,
        string model, HistorySourceKind kind, Guid owner) {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(60)) {
            if (await page.GetByTestId("history-results").CountAsync() > 0) {
                await page.GetByTestId("history-clear").ClickAsync();
                await page.GetByText("History not requested", new() { Exact = true }).WaitForAsync();
            }
            await page.GetByTestId("history-search").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-search")).ToBeEnabledAsync();
            await page.GetByTestId("history-results").WaitForAsync();
            var buttons = page.GetByTestId("history-details");
            var count = await buttons.CountAsync();
            Assert.InRange(count, 0, 200);
            for (var index = 0; index < count; index++) {
                await buttons.Nth(index).ClickAsync();
                var detail = page.GetByTestId("history-detail-dialog");
                await detail.GetByText("Entry / provider", new() { Exact = true }).WaitForAsync();
                await Assertions.Expect(page.GetByTestId("history-content-text")).ToHaveCountAsync(0);
                if ((await Field(detail, "Execution").InnerTextAsync()).Contains(nameof(HistoryGranularity.ProviderCallAttempt), StringComparison.Ordinal)
                    && await OwnerButton(page, kind, owner).CountAsync() > 0) {
                    var identityText = await Field(detail, "Entry / provider").InnerTextAsync();
                    Assert.Contains(profile.Id.ToString("D"), identityText, StringComparison.Ordinal);
                    Assert.Contains(model, identityText, StringComparison.Ordinal);
                    var ids = Regex.Matches(await Field(detail, "Request / attempt").InnerTextAsync(), "[0-9a-fA-F-]{36}");
                    Assert.Equal(2, ids.Count);
                    var reference = await OwnerLabel(page, kind, owner).InnerTextAsync();
                    return new(Guid.Parse(identityText[..36]), Guid.Parse(ids[0].Value), Guid.Parse(ids[1].Value), reference);
                }
                await detail.GetByTestId("history-detail-close").ClickAsync();
                await detail.WaitForAsync(new() { State = WaitForSelectorState.Detached });
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"History did not project canonical {kind} owner {owner:D} within 60 seconds.");
    }

    private static ILocator Field(ILocator detail, string label) => detail.GetByText(label, new() { Exact = true })
        .Locator("xpath=following-sibling::*[1]");

    private static ILocator OwnerLabel(IPage page, HistorySourceKind kind, Guid owner) => page.GetByTestId("history-detail-dialog")
        .GetByText(new Regex($"^{kind} · {owner:N} · ")).First;

    private static ILocator OwnerButton(IPage page, HistorySourceKind kind, Guid owner) => OwnerLabel(page, kind, owner)
        .Locator("..").GetByTestId("history-owner-content");

    private sealed record NativeHistoryIdentity(Guid EntryId, Guid RequestId, Guid AttemptId, string CanonicalReference);
}
