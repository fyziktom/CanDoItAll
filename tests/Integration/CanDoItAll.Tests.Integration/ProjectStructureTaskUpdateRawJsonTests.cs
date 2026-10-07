using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

// Exercises the documented task update workflow the way a client without the product's C# types does: every request is
// literal JSON, and every value is taken from the JSON the owner returned, including the metadataJson string.
public sealed class ProjectStructureTaskUpdateRawJsonTests(ProjectStructureTaskUpdateRawJsonTests.HostFixture fixture)
    : IClassFixture<ProjectStructureTaskUpdateRawJsonTests.HostFixture>
{
    private static readonly Dictionary<string, int> ExecutionStates = new(StringComparer.Ordinal)
    {
        ["unknown"] = 0,
        ["notStarted"] = 1,
        ["started"] = 2,
        ["completed"] = 3,
        ["cancelled"] = 4
    };

    private static readonly Dictionary<string, int> EffortUnits = new(StringComparer.Ordinal)
    {
        ["hours"] = 0,
        ["manDays"] = 1
    };

    private static readonly Dictionary<string, int> ResourceKinds = new(StringComparer.Ordinal)
    {
        ["person"] = 0,
        ["agent"] = 1,
        ["workflow"] = 2,
        ["process"] = 3
    };

    private static readonly Dictionary<string, int> CostSources = new(StringComparer.Ordinal)
    {
        ["unknown"] = 0,
        ["crmWorkforceRate"] = 1,
        ["agentRunHistory"] = 2,
        ["workflowRunHistory"] = 3,
        ["processRunHistory"] = 4
    };

    private HttpClient Client => fixture.Host.Client;

    [Fact]
    public async Task Read_modify_write_changes_only_the_intended_fields()
    {
        var task = await CreateProjectWithTaskAsync("Read modify write");
        Assert.Equal("notStarted", task.WorkItem["executionState"]?.GetValue<string>());
        Assert.Equal("hours", task.WorkItem["expectedEffortUnit"]?.GetValue<string>());
        Assert.Null(task.WorkItem["expectedCostBasis"]);

        var body = UnchangedUpdate(task);
        body["proposedTitle"] = "Read modify write, renamed";
        body["proposedProgressPercent"] = 40;

        using var response = await PutTaskAsync(task, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadObjectAsync(response);
        Assert.Equal(task.TaskId, result["affectedTaskIds"]![0]!["value"]!.GetValue<string>());
        Assert.Equal(0, result["addedDependencyCount"]!.GetValue<int>());
        Assert.Equal(0, result["removedDependencyCount"]!.GetValue<int>());

        var stored = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Equal("Read modify write, renamed", stored.Title);
        Assert.Equal(40, stored.ProgressPercent);
        Assert.Equal(task.StartUtc, stored.StartUtc);
        Assert.Equal(task.EndUtc, stored.EndUtc);
        Assert.Equal(task.WorkItem.ToJsonString(), stored.WorkItem.ToJsonString());
    }

    [Fact]
    public async Task Reschedule_with_plain_string_identifiers_persists_the_proposed_interval()
    {
        var task = await CreateProjectWithTaskAsync("Reschedule");
        const string proposedStart = "2026-08-03T09:00:00+00:00";
        const string proposedEnd = "2026-08-04T17:00:00+00:00";
        var body = UnchangedUpdate(task);
        body["scheduleChange"] = new JsonObject
        {
            ["gesture"] = 3,
            ["affectedTasks"] = new JsonArray(new JsonObject
            {
                ["taskId"] = task.TaskId,
                ["previousStart"] = task.StartUtc,
                ["previousEnd"] = task.EndUtc,
                ["proposedStart"] = proposedStart,
                ["proposedEnd"] = proposedEnd
            })
        };

        using var response = await PutTaskAsync(task, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Equal(DateTimeOffset.Parse(proposedStart), DateTimeOffset.Parse(Assert.IsType<string>(stored.StartUtc)));
        Assert.Equal(DateTimeOffset.Parse(proposedEnd), DateTimeOffset.Parse(Assert.IsType<string>(stored.EndUtc)));
        Assert.Equal(task.Title, stored.Title);
    }

    [Fact]
    public async Task An_unknown_task_identifier_is_not_found()
    {
        var task = await CreateProjectWithTaskAsync("Unknown task");
        const string unknownTaskId = "custom:0f8fad5bd9cb469fa165708fc9e5b7a1";
        var body = UnchangedUpdate(task);
        body["taskId"] = unknownTaskId;

        using var response = await PutTaskAsync(task, body, routeTaskId: unknownTaskId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("WorkItemNotFound", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Route_and_body_task_identifiers_must_match_exactly()
    {
        var task = await CreateProjectWithTaskAsync("Route mismatch");
        var body = UnchangedUpdate(task);
        body["taskId"] = task.TaskId.ToUpperInvariant();
        body["proposedTitle"] = "Must not be stored";

        using var response = await PutTaskAsync(task, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("TaskRouteMismatch", await ReadErrorCodeAsync(response));
        Assert.Equal(task.Title, (await ReadTaskAsync(task.ProjectId, task.TaskId)).Title);
    }

    // Earlier published documents described task identifiers as { "value": ... } objects. Clients built from them must
    // switch to plain strings: the wrapper is rejected by the framework before the operation runs.
    [Fact]
    public async Task The_superseded_identifier_wrapper_is_rejected_without_changing_the_task()
    {
        var task = await CreateProjectWithTaskAsync("Superseded wrapper");
        var wrappedTask = UnchangedUpdate(task);
        wrappedTask["taskId"] = new JsonObject { ["value"] = task.TaskId };
        wrappedTask["proposedTitle"] = "Must not be stored";

        using var wrappedTaskResponse = await PutTaskAsync(task, wrappedTask);

        Assert.Equal(HttpStatusCode.BadRequest, wrappedTaskResponse.StatusCode);
        Assert.Null(await TryReadErrorCodeAsync(wrappedTaskResponse));

        var wrappedSchedule = UnchangedUpdate(task);
        var schedule = Reschedule(task, 3, "2026-08-18T09:00:00+00:00", "2026-08-18T17:00:00+00:00");
        schedule["affectedTasks"]![0]!["taskId"] = new JsonObject { ["value"] = task.TaskId };
        wrappedSchedule["scheduleChange"] = schedule;

        using var wrappedScheduleResponse = await PutTaskAsync(task, wrappedSchedule);

        Assert.Equal(HttpStatusCode.BadRequest, wrappedScheduleResponse.StatusCode);
        Assert.Null(await TryReadErrorCodeAsync(wrappedScheduleResponse));
        var stored = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Equal(task.Title, stored.Title);
        Assert.Equal(task.StartUtc, stored.StartUtc);
        Assert.Equal(task.EndUtc, stored.EndUtc);
    }

    [Fact]
    public async Task Current_cost_basis_must_be_present_and_may_be_null()
    {
        var task = await CreateProjectWithTaskAsync("Cost basis presence");
        var omitted = UnchangedUpdate(task);
        omitted.Remove("currentCostBasis");
        omitted["proposedTitle"] = "Must not be stored";

        using var rejected = await PutTaskAsync(task, omitted);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Null(await TryReadErrorCodeAsync(rejected));
        Assert.Equal(task.Title, (await ReadTaskAsync(task.ProjectId, task.TaskId)).Title);

        var explicitNull = UnchangedUpdate(task);
        Assert.True(explicitNull.ContainsKey("currentCostBasis"));
        Assert.Null(explicitNull["currentCostBasis"]);
        explicitNull["proposedTitle"] = "Explicit null cost basis";

        using var accepted = await PutTaskAsync(task, explicitNull);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("Explicit null cost basis", (await ReadTaskAsync(task.ProjectId, task.TaskId)).Title);
    }

    [Fact]
    public async Task The_admission_must_be_present_and_name_the_edited_project()
    {
        var task = await CreateProjectWithTaskAsync("Admission");
        var missing = UnchangedUpdate(task);
        missing.Remove("expectedProjectAdmission");
        missing["proposedTitle"] = "Must not be stored";

        using var missingResponse = await PutTaskAsync(task, missing);

        Assert.Equal(HttpStatusCode.Conflict, missingResponse.StatusCode);
        Assert.Equal("ProjectLifetimeRefreshRequired", await ReadErrorCodeAsync(missingResponse));

        var otherProjectId = await CreateProjectAsync("Admission elsewhere");
        var otherStructure = await ReadStructureAsync(otherProjectId);
        var wrongProject = UnchangedUpdate(task);
        wrongProject["expectedProjectAdmission"] = otherStructure["expectedProjectAdmission"]!.DeepClone();
        wrongProject["proposedTitle"] = "Must not be stored";

        using var wrongResponse = await PutTaskAsync(task, wrongProject);

        Assert.Equal(HttpStatusCode.Conflict, wrongResponse.StatusCode);
        Assert.Equal("ProjectLifetimeRefreshRequired", await ReadErrorCodeAsync(wrongResponse));
        Assert.Equal(task.Title, (await ReadTaskAsync(task.ProjectId, task.TaskId)).Title);
    }

    [Fact]
    public async Task Stale_preconditions_are_rejected_without_changing_the_task()
    {
        var task = await CreateProjectWithTaskAsync("Stale preconditions");

        var staleTitle = UnchangedUpdate(task);
        staleTitle["currentTitle"] = "A title the caller never read";
        staleTitle["proposedTitle"] = "Must not be stored";
        using var staleTitleResponse = await PutTaskAsync(task, staleTitle);
        Assert.Equal(HttpStatusCode.Conflict, staleTitleResponse.StatusCode);
        Assert.Equal("StaleTask", await ReadErrorCodeAsync(staleTitleResponse));

        var staleRevision = UnchangedUpdate(task);
        staleRevision["currentDirectAssignmentRevision"] = task.DirectAssignmentRevision + 3;
        staleRevision["proposedTitle"] = "Must not be stored";
        using var staleRevisionResponse = await PutTaskAsync(task, staleRevision);
        Assert.Equal(HttpStatusCode.Conflict, staleRevisionResponse.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadErrorCodeAsync(staleRevisionResponse));

        var staleEstimate = UnchangedUpdate(task);
        staleEstimate["currentEstimate"]!["expectedEffortHours"] = 7;
        staleEstimate["proposedTitle"] = "Must not be stored";
        using var staleEstimateResponse = await PutTaskAsync(task, staleEstimate);
        Assert.Equal(HttpStatusCode.Conflict, staleEstimateResponse.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadErrorCodeAsync(staleEstimateResponse));

        var stored = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Equal(task.Title, stored.Title);
        Assert.Equal(task.WorkItem.ToJsonString(), stored.WorkItem.ToJsonString());
    }

    [Fact]
    public async Task A_direct_assignee_is_ignored_unless_changed_and_can_be_set_and_cleared()
    {
        var task = await CreateProjectWithTaskAsync("Direct assignee");
        var personId = await CreatePersonPartyAsync("Synthetic Assignee One");

        var ignored = UnchangedUpdate(task);
        ignored["assigneeChanged"] = false;
        ignored["proposedAssignee"] = PersonSelection(personId);
        using var ignoredResponse = await PutTaskAsync(task, ignored);
        Assert.Equal(HttpStatusCode.OK, ignoredResponse.StatusCode);
        var afterIgnored = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Equal(task.DirectAssignmentRevision, afterIgnored.DirectAssignmentRevision);

        var assign = UnchangedUpdate(afterIgnored);
        assign["assigneeChanged"] = true;
        assign["proposedAssignee"] = PersonSelection(personId);
        using var assignResponse = await PutTaskAsync(afterIgnored, assign);
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        var assigned = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Equal(afterIgnored.DirectAssignmentRevision + 1, assigned.DirectAssignmentRevision);
        Assert.Equal("Synthetic Assignee One", assigned.WorkItem["assigneePartyName"]?.GetValue<string>());

        var clear = UnchangedUpdate(assigned);
        clear["assigneeChanged"] = true;
        clear["proposedAssignee"] = null;
        using var clearResponse = await PutTaskAsync(assigned, clear);
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var cleared = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Equal(assigned.DirectAssignmentRevision + 1, cleared.DirectAssignmentRevision);
        Assert.True(string.IsNullOrEmpty(cleared.WorkItem["assigneePartyName"]?.GetValue<string>()));
    }

    [Theory]
    [InlineData("proposed-progress-untracked", 400, "InvalidRequest")]
    [InlineData("gesture-out-of-range", 400, "TaskUpdateRequestInvalid")]
    [InlineData("gesture-as-text", 400, null)]
    [InlineData("inverted-interval", 400, "TaskUpdateRequestInvalid")]
    [InlineData("zero-effort", 400, "InvalidRequest")]
    [InlineData("invalid-currency", 400, "InvalidRequest")]
    [InlineData("transition-to-unknown", 400, "InvalidRequest")]
    [InlineData("started-without-start", 400, "InvalidRequest")]
    [InlineData("workflow-as-direct-assignee", 400, "InvalidRequest")]
    public async Task Invalid_values_are_rejected_before_any_change(string invalidValue, int expectedStatus, string? expectedErrorCode)
    {
        var task = await CreateProjectWithTaskAsync($"Invalid {invalidValue}");
        var body = UnchangedUpdate(task);
        body["proposedTitle"] = "Must not be stored";
        switch (invalidValue)
        {
            case "proposed-progress-untracked":
                body["proposedProgressPercent"] = -1;
                break;
            case "gesture-out-of-range":
                body["scheduleChange"] = Reschedule(task, 9, "2026-08-03T09:00:00+00:00", "2026-08-04T17:00:00+00:00");
                break;
            case "gesture-as-text":
                body["scheduleChange"] = Reschedule(task, "SetInterval", "2026-08-03T09:00:00+00:00", "2026-08-04T17:00:00+00:00");
                break;
            case "inverted-interval":
                body["scheduleChange"] = Reschedule(task, 3, "2026-08-04T17:00:00+00:00", "2026-08-03T09:00:00+00:00");
                break;
            case "zero-effort":
                body["proposedEstimate"]!["expectedEffortHours"] = 0;
                break;
            case "invalid-currency":
                body["proposedEstimate"]!["expectedCostAmount"] = 125.5;
                body["proposedEstimate"]!["expectedCostCurrencyCode"] = "EU";
                break;
            case "transition-to-unknown":
                body["proposedExecution"]!["state"] = 0;
                break;
            case "started-without-start":
                body["proposedExecution"]!["state"] = 2;
                break;
            case "workflow-as-direct-assignee":
                body["assigneeChanged"] = true;
                body["proposedAssignee"] = new JsonObject
                {
                    ["kind"] = 2,
                    ["resourceId"] = "5a0a3f1e-3c5e-4d52-9d7b-3b7a8f2f0c11"
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidValue), invalidValue, null);
        }

        using var response = await PutTaskAsync(task, body);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal(expectedErrorCode, await TryReadErrorCodeAsync(response));
        Assert.Equal(task.Title, (await ReadTaskAsync(task.ProjectId, task.TaskId)).Title);
    }

    private static JsonObject UnchangedUpdate(TaskState task)
    {
        var estimate = new JsonObject
        {
            ["expectedEffortHours"] = task.WorkItem["expectedEffortHours"]?.DeepClone(),
            ["expectedEffortUnit"] = EffortUnits[task.WorkItem["expectedEffortUnit"]?.GetValue<string>() ?? "hours"],
            ["expectedCostAmount"] = task.WorkItem["expectedCostAmount"]?.DeepClone(),
            ["expectedCostCurrencyCode"] = task.WorkItem["expectedCostCurrencyCode"]?.GetValue<string>() ?? string.Empty
        };
        var execution = new JsonObject
        {
            ["state"] = ExecutionStates[task.WorkItem["executionState"]?.GetValue<string>() ?? "unknown"],
            ["actualStartedAtUtc"] = task.WorkItem["actualStartedAtUtc"]?.DeepClone(),
            ["actualEndedAtUtc"] = task.WorkItem["actualEndedAtUtc"]?.DeepClone()
        };
        return new JsonObject
        {
            ["taskId"] = task.TaskId,
            ["currentTitle"] = task.Title,
            ["proposedTitle"] = task.Title,
            ["currentProgressPercent"] = task.ProgressPercent,
            ["proposedProgressPercent"] = Math.Clamp(task.ProgressPercent, 0, 100),
            ["currentEstimate"] = estimate,
            ["proposedEstimate"] = estimate.DeepClone(),
            ["scheduleChange"] = null,
            ["assigneeChanged"] = false,
            ["proposedAssignee"] = null,
            ["currentExecution"] = execution,
            ["proposedExecution"] = execution.DeepClone(),
            ["currentCostBasis"] = CostBasis(task.WorkItem["expectedCostBasis"]),
            ["currentDirectAssignmentRevision"] = task.DirectAssignmentRevision,
            ["expectedProjectAdmission"] = task.Admission.DeepClone()
        };
    }

    private static JsonNode? CostBasis(JsonNode? metadataBasis)
        => metadataBasis is not JsonObject basis
            ? null
            : new JsonObject
            {
                ["resourceKind"] = ResourceKinds[basis["resourceKind"]!.GetValue<string>()],
                ["resourceId"] = basis["resourceId"]!.DeepClone(),
                ["resourceVersionId"] = basis["resourceVersionId"]?.DeepClone(),
                ["source"] = CostSources[basis["source"]!.GetValue<string>()],
                ["calculatedAtUtc"] = basis["calculatedAtUtc"]?.DeepClone()
            };

    private static JsonObject Reschedule(TaskState task, JsonNode gesture, string proposedStart, string proposedEnd)
        => new()
        {
            ["gesture"] = gesture,
            ["affectedTasks"] = new JsonArray(new JsonObject
            {
                ["taskId"] = task.TaskId,
                ["previousStart"] = task.StartUtc,
                ["previousEnd"] = task.EndUtc,
                ["proposedStart"] = proposedStart,
                ["proposedEnd"] = proposedEnd
            })
        };

    private static JsonObject PersonSelection(string personId)
        => new()
        {
            ["kind"] = 0,
            ["resourceId"] = personId
        };

    [Fact]
    public async Task Imported_task_can_receive_its_first_schedule_and_rejects_a_stale_second_initialization() {
        var task = await CreateImportedTaskAsync();
        Assert.Null(task.StartUtc);
        Assert.Null(task.EndUtc);
        var body = UnchangedUpdate(task);
        body["initialSchedule"] = InitialSchedule(task);
        using var response = await PutTaskAsync(task, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var stored = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Equal(DateTimeOffset.Parse("2027-02-05T09:00:00Z"), DateTimeOffset.Parse(Assert.IsType<string>(stored.StartUtc)));
        Assert.Equal(DateTimeOffset.Parse("2027-02-05T10:00:00Z"), DateTimeOffset.Parse(Assert.IsType<string>(stored.EndUtc)));
        var stale = UnchangedUpdate(stored);
        stale["initialSchedule"] = body["initialSchedule"]!.DeepClone();
        stale["proposedTitle"] = "Must not be stored";
        using var rejected = await PutTaskAsync(stored, stale);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("StaleTask", await ReadErrorCodeAsync(rejected));
        Assert.Equal(stored.Title, (await ReadTaskAsync(task.ProjectId, task.TaskId)).Title);
    }

    [Theory]
    [InlineData("both-modes")]
    [InlineData("inverted")]
    [InlineData("missing-snapshot")]
    public async Task Invalid_initial_schedule_does_not_mutate_the_imported_task(string scenario) {
        var task = await CreateImportedTaskAsync();
        var body = UnchangedUpdate(task);
        var initial = InitialSchedule(task);
        body["initialSchedule"] = initial;
        body["proposedTitle"] = "Must not be stored";
        switch (scenario) {
            case "both-modes":
                body["scheduleChange"] = Reschedule(task with { StartUtc = "2027-02-05T09:00:00Z", EndUtc = "2027-02-05T10:00:00Z" },
                    3, "2027-02-06T09:00:00Z", "2027-02-06T10:00:00Z");
                break;
            case "inverted":
                initial["proposedEndUtc"] = "2027-02-04T10:00:00Z";
                break;
            case "missing-snapshot":
                initial.Remove("currentDurationSeconds");
                break;
        }
        using var rejected = await PutTaskAsync(task, body);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var unchanged = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Null(unchanged.StartUtc);
        Assert.Null(unchanged.EndUtc);
        Assert.Equal(task.Title, unchanged.Title);
    }

    private static JsonObject InitialSchedule(TaskState task) => new() {
        ["currentStartUtc"] = task.StartUtc,
        ["currentEndUtc"] = task.EndUtc,
        ["currentDurationSeconds"] = task.DurationSeconds,
        ["proposedStartUtc"] = "2027-02-05T09:00:00Z",
        ["proposedEndUtc"] = "2027-02-05T10:00:00Z"
    };

    [Fact]
    public async Task Initial_schedule_respects_persisted_dependencies_without_partial_title_updates() {
        var task = await CreateImportedTaskAsync();
        using var predecessorResponse = await SendJsonAsync(HttpMethod.Post, $"/api/project-structure/projects/{task.ProjectId}/tasks", new JsonObject {
            ["title"] = "Synthetic prerequisite",
            ["startUtc"] = "2027-02-06T09:00:00Z",
            ["endUtc"] = "2027-02-06T10:00:00Z",
            ["expectedProjectAdmission"] = task.Admission.DeepClone()
        });
        Assert.True(predecessorResponse.IsSuccessStatusCode, await predecessorResponse.Content.ReadAsStringAsync());
        var predecessorId = (await ReadObjectAsync(predecessorResponse))["taskNodeId"]!.GetValue<string>();
        using var link = await SendJsonAsync(HttpMethod.Post, $"/api/project-structure/projects/{task.ProjectId}/links", new JsonObject {
            ["sourceNodeId"] = task.TaskId,
            ["targetNodeId"] = predecessorId,
            ["kind"] = 1
        });
        Assert.True(link.IsSuccessStatusCode, await link.Content.ReadAsStringAsync());
        var body = UnchangedUpdate(await ReadTaskAsync(task.ProjectId, task.TaskId));
        body["initialSchedule"] = InitialSchedule(task);
        body["proposedTitle"] = "Must not be stored";
        using var rejected = await PutTaskAsync(task, body);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("InvalidSchedule", await ReadErrorCodeAsync(rejected));
        var unchanged = await ReadTaskAsync(task.ProjectId, task.TaskId);
        Assert.Null(unchanged.StartUtc);
        Assert.Null(unchanged.EndUtc);
        Assert.Equal(task.Title, unchanged.Title);
    }

    private async Task<TaskState> CreateImportedTaskAsync() {
        var projectId = await CreateProjectAsync("Initial schedule from JSON outline");
        using var response = await SendJsonAsync(HttpMethod.Post, "/api/project-structure/imports", new JsonObject {
            ["projectId"] = projectId,
            ["parentNodeKey"] = $"project:{projectId}",
            ["sourceKind"] = 3,
            ["title"] = "Synthetic stay template",
            ["sourceText"] = "[{\"title\":\"Pre-arrival\",\"children\":[{\"title\":\"Confirm pickup\"}]}]"
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var structure = await ReadStructureAsync(projectId);
        var task = Assert.Single(structure["nodes"]!.AsArray().OfType<JsonObject>(), node =>
            node["objectType"]!.GetValue<string>() == "WorkItem" && node["objectSubtype"]!.GetValue<string>() == "task");
        return await ReadTaskAsync(projectId, task["id"]!.GetValue<string>());
    }

    private async Task<TaskState> CreateProjectWithTaskAsync(string title)
    {
        var projectId = await CreateProjectAsync(title);
        var structure = await ReadStructureAsync(projectId);
        using var created = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/project-structure/projects/{projectId}/tasks",
            new JsonObject
            {
                ["title"] = title,
                ["startUtc"] = "2026-07-27T09:00:00+00:00",
                ["endUtc"] = "2026-07-27T17:00:00+00:00",
                ["estimate"] = new JsonObject
                {
                    ["expectedEffortHours"] = 6,
                    ["expectedEffortUnit"] = 0,
                    ["expectedCostAmount"] = 240,
                    ["expectedCostCurrencyCode"] = "usd"
                },
                ["expectedProjectAdmission"] = structure["expectedProjectAdmission"]!.DeepClone()
            });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var taskId = (await ReadObjectAsync(created))["taskNodeId"]!.GetValue<string>();
        return await ReadTaskAsync(projectId, taskId);
    }

    private async Task<string> CreateProjectAsync(string name)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/project-structure/projects",
            new JsonObject
            {
                ["name"] = $"{name} {Guid.NewGuid():N}",
                ["description"] = "Synthetic project for the raw JSON task update contract.",
                ["objective"] = "Exercise the documented task update workflow.",
                ["currentPhase"] = "Validation",
                ["status"] = 1
            });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await ReadObjectAsync(response))["id"]!.GetValue<string>();
    }

    private async Task<string> CreatePersonPartyAsync(string displayName)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/crm-hr/parties",
            new JsonObject
            {
                ["partyType"] = 0,
                ["lifecycleStatus"] = 1,
                ["displayName"] = displayName
            });
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, content);
        return JsonNode.Parse(content)!.GetValue<string>();
    }

    private async Task<JsonObject> ReadStructureAsync(string projectId)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/project-structure/projects/{projectId}/structure/read",
            new JsonObject { ["includeMetadata"] = true });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await ReadObjectAsync(response);
    }

    private async Task<TaskState> ReadTaskAsync(string projectId, string taskId)
    {
        var structure = await ReadStructureAsync(projectId);
        var node = structure["nodes"]!.AsArray().OfType<JsonObject>()
            .Single(candidate => candidate["id"]!.GetValue<string>() == taskId);
        var metadata = JsonNode.Parse(node["metadataJson"]!.GetValue<string>())!.AsObject();
        var workItem = metadata["workItem"]!.AsObject();
        return new TaskState(
            projectId,
            taskId,
            node["title"]!.GetValue<string>(),
            node["progressPercent"]!.GetValue<int>(),
            node["startUtc"]?.GetValue<string>(),
            node["endUtc"]?.GetValue<string>(),
            workItem,
            workItem["directAssignmentRevision"]?.GetValue<long>() ?? 0,
            structure["expectedProjectAdmission"]!.AsObject(),
            node["durationSeconds"]?.GetValue<int>());
    }

    private Task<HttpResponseMessage> PutTaskAsync(TaskState task, JsonObject body, string? routeTaskId = null)
        => SendJsonAsync(
            HttpMethod.Put,
            $"/api/project-structure/projects/{task.ProjectId}/tasks/{Uri.EscapeDataString(routeTaskId ?? task.TaskId)}",
            body);

    private Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, JsonNode body)
        => Client.SendAsync(new HttpRequestMessage(method, path)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        });

    private static async Task<JsonObject> ReadObjectAsync(HttpResponseMessage response)
        => JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
        => await TryReadErrorCodeAsync(response)
            ?? throw new InvalidOperationException(
                $"The response has no Project Structure error envelope: {await response.Content.ReadAsStringAsync()}");

    private static async Task<string?> TryReadErrorCodeAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonNode.Parse(content) is JsonObject envelope &&
                   envelope["error"] is JsonObject error &&
                   error["errorCode"] is JsonValue code
                ? code.GetValue<string>()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record TaskState(
        string ProjectId,
        string TaskId,
        string Title,
        int ProgressPercent,
        string? StartUtc,
        string? EndUtc,
        JsonObject WorkItem,
        long DirectAssignmentRevision,
        JsonObject Admission,
        int? DurationSeconds);

    public sealed class HostFixture : IAsyncLifetime
    {
        internal ProjectStructureAgentApiTestHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
            => Host = await ProjectStructureAgentApiTestHost.CreateAsync(
                "project-structure-task-update-raw-json",
                environment => environment.CreatePostgreSqlProfile("task-update-raw-json"));

        public async Task DisposeAsync()
            => await Host.DisposeAsync();
    }
}
