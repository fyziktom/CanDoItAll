using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// The CRM account summary and the activity history through the real Web host and PostgreSQL: a seeded account with
// interactions in the CRM workspace, the same history in the Directory host, a constrained-width layout, and the
// conversion mutation clicked exactly once on an interactive surface.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrAccountActivityBrowserTests
{
    private const string OverflowProbe = """
        () => {
            const roots = ['crmhr-account-summary', 'crmhr-account-activity']
                .map(id => document.querySelector(`[data-testid="${id}"]`))
                .filter(root => root !== null);
            const items = Array.from(document.querySelectorAll('[data-testid="crmhr-account-activity-item"]'));
            return {
                documentOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
                rootCount: roots.length,
                overflowingRoots: roots.filter(root => root.scrollWidth > root.clientWidth + 1).length,
                overflowingItems: items.filter(item => item.scrollWidth > item.clientWidth + 1).length,
                itemCount: items.length
            };
        }
        """;

    private readonly PlaywrightAppFixture fixture;

    public CrmHrAccountActivityBrowserTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Account_summary_and_activity_render_for_a_seeded_account_and_the_directory_host_shows_the_same_history()
    {
        var artifactsDir = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "crm-hr-account-activity");
        Directory.CreateDirectory(artifactsDir);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var seed = await SeedAsync(suffix, interactions: true);

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

        var response = await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/crm?accountId={seed.AccountId:D}");
        Assert.True(response?.Ok, $"Expected the CRM route to return 2xx, got {(int?)response?.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("crmhr-crm-record-dialog").WaitForAsync(new() { Timeout = 30_000 });

        // The account summary: identity, labels, contact copy and counts from the seeded record.
        var summary = page.GetByTestId("crmhr-account-summary");
        await Assertions.Expect(summary).ToHaveAttributeAsync("data-account-id", seed.AccountId.ToString("D"));
        await Assertions.Expect(page.GetByTestId("crmhr-account-summary-name")).ToHaveTextAsync(seed.AccountName);
        await Assertions.Expect(page.GetByTestId("crmhr-account-summary-stage")).ToHaveTextAsync(CrmAccountRelationshipStage.Prospect.ToString());
        // The lifecycle label is whatever the query owner reports for the account after the profile save.
        await Assertions.Expect(page.GetByTestId("crmhr-account-summary-lifecycle")).ToHaveTextAsync(seed.LifecycleLabel);
        await Assertions.Expect(summary).ToContainTextAsync(seed.AccountEmail);
        await Assertions.Expect(summary).ToContainTextAsync("Not set");
        await Assertions.Expect(page.GetByTestId("crmhr-account-convert-active-button")).ToBeVisibleAsync();

        // The account activity host: the seeded interactions with their overdue state and the whole-history counts.
        await page.GetByTestId("crmhr-crm-tab-interactions").ClickAsync();
        var activity = page.GetByTestId("crmhr-account-activity");
        await activity.WaitForAsync();
        await Assertions.Expect(activity).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 30_000 });
        // Logging an interaction also writes an audit row that quotes the subject, so the rows are matched by kind.
        var overdueItem = InteractionRow(page, "crmhr-account-activity-item", seed.OverdueSubject);
        await Assertions.Expect(overdueItem).ToHaveAttributeAsync("data-overdue", "true");
        await Assertions.Expect(overdueItem).ToContainTextAsync("Overdue");
        await Assertions.Expect(overdueItem).ToContainTextAsync(seed.ContactName);
        var settledItem = InteractionRow(page, "crmhr-account-activity-item", seed.SettledSubject);
        await Assertions.Expect(settledItem).ToHaveAttributeAsync("data-overdue", "false");
        await Assertions.Expect(page.GetByTestId("crmhr-account-activity-overdue-total")).ToHaveTextAsync("1 overdue");
        await Assertions.Expect(page.GetByTestId("crmhr-account-activity-totals")).ToContainTextAsync("1 next actions");
        await Assertions.Expect(activity).ToHaveAttributeAsync("data-accepted", "true");
        // The adjacent follow-up pressure card renders the same accepted history, not a placeholder zero.
        await Assertions.Expect(page.GetByTestId("crmhr-account-action-count")).ToHaveTextAsync("1 open follow-ups");
        await Assertions.Expect(page.GetByTestId("crmhr-account-overdue-count")).ToHaveTextAsync("1 overdue");
        await Assertions.Expect(page.GetByTestId("crmhr-account-activity-counts-unavailable")).ToHaveCountAsync(0);
        Assert.True(await page.GetByTestId("crmhr-account-activity-item").CountAsync() >= 2);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-account-activity-1600.png") });

        // Constrained width: neither the summary nor the timeline rows overflow their boxes.
        await page.SetViewportSizeAsync(1100, 900);
        await page.WaitForTimeoutAsync(300);
        var probe = await page.EvaluateAsync<System.Text.Json.JsonElement>(OverflowProbe);
        Assert.False(probe.GetProperty("documentOverflow").GetBoolean(), "The document overflows horizontally at 1100 px.");
        Assert.Equal(2, probe.GetProperty("rootCount").GetInt32());
        Assert.Equal(0, probe.GetProperty("overflowingRoots").GetInt32());
        Assert.Equal(0, probe.GetProperty("overflowingItems").GetInt32());
        Assert.True(probe.GetProperty("itemCount").GetInt32() >= 2);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-account-activity-1100.png") });
        await page.SetViewportSizeAsync(1600, 1000);

        // A second timeline host: the Directory page renders the same shared history under its own wording.
        response = await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/directory?partyId={seed.AccountId:D}");
        Assert.True(response?.Ok);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("crmhr-party-save-button").WaitForAsync();
        await page.GetByTestId("crmhr-directory-tab-activity").ClickAsync();
        var directoryActivity = page.GetByTestId("crmhr-directory-activity");
        await directoryActivity.WaitForAsync();
        await Assertions.Expect(directoryActivity).ToContainTextAsync("Party history");
        await Assertions.Expect(InteractionRow(page, "crmhr-directory-activity-item", seed.OverdueSubject)).ToHaveAttributeAsync("data-overdue", "true");
        await Assertions.Expect(page.GetByTestId("crmhr-directory-activity-overdue-total")).ToHaveTextAsync("1 overdue");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "03-directory-activity-1600.png") });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
        Assert.Empty(consoleErrors);
    }

    [Fact]
    public async Task Converting_a_prospect_saves_the_profile_from_one_click_on_the_interactive_summary()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var seed = await SeedAsync(suffix, interactions: false);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        var page = await context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);

        var response = await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/crm?accountId={seed.AccountId:D}");
        Assert.True(response?.Ok);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("crmhr-crm-record-dialog").WaitForAsync(new() { Timeout = 30_000 });
        var summary = page.GetByTestId("crmhr-account-summary");
        await Assertions.Expect(summary).ToHaveAttributeAsync("data-account-id", seed.AccountId.ToString("D"));
        await Assertions.Expect(page.GetByTestId("crmhr-account-summary-stage")).ToHaveTextAsync(CrmAccountRelationshipStage.Prospect.ToString());

        // The page flips the summary to interactive only once the circuit can handle events; one click is enough.
        await Assertions.Expect(summary).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });
        await page.GetByTestId("crmhr-account-convert-active-button").ClickAsync();

        await page.WaitForSelectorAsync("text=CRM account profile saved.", new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId("crmhr-account-summary-stage")).ToHaveTextAsync(CrmAccountRelationshipStage.ActiveCustomer.ToString());
        await Assertions.Expect(page.GetByTestId("crmhr-account-convert-active-button")).ToHaveCountAsync(0);

        // The mutation result is asserted on the persisted profile, through the application services.
        var persisted = await ReadProfileAsync(seed.AccountId);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, persisted.RelationshipStage);
        Assert.Equal(seed.AccountId, persisted.AccountPartyId);

        // An account without interactions: the accepted history is empty, and the follow-up counts are accepted
        // zeros after the read, not the placeholder shown before anything was accepted.
        await page.GetByTestId("crmhr-crm-tab-interactions").ClickAsync();
        var activity = page.GetByTestId("crmhr-account-activity");
        await Assertions.Expect(activity).ToHaveAttributeAsync("data-accepted", "true", new() { Timeout = 30_000 });
        await Assertions.Expect(activity).ToHaveAttributeAsync("data-loading", "false");
        await Assertions.Expect(page.GetByTestId("crmhr-account-activity-totals")).ToContainTextAsync("0 next actions");
        await Assertions.Expect(page.GetByTestId("crmhr-account-action-count")).ToHaveTextAsync("0 open follow-ups");
        await Assertions.Expect(page.GetByTestId("crmhr-account-overdue-count")).ToHaveTextAsync("0 overdue");
        await Assertions.Expect(page.GetByTestId("crmhr-account-activity-counts-unavailable")).ToHaveCountAsync(0);

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
    }

    private static ILocator InteractionRow(IPage page, string itemTestId, string subject)
        => page.Locator($"[data-testid='{itemTestId}'][data-kind='Interaction']").Filter(new() { HasText = subject });

    private async Task<SeededAccount> SeedAsync(string suffix, bool interactions)
    {
        await using var serviceProvider = await BuildSeedProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();

        var accountName = $"Activity Account {suffix}";
        var accountEmail = $"account.{suffix}@example.test";
        var contactName = $"Activity Contact {suffix}";
        var overdueSubject = $"Steering call {suffix}";
        var settledSubject = $"Reference call {suffix}";

        var accountId = await CreatePartyAsync(partyDirectoryService, accountName, PartyType.Organization, PartyRoleKind.Customer, accountEmail);
        var contactId = await CreatePartyAsync(partyDirectoryService, contactName, PartyType.Person, PartyRoleKind.CustomerContact, $"contact.{suffix}@example.test");

        var profile = await crmService.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.Prospect,
            CommercialNotes = "Synthetic prospect for the account summary browser lane.",
            LastChangedBy = "playwright-tests"
        });
        Assert.True(profile.IsSuccess, string.Join(" ", profile.Errors.Select(error => error.Message)));
        var workspace = await crmService.GetAccountWorkspaceAsync(accountId);
        Assert.NotNull(workspace);
        Assert.Equal(CrmAccountRelationshipStage.Prospect, workspace!.Profile.RelationshipStage);

        if (interactions)
        {
            var overdue = await crmService.AddInteractionAsync(
                accountId,
                new CrmInteractionEditorModel
                {
                    InteractionType = InteractionType.Meeting,
                    Subject = overdueSubject,
                    OccurredOn = new DateOnly(2026, 3, 2),
                    Summary = "Confirmed the renewal scope and open risks.",
                    NextActionText = "Send the revised statement of work",
                    NextActionOwnerPartyId = contactId,
                    NextActionDueOn = new DateOnly(2026, 3, 5),
                    ParticipantPartyIds = [contactId]
                },
                "playwright-tests");
            Assert.True(overdue.IsSuccess, string.Join(" ", overdue.Errors.Select(error => error.Message)));

            var settled = await crmService.AddInteractionAsync(
                accountId,
                new CrmInteractionEditorModel
                {
                    InteractionType = InteractionType.Call,
                    Subject = settledSubject,
                    OccurredOn = new DateOnly(2026, 3, 1),
                    Summary = "Reference for the analytics onboarding."
                },
                "playwright-tests");
            Assert.True(settled.IsSuccess, string.Join(" ", settled.Errors.Select(error => error.Message)));
        }

        return new SeededAccount(accountId, accountName, accountEmail, workspace.LifecycleStatus.ToString(), contactName, overdueSubject, settledSubject);
    }

    private async Task<CrmAccountProfileEditorModel> ReadProfileAsync(Guid accountId)
    {
        await using var serviceProvider = await BuildSeedProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var workspace = await crmService.GetAccountWorkspaceAsync(accountId);
        Assert.NotNull(workspace);
        return workspace!.Profile;
    }

    private Task<ServiceProvider> BuildSeedProviderAsync()
        => TestApplicationBootstrap.BuildServiceProviderAsync(
            CreateActiveProfile(),
            "CanDoItAll.Tests.Playwright.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });

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
            "playwright-account-activity-seed",
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
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "playwright-tests",
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

    private sealed record SeededAccount(
        Guid AccountId,
        string AccountName,
        string AccountEmail,
        string LifecycleLabel,
        string ContactName,
        string OverdueSubject,
        string SettledSubject);
}
