using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class ProjectPartyAssignmentFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public ProjectPartyAssignmentFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Project_assignment_workspace_and_structure_editor_stay_in_sync()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b10";
        Directory.CreateDirectory(evidenceDirectory);

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var seed = await SeedProjectAsync(suffix);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/assignments?projectId={seed.ProjectId:D}");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-assignment-role").WaitForAsync();

        await page.GetByTestId("crmhr-assignment-role").SelectOptionAsync(ProjectPartyAssignmentRole.Customer.ToString());
        await page.GetByTestId("crmhr-assignment-party-select").SelectOptionAsync(seed.CustomerId.ToString());
        await page.GetByTestId("crmhr-assignment-save-button").ClickAsync();
        await page.GetByTestId("crmhr-assignment-message").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-assignment-message"), "Project assignment saved.");

        await page.GetByTestId("crmhr-assignment-role").SelectOptionAsync(ProjectPartyAssignmentRole.DeliveryUnit.ToString());
        await page.GetByTestId("crmhr-assignment-party-select").SelectOptionAsync(seed.DeliveryUnitId.ToString());
        await page.GetByTestId("crmhr-assignment-allocation").FillAsync("60");
        await page.GetByTestId("crmhr-assignment-save-button").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-assignment-message"), "Project assignment saved.");

        await page.GetByTestId("crmhr-assignment-role").SelectOptionAsync(ProjectPartyAssignmentRole.Manager.ToString());
        await page.GetByTestId("crmhr-assignment-party-select").SelectOptionAsync(seed.OwnerId.ToString());
        await page.GetByTestId("crmhr-assignment-allocation").FillAsync(string.Empty);
        await page.GetByTestId("crmhr-assignment-save-button").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-assignment-message"), "Project assignment saved.");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-assignments-b10-desktop.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await page.GetByTestId("projects-board").WaitForAsync();
        await page.Locator("input[placeholder='Search by name or current phase']").FillAsync(seed.ProjectName);

        var projectCard = page.GetByTestId("project-card")
            .Filter(new LocatorFilterOptions
            {
                HasText = seed.ProjectName
            });
        await projectCard.WaitForAsync();
        await ExpectTextContainsAsync(projectCard, $"Customer: {seed.CustomerName}");
        await ExpectTextContainsAsync(projectCard, $"Delivery unit: {seed.DeliveryUnitName}");
        await ExpectTextContainsAsync(projectCard, $"Owner: {seed.OwnerName}");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-projects-b10-desktop.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/projects/{seed.ProjectId:D}/structure");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-structure-b10-before-select.png"),
            FullPage = true
        });
        await page.GetByTestId(BuildOutlineNodeTestId(seed.ParticipantNodeId)).ClickAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-structure-b10-after-participant-click.png"),
            FullPage = true
        });
        await page.GetByTestId("project-structure-participant-local-only").WaitForAsync();
        await page.GetByTestId("project-structure-participant-local-only").UncheckAsync();
        await page.GetByTestId("project-structure-participant-party").WaitForAsync();
        await page.GetByTestId("project-structure-participant-party").SelectOptionAsync(seed.OwnerId.ToString());
        await page.GetByTestId("project-structure-participant-save").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("project-structure-party-editor-message"), "Participant linked to the directory.");

        await page.GetByTestId(BuildOutlineNodeTestId(seed.MeetingNodeId)).ClickAsync();
        await page.GetByTestId("project-structure-meeting-project-defaults").WaitForAsync();
        await page.GetByTestId("project-structure-party-editor").GetByText(seed.CustomerName, new LocatorGetByTextOptions
        {
            Exact = false
        }).WaitForAsync();
        await page.GetByTestId("project-structure-party-editor").GetByText(seed.OwnerName, new LocatorGetByTextOptions
        {
            Exact = false
        }).WaitForAsync();
        await page.GetByTestId("project-structure-meeting-project-defaults").ClickAsync();
        await page.GetByText("3 selected", new PageGetByTextOptions
        {
            Exact = false
        }).WaitForAsync();
        await page.GetByTestId("project-structure-meeting-save").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("project-structure-party-editor-message"), "Meeting parties saved.");

        await page.GetByTestId(BuildOutlineNodeTestId(seed.WorkItemNodeId)).ClickAsync();
        await page.GetByTestId("project-structure-work-item-party").WaitForAsync();
        await page.GetByTestId("project-structure-work-item-party").SelectOptionAsync(seed.OwnerId.ToString());
        await page.GetByTestId("project-structure-work-item-save").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("project-structure-party-editor-message"), "Work-item assignee saved.");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-structure-b10-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1100, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-structure-b10-tablet.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/projects/{seed.ProjectId:D}/calendar");
        await page.WaitForURLAsync($"**/projects/{seed.ProjectId:D}/calendar");
        await page.WaitForTimeoutAsync(1500);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-calendar-b10-desktop.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<SeededProjectData> SeedProjectAsync(string suffix)
    {
        var activeProfile = CreateActiveProfile();
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Playwright.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var projectName = $"B10 Project {suffix}";
        var customerName = $"B10 Customer {suffix}";
        var deliveryUnitName = $"B10 Delivery {suffix}";
        var ownerName = $"B10 Owner {suffix}";
        var participantNodeTitle = $"B10 Participant {suffix}";
        var meetingNodeTitle = $"B10 Meeting {suffix}";
        var workItemNodeTitle = $"B10 Work Item {suffix}";

        var projectId = await CreateProjectAsync(projectsService, projectName);
        var customerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, customerName);
        var deliveryUnitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, deliveryUnitName);
        var ownerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, ownerName);

        var participantNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                participantNodeTitle,
                "Local participant",
                "Will be linked through the B10 editor.",
                $"project:{projectId}",
                420,
                240,
                ObjectSubtype: "freelancer",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Participant = new ProjectParticipantMetadata
                    {
                        ParticipantKind = ProjectParticipantKind.Freelancer,
                        Role = "Coordinator"
                    }
                })));
        var meetingNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Meeting,
                meetingNodeTitle,
                "Customer sync",
                "Meeting used by the B10 Playwright flow.",
                $"project:{projectId}",
                620,
                240,
                StartUtc: DateTimeOffset.UtcNow.AddDays(1),
                EndUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                ObjectSubtype: "online"));
        var workItemNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                workItemNodeTitle,
                "Follow-up",
                "Work item used by the B10 Playwright flow.",
                $"project:{projectId}",
                820,
                240,
                ObjectSubtype: "task",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    WorkItem = new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Task,
                        DeliveryChannel = ProjectMessageChannel.None
                    }
                })));

        return new SeededProjectData(
            projectId,
            projectName,
            customerId,
            customerName,
            deliveryUnitId,
            deliveryUnitName,
            ownerId,
            ownerName,
            participantNode.Id,
            participantNodeTitle,
            meetingNode.Id,
            meetingNodeTitle,
            workItemNode.Id,
            workItemNodeTitle);
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
            "playwright-seed",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.Sqlite,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, PartyType partyType, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "playwright-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task ExpectTextContainsAsync(ILocator locator, string expectedValue, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await locator.InnerTextAsync()).Contains(expectedValue, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for text '{expectedValue}'.");
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        try
        {
            await startupDialog.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
    }

    private static string BuildOutlineNodeTestId(string nodeId)
    {
        var buffer = new char[nodeId.Length];
        for (var index = 0; index < nodeId.Length; index++)
        {
            var character = nodeId[index];
            buffer[index] = char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-';
        }

        return $"project-structure-outline-node-{new string(buffer)}";
    }

    private sealed record SeededProjectData(
        Guid ProjectId,
        string ProjectName,
        Guid CustomerId,
        string CustomerName,
        Guid DeliveryUnitId,
        string DeliveryUnitName,
        Guid OwnerId,
        string OwnerName,
        string ParticipantNodeId,
        string ParticipantNodeTitle,
        string MeetingNodeId,
        string MeetingNodeTitle,
        string WorkItemNodeId,
        string WorkItemNodeTitle);
}
