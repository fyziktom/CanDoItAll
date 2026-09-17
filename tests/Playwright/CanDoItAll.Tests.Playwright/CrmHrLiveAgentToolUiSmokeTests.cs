using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

/// <summary>
/// Opt-in live smoke through the shipped UI: the ordinary planner is granted in the agent settings dialog and asked
/// from the Project Structure floating chat; the managed HR agent is opened from the Agents workspace and its party
/// creation is denied and approved in the real approval UI. The test owns its Web host and disposable database, so a
/// closed gate starts nothing. Proof comes from the persisted run and the CRM owner; the browser adds what the operator
/// sees. With <c>CANDOITALL_LIVE_AGENT_UI_REHEARSAL=true</c> each journey stops before its first Send and costs nothing.
/// </summary>
public sealed partial class CrmHrLiveAgentToolUiSmokeTests
{
    private const string LiveValidationVariable = "CANDOITALL_RUN_LIVE_AGENT_VALIDATION";
    private const string LiveOpenAiSmokeVariable = "CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE";
    private const string RehearsalVariable = "CANDOITALL_LIVE_AGENT_UI_REHEARSAL";
    private const string PreferredPlannerTemplateKey = "delivery-manager";
    private const string FixtureActor = "live-agent-ui-smoke-fixture";
    // The managed HR flow asks the operator to confirm a creation in its own turn: per chat a directory search, the
    // confirmed proposal and the continuation after the decision, for two chats plus a small reserve.
    private const int MaximumModelRequestsPerExecution = 10;
    private const float ModelTurnTimeoutMilliseconds = 240_000;
    private static readonly Regex TerminalPhase = TerminalPhasePattern();

    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Planner_granted_in_the_agent_settings_UI_invokes_CRM_planning_search_from_the_project_structure_chat()
    {
        if (!IsLiveValidationEnabled())
        {
            return;
        }

        var evidence = new UiEvidence("ui-ordinary-planner-crm-planning-read");
        await using var host = await LiveUiHost.StartAsync();
        try
        {
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var personName = $"Planning UI Smoke Person {suffix}";
            var projectName = $"Live planning UI smoke {suffix}";
            var (projectId, personId, planner, planningCapabilityNames) = await host.SeedAsync(async services =>
            {
                var project = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel
                {
                    Name = projectName,
                    Description = "Synthetic project for the opt-in live agent UI smoke.",
                    Objective = "Prove governed CRM planning reads from the shipped chat.",
                    CurrentPhase = "Validation"
                });
                Assert.True(project.IsSuccess, "The synthetic project was not saved by the Projects owner.");
                var person = await CreatePartyAsync(services, PartyType.Person, personName);
                var profile = await services.GetRequiredService<HrService>().SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
                {
                    PartyId = person,
                    WorkforceKind = WorkforceKind.Employee,
                    Status = "Active",
                    JobTitle = "Synthetic planner",
                    LastChangedBy = FixtureActor
                });
                Assert.True(profile.IsSuccess, "The synthetic workforce profile was not saved by the HR owner.");
                var planningKeys = new[] { CrmPlanningToolPolicy.SearchCapability, CrmPlanningToolPolicy.SummaryCapability };
                var names = (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListCapabilitiesAsync())
                    .Where(item => item.Kind == CapabilityKind.Tool && planningKeys.Contains(item.Key))
                    .Select(item => item.Name).Order(StringComparer.Ordinal).ToArray();
                Assert.Equal(planningKeys.Length, names.Length);
                return (project.Value, person, await SelectOrdinaryPlannerAsync(services), names);
            });
            evidence.Targets["projectId"] = projectId;
            evidence.Targets["personPartyId"] = personId;
            evidence.Targets["personName"] = personName;
            evidence.Targets["plannerAgentId"] = planner.Id;
            host.Watch(planner.Id);
            var countsBefore = await host.SeedAsync(ReadOwnerCountsAsync);
            evidence.CountsBefore = countsBefore;

            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);

            // 1. Grants in the shipped agent settings dialog: CRM source reads and the two planning capabilities.
            var response = await oracle.NavigateAsync($"{host.BaseUrl}/agents?tab=agents&agentId={planner.Id:D}");
            Assert.True(response?.Ok, $"Expected the Agents route to return 2xx, got {(int?)response?.Status}.");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            var dialog = page.GetByTestId("agents-details-dialog").Last;
            await dialog.WaitForAsync(new() { Timeout = 60_000 });
            await Assertions.Expect(dialog.GetByTestId("agents-catalog-name")).ToHaveValueAsync(planner.Name, new() { Timeout = 30_000 });
            await dialog.GetByRole(AriaRole.Tab, new() { Name = "Memory", Exact = true }).ClickAsync();
            var crmSourceRead = dialog.GetByTestId("agents-catalog-crm-source-read");
            await Assertions.Expect(crmSourceRead).Not.ToBeCheckedAsync();
            await crmSourceRead.CheckAsync();
            await dialog.GetByRole(AriaRole.Tab, new() { Name = "Capabilities", Exact = true }).ClickAsync();
            foreach (var capabilityName in planningCapabilityNames)
            {
                await dialog.GetByTestId("agents-details-capability-search").FillAsync(capabilityName);
                var card = dialog.GetByTestId("agents-details-capability-card").Filter(new() { HasTextString = capabilityName });
                await Assertions.Expect(card).ToHaveCountAsync(1, new() { Timeout = 30_000 });
                var toggle = card.GetByTestId("agents-details-capability-toggle");
                await Assertions.Expect(toggle).ToContainTextAsync("Assign");
                await toggle.ClickAsync();
                await Assertions.Expect(toggle).ToContainTextAsync("Remove", new() { Timeout = 30_000 });
            }

            await page.ScreenshotAsync(new() { Path = host.Artifact("planner-01-grants-before-save.png") });
            await dialog.GetByTestId("agents-catalog-save").ClickAsync();
            await page.GetByText("Agent saved", new() { Exact = true }).First.WaitForAsync(new() { Timeout = 60_000 });
            var granted = await host.SeedAsync(services => FindAgentAsync(services, planner.Id));
            Assert.Contains(MemorySourceScope.Crm, AgentMemoryAccessMetadata.Read(granted.ConfigurationJson).AllowedSourceScopes);
            Assert.Contains(granted.Capabilities, item => item.CapabilityKey == CrmPlanningToolPolicy.SearchCapability);
            Assert.Contains(granted.Capabilities, item => item.CapabilityKey == CrmPlanningToolPolicy.SummaryCapability);
            await evidence.DescribeProviderAsync(host, granted);

            // 2. The project's Project Structure page and its floating agent chat.
            response = await oracle.NavigateAsync($"{host.BaseUrl}/projects/{projectId:D}/structure");
            Assert.True(response?.Ok, $"Expected the Project Structure route to return 2xx, got {(int?)response?.Status}.");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.WaitForSelectorAsync("[data-testid='project-structure-canvas-loaded']", new() { Timeout = 60_000, State = WaitForSelectorState.Attached });
            // The canvas host is initialized from an interactive render; before that the toolbar has no handlers and a
            // click would be lost.
            await page.WaitForFunctionAsync(
                "() => Array.from(document.querySelectorAll('.cw-canvas-host')).some(host => !!host.__canvasWorkbenchState)",
                null,
                new() { Timeout = 60_000 });
            var chat = await OpenFloatingChatFromCatalogAsync(page, "project-structure-agents-toggle", planner.Id,
                host.Artifact("planner-02-catalog-failure.png"));
            await page.ScreenshotAsync(new() { Path = host.Artifact("planner-02-chat-ready.png") });
            if (IsRehearsal())
            {
                evidence.Observations["rehearsal"] = "stopped-before-send";
                evidence.Passed = true;
                return;
            }

            await SendAsync(chat,
                $"Call the {CrmPlanningToolPolicy.Search} tool exactly once with searchText \"{personName}\", recordKind Party and take 5. " +
                "Do not call any other tool. Reply with only the id of the party it returned.");
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync(TerminalPhase, new() { Timeout = ModelTurnTimeoutMilliseconds });
            var run = await host.SeedAsync(services => ReadLatestRunAsync(services, planner.Id));
            evidence.RecordRun("planner-read", run);
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Completed");

            // 3. Persisted proof through the owner services (the run was read when it became terminal).
            Assert.Equal(ExecutionState.Completed, run.Run.State);
            Assert.Equal(AgentChatTrustedSourceKinds.ProjectStructure, run.Run.SourceKind);
            var search = Assert.Single(Proposals(run), item => item.Payload.ToolName == CrmPlanningToolPolicy.Search);
            Assert.Equal(AgentToolProposalState.Completed, search.State);
            Assert.Equal(AgentToolProposalEffect.Read, search.Payload.Effect);
            Assert.False(search.RequiresApproval);
            Assert.Contains(personName, search.Result!.PayloadJson, StringComparison.Ordinal);
            Assert.Contains(personId.ToString("D"), search.Result.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.All(Proposals(run), item => Assert.NotEqual(AgentToolProposalEffect.Mutation, item.Payload.Effect));
            Assert.Empty(run.Approvals);
            var receipt = Assert.Single(run.ToolReceipts, item => item.ToolName == CrmPlanningToolPolicy.Search);
            Assert.Equal(CrmPlanningToolPolicy.ProviderKey, receipt.RuntimeToolProviderKey);
            Assert.Equal(AgentToolInvocationOutcome.Succeeded, receipt.InvocationOutcome);

            // 4. The same tool call as the chat renders it. A completed turn collapses its execution steps into a summary;
            // the summary opens the execution log dialog that lists every step.
            var executionStream = chat.GetByTestId("chat-execution-stream");
            await executionStream.WaitForAsync(new() { Timeout = 60_000 });
            var invocationText = $"Invoking tool '{CrmPlanningToolPolicy.Search}'";
            var expand = chat.GetByTestId("chat-execution-summary").Or(chat.GetByTestId("chat-execution-history"));
            await Assertions.Expect(expand).ToHaveCountAsync(1, new() { Timeout = 30_000 });
            await expand.ClickAsync();
            var executionLog = page.GetByTestId("agent-execution-log-dialog-body");
            await Assertions.Expect(executionLog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(executionLog).ToContainTextAsync(invocationText, new() { Timeout = 60_000 });
            await page.ScreenshotAsync(new() { Path = host.Artifact("planner-03-tool-call-rendered.png") });
            evidence.Observations["uiRenderedToolInvocation"] = invocationText;

            evidence.CountsAfter = await host.SeedAsync(ReadOwnerCountsAsync);
            Assert.Equal(countsBefore, evidence.CountsAfter);

            evidence.Observations["liveProof"] = "passed";
            await File.WriteAllLinesAsync(host.Artifact("planner-expected-teardown.txt"), oracle.ExpectedTeardown);
            await oracle.AssertCleanAsync();
            evidence.Passed = true;
        }
        finally
        {
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }

        Assert.InRange(evidence.ModelRequests, IsRehearsal() ? 0 : 1, MaximumModelRequestsPerExecution);
    }

    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Managed_HR_chat_shows_the_real_approval_UI_and_creates_a_party_only_after_Approve()
    {
        if (!IsLiveValidationEnabled())
        {
            return;
        }

        var evidence = new UiEvidence("ui-managed-hr-approved-crm-party-create");
        await using var host = await LiveUiHost.StartAsync();
        try
        {
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var controlName = $"Directory Control {suffix}";
            var deniedName = $"Denied UI Smoke Person {suffix}";
            var approvedName = $"Approved UI Smoke Person {suffix}";
            var hrAgent = await host.SeedAsync(async services =>
            {
                await CreatePartyAsync(services, PartyType.Person, controlName);
                return await FindAgentAsync(services, HrAgentIdentity.AgentId);
            });
            Assert.True(HrAgentIdentity.Matches(hrAgent));
            evidence.Targets["deniedPersonName"] = deniedName;
            evidence.Targets["approvedPersonName"] = approvedName;
            host.Watch(hrAgent.Id);
            await evidence.DescribeProviderAsync(host, hrAgent);
            evidence.CountsBefore = await host.SeedAsync(ReadOwnerCountsAsync);

            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);

            // Turn 1: the real approval UI appears and the operator rejects once.
            var chat = await OpenManagedHrChatAsync(host, oracle, page);
            await page.ScreenshotAsync(new() { Path = host.Artifact("hr-01-chat-ready.png") });
            if (IsRehearsal())
            {
                evidence.Observations["rehearsal"] = "stopped-before-send";
                evidence.Passed = true;
                return;
            }

            var reject = chat.Locator("[data-testid^='chat-approval-reject-']");
            await RequestPartyCreationAsync(chat, deniedName, reject);
            await Assertions.Expect(chat).ToContainTextAsync("Approval required");
            await Assertions.Expect(chat).ToContainTextAsync(HrAgentToolPolicy.HrCrmPartyCreate);
            await page.ScreenshotAsync(new() { Path = host.Artifact("hr-02-approval-visible-before-deny.png") });
            Assert.Empty(await host.SeedAsync(services => FindPartyIdsAsync(services, deniedName)));
            await reject.ClickAsync();
            await Assertions.Expect(reject).ToHaveCountAsync(0, new() { Timeout = ModelTurnTimeoutMilliseconds });
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync(TerminalPhase, new() { Timeout = ModelTurnTimeoutMilliseconds });
            var deniedRun = await host.SeedAsync(services => ReadLatestRunAsync(services, hrAgent.Id));
            evidence.RecordRun("hr-party-create-denied", deniedRun);
            var deniedProposal = Assert.Single(Proposals(deniedRun), item => item.Payload.ToolName == HrAgentToolPolicy.HrCrmPartyCreate);
            Assert.Equal(ExecutionApprovalStatus.Rejected, deniedProposal.ApprovalStatus);
            Assert.NotEqual(AgentToolEffectState.Committed, deniedProposal.EffectState);
            Assert.Empty(await host.SeedAsync(services => FindPartyIdsAsync(services, deniedName)));
            await AssertDirectorySearchAsync(host, oracle, page, controlName, expected: 1);
            await AssertDirectorySearchAsync(host, oracle, page, deniedName, expected: 0, navigate: false);
            await page.ScreenshotAsync(new() { Path = host.Artifact("hr-03-directory-without-denied-party.png") });

            // Turn 2 (new chat, new run): the operator approves once and the owner holds exactly one party.
            chat = await OpenManagedHrChatAsync(host, oracle, page);
            var approve = chat.Locator("[data-testid^='chat-approval-approve-']");
            await RequestPartyCreationAsync(chat, approvedName, approve);
            await Assertions.Expect(chat).ToContainTextAsync(HrAgentToolPolicy.HrCrmPartyCreate);
            await page.ScreenshotAsync(new() { Path = host.Artifact("hr-04-approval-visible-before-approve.png") });
            Assert.Empty(await host.SeedAsync(services => FindPartyIdsAsync(services, approvedName)));
            await approve.ClickAsync();
            await Assertions.Expect(approve).ToHaveCountAsync(0, new() { Timeout = ModelTurnTimeoutMilliseconds });
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync(TerminalPhase, new() { Timeout = ModelTurnTimeoutMilliseconds });
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Completed");
            await page.ScreenshotAsync(new() { Path = host.Artifact("hr-05-approved-run-completed.png") });
            var createdPartyId = Assert.Single(await host.SeedAsync(services => FindPartyIdsAsync(services, approvedName)));
            evidence.Targets["approvedPersonPartyId"] = createdPartyId;
            var approvedRun = await host.SeedAsync(services => ReadLatestRunAsync(services, hrAgent.Id));
            evidence.RecordRun("hr-party-create-approved", approvedRun);
            Assert.NotEqual(deniedRun.Run.Id, approvedRun.Run.Id);
            Assert.Equal(ExecutionState.Completed, approvedRun.Run.State);
            var approvedProposal = Assert.Single(Proposals(approvedRun), item => item.Payload.ToolName == HrAgentToolPolicy.HrCrmPartyCreate);
            Assert.Equal(ExecutionApprovalStatus.Approved, approvedProposal.ApprovalStatus);
            Assert.Equal(AgentToolEffectState.Committed, approvedProposal.EffectState);
            Assert.Contains(createdPartyId.ToString("D"), approvedProposal.Result!.PayloadJson, StringComparison.OrdinalIgnoreCase);
            var createReceipt = Assert.Single(approvedRun.ToolReceipts, item => item.ToolName == HrAgentToolPolicy.HrCrmPartyCreate);
            Assert.Equal(AgentToolEffectState.Committed, createReceipt.EffectState);
            Assert.Equal(createdPartyId.ToString("D"), createReceipt.EffectSourceId);
            await AssertDirectorySearchAsync(host, oracle, page, approvedName, expected: 1);
            await page.ScreenshotAsync(new() { Path = host.Artifact("hr-06-directory-with-approved-party.png") });
            evidence.CountsAfter = await host.SeedAsync(ReadOwnerCountsAsync);
            Assert.Equal(evidence.CountsBefore!.Parties + 1, evidence.CountsAfter.Parties);

            evidence.Observations["liveProof"] = "passed";
            await File.WriteAllLinesAsync(host.Artifact("hr-expected-teardown.txt"), oracle.ExpectedTeardown);
            await oracle.AssertCleanAsync();
            evidence.Passed = true;
        }
        finally
        {
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }

        Assert.InRange(evidence.ModelRequests, IsRehearsal() ? 0 : 1, MaximumModelRequestsPerExecution);
    }

    // The operator conversation the managed HR agent holds: it checks the directory for an exact match and asks the
    // operator to confirm before it proposes the creation; the confirmation turn then shows the host approval control.
    private static async Task RequestPartyCreationAsync(ILocator chat, string displayName, ILocator approvalControl)
    {
        await SendAsync(chat, PartyCreatePrompt(displayName));
        var phase = chat.GetByTestId("agent-execution-activity-phase");
        await Assertions.Expect(approvalControl.Or(phase.Filter(new() { HasTextRegex = TerminalPhase })).First)
            .ToBeVisibleAsync(new() { Timeout = ModelTurnTimeoutMilliseconds });
        if (await approvalControl.CountAsync() == 0)
        {
            await SendAsync(chat,
                $"Confirmed: no party named \"{displayName}\" exists. Create it now: call the {HrAgentToolPolicy.HrCrmPartyCreate} " +
                $"tool exactly once with partyType Person and displayName \"{displayName}\". Do not call any other tool.");
        }

        await Assertions.Expect(approvalControl).ToHaveCountAsync(1, new() { Timeout = ModelTurnTimeoutMilliseconds });
    }

    // The managed HR agent checks the CRM directory for an exact match before it proposes a new person, so the prompt
    // asks for that governed read first and then for exactly one creation.
    private static string PartyCreatePrompt(string displayName)
        => $"First call the {HrAgentToolPolicy.HrCrmSearch} tool once with searchText \"{displayName}\" and recordKind Party " +
           "to confirm that no party with exactly this name exists. If none exists, call the " +
           $"{HrAgentToolPolicy.HrCrmPartyCreate} tool exactly once to create a CRM party with partyType Person and " +
           $"displayName \"{displayName}\", leaving every optional field at its default. Do not call any other tool. " +
           "After the tool results reply with the single word DONE.";

    // The managed HR chat the way the product exposes it: the HR action of the Agents workspace header.
    private static async Task<ILocator> OpenManagedHrChatAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page)
    {
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

    private static async Task<ILocator> OpenFloatingChatFromCatalogAsync(IPage page, string toggleTestId, Guid agentId, string failureScreenshot)
    {
        var catalog = page.GetByTestId("floating-agent-catalog-window");
        if (await catalog.CountAsync() == 0)
        {
            await page.GetByTestId(toggleTestId).ClickAsync();
        }

        try
        {
            await catalog.GetByTestId("floating-agent-chat-agent-list").WaitForAsync(new() { Timeout = 60_000 });
        }
        catch (TimeoutException)
        {
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

    private static async Task SendAsync(ILocator chat, string prompt)
    {
        var input = chat.GetByTestId("chat-prompt-input");
        await input.FillAsync(prompt);
        await input.PressAsync("Tab");
        var send = chat.GetByTestId("chat-send-button");
        await Assertions.Expect(send).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await send.ClickAsync();
    }

    private static async Task AssertDirectorySearchAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, string name,
        int expected, bool navigate = true)
    {
        if (navigate)
        {
            var response = await oracle.NavigateAsync($"{host.BaseUrl}/crm-hr/directory");
            Assert.True(response?.Ok, $"Expected the Directory route to return 2xx, got {(int?)response?.Status}.");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        }

        var search = page.GetByTestId("crmhr-directory-search");
        await search.WaitForAsync(new() { Timeout = 60_000 });
        await search.FillAsync(name);
        var matches = page.GetByTestId("crmhr-directory-item").Filter(new() { HasTextString = name });
        await Assertions.Expect(matches).ToHaveCountAsync(expected, new() { Timeout = 60_000 });
        if (expected == 0)
        {
            await Assertions.Expect(page.GetByTestId("crmhr-directory-item")).ToHaveCountAsync(0, new() { Timeout = 60_000 });
        }
    }

    private static bool IsLiveValidationEnabled()
        => IsEnabled(LiveValidationVariable) && IsEnabled(LiveOpenAiSmokeVariable);

    private static bool IsRehearsal() => IsEnabled(RehearsalVariable);

    private static bool IsEnabled(string environmentVariable)
        => string.Equals(Environment.GetEnvironmentVariable(environmentVariable), "true", StringComparison.OrdinalIgnoreCase);

    private static async Task<Guid> CreatePartyAsync(IServiceProvider services, PartyType partyType, string displayName)
    {
        var result = await services.GetRequiredService<ICrmPartyCommandService>().CreatePartyAsync(
            new(partyType, displayName, PartyLifecycleStatus.Active), FixtureActor);
        Assert.True(result.IsSuccess, "The synthetic CRM party was not saved by the CRM owner.");
        return result.Value!.PartyId;
    }

    private static async Task<AgentDefinition> SelectOrdinaryPlannerAsync(IServiceProvider services)
    {
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

    private static async Task<AgentDefinition> FindAgentAsync(IServiceProvider services, Guid agentId)
        => (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(includeTemplates: true))
            .Single(agent => agent.Id == agentId);

    private static async Task<ExecutionRunDetail> ReadLatestRunAsync(IServiceProvider services, Guid agentId)
    {
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var latest = (await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId)))
            .OrderByDescending(run => run.CreatedAtUtc).First();
        return await workspace.GetExecutionRunDetailAsync(latest.Id);
    }

    [GeneratedRegex(@"sk-[^\s""']+|[A-Za-z0-9_\-\*]{24,}")]
    private static partial Regex SecretLikeToken();

    // Identifiers, names and states only: secret-like tokens are redacted and long text is cut.
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = SecretLikeToken().Replace(value, "[redacted]");
        return redacted.Length <= 240 ? redacted : redacted[..240];
    }

    private static IReadOnlyList<AgentToolProposalRecord> Proposals(ExecutionRunDetail run)
        => run.Run.ToolAdmission?.Batches.SelectMany(batch => batch.Proposals).ToArray() ?? [];

    private static async Task<OwnerCounts> ReadOwnerCountsAsync(IServiceProvider services)
    {
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

    private static async Task<Guid[]> FindPartyIdsAsync(IServiceProvider services, string displayName)
    {
        await using var owner = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        return await owner.Set<Party>().AsNoTracking().Where(item => item.DisplayName == displayName).Select(item => item.Id).ToArrayAsync();
    }

    [GeneratedRegex("^(Completed|Failed|Cancelled)$")]
    private static partial Regex TerminalPhasePattern();

    private sealed record OwnerCounts(int Parties, int WorkforceProfiles, int CapacityBlocks, int ProjectAssignments,
        int StaffingRequests, int Affiliations);

    // Owns the Web host, its disposable database, the browser and the owner-side service provider of one execution.
    private sealed class LiveUiHost : IAsyncDisposable
    {
        private readonly PlaywrightAppFixture fixture = new();
        private readonly CancellationTokenSource stopWatchdog = new();
        private readonly string artifacts;
        private ServiceProvider? owner;
        private IBrowserContext? context;
        private Task watchdog = Task.CompletedTask;
        private Guid watchedAgentId;

        private LiveUiHost()
        {
            artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "crm-hr-live-agent-tools");
            Directory.CreateDirectory(artifacts);
        }

        internal string BaseUrl => fixture.BaseUrl;

        internal bool SpendBoundExceeded { get; private set; }

        internal static async Task<LiveUiHost> StartAsync()
        {
            var host = new LiveUiHost();
            try
            {
                await host.fixture.InitializeAsync();
                host.owner = await TestApplicationBootstrap.BuildServiceProviderAsync(
                    host.fixture.OwnedDatabaseProfile,
                    "CanDoItAll.Tests.Playwright.LiveAgentSeed",
                    TestSchemaBootstrapModules.Full,
                    new Dictionary<string, string?> { ["DevelopmentManager:TuningModeEnabled"] = "false" });
                return host;
            }
            catch
            {
                await host.DisposeAsync();
                throw;
            }
        }

        internal string Artifact(string fileName) => Path.Combine(artifacts, fileName);

        internal async Task<T> SeedAsync<T>(Func<IServiceProvider, Task<T>> read)
        {
            await using var scope = owner!.CreateAsyncScope();
            return await read(scope.ServiceProvider);
        }

        internal async Task<IPage> NewPageAsync()
        {
            context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(45_000);
            return page;
        }

        internal Task<int> CountModelRequestsAsync()
            => watchedAgentId == Guid.Empty
                ? Task.FromResult(0)
                : SeedAsync(async services => (await services.GetRequiredService<IAgentFrameworkWorkspaceService>()
                    .ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: watchedAgentId)))
                    .Sum(run => run.ToolAdmission?.Batches.Length ?? 0));

        // Spend guard: the journey has no cancellation handle on the server-side run, so a run that exceeds the bound
        // loses its host.
        internal Guid? WatchedAgentId => watchedAgentId == Guid.Empty ? null : watchedAgentId;

        internal void Watch(Guid agentId)
        {
            watchedAgentId = agentId;
            watchdog = Task.Run(async () =>
            {
                try
                {
                    while (!stopWatchdog.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stopWatchdog.Token);
                        int used;
                        try
                        {
                            used = await CountModelRequestsAsync();
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            continue;
                        }

                        if (used >= MaximumModelRequestsPerExecution)
                        {
                            SpendBoundExceeded = true;
                            await fixture.DisposeAsync();
                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            await stopWatchdog.CancelAsync();
            await watchdog;
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            if (owner is not null)
            {
                await owner.DisposeAsync();
            }

            if (!SpendBoundExceeded)
            {
                await fixture.DisposeAsync();
            }

            stopWatchdog.Dispose();
        }
    }

    // Sanitized evidence: identifiers, resolved tool names, decisions, states, counts and UTC times only.
    private sealed class UiEvidence(string scenario)
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        private readonly List<object> runs = [];
        private object? provider;

        internal Dictionary<string, object> Targets { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, object> Observations { get; } = new(StringComparer.Ordinal);

        internal OwnerCounts? CountsBefore { get; set; }

        internal OwnerCounts? CountsAfter { get; set; }

        internal bool Passed { get; set; }

        internal int ModelRequests { get; private set; }

        internal async Task DescribeProviderAsync(LiveUiHost host, AgentDefinition agent)
        {
            provider = await host.SeedAsync(async services =>
            {
                var profile = (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListProvidersAsync())
                    .Single(item => item.Id == agent.ProviderProfileId);
                var model = ManagedSeedProviderFallbacks.ResolveModel(agent, profile);
                Assert.Equal(ProviderKind.OpenAi, profile.Kind);
                Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, model);
                return (object)new { kind = profile.Kind, name = profile.Name, transport = profile.Transport, model, agentId = agent.Id, agentName = agent.Name };
            });
        }

        internal void RecordRun(string label, ExecutionRunDetail detail)
            => runs.Add(new
            {
                label,
                executionRunId = detail.Run.Id,
                chatSessionId = detail.Run.ChatSessionId,
                state = detail.Run.State,
                outcome = detail.Run.Outcome,
                providerName = detail.Run.ProviderName,
                model = detail.Run.Model,
                sourceKind = detail.Run.SourceKind,
                modelRequestsInRun = detail.Run.ToolAdmission?.Batches.Length ?? 0,
                usageObservations = detail.UsageObservations.Count,
                resultSummary = Sanitize(detail.Run.ResultSummary),
                executionLog = detail.ExecutionLog.OrderBy(item => item.CreatedAtUtc).TakeLast(12).Select(item => new
                {
                    phase = item.Phase,
                    state = item.State,
                    message = Sanitize(item.Message)
                }),
                createdAtUtc = detail.Run.CreatedAtUtc,
                completedAtUtc = detail.Run.CompletedAtUtc,
                proposals = Proposals(detail).Select(item => new
                {
                    proposalId = item.IntentId.Value,
                    toolName = item.Payload.ToolName,
                    effect = item.Payload.Effect,
                    requiresApproval = item.RequiresApproval,
                    state = item.State,
                    approvalStatus = item.ApprovalStatus,
                    approvalId = item.ApprovalId,
                    effectState = item.EffectState
                }),
                approvals = detail.Approvals.Select(item => new
                {
                    approvalId = item.ApprovalId,
                    toolName = item.ToolName,
                    decision = item.Status,
                    requestedAtUtc = item.RequestedAtUtc,
                    decidedAtUtc = item.DecidedAtUtc
                }),
                toolInvocations = detail.ToolReceipts.OrderBy(item => item.StartedAtUtc).Select(item => new
                {
                    toolName = item.ToolName,
                    providerKey = item.RuntimeToolProviderKey,
                    riskClass = item.RiskClass,
                    outcome = item.InvocationOutcome,
                    effectState = item.EffectState,
                    effectSourceKind = item.EffectSourceKind,
                    effectSourceId = item.EffectSourceId,
                    failureCode = item.FailureCode,
                    completedAtUtc = item.CompletedAtUtc
                })
            });

        // A failed journey still leaves the persisted run of the watched agent behind, sanitized.
        internal async Task RecordLatestRunUnlessPassedAsync(LiveUiHost host)
        {
            if (Passed || host.WatchedAgentId is not { } agentId)
            {
                return;
            }

            try
            {
                RecordRun("latest-run-after-failure", await host.SeedAsync(services => ReadLatestRunAsync(services, agentId)));
            }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
            {
                // No run was created before the failure.
            }
        }

        internal async Task WriteAsync(LiveUiHost host)
        {
            try
            {
                ModelRequests = await host.CountModelRequestsAsync();
            }
            catch (Exception)
            {
                // An unreadable journal must not hide the original failure; the bound assertion then fails closed.
                ModelRequests = -1;
            }

            var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "live-agent-smoke",
                $"{startedAtUtc:yyyyMMddTHHmmssfffZ}");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "evidence.json"), JsonSerializer.Serialize(new
            {
                scenario,
                passed = Passed,
                startedAtUtc,
                completedAtUtc = DateTimeOffset.UtcNow,
                provider,
                modelRequests = new { used = ModelRequests, bound = MaximumModelRequestsPerExecution, hostStoppedBySpendGuard = host.SpendBoundExceeded },
                targets = Targets,
                countsBefore = CountsBefore,
                countsAfter = CountsAfter,
                observations = Observations,
                runs
            }, Json));
        }
    }
}
