using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// The CRM / HR Home overview through the real Web host and PostgreSQL: seeded data, sensitive handling, a real
// route transition, the persisted opportunity deep link with both identifiers, and a constrained-width layout.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrHomeBrowserTests
{
    private const string OverflowProbe = """
        () => {
            const root = document.querySelector('[data-testid="crmhr-home"]');
            const items = Array.from(root.querySelectorAll('[data-testid="crmhr-home-directory-item"], [data-testid="crmhr-home-sensitive-item"], [data-testid="crmhr-home-opportunity-item"]'));
            return {
                documentOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
                rootOverflow: root.scrollWidth > root.clientWidth + 1,
                overflowingItems: items.filter(item => item.scrollWidth > item.clientWidth + 1).length,
                itemCount: items.length
            };
        }
        """;

    private readonly PlaywrightAppFixture fixture;

    public CrmHrHomeBrowserTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Home_shows_the_seeded_overview_handles_sensitive_records_and_opens_a_persisted_opportunity()
    {
        var artifactsDir = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "crm-hr-home");
        Directory.CreateDirectory(artifactsDir);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var seed = await SeedAsync(suffix);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        var page = await context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);
        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                consoleErrors.Add(message.Text);
            }
        };

        var response = await page.GotoAsync($"{fixture.BaseUrl}/crm-hr");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /crm-hr to return 2xx, got {(int)response.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        var home = page.GetByTestId("crmhr-home");
        await home.WaitForAsync();
        await Assertions.Expect(home).ToHaveAttributeAsync("data-phase", "ready", new() { Timeout = 30_000 });
        Assert.Equal("CRM / HR", await page.TitleAsync());

        // Totals come from the query owner; at least the seeded records are counted.
        var partiesText = await page.GetByTestId("crmhr-home-stat-parties").InnerTextAsync();
        var parties = int.Parse(System.Text.RegularExpressions.Regex.Match(partiesText, "\\d+").Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(parties >= 3, $"Expected at least the three seeded parties, got '{partiesText}'.");

        // Sensitive handling: the sensitive party is listed in the sensitive card without its operational summary, and
        // neither its ordinary notes nor its confidential note reaches the Home document at all.
        var sensitiveCard = page.GetByTestId("crmhr-home-sensitive-card");
        await Assertions.Expect(sensitiveCard).ToContainTextAsync(seed.SensitiveName);
        Assert.DoesNotContain(seed.SensitiveSummary, await sensitiveCard.InnerTextAsync(), StringComparison.Ordinal);
        var homeDocument = await page.ContentAsync();
        Assert.DoesNotContain(seed.OrdinaryNotes, homeDocument, StringComparison.Ordinal);
        Assert.DoesNotContain(seed.ConfidentialNote, homeDocument, StringComparison.Ordinal);

        // The open pipeline shows the seeded opportunity with account, owner, stage and amount.
        var opportunity = page.GetByTestId("crmhr-home-opportunity-item").Filter(new() { HasText = seed.OpportunityTitle });
        await opportunity.WaitForAsync();
        await Assertions.Expect(opportunity).ToContainTextAsync(seed.AccountName);
        await Assertions.Expect(opportunity).ToContainTextAsync(seed.OwnerName);
        await Assertions.Expect(opportunity).ToContainTextAsync("Proposal");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-home-1600.png") });

        // Constrained width: nothing overflows and every preview row stays inside its box.
        await page.SetViewportSizeAsync(1100, 900);
        await page.WaitForTimeoutAsync(300);
        var probe = await page.EvaluateAsync<System.Text.Json.JsonElement>(OverflowProbe);
        Assert.False(probe.GetProperty("documentOverflow").GetBoolean(), "The document overflows horizontally at 1100 px.");
        Assert.False(probe.GetProperty("rootOverflow").GetBoolean(), "The Home root overflows horizontally at 1100 px.");
        Assert.Equal(0, probe.GetProperty("overflowingItems").GetInt32());
        Assert.True(probe.GetProperty("itemCount").GetInt32() > 0);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-home-1100.png") });
        await page.SetViewportSizeAsync(1600, 1000);

        // A real route transition through a Home action. The host marks the surface interactive only once the circuit
        // can handle events, so one click is enough; Blazor navigates client-side, so the URL is observed directly.
        await Assertions.Expect(home).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });
        await page.GetByTestId("crmhr-home-route-workforce").ClickAsync();
        await WaitForUrlAsync(page, url => url.EndsWith("/crm-hr/workforce", StringComparison.Ordinal));
        response = await page.GotoAsync($"{fixture.BaseUrl}/crm-hr");
        Assert.True(response?.Ok);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await Assertions.Expect(home).ToHaveAttributeAsync("data-phase", "ready", new() { Timeout = 30_000 });
        await Assertions.Expect(home).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });

        // The persisted opportunity opens with both identifiers in the CRM route.
        await page.GetByTestId("crmhr-home-opportunity-item")
            .Filter(new() { HasText = seed.OpportunityTitle })
            .GetByTestId("crmhr-home-opportunity-open")
            .ClickAsync();
        await WaitForUrlAsync(page, url => url.Contains("/crm-hr/crm?", StringComparison.Ordinal));
        Assert.Contains($"accountId={seed.AccountId:D}", page.Url, StringComparison.Ordinal);
        Assert.Contains($"opportunityId={seed.OpportunityId:D}", page.Url, StringComparison.Ordinal);
        await page.WaitForSelectorAsync($"text={seed.OpportunityTitle}");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "03-opportunity-deep-link.png") });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
        Assert.Empty(consoleErrors);
    }

    private async Task<SeededHome> SeedAsync(string suffix)
    {
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            CreateActiveProfile(),
            "CanDoItAll.Tests.Playwright.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();

        var accountName = $"Home Account {suffix}";
        var ownerName = $"Home Owner {suffix}";
        var sensitiveName = $"Home Sensitive {suffix}";
        var sensitiveSummary = $"Sensitive summary {suffix} stays out of the sensitive card";
        var ordinaryNotes = $"Ordinary notes {suffix} stay in the directory record";
        var confidentialNote = $"Confidential note {suffix} never reaches Home";
        var opportunityTitle = $"Home renewal {suffix}";

        var accountId = await CreatePartyAsync(partyDirectoryService, accountName, PartyType.Organization, PartyRoleKind.Customer, $"account.{suffix}@example.test");
        var ownerId = await CreatePartyAsync(partyDirectoryService, ownerName, PartyType.Person, PartyRoleKind.AccountManager, $"owner.{suffix}@example.test");
        // Three distinguishable values in three different fields: the operational summary, the ordinary notes and an
        // actual confidential note, which the directory keeps in its own collection.
        var sensitiveId = await CreatePartyAsync(
            partyDirectoryService,
            sensitiveName,
            PartyType.Person,
            PartyRoleKind.Employee,
            $"sensitive.{suffix}@example.test",
            isSensitive: true,
            summary: sensitiveSummary,
            notes: ordinaryNotes,
            confidentialNote: confidentialNote);
        var persisted = await partyDirectoryService.GetPartyAsync(sensitiveId);
        Assert.NotNull(persisted);
        Assert.Equal(sensitiveSummary, persisted!.Summary);
        Assert.Equal(ordinaryNotes, persisted.Notes);
        Assert.Equal(confidentialNote, Assert.Single(persisted.ConfidentialNotes).NoteText);

        var saved = await crmService.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = opportunityTitle,
            Stage = OpportunityStage.Proposal,
            OpportunitySource = OpportunitySource.Renewal,
            OwnerPartyId = ownerId,
            CurrencyCode = "USD",
            Amount = 45000m,
            ProbabilityPercent = 65,
            ExpectedCloseOn = new DateOnly(2026, 6, 20),
            Summary = "Renewal of the advisory retainer.",
            LastChangedBy = "playwright-tests"
        });
        Assert.True(saved.IsSuccess, string.Join(" ", saved.Errors.Select(error => error.Message)));

        return new SeededHome(accountId, saved.Value, accountName, ownerName, sensitiveName, sensitiveSummary, ordinaryNotes, confidentialNote, opportunityTitle);
    }

    private TestDatabaseProfile CreateActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString))
        {
            throw new InvalidOperationException("Playwright fixture did not expose a database connection string.");
        }

        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        var workspaceRoot = fixture.StorageWorkspaceRoot;
        var profileRoot = Directory.GetParent(workspaceRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve profile root from '{workspaceRoot}'.");
        var environmentRoot = Path.GetFullPath(Path.Combine(profileRoot, "..", ".."));

        return new TestDatabaseProfile(
            "playwright-home-seed",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyRoleKind roleKind,
        string email,
        bool isSensitive = false,
        string? summary = null,
        string? notes = null,
        string? confidentialNote = null)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = summary ?? $"{displayName} summary",
            Notes = notes ?? string.Empty,
            IsSensitive = isSensitive,
            LastChangedBy = "playwright-tests",
            ConfidentialNotes = confidentialNote is null
                ? []
                :
                [
                    new PartyConfidentialNoteEditorModel
                    {
                        Category = PartyConfidentialNoteCategories.HumanResources,
                        NoteText = confidentialNote,
                        CreatedBy = "playwright-tests"
                    }
                ],
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    // Blazor navigates client-side without a document load event, so the URL is observed until it matches.
    private static async Task WaitForUrlAsync(IPage page, Func<string, bool> predicate, int timeoutMs = 30_000)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate(page.Url))
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"The page did not reach the expected URL; current URL: {page.Url}");
    }

    private sealed record SeededHome(
        Guid AccountId,
        Guid OpportunityId,
        string AccountName,
        string OwnerName,
        string SensitiveName,
        string SensitiveSummary,
        string OrdinaryNotes,
        string ConfidentialNote,
        string OpportunityTitle);
}
