using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[CollectionDefinition(Name)]
public sealed class WorkspaceSettingsBrowserCollection : ICollectionFixture<PlaywrightAppFixture> {
    public const string Name = "Workspace Settings owned browser host";
}

[Collection(WorkspaceSettingsBrowserCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class WorkspaceSettingsBrowserTests(PlaywrightAppFixture fixture) {
    private static readonly string Artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-settings-core-ui");

    [Fact]
    public async Task Production_settings_use_real_owners_and_keep_deferred_hosts_reachable() {
        Directory.CreateDirectory(Artifacts);
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
            "Workspace.Settings.Browser", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<WorkspaceService>();
        var secrets = services.GetRequiredService<SecretService>();
        var preferences = services.GetRequiredService<IFileApplicationPreferenceService>();
        var executable = Path.Combine(fixture.OwnedDatabaseProfile.WorkspaceRootPath, "never-execute-workspace-fixture.exe");
        await File.WriteAllTextAsync(executable, "Harmless fixture; never execute.");
        await using var context = await fixture.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, Permissions = ["clipboard-read", "clipboard-write"]
        });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        var secretName = "Workspace browser secret " + Guid.NewGuid().ToString("N")[..8];
        var value = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(20));
        try {
            await page.GotoAsync(fixture.BaseUrl + "/settings");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.GetByTestId("defaults-name").FillAsync(" Browser saved workspace ");
            await page.GetByTestId("defaults-currency").FillAsync("eur");
            await page.GetByTestId("defaults-culture").FillAsync("de-DE");
            await page.GetByTestId("defaults-notes").FillAsync("Owned production proof");
            await page.GetByTestId("defaults-save").EvaluateAsync("element => element.click()");
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            var saved = await workspace.GetSettingsAsync();
            Assert.Equal("Browser saved workspace", saved.WorkspaceName);
            Assert.Equal("EUR", saved.CurrencyCode);
            Assert.Equal("de-DE", saved.CurrencyCultureName);
            Assert.Equal("Owned production proof", saved.Notes);
            var formatter = services.GetRequiredService<ICurrencyFormatter>();
            Assert.Equal("EUR", formatter.CurrencyCode);
            Assert.Contains("1.234,50", formatter.Format(1234.5m), StringComparison.Ordinal);
            Assert.Contains("€", formatter.Format(1234.5m), StringComparison.Ordinal);
            await Shot(page, "production-defaults");

            await Tab(page, "Secrets");
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=secrets$"));
            await page.GetByTestId("secret-name").FillAsync(secretName);
            await page.GetByTestId("settings-secret-value").FillAsync(value);
            await page.GetByTestId("secret-metadatajson").FillAsync("{\"synthetic\":true}");
            await page.GetByTestId("secret-save").EvaluateAsync("element => element.click()");
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            var metadata = Assert.Single(await secrets.ListForPickerAsync(), item => item.Name == secretName);
            Assert.True((await secrets.GetAsync(metadata.Id))!.SecretValue == value);
            await page.Locator(".cda-selection-list-item__button").Filter(new() { HasText = secretName }).ClickAsync();
            var secretLocation = new Regex($"/settings\\?tab=secrets&secretId={metadata.Id:D}$");
            await Assertions.Expect(page).ToHaveURLAsync(secretLocation);
            await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "password");
            var copyValue = page.Locator(".cda-secret-field").GetByRole(AriaRole.Button, new() { Name = "Copy value", Exact = true });
            await copyValue.ClickAsync();
            await Assertions.Expect(copyValue).ToHaveAttributeAsync("data-copy-state", "copied");
            Assert.True(await page.EvaluateAsync<string>("navigator.clipboard.readText()") == value);
            await page.Locator(".cda-secret-field__reveal").ClickAsync();
            await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "text");
            await Tab(page, "Files");
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=files$"));
            await Tab(page, "Secrets");
            await Assertions.Expect(page).ToHaveURLAsync(secretLocation);
            await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "password");
            await Shot(page, "production-secret-masked");
            await page.GoBackAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=files$"));
            await Assertions.Expect(page.GetByTestId("file-application-extension")).ToBeVisibleAsync();
            await page.GoForwardAsync();
            await Assertions.Expect(page).ToHaveURLAsync(secretLocation);
            await Assertions.Expect(page.GetByTestId("secret-name")).ToHaveValueAsync(secretName);
            await page.GetByTestId("secret-delete").ClickAsync();
            await Assertions.Expect(page.GetByTestId("settings-operation")).ToHaveCountAsync(2);
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=secrets$"));
            Assert.Null(await secrets.GetAsync(metadata.Id));

            await Tab(page, "Files");
            await page.GetByTestId("file-application-extension").FillAsync(" .OWNED ");
            await page.GetByTestId("file-application-executable").FillAsync(executable);
            await page.GetByTestId("file-application-save").EvaluateAsync("element => element.click()");
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            var preference = Assert.Single(await preferences.ListAsync(), item => item.Extension.Value == ".owned");
            Assert.False(preference.RequiresRebind);
            Assert.Equal(executable, preference.ExecutablePath);
            await Assertions.Expect(page.GetByText("All files use the system default", new() { Exact = true })).ToHaveCountAsync(0);
            await Shot(page, "production-files");
            await page.GetByTestId("file-application-delete").ClickAsync();
            await Assertions.Expect(page.GetByTestId("settings-operation")).ToHaveCountAsync(2);
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            Assert.DoesNotContain(await preferences.ListAsync(), item => item.Extension.Value == ".owned");

            await Tab(page, "Provider history");
            await Assertions.Expect(page.GetByText("Policy not requested", new() { Exact = true })).ToBeVisibleAsync();
            await page.GetByTestId("history-policy-load").ClickAsync();
            await page.GetByTestId("history-policy-metadata-days").FillAsync("20");
            await page.GetByTestId("history-policy-apply").EvaluateAsync("element => element.click()");
            await Assertions.Expect(page.GetByTestId("history-policy-success")).ToContainTextAsync("Existing expiry dates are unchanged");
            await page.GetByTestId("history-policy-preview").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-confirmation")).ToBeVisibleAsync();
            Assert.True(await page.GetByTestId("history-policy-confirmation").EvaluateAsync<bool>("e => { const r=e.getBoundingClientRect(); return r.top >= 0 && r.bottom <= innerHeight; }"));
            await Shot(page, "production-history-preview");
            await page.GetByTestId("history-policy-confirm").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-success")).ToContainTextAsync("shorter retention applied");
            await page.ReloadAsync();
            await ShellReady(page);
            await page.GetByTestId("history-policy-load").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-metadata-days")).ToHaveValueAsync("20");
            await Shot(page, "production-history");

            await Tab(page, "Data Sources");
            await Assertions.Expect(page.GetByText("Workspace defaults", new() { Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("database-data-sources-summary")).ToBeVisibleAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=data-sources(&profileId=[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})?$"));
            await Shot(page, "production-data-sources");
            await Tab(page, "Storage");
            await Assertions.Expect(page.GetByText("Storage catalog", new() { Exact = true }).First).ToBeVisibleAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=storage$"));
            await Shot(page, "production-storage");
            await Tab(page, "API Access");
            await Assertions.Expect(page.GetByText("Bearer tokens are not required.", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=api-access$"));
            await Shot(page, "production-api-access");
            await page.GoBackAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=storage$"));
            await Assertions.Expect(page.GetByText("Storage catalog", new() { Exact = true }).First).ToBeVisibleAsync();
            await page.GoForwardAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings\\?tab=api-access$"));
            await Assertions.Expect(page.GetByText("Bearer tokens are not required.", new() { Exact = true })).ToBeVisibleAsync();
            await page.GotoAsync(fixture.BaseUrl + "/settings?tab=invalid");
            await ShellReady(page);
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings$"));
            await Assertions.Expect(page.GetByTestId("defaults-name")).ToHaveValueAsync("Browser saved workspace");
            await page.GotoAsync(fixture.BaseUrl + "/settings?tab=files");
            await ShellReady(page);
            await Assertions.Expect(page.GetByTestId("file-application-extension")).ToBeVisibleAsync();
            await page.GoBackAsync();
            await ShellReady(page);
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings$"));
            await Assertions.Expect(page.GetByTestId("defaults-name")).ToHaveValueAsync("Browser saved workspace");
            await page.GoForwardAsync();
            await ShellReady(page);
            await Assertions.Expect(page.GetByTestId("file-application-extension")).ToBeVisibleAsync();
            await Tab(page, "Providers");
            // The providers panel publishes the provider it selects by default with Replace.
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/agents\\?tab=providers(?:&providerId=[0-9a-fA-F-]{36})?$"));
            Assert.Empty(errors);
        } finally {
            File.Delete(executable);
        }
    }

    [Fact]
    public async Task Two_native_editors_preserve_the_stale_draft_and_require_explicit_current_based_reconciliation() {
        Directory.CreateDirectory(Artifacts);
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
            "Workspace.Settings.Conflict.Browser", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
        await using var scope = provider.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        await using var firstContext = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await using var secondContext = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var first = await firstContext.NewPageAsync();
        var second = await secondContext.NewPageAsync();
        var firstErrors = Observe(first);
        var secondErrors = Observe(second);
        await first.GotoAsync(fixture.BaseUrl + "/settings");
        await ShellReady(first);
        await Assertions.Expect(first.GetByTestId("defaults-save")).ToBeEnabledAsync();
        await second.GotoAsync(fixture.BaseUrl + "/settings");
        await ShellReady(second);
        await Assertions.Expect(second.GetByTestId("defaults-save")).ToBeEnabledAsync();
        var original = await workspace.ReadSettingsSnapshotAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var firstName = "First workspace " + suffix;
        var localName = "Retained local workspace " + suffix;
        var localNotes = "First local line\nSecond local line " + suffix;
        await first.GetByTestId("defaults-name").FillAsync(firstName);
        await second.GetByTestId("defaults-name").FillAsync(localName);
        await second.GetByTestId("defaults-notes").FillAsync(localNotes);
        await first.GetByTestId("defaults-save").ClickAsync();
        await Assertions.Expect(first.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
        var accepted = await workspace.ReadSettingsSnapshotAsync();
        Assert.Equal(firstName, accepted.Values.WorkspaceName);
        Assert.NotEqual(original.Expectation.Head!.Revision, accepted.Expectation.Head!.Revision);

        await second.GetByTestId("defaults-save").ClickAsync();
        await Assertions.Expect(second.GetByTestId("settings-operation").First).ToContainTextAsync("Refused");
        await Assertions.Expect(second.GetByTestId("defaults-save")).ToBeDisabledAsync();
        await Assertions.Expect(second.GetByTestId("defaults-name")).ToHaveValueAsync(localName);
        await Assertions.Expect(second.GetByTestId("defaults-notes")).ToHaveValueAsync(localNotes);
        Assert.Equal(accepted, await workspace.ReadSettingsSnapshotAsync());
        await second.GetByTestId("defaults-read-current").ClickAsync();
        await Assertions.Expect(second.GetByTestId("defaults-current-comparison")).ToContainTextAsync(firstName);
        await Assertions.Expect(second.GetByTestId("defaults-name")).ToHaveValueAsync(localName);
        await Assertions.Expect(second.GetByTestId("defaults-save")).ToBeDisabledAsync();
        await Shot(second, "production-defaults-conflict");
        await second.GetByTestId("defaults-reconcile").ClickAsync();
        await Assertions.Expect(second.GetByTestId("defaults-name")).ToHaveValueAsync(firstName);
        await Assertions.Expect(second.GetByTestId("defaults-previous-comparison")).ToContainTextAsync(localName);
        await Assertions.Expect(second.GetByLabel("Previous local draft notes", new() { Exact = true })).ToHaveValueAsync(localNotes);
        await Assertions.Expect(second.GetByTestId("defaults-save")).ToBeEnabledAsync();
        await second.GetByTestId("defaults-name").FillAsync(localName);
        await second.GetByTestId("defaults-notes").FillAsync(localNotes);
        await second.GetByTestId("defaults-save").ClickAsync();
        await Assertions.Expect(second.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
        var reconciled = await workspace.ReadSettingsSnapshotAsync();
        Assert.Equal(localName, reconciled.Values.WorkspaceName);
        Assert.Equal(localNotes, reconciled.Values.Notes);
        Assert.NotEqual(accepted.Expectation.Head.Revision, reconciled.Expectation.Head!.Revision);
        await Assertions.Expect(second.GetByLabel("Previous local draft notes", new() { Exact = true })).ToHaveValueAsync(localNotes);
        Assert.False(await second.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth"));
        await Shot(second, "production-defaults-reconciled");
        Assert.Empty(firstErrors);
        Assert.Empty(secondErrors);
    }

    [Fact]
    public async Task Complete_decision_policy_persists_reloads_and_keeps_a_conflicting_editors_original_draft() {
        Directory.CreateDirectory(Artifacts);
        ServiceProvider? provider = null;
        IAsyncDisposable? serviceScope = null;
        IBrowserContext? firstContext = null;
        IBrowserContext? secondContext = null;
        Exception? primary = null;
        try {
            provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
                "Workspace.Settings.Policy.Browser", TestSchemaBootstrapModules.Full,
                new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
            var scope = provider.CreateAsyncScope();
            serviceScope = scope;
            var workspace = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
            firstContext = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
            secondContext = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
            var first = await firstContext.NewPageAsync();
            var second = await secondContext.NewPageAsync();
            var firstErrors = Observe(first);
            var secondErrors = Observe(second);
            await first.GotoAsync(fixture.BaseUrl + "/settings");
            await ShellReady(first);
            await Assertions.Expect(first.GetByTestId("defaults-save")).ToBeEnabledAsync();
            await second.GotoAsync(fixture.BaseUrl + "/settings");
            await ShellReady(second);
            await Assertions.Expect(second.GetByTestId("defaults-save")).ToBeEnabledAsync();
            var original = await workspace.ReadSettingsSnapshotV2Async();
            var selected = new DecisionDataPolicy(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent,
                OnBasisChange.Replan, new(3, new TimeSpan(0, 7, 11), new TimeSpan(0, 2, 13)));
            var stalePolicy = new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.PinnedObservation,
                OnBasisChange.Fail, new(0, new TimeSpan(0, 9, 17), new TimeSpan(0, 4, 19)));
            var localNotes = "Retained policy draft " + Guid.NewGuid().ToString("N");
            await FillPolicy(first, selected);
            await FillPolicy(second, stalePolicy);
            await second.GetByTestId("defaults-notes").FillAsync(localNotes);
            await Assertions.Expect(first.GetByTestId("defaults-save")).ToContainTextAsync("Save defaults and policy");
            await first.GetByTestId("defaults-save").ClickAsync();
            await Assertions.Expect(first.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            var accepted = await workspace.ReadSettingsSnapshotV2Async();
            Assert.Equal(selected, accepted.DecisionDataPolicy);
            Assert.Equal(original.Values, accepted.Values);
            Assert.Equal(original.Expectation.Namespace, accepted.Expectation.Namespace);
            Assert.NotEqual(original.Expectation.Head?.Revision, accepted.Expectation.Head!.Revision);
            if (original.Expectation.Head is { } originalHead) {
                Assert.Equal(originalHead.Id, accepted.Expectation.Head.Id);
            }
            await first.ReloadAsync();
            await ShellReady(first);
            await ExpectPolicy(first, selected);
            await Shot(first, "production-decision-policy-reloaded");

            await second.GetByTestId("defaults-save").ClickAsync();
            await Assertions.Expect(second.GetByTestId("settings-operation").First).ToContainTextAsync("Refused");
            await Assertions.Expect(second.GetByTestId("defaults-save")).ToBeDisabledAsync();
            await ExpectPolicy(second, stalePolicy);
            await Assertions.Expect(second.GetByTestId("defaults-notes")).ToHaveValueAsync(localNotes);
            Assert.Equal(accepted, await workspace.ReadSettingsSnapshotV2Async());
            await second.GetByTestId("defaults-read-current").ClickAsync();
            await Assertions.Expect(second.GetByTestId("defaults-current-comparison")).ToContainTextAsync("StepSnapshot; RequireCurrent; Replan; replans 3");
            await ExpectPolicy(second, stalePolicy);
            await Assertions.Expect(second.GetByTestId("defaults-notes")).ToHaveValueAsync(localNotes);
            await Assertions.Expect(second.GetByTestId("defaults-save")).ToBeDisabledAsync();
            Assert.Equal(accepted, await workspace.ReadSettingsSnapshotV2Async());
            await Shot(second, "production-decision-policy-conflict");

            await second.GetByTestId("defaults-reconcile").ClickAsync();
            await ExpectPolicy(second, selected);
            await Assertions.Expect(second.GetByTestId("defaults-previous-comparison")).ToContainTextAsync("RunSnapshot; PinnedObservation; Fail; replans 0");
            await Assertions.Expect(second.GetByLabel("Previous local draft notes", new() { Exact = true })).ToHaveValueAsync(localNotes);
            await Assertions.Expect(second.GetByTestId("defaults-save")).ToBeEnabledAsync();
            await FillPolicy(second, stalePolicy);
            await second.GetByTestId("defaults-notes").FillAsync(localNotes);
            await second.GetByTestId("defaults-save").ClickAsync();
            await Assertions.Expect(second.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            var reconciled = await workspace.ReadSettingsSnapshotV2Async();
            Assert.Equal(stalePolicy, reconciled.DecisionDataPolicy);
            Assert.Equal(new WorkspaceSettingsValues(accepted.Values.WorkspaceName, accepted.Values.DefaultProviderProfileId,
                accepted.Values.DefaultPromptOutputFormat, accepted.Values.CurrencyCode, accepted.Values.CurrencyCultureName, localNotes), reconciled.Values);
            Assert.Equal(accepted.Expectation.Namespace, reconciled.Expectation.Namespace);
            Assert.Equal(accepted.Expectation.Head.Id, reconciled.Expectation.Head!.Id);
            Assert.NotEqual(accepted.Expectation.Head.Revision, reconciled.Expectation.Head.Revision);
            await second.ReloadAsync();
            await ShellReady(second);
            await ExpectPolicy(second, stalePolicy);
            await Assertions.Expect(second.GetByTestId("defaults-notes")).ToHaveValueAsync(localNotes);
            await Shot(second, "production-decision-policy-reconciled");
            Assert.Empty(firstErrors);
            Assert.Empty(secondErrors);
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "decision-policy-proof.json"), JsonSerializer.Serialize(new {
                Original = original, ObservedAfterFirstSave = accepted, ObservedAfterExplicitReconciliation = reconciled,
                ConflictPreservedPolicyAndNotes = true, CurrentReadDidNotAuthorizeRetry = true,
                ReloadVerifiedAllSixPolicyFields = true, RuntimePolicyEnforcementTested = false
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            List<Exception> cleanup = [];
            IAsyncDisposable?[] owned = [secondContext, firstContext, serviceScope, provider];
            foreach (var resource in owned) {
                if (resource is null) {
                    continue;
                }
                try {
                    await resource.DisposeAsync();
                } catch (Exception failure) {
                    cleanup.Add(failure);
                }
            }
            if (cleanup.Count != 0) {
                throw new AggregateException(primary is null ? cleanup : [primary, .. cleanup]);
            }
        }

        static async Task FillPolicy(IPage page, DecisionDataPolicy policy) {
            await page.GetByTestId("defaults-policy-read").SelectOptionAsync(policy.ReadBasisMode.ToString());
            await page.GetByTestId("defaults-policy-freshness").SelectOptionAsync(policy.PublicationFreshness.ToString());
            await page.GetByTestId("defaults-policy-change").SelectOptionAsync(policy.OnBasisChange.ToString());
            await page.GetByTestId("defaults-policy-replans").FillAsync(policy.Budget.MaximumReplans.ToString(CultureInfo.InvariantCulture));
            await page.GetByTestId("defaults-policy-work").FillAsync(policy.Budget.MaximumWorkDuration.ToString("c", CultureInfo.InvariantCulture));
            await page.GetByTestId("defaults-policy-age").FillAsync(policy.Budget.MaximumBasisAge.ToString("c", CultureInfo.InvariantCulture));
        }
        static async Task ExpectPolicy(IPage page, DecisionDataPolicy policy) {
            await Assertions.Expect(page.GetByTestId("defaults-policy-read")).ToHaveValueAsync(policy.ReadBasisMode.ToString());
            await Assertions.Expect(page.GetByTestId("defaults-policy-freshness")).ToHaveValueAsync(policy.PublicationFreshness.ToString());
            await Assertions.Expect(page.GetByTestId("defaults-policy-change")).ToHaveValueAsync(policy.OnBasisChange.ToString());
            await Assertions.Expect(page.GetByTestId("defaults-policy-replans")).ToHaveValueAsync(policy.Budget.MaximumReplans.ToString(CultureInfo.InvariantCulture));
            await Assertions.Expect(page.GetByTestId("defaults-policy-work")).ToHaveValueAsync(policy.Budget.MaximumWorkDuration.ToString("c", CultureInfo.InvariantCulture));
            await Assertions.Expect(page.GetByTestId("defaults-policy-age")).ToHaveValueAsync(policy.Budget.MaximumBasisAge.ToString("c", CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public async Task Standalone_sandbox_preserves_focus_and_uses_real_secret_and_policy_controls() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new WorkspaceSandboxHost();
        await host.ReadyAsync();
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        await page.GotoAsync(host.BaseUrl);
        var name = page.GetByTestId("defaults-name");
        await Assertions.Expect(name).ToHaveValueAsync("Scenario workspace");
        await page.GetByLabel("Operation", new() { Exact = true }).SelectOptionAsync("ProvidersRead");
        await page.GetByRole(AriaRole.Button, new() { Name = "Hold next operation", Exact = true }).ClickAsync();
        await page.GetByTestId("defaults-refresh").ClickAsync();
        await name.FillAsync("Unblurred sandbox input");
        await name.EvaluateAsync("element => element.setSelectionRange(6,6)");
        await page.GetByRole(AriaRole.Button, new() { Name = "Release operation", Exact = true }).EvaluateAsync("element => element.click()");
        await Assertions.Expect(page.GetByTestId("defaults-refresh")).ToBeEnabledAsync();
        await Assertions.Expect(name).ToBeFocusedAsync();
        Assert.Equal(6, await name.EvaluateAsync<int>("element => element.selectionStart"));
        await Shot(page, "sandbox-defaults");
        await Tab(page, "Secrets");
        await page.Locator(".cda-selection-list-item__button").ClickAsync();
        await page.Locator(".cda-secret-field__reveal").ClickAsync();
        await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "text");
        await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "password", new() { Timeout = 35000 });
        await Shot(page, "sandbox-secret-masked");
        await Tab(page, "Files");
        await Shot(page, "sandbox-files");
        await Tab(page, "Provider history");
        await Assertions.Expect(page.GetByTestId("scenario-reads")).ToContainTextAsync("Policy reads: 0");
        await page.GetByTestId("history-policy-load").ClickAsync();
        await page.GetByTestId("history-policy-preview").ClickAsync();
        await Assertions.Expect(page.GetByTestId("history-policy-confirmation")).ToBeVisibleAsync();
        await Shot(page, "sandbox-history-preview");
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("history-policy-confirmation")).ToHaveCountAsync(0);
        await page.GetByTestId("history-policy-batch").FillAsync("1e-");
        await Assertions.Expect(page.Locator(".validation-errors")).ToContainTextAsync("number");
        await page.GetByTestId("history-policy-apply").EvaluateAsync("element => element.click()");
        await Assertions.Expect(page.GetByTestId("scenario-effects")).ToContainTextAsync("Current store writes: 0");
        await Assertions.Expect(page.GetByTestId("history-policy-batch")).ToHaveValueAsync("1e-");
        Assert.Empty(errors);
    }

    private static Task Tab(IPage page, string name) => page.GetByRole(AriaRole.Button,
        new() { NameRegex = new Regex("^" + Regex.Escape(name)) }).ClickAsync();
    private static async Task ShellReady(IPage page) {
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
    }
    private static Task Shot(IPage page, string name) => page.ScreenshotAsync(new() { Path = Path.Combine(Artifacts, name + ".png") });
    private static List<string> Observe(IPage page) {
        var errors = new List<string>();
        page.PageError += (_, _) => errors.Add("Page error");
        page.Response += (_, response) => {
            if (response.Status >= 400 && (response.Url.Contains("_content/", StringComparison.Ordinal) || response.Url.EndsWith(".css", StringComparison.Ordinal))) {
                errors.Add($"Asset HTTP {response.Status}");
            }
        };
        return errors;
    }
}
