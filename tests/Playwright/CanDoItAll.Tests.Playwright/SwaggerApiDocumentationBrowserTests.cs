using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.ApiDocumentation;

// Proves that the XML documentation reaches the rendered Swagger UI: operation, parameter, request-body, response and
// nested model descriptions, and that the documented task update works from Swagger's Try it out on synthetic data.
[Collection(PlaywrightCollection.Name)]
public sealed class SwaggerApiDocumentationBrowserTests(PlaywrightAppFixture fixture)
{
    private const string TaskUpdatePath = "/api/project-structure/projects/{projectId}/tasks/{taskId}";
    private const string PartyListPath = "/api/crm-hr/parties";

    [Fact]
    public async Task Task_update_operation_shows_its_documentation_and_nested_model_descriptions()
    {
        await using var context = await NewContextAsync();
        var page = await OpenSwaggerAsync(context);
        var operation = await ExpandOperationAsync(page, "put", TaskUpdatePath);

        await Assertions.Expect(operation.Locator(".opblock-summary-description"))
            .ToHaveTextAsync("Update a canonical project task using the task state previously read by the caller.");
        await Assertions.Expect(operation.Locator(".opblock-description-wrapper").First)
            .ToContainTextAsync("Read, modify, write:");
        await Assertions.Expect(operation.Locator("tr[data-param-name='taskId']"))
            .ToContainTextAsync("not a GUID in general");
        await Assertions.Expect(operation.Locator(".opblock-section-request-body"))
            .ToContainTextAsync("project write admission");
        await Assertions.Expect(operation.Locator("tr.response[data-code='409']"))
            .ToContainTextAsync("ProjectLifetimeRefreshRequired");
        await CaptureAsync(operation, "task-update-operation.png");

        var model = await ExpandModelAsync(page, "ProjectStructureTaskUpdateAgentInput");
        await Assertions.Expect(model).ToContainTextAsync("Every current member is an edit precondition");
        await Assertions.Expect(model).ToContainTextAsync("This member must always be present: send null");
        await Assertions.Expect(model).ToContainTextAsync("-1 (untracked) is not accepted here");
        await CaptureAsync(model, "task-update-request-model.png");

        var estimate = await ExpandModelAsync(page, "ProjectTaskEstimate");
        await Assertions.Expect(estimate).ToContainTextAsync("stays in hours when");
        await Assertions.Expect(estimate).ToContainTextAsync("not a lookup in a currency registry");
        await CaptureAsync(estimate, "task-estimate-model.png");

        var error = await ExpandModelAsync(page, "ProjectStructureErrorResponse");
        await Assertions.Expect(error).ToContainTextAsync("single error object");
        await CaptureAsync(error, "project-structure-error-model.png");
    }

    [Fact]
    public async Task Party_list_shows_query_parameter_and_page_model_descriptions()
    {
        await using var context = await NewContextAsync();
        var page = await OpenSwaggerAsync(context);
        var operation = await ExpandOperationAsync(page, "get", PartyListPath);

        await Assertions.Expect(operation.Locator(".opblock-summary-description"))
            .ToContainTextAsync("List CRM/HR parties");
        await Assertions.Expect(operation.Locator("tr[data-param-name='PageIndex']"))
            .ToContainTextAsync("Zero-based page number");
        await Assertions.Expect(operation.Locator("tr[data-param-name='Scope']"))
            .ToContainTextAsync("flags value");
        await Assertions.Expect(operation.Locator("tr.response[data-code='400']"))
            .ToContainTextAsync("crmhr.party.query-invalid");
        await CaptureAsync(operation, "party-list-operation.png");

        var page2 = await ExpandModelAsync(page, "PartyRecordPage");
        await Assertions.Expect(page2).ToContainTextAsync("ordered by display name");
        await CaptureAsync(page2, "party-page-model.png");
    }

    [Fact]
    public async Task Task_update_try_it_out_changes_only_the_intended_fields_of_a_synthetic_task()
    {
        using var client = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };
        var (projectId, taskId, body) = await CreateSyntheticTaskUpdateAsync(client);
        var before = await ReadTaskNodeAsync(client, projectId, taskId);
        body["proposedTitle"] = "Swagger try-it-out rename";
        body["proposedProgressPercent"] = 30;

