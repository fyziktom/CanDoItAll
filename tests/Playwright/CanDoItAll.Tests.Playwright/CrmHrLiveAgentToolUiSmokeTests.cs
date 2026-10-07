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

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class CrmHrLiveAgentToolUiSmokeTests {
    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Planner_granted_in_the_agent_settings_UI_invokes_CRM_planning_search_from_the_project_structure_chat() {
        if (!IsLiveValidationEnabled()) {
            await UiEvidence.WriteNotRunAsync(
                "ui-ordinary-planner-crm-planning-read",
                $"{LiveValidationVariable} and {LiveOpenAiSmokeVariable} were not both set.");
            return;
        }

        var evidence = new UiEvidence("ui-ordinary-planner-crm-planning-read");
        var rehearsalOnly = false;
        await using var host = await LiveUiHost.StartAsync();
        try {
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var personName = $"Planning UI Smoke Person {suffix}";
            var projectName = $"Live planning UI smoke {suffix}";
            var (projectId, personId, planner, planningCapabilityNames) = await host.SeedAsync(async services => {
                var project = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
                    Name = projectName,
                    Description = "Synthetic project for the opt-in live agent UI smoke.",
                    Objective = "Prove governed CRM planning reads from the shipped chat.",
                    CurrentPhase = "Validation"
                });
                Assert.True(project.IsSuccess, "The synthetic project was not saved by the Projects owner.");
                var person = await CreatePartyAsync(services, PartyType.Person, personName);
                var profile = await services.GetRequiredService<HrService>().SaveWorkforceProfileAsync(new WorkforceProfileEditorModel {
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
            foreach (var capabilityName in planningCapabilityNames) {
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
            if (IsRehearsal()) {
                evidence.Observations["rehearsal"] = "stopped-before-send";
                evidence.Execution = "rehearsal";
                evidence.Passed = true;
                rehearsalOnly = true;
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
        } finally {
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }

        Assert.InRange(evidence.ModelRequests, IsRehearsal() ? 0 : 1, MaximumModelRequestsPerExecution);
        Assert.Equal(rehearsalOnly, IsRehearsal());
    }

    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Managed_HR_chat_shows_the_real_approval_UI_and_creates_a_party_only_after_Approve() {
        if (!IsLiveValidationEnabled()) {
            await UiEvidence.WriteNotRunAsync(
                "ui-managed-hr-approved-crm-party-create",
                $"{LiveValidationVariable} and {LiveOpenAiSmokeVariable} were not both set.");
            return;
        }

        var evidence = new UiEvidence("ui-managed-hr-approved-crm-party-create");
        var rehearsalOnly = false;
        await using var host = await LiveUiHost.StartAsync();
        try {
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var controlName = $"Directory Control {suffix}";
            var deniedName = $"Denied UI Smoke Person {suffix}";
            var approvedName = $"Approved UI Smoke Person {suffix}";
            var hrAgent = await host.SeedAsync(async services => {
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
            if (IsRehearsal()) {
                evidence.Observations["rehearsal"] = "stopped-before-send";
                evidence.Execution = "rehearsal";
                evidence.Passed = true;
                rehearsalOnly = true;
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
        } finally {
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }

        Assert.InRange(evidence.ModelRequests, IsRehearsal() ? 0 : 1, MaximumModelRequestsPerExecution);
        Assert.Equal(rehearsalOnly, IsRehearsal());
    }

}
