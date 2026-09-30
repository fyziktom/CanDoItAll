using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal static partial class AgentUiJourneySupport {
    internal const string LiveValidationVariable = "CANDOITALL_RUN_LIVE_AGENT_VALIDATION";
    internal const string LiveOpenAiSmokeVariable = "CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE";
    internal const string RehearsalVariable = "CANDOITALL_LIVE_AGENT_UI_REHEARSAL";
    internal const string PreferredPlannerTemplateKey = "delivery-manager";
    internal const string FixtureActor = "live-agent-ui-smoke-fixture";
    // The managed HR flow asks the operator to confirm a creation in its own turn: per chat a directory search, the
    // confirmed proposal and the continuation after the decision, for two chats plus a small reserve.
    internal const int MaximumModelRequestsPerExecution = LiveModelRequestProxy.PerExecutionLimit;
    internal const float ModelTurnTimeoutMilliseconds = 240_000;
    internal static readonly Regex TerminalPhase = TerminalPhasePattern();

    // The operator conversation the managed HR agent holds: it checks the directory for an exact match and asks the
    // operator to confirm before it proposes the creation; the confirmation turn then shows the host approval control.
    internal static async Task RequestPartyCreationAsync(ILocator chat, string displayName, ILocator approvalControl) {
        await SendAsync(chat, PartyCreatePrompt(displayName));
        var phase = chat.GetByTestId("agent-execution-activity-phase");
        await Assertions.Expect(approvalControl.Or(phase.Filter(new() { HasTextRegex = TerminalPhase })).First)
            .ToBeVisibleAsync(new() { Timeout = ModelTurnTimeoutMilliseconds });
        if (await approvalControl.CountAsync() == 0) {
            await SendAsync(chat,
                $"Confirmed: no party named \"{displayName}\" exists. Create it now: call the {HrAgentToolPolicy.HrCrmPartyCreate} " +
                $"tool exactly once with partyType Person and displayName \"{displayName}\". Do not call any other tool.");
        }

        await Assertions.Expect(approvalControl).ToHaveCountAsync(1, new() { Timeout = ModelTurnTimeoutMilliseconds });
    }

    // The managed HR agent checks the CRM directory for an exact match before it proposes a new person, so the prompt
    // asks for that governed read first and then for exactly one creation.
    internal static string PartyCreatePrompt(string displayName)
        => $"First call the {HrAgentToolPolicy.HrCrmSearch} tool once with searchText \"{displayName}\" and recordKind Party " +
           "to confirm that no party with exactly this name exists. If none exists, call the " +
           $"{HrAgentToolPolicy.HrCrmPartyCreate} tool exactly once to create a CRM party with partyType Person and " +
           $"displayName \"{displayName}\", leaving every optional field at its default. Do not call any other tool. " +
           "After the tool results reply with the single word DONE.";

    // The managed HR chat the way the product exposes it: the HR action of the Agents workspace header.
    internal static async Task<ILocator> OpenManagedHrChatAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page) {
        var response = await oracle.NavigateAsync($"{host.BaseUrl}/agents");
        Assert.True(response?.Ok, $"Expected the Agents route to return 2xx, got {(int?)response?.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        var open = page.GetByTestId("agents-hr-agent-open-header");
        await Assertions.Expect(open).ToBeEnabledAsync(new() { Timeout = 60_000 });
        await open.ClickAsync();
        var chat = page.GetByTestId("floating-agent-chat-content");
        await chat.GetByTestId("chat-prompt-input").WaitForAsync(new() { Timeout = 60_000 });
        await Assertions.Expect(chat.GetByTestId("chat-send-button")).ToBeVisibleAsync();
        return chat;
    }

    internal static async Task<ILocator> OpenFloatingChatFromCatalogAsync(IPage page, string toggleTestId, Guid agentId, string failureScreenshot) {
        var catalog = page.GetByTestId("floating-agent-catalog-window");
        if (await catalog.CountAsync() == 0) {
            await page.GetByTestId(toggleTestId).ClickAsync();
        }

        try {
            await catalog.GetByTestId("floating-agent-chat-agent-list").WaitForAsync(new() { Timeout = 60_000 });
        } catch (TimeoutException) {
            await page.ScreenshotAsync(new() { Path = failureScreenshot });
            throw;
        }
        var start = catalog.GetByTestId($"floating-agent-chat-agent-list-new-chat-{agentId:N}");
        await Assertions.Expect(start).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await start.ClickAsync();
        var chat = page.GetByTestId("floating-agent-chat-content");
        await chat.GetByTestId("chat-prompt-input").WaitForAsync(new() { Timeout = 60_000 });
        return chat;
    }

    internal static async Task SendAsync(ILocator chat, string prompt) {
        var input = chat.GetByTestId("chat-prompt-input");
        await input.FillAsync(prompt);
        await input.PressAsync("Tab");
        var send = chat.GetByTestId("chat-send-button");
        await Assertions.Expect(send).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await send.ClickAsync();
    }

    internal static async Task AssertDirectorySearchAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, string name,
        int expected, bool navigate = true) {
        if (navigate) {
            var response = await oracle.NavigateAsync($"{host.BaseUrl}/crm-hr/directory");
            Assert.True(response?.Ok, $"Expected the Directory route to return 2xx, got {(int?)response?.Status}.");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        }

        var search = page.GetByTestId("crmhr-directory-search");
        await search.WaitForAsync(new() { Timeout = 60_000 });
        await search.FillAsync(name);
        var matches = page.GetByTestId("crmhr-directory-item").Filter(new() { HasTextString = name });
        await Assertions.Expect(matches).ToHaveCountAsync(expected, new() { Timeout = 60_000 });
        if (expected == 0) {
            await Assertions.Expect(page.GetByTestId("crmhr-directory-item")).ToHaveCountAsync(0, new() { Timeout = 60_000 });
        }
    }

    internal static bool IsLiveValidationEnabled()
        => IsEnabled(LiveValidationVariable) && IsEnabled(LiveOpenAiSmokeVariable);

    internal static bool IsRehearsal() => IsEnabled(RehearsalVariable);

    internal static bool IsEnabled(string environmentVariable)
        => string.Equals(Environment.GetEnvironmentVariable(environmentVariable), "true", StringComparison.OrdinalIgnoreCase);

    internal static async Task<Guid> CreatePartyAsync(IServiceProvider services, PartyType partyType, string displayName) {
        var result = await services.GetRequiredService<ICrmPartyCommandService>().CreatePartyAsync(
            new(partyType, displayName, PartyLifecycleStatus.Active), FixtureActor);
        Assert.True(result.IsSuccess, "The synthetic CRM party was not saved by the CRM owner.");
        return result.Value!.PartyId;
    }

    internal static async Task<AgentDefinition> SelectOrdinaryPlannerAsync(IServiceProvider services) {
        var candidates = (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(includeTemplates: false))
            .Where(agent => !agent.IsTemplate && agent.Status == AgentLifecycleStatus.Active && agent.Permissions.CanUseTools &&
                agent.ProviderProfileId.HasValue && !ManagedAdministrativeAgentIdentityCatalog.AgentIds.Contains(agent.Id) &&
                AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson) is { CanRead: true, AllowAllProjects: true })
            .OrderBy(agent => agent.TemplateKey == PreferredPlannerTemplateKey ? 0 : 1)
            .ThenBy(agent => agent.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(candidates);
        Assert.DoesNotContain(MemorySourceScope.Crm, AgentMemoryAccessMetadata.Read(candidates[0].ConfigurationJson).AllowedSourceScopes);
        return candidates[0];
    }

    internal static async Task<AgentDefinition> FindAgentAsync(IServiceProvider services, Guid agentId)
        => (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(includeTemplates: true))
            .Single(agent => agent.Id == agentId);

    internal static async Task<ExecutionRunDetail> ReadLatestRunAsync(IServiceProvider services, Guid agentId) {
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var latest = (await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId)))
            .OrderByDescending(run => run.CreatedAtUtc).First();
        return await workspace.GetExecutionRunDetailAsync(latest.Id);
    }

    [GeneratedRegex(@"sk-[^\s""']+|[A-Za-z0-9_\-\*]{24,}")]
    internal static partial Regex SecretLikeToken();

    // Identifiers, names and states only: secret-like tokens are redacted and long text is cut.
    internal static string Sanitize(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        var redacted = SecretLikeToken().Replace(value, "[redacted]");
        return redacted.Length <= 240 ? redacted : redacted[..240];
    }

    internal static IReadOnlyList<AgentToolProposalRecord> Proposals(ExecutionRunDetail run)
        => run.Run.ToolAdmission?.Batches.SelectMany(batch => batch.Proposals).ToArray() ?? [];

    internal static async Task<OwnerCounts> ReadOwnerCountsAsync(IServiceProvider services) {
        await using var owner = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        return new OwnerCounts(
            // Projected technical-agent parties are written by the host's startup projection while the smoke runs.
            await owner.Set<Party>().CountAsync(party => party.PartyType != PartyType.AiAgent),
            await owner.Set<WorkforceProfile>().CountAsync(),
            await owner.Set<CapacityBlock>().CountAsync(),
            await owner.Set<ProjectPartyAssignment>().CountAsync(),
            await owner.Set<StaffingRequest>().CountAsync(),
            await owner.Set<PartyOrganizationAffiliation>().CountAsync());
    }

    internal static async Task<Guid[]> FindPartyIdsAsync(IServiceProvider services, string displayName) {
        await using var owner = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        return await owner.Set<Party>().AsNoTracking().Where(item => item.DisplayName == displayName).Select(item => item.Id).ToArrayAsync();
    }

    [GeneratedRegex("^(Completed|Failed|Cancelled)$")]
    internal static partial Regex TerminalPhasePattern();

    internal sealed record OwnerCounts(int Parties, int WorkforceProfiles, int CapacityBlocks, int ProjectAssignments,
        int StaffingRequests, int Affiliations);

}