        await using var context = await NewContextAsync();
        var page = await OpenSwaggerAsync(context);
        var operation = await ExpandOperationAsync(page, "put", TaskUpdatePath);
        await operation.Locator(".try-out__btn").ClickAsync();
        await operation.Locator("tr[data-param-name='projectId'] input").FillAsync(projectId);
        await operation.Locator("tr[data-param-name='taskId'] input").FillAsync(taskId);
        await operation.Locator("textarea.body-param__text").FillAsync(body.ToJsonString());
        await operation.Locator("button.execute").ClickAsync();

        var liveStatus = operation.Locator(".live-responses-table tbody .response-col_status").First;
        await Assertions.Expect(liveStatus).ToContainTextAsync("200", new() { Timeout = 30000 });
        await CaptureAsync(operation, "task-update-try-it-out.png");

        var readBack = await ReadTaskNodeAsync(client, projectId, taskId);
        Assert.Equal("Swagger try-it-out rename", readBack["title"]!.GetValue<string>());
        Assert.Equal(30, readBack["progressPercent"]!.GetValue<int>());
        Assert.Equal(before["startUtc"]!.GetValue<string>(), readBack["startUtc"]!.GetValue<string>());
        Assert.Equal(before["endUtc"]!.GetValue<string>(), readBack["endUtc"]!.GetValue<string>());
    }

    private async Task<IBrowserContext> NewContextAsync()
        => await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1680, Height = 950 }
        });

    private async Task<IPage> OpenSwaggerAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        var failedRequests = new List<string>();
        page.RequestFailed += (_, request) => failedRequests.Add(request.Url);
        var response = await page.GotoAsync($"{fixture.BaseUrl}/swagger/index.html");
        Assert.True(response?.Ok, $"Swagger UI returned {(int?)response?.Status}.");
        await page.Locator(".opblock").First.WaitForAsync(new() { Timeout = 60000 });
        Assert.Empty(failedRequests);
        return page;
    }

    private static async Task<ILocator> ExpandOperationAsync(IPage page, string method, string path)
    {
        var operation = page.Locator($".opblock-{method}")
            .Filter(new() { Has = page.Locator($".opblock-summary-path[data-path='{path}']") });
        await operation.Locator(".opblock-summary-control").ClickAsync();
        await operation.Locator(".opblock-body").WaitForAsync();
        return operation;
    }

    // An OpenAPI 3.1 document renders its schemas with Swagger UI's JSON Schema 2020-12 components: each schema is a
    // top-level article whose head holds the title, and "Expand all" opens every nested property with its description.
    private static async Task<ILocator> ExpandModelAsync(IPage page, string schemaName)
    {
        var title = page
            .Locator("section.models article[data-json-schema-level='0'] > .json-schema-2020-12-head .json-schema-2020-12__title")
            .Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(schemaName)}$") });
        var model = title.Locator("xpath=ancestor::article[1]");
        await model.ScrollIntoViewIfNeededAsync();
        await model.Locator(".json-schema-2020-12-expand-deep-button").First.ClickAsync();
        await model.Locator(".json-schema-2020-12-keyword--properties").First.WaitForAsync();
        return model;
    }

    private static async Task CaptureAsync(ILocator element, string fileName)
    {
        if (Environment.GetEnvironmentVariable("CANDOITALL_PLAYWRIGHT_CAPTURE_EVIDENCE") != "true")
        {
            return;
        }

        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "swagger-api-documentation");
        Directory.CreateDirectory(directory);
        await element.ScreenshotAsync(new LocatorScreenshotOptions { Path = Path.Combine(directory, fileName) });
    }

    private static async Task<(string ProjectId, string TaskId, JsonObject Body)> CreateSyntheticTaskUpdateAsync(HttpClient client)
    {
        var project = await PostAsync(client, "/api/project-structure/projects", new JsonObject
        {
            ["name"] = $"Swagger documentation proof {Guid.NewGuid():N}",
            ["description"] = "Synthetic project for the Swagger try-it-out proof.",
            ["objective"] = "Exercise the documented task update from Swagger UI.",
            ["currentPhase"] = "Validation",
            ["status"] = 1
        });
        var projectId = project["id"]!.GetValue<string>();
        var structure = await ReadStructureAsync(client, projectId);
        var created = await PostAsync(client, $"/api/project-structure/projects/{projectId}/tasks", new JsonObject
        {
            ["title"] = "Swagger try-it-out task",
            ["startUtc"] = "2026-08-17T09:00:00+00:00",
            ["endUtc"] = "2026-08-17T17:00:00+00:00",
            ["expectedProjectAdmission"] = structure["expectedProjectAdmission"]!.DeepClone()
        });
        var taskId = created["taskNodeId"]!.GetValue<string>();
        var readStructure = await ReadStructureAsync(client, projectId);
        var node = readStructure["nodes"]!.AsArray().OfType<JsonObject>().Single(candidate => candidate["id"]!.GetValue<string>() == taskId);
        // metadataJson writes enums as camel-case text; the update body expects their documented integer values.
        var workItem = JsonNode.Parse(node["metadataJson"]!.GetValue<string>())!["workItem"]!.AsObject();
        Assert.Equal("hours", workItem["expectedEffortUnit"]?.GetValue<string>());
        Assert.Equal("notStarted", workItem["executionState"]?.GetValue<string>());
        Assert.Null(workItem["expectedCostBasis"]);
        var estimate = new JsonObject
        {
            ["expectedEffortHours"] = workItem["expectedEffortHours"]?.DeepClone(),
            ["expectedEffortUnit"] = 0,
            ["expectedCostAmount"] = workItem["expectedCostAmount"]?.DeepClone(),
            ["expectedCostCurrencyCode"] = workItem["expectedCostCurrencyCode"]?.GetValue<string>() ?? string.Empty
        };
        var execution = new JsonObject
        {
            ["state"] = 1,
            ["actualStartedAtUtc"] = workItem["actualStartedAtUtc"]?.DeepClone(),
            ["actualEndedAtUtc"] = workItem["actualEndedAtUtc"]?.DeepClone()
        };
        var progress = node["progressPercent"]!.GetValue<int>();
        var body = new JsonObject
        {
            ["taskId"] = taskId,
            ["currentTitle"] = node["title"]!.GetValue<string>(),
            ["proposedTitle"] = node["title"]!.GetValue<string>(),
            ["currentProgressPercent"] = progress,
            ["proposedProgressPercent"] = Math.Clamp(progress, 0, 100),
            ["currentEstimate"] = estimate,
            ["proposedEstimate"] = estimate.DeepClone(),
            ["scheduleChange"] = null,
            ["assigneeChanged"] = false,
            ["proposedAssignee"] = null,
            ["currentExecution"] = execution,
            ["proposedExecution"] = execution.DeepClone(),
            ["currentCostBasis"] = null,
            ["currentDirectAssignmentRevision"] = workItem["directAssignmentRevision"]?.GetValue<long>() ?? 0,
            ["expectedProjectAdmission"] = readStructure["expectedProjectAdmission"]!.DeepClone()
        };
        return (projectId, taskId, body);
    }

    private static async Task<JsonObject> ReadTaskNodeAsync(HttpClient client, string projectId, string taskId)
        => (await ReadStructureAsync(client, projectId))["nodes"]!.AsArray().OfType<JsonObject>()
            .Single(candidate => candidate["id"]!.GetValue<string>() == taskId);

    private static Task<JsonObject> ReadStructureAsync(HttpClient client, string projectId)
        => PostAsync(client, $"/api/project-structure/projects/{projectId}/structure/read", new JsonObject
        {
            ["includeMetadata"] = true
        });

    private static async Task<JsonObject> PostAsync(HttpClient client, string path, JsonObject body)
    {
        using var response = await client.PostAsync(path, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, content);
        return JsonNode.Parse(content)!.AsObject();
    }
}
