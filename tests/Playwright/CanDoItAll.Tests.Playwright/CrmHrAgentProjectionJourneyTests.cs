using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// The CRM / HR agent projection through the real Web host and PostgreSQL. The Playwright host runs without background
// workers, so nothing projects the managed technical agents at startup: the journey loads the managed defaults through
// the AgentFramework shell's own action, which repairs the organization catalog and synchronizes the CRM / HR
// projection in the host process. The expected agents are then read from the owner (the persisted bindings and
// AiAgentService), never from the catalog under test.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrAgentProjectionJourneyTests
{
    private const int CatalogSyncTimeoutMs = 240_000;

    private readonly PlaywrightAppFixture fixture;

    public CrmHrAgentProjectionJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Projected_technical_agents_open_read_only_with_their_technical_identity_and_keep_human_owned_party_fields()
    {
        var artifactsDir = CrmHrWorkspaceJourneySupport.ArtifactsDirectory("crm-hr-agent-projection-journey");
        var suffix = CrmHrWorkspaceJourneySupport.NewSuffix();
        await using var owner = await CrmHrWorkspaceJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-agent-projection-journey");

        await CrmHrWorkspaceJourneySupport.RunAsync(fixture, artifactsDir, async (page, oracle) =>
        {
            // Project the managed technical agents: the AgentFramework shell's "load defaults" action, confirmed once.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, "/agents?tab=agents");
            var loadDefaults = page.GetByTestId("agents-shell-feed-defaults");
            await Assertions.Expect(loadDefaults).ToBeEnabledAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await loadDefaults.ClickAsync();
            var confirmation = page.GetByTestId("agents-feed-defaults-confirmation");
            await Assertions.Expect(confirmation).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.GetByTestId("agents-feed-defaults-confirm").ClickAsync();
            var synchronized = page.Locator(".rz-notification").GetByText(
                "Default agents, providers, capabilities, workflows, and CRM-HR projections were synchronized.",
                new() { Exact = true });
            await Assertions.Expect(synchronized.First).ToBeVisibleAsync(new() { Timeout = CatalogSyncTimeoutMs });
            await Assertions.Expect(loadDefaults).ToBeEnabledAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });

            // The owner's projection is the oracle: bound technical agents with their projected identity.
            var projected = await ReadProjectionAsync(owner);
            Assert.NotEmpty(projected);
            Assert.All(projected, agent => Assert.Equal(AiResourceBindingStatus.Bound, agent.BindingStatus));
            Assert.Equal(projected.Count, projected.Select(agent => agent.TechnicalAgentId).Distinct().Count());
            var target = projected
                .Where(agent => projected.Count(other => string.Equals(other.TechnicalName, agent.TechnicalName, StringComparison.Ordinal)) == 1)
                .OrderBy(agent => agent.TechnicalName, StringComparer.Ordinal)
                .First();
            var bystander = projected.First(agent => agent.PartyId != target.PartyId);

            // The catalog lists the projected agents: the total, and each probed agent by its exact name and role.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, "/crm-hr/agents");
            var search = await CrmHrWorkspaceJourneySupport.WaitForCatalogAsync(page, "crmhr-agent");
            await Assertions.Expect(page.GetByTestId("crmhr-agent-catalog")).ToContainTextAsync(
                new Regex($@"(?<![0-9]){Math.Min(projected.Count, AiAgentDirectoryQueryLimits.DefaultPageSize)} of {projected.Count} agent\(s\)"),
                new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            foreach (var agent in new[] { bystander, target })
            {
                await search.FillAsync(agent.TechnicalName);
                var listed = page.GetByTestId("crmhr-agent-item-shell").Filter(new() { Has = page.Locator($"[data-testid='crmhr-agent-item'][aria-label='Select {agent.TechnicalName}']") });
                await Assertions.Expect(listed).ToHaveCountAsync(1, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
                await Assertions.Expect(listed.Locator(".agent-selection-card__name")).ToHaveTextAsync(agent.TechnicalName);
                if (!string.IsNullOrWhiteSpace(agent.RoleTitle))
                {
                    await Assertions.Expect(listed.Locator(".agent-selection-card__role")).ToHaveTextAsync(agent.RoleTitle);
                }
            }

            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-agent-catalog-1600.png") });

            // Select the agent card once: the read-only record shows the same agent, its binding and validation state.
            var card = page.Locator($"[data-testid='crmhr-agent-item'][aria-label='Select {target.TechnicalName}']");
            await Assertions.Expect(card).ToBeEnabledAsync();
            await card.ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectUrlAsync(page, $"partyId={target.PartyId:D}");
            var dialog = page.GetByTestId("crmhr-agent-record-dialog");
            await AssertAgentRecordAsync(page, dialog, target);
            await Assertions.Expect(dialog.Locator("input, textarea, select")).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-agent-record-1600.png") });

            await CrmHrWorkspaceJourneySupport.AssertDialogFitsConstrainedWidthAsync(
                page,
                "crmhr-agent-record-dialog",
                ["crmhr-agent-open-technical-record", "crmhr-agent-open-directory-record", "crmhr-agent-record-close"],
                Path.Combine(artifactsDir, "03-agent-record-1100.png"));

            // "Open technical record" leads to the AgentFramework editor of the same technical agent; CRM / HR holds no
            // second technical catalog, and the projection is unchanged by the visit.
            await dialog.GetByTestId("crmhr-agent-open-technical-record").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectUrlAsync(page, $"/agents?tab=agents&agentId={target.TechnicalAgentId:D}");
            await Assertions.Expect(page.GetByTestId("agents-catalog-name")).ToHaveValueAsync(target.TechnicalName, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "04-technical-record-1600.png") });
            var afterTechnicalVisit = await ReadProjectionAsync(owner);
            Assert.Equal(
                projected.Select(agent => (agent.PartyId, agent.TechnicalAgentId)).OrderBy(pair => pair.PartyId).ToArray(),
                afterTechnicalVisit.Select(agent => (agent.PartyId, agent.TechnicalAgentId)).OrderBy(pair => pair.PartyId).ToArray());

            // Back on the agent's record, "Open directory record" reaches the linked directory party.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/agents?partyId={target.PartyId:D}");
            await CrmHrWorkspaceJourneySupport.WaitForCatalogAsync(page, "crmhr-agent");
            await AssertAgentRecordAsync(page, dialog, target);
            await dialog.GetByTestId("crmhr-agent-open-directory-record").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectUrlAsync(page, $"/crm-hr/directory?partyId={target.PartyId:D}");
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(target.PartyDisplayName, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-party-type")).ToHaveValueAsync(PartyType.AiAgent.ToString());

            // An unknown party never opens another agent's record.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/agents?partyId={Guid.NewGuid():D}");
            search = await CrmHrWorkspaceJourneySupport.WaitForCatalogAsync(page, "crmhr-agent");
            await Assertions.Expect(page.GetByTestId("crmhr-agent-item").First).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(dialog).ToHaveCountAsync(0, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.Locator("[data-testid='crmhr-agent-item'][aria-pressed='true']")).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "05-unknown-party-1600.png") });

            // Human-owned party fields: the summary edited in Directory is kept on the party, and the agent record
            // still shows the technical identity unchanged.
            var editedSummary = $"Human-owned summary {suffix}";
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/directory?partyId={target.PartyId:D}");
            await CrmHrWorkspaceJourneySupport.WaitForCatalogAsync(page, "crmhr-directory");
            var directoryDialog = page.GetByTestId("crmhr-directory-record-dialog");
            await Assertions.Expect(directoryDialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(target.PartyDisplayName);
            await page.GetByTestId("crmhr-party-summary").FillAsync(editedSummary);
            await page.GetByTestId("crmhr-party-summary").PressAsync("Tab");
            await page.GetByTestId("crmhr-party-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Party saved.");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "06-directory-summary-saved-1600.png") });

            var editedParty = await ReadPartyAsync(owner, target.PartyId);
            Assert.Equal(editedSummary, editedParty.Summary);
            Assert.Equal(target.PartyDisplayName, editedParty.DisplayName);
            Assert.Equal(PartyType.AiAgent, editedParty.PartyType);
            var afterEdit = Assert.Single((await ReadProjectionAsync(owner)).Where(agent => agent.PartyId == target.PartyId));
            Assert.Equal(target with { PartySummary = editedSummary }, afterEdit);

            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/agents?partyId={target.PartyId:D}");
            await CrmHrWorkspaceJourneySupport.WaitForCatalogAsync(page, "crmhr-agent");
            await AssertAgentRecordAsync(page, dialog, target);
            Assert.DoesNotContain(editedSummary, await dialog.InnerTextAsync(), StringComparison.Ordinal);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "07-agent-record-after-party-edit-1600.png") });
        });
    }

    // The read-only record of exactly this agent: technical name and role, validation state, and the binding state on
    // the projection tab. The record returns to its overview afterwards.
    private static async Task AssertAgentRecordAsync(IPage page, ILocator dialog, ProjectedAgent agent)
    {
        await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
        await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(agent.TechnicalName, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
        await Assertions.Expect(dialog.GetByTestId("crmhr-agent-open-technical-record")).ToBeEnabledAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
        await Assertions.Expect(dialog).ToContainTextAsync(string.IsNullOrWhiteSpace(agent.RoleTitle) ? "Not set" : agent.RoleTitle);
        await Assertions.Expect(dialog).ToContainTextAsync(agent.ValidationStatus.ToString());

        await dialog.GetByTestId("crmhr-agent-record-tab-projection").ClickAsync();
        await Assertions.Expect(dialog).ToContainTextAsync(agent.BindingSummary);
        await Assertions.Expect(dialog).ToContainTextAsync("AgentFramework owns this runtime");
        await dialog.GetByTestId("crmhr-agent-record-tab-overview").ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("crmhr-agent-summary-capabilities")).ToBeVisibleAsync();
    }

    // The owner's view of the projection: AiAgentService's directory and workspace over the persisted binding rows.
    private static async Task<IReadOnlyList<ProjectedAgent>> ReadProjectionAsync(ServiceProvider owner)
    {
        await using var scope = owner.CreateAsyncScope();
        var agentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var directory = await agentService.ListAgentDirectoryAsync();
        await using var dbContext = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var partyIds = directory.Select(item => item.PartyId).ToArray();
        var bindings = await dbContext.Set<AiResourceBinding>().AsNoTracking()
            .Where(binding => partyIds.Contains(binding.PartyId))
            .ToDictionaryAsync(binding => binding.PartyId);

        var agents = new List<ProjectedAgent>(directory.Count);
        foreach (var item in directory)
        {
            var workspace = await agentService.GetAgentWorkspaceAsync(item.PartyId);
            Assert.NotNull(workspace);
            var binding = bindings[item.PartyId];
            Assert.Equal(item.TechnicalAgentId, binding.TechnicalAgentId);
            Assert.Equal(item.TechnicalAgentId, workspace!.TechnicalAgentId);
            agents.Add(new ProjectedAgent(
                item.PartyId,
                Assert.IsType<Guid>(item.TechnicalAgentId),
                binding.ProjectedDisplayName,
                binding.ProjectedRoleTitle,
                binding.ProjectedSummary,
                item.DisplayName,
                item.Summary,
                workspace.BindingStatus,
                workspace.BindingSummary,
                workspace.Profile.ValidationStatus));
        }

        return agents;
    }

    private static async Task<PartyEditorModel> ReadPartyAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        var party = await scope.ServiceProvider.GetRequiredService<PartyDirectoryService>().GetPartyAsync(partyId);
        Assert.NotNull(party);
        return party!;
    }

    private sealed record ProjectedAgent(
        Guid PartyId,
        Guid TechnicalAgentId,
        string TechnicalName,
        string RoleTitle,
        string TechnicalSummary,
        string PartyDisplayName,
        string PartySummary,
        AiResourceBindingStatus BindingStatus,
        string BindingSummary,
        AiValidationStatus ValidationStatus);
}
