using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiDocumentationPipelineTests(ApiDocumentationDocumentFixture fixture)
    : IClassFixture<ApiDocumentationDocumentFixture>
{
    private const string TaskUpdatePath = "/api/project-structure/projects/{projectId}/tasks/{taskId}";

    [Fact]
    public void Both_document_routes_serve_the_same_document()
    {
        Assert.NotEmpty(fixture.OpenApiBytes);
        Assert.Equal(fixture.OpenApiBytes, fixture.SwaggerBytes);
    }

    [Fact]
    public void Named_handler_comments_document_the_operation_parameters_body_and_every_response()
    {
        var operation = fixture.Operation(TaskUpdatePath, "put");

        Assert.Equal(
            "Update a canonical project task using the task state previously read by the caller.",
            Text(operation["summary"]));
        Assert.Contains("Read, modify, write:", Text(operation["description"]), StringComparison.Ordinal);
        Assert.Contains("1. Call `POST /api/project-structure/projects/{projectId}/structure/read`", Text(operation["description"]), StringComparison.Ordinal);
        Assert.Equal(new[] { "Project Structure" }, operation["tags"]!.AsArray().Select(tag => Text(tag)).ToArray());

        var parameters = operation["parameters"]!.AsArray().OfType<JsonObject>().ToDictionary(parameter => Text(parameter["name"]));
        Assert.Contains("`expectedProjectAdmission` must name this project", Text(parameters["projectId"]["description"]), StringComparison.Ordinal);
        Assert.Contains("not a GUID", Text(parameters["taskId"]["description"]), StringComparison.Ordinal);
        Assert.Contains("project write admission", Text(operation["requestBody"]!["description"]), StringComparison.Ordinal);

        var responses = operation["responses"]!.AsObject();
        Assert.Equal(new[] { "200", "400", "404", "409", "500" }, responses.Select(response => response.Key).Order().ToArray());
        Assert.StartsWith("The update was committed.", Text(responses["200"]!["description"]), StringComparison.Ordinal);
        Assert.Contains("ProjectLifetimeRefreshRequired", Text(responses["409"]!["description"]), StringComparison.Ordinal);
        foreach (var status in new[] { "400", "404", "409", "500" })
        {
            var error = responses[status]!["content"]!["application/json"]!["schema"]!.AsObject();
            Assert.Equal(new[] { "error" }, error["required"]!.AsArray().Select(member => Text(member)).ToArray());
            Assert.Contains("rejected or failed", Text(error["description"]), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void As_parameters_properties_document_the_query_parameters()
    {
        var operation = fixture.Operation("/api/crm-hr/parties", "get");
        var parameters = operation["parameters"]!.AsArray().OfType<JsonObject>().ToDictionary(parameter => Text(parameter["name"]));

        Assert.Equal(new[] { "IncludeArchived", "PageIndex", "PageSize", "Scope", "Search", "Tags" }, parameters.Keys.Order().ToArray());
        Assert.Contains("Zero-based", Text(parameters["PageIndex"]["description"]), StringComparison.Ordinal);
        Assert.Contains("1 through 100", Text(parameters["PageSize"]["description"]), StringComparison.Ordinal);
        Assert.Contains("flags value", Text(parameters["Scope"]["description"]), StringComparison.Ordinal);
        Assert.Contains("?Tags=vip&Tags=partner", Text(parameters["Tags"]["description"]), StringComparison.Ordinal);
        Assert.Equal(
            "#/components/schemas/PartyRecordPage",
            Text(operation["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]));
    }

    [Fact]
    public void Parameters_that_xml_comments_cannot_reach_keep_their_attribute_descriptions()
    {
        var update = fixture.Operation("/api/llm-chats/{definitionId}", "put");
        var parameters = update["parameters"]!.AsArray().OfType<JsonObject>().ToDictionary(parameter => Text(parameter["name"]));
        Assert.StartsWith("Strong ETag of the definition version you read", Text(parameters["If-Match"]["description"]), StringComparison.Ordinal);
        Assert.StartsWith("The complete new configuration", Text(update["requestBody"]!["description"]), StringComparison.Ordinal);

        var upload = fixture.Operation("/api/plugins/packages/upload", "post");
        var form = upload["requestBody"]!["content"]!["multipart/form-data"]!["schema"]!;
        Assert.StartsWith("The plugin package file (`.zip`), sent as the form field `file`.", Text(upload["requestBody"]!["description"]), StringComparison.Ordinal);
        Assert.Equal("The plugin package file (`.zip`). It must not be empty.", Text(form["properties"]!["file"]!["description"]));
        Assert.Equal("#/components/schemas/IFormFile", Text(form["properties"]!["file"]!["$ref"]));
    }

    [Fact]
    public void Positional_record_members_from_another_assembly_describe_their_role_at_each_reference()
    {
        var input = fixture.Schema("ProjectStructureTaskUpdateAgentInput");
        var properties = input["properties"]!.AsObject();

        Assert.Contains("Task update request", Text(input["description"]), StringComparison.Ordinal);
        Assert.Contains("TaskRouteMismatch", Text(properties["taskId"]!["description"]), StringComparison.Ordinal);
        Assert.Contains("-1 means progress is untracked", Text(properties["currentProgressPercent"]!["description"]), StringComparison.Ordinal);
        Assert.Contains("-1 (untracked) is not accepted", Text(properties["proposedProgressPercent"]!["description"]), StringComparison.Ordinal);

        var currentEstimate = properties["currentEstimate"]!.AsObject();
        var proposedEstimate = properties["proposedEstimate"]!.AsObject();
        Assert.Equal("#/components/schemas/ProjectTaskEstimate", Text(currentEstimate["$ref"]));
        Assert.Contains("caller's latest read", Text(currentEstimate["description"]), StringComparison.Ordinal);
        Assert.Contains("Requested estimate", Text(proposedEstimate["description"]), StringComparison.Ordinal);
        Assert.NotEqual(Text(fixture.Schema("ProjectTaskEstimate")["description"]), Text(currentEstimate["description"]));

        var estimate = fixture.Schema("ProjectTaskEstimate")["properties"]!.AsObject();
        Assert.Contains("stays in hours when `expectedEffortUnit` is ManDays", Text(estimate["expectedEffortHours"]!["description"]), StringComparison.Ordinal);
        Assert.Contains("not a lookup in a currency registry", Text(estimate["expectedCostCurrencyCode"]!["description"]), StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_members_keep_their_role_and_their_type_descriptions()
    {
        var properties = fixture.Schema("ProjectStructureTaskUpdateAgentInput")["properties"]!.AsObject();
        var costBasis = properties["currentCostBasis"]!["oneOf"]!.AsArray().OfType<JsonObject>().Single(branch => branch["$ref"] is not null);
        var admission = properties["expectedProjectAdmission"]!["oneOf"]!.AsArray().OfType<JsonObject>().Single(branch => branch["$ref"] is not null);

        Assert.Contains("must always be present", Text(costBasis["description"]), StringComparison.Ordinal);
        Assert.Contains("sent back unchanged", Text(admission["description"]), StringComparison.Ordinal);
        Assert.Contains("Owner-issued evidence", Text(fixture.Schema("ProjectWriteAdmission")["description"]), StringComparison.Ordinal);
        Assert.Contains("bit flags value", Text(fixture.Schema("PartyRecordScope")["description"]), StringComparison.Ordinal);
        Assert.Contains("camel-case string", Text(fixture.Schema("AgentProviderFailureCategory")["description"]), StringComparison.Ordinal);

        // A struct component first generated from an optional member keeps the descriptions of its own properties.
        var credential = fixture.Schema("ManagedCredentialId");
        Assert.Equal("Identifier of a managed API credential issued by this host.", Text(credential["description"]));
        Assert.Equal("The credential's GUID.", Text(credential["properties"]!["value"]!["description"]));
    }

    [Fact]
    public void Inlined_project_structure_responses_keep_property_and_external_type_descriptions()
    {
        var success = fixture.Operation(TaskUpdatePath, "put")["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.AsObject();
        var affectedTaskIds = success["properties"]!["affectedTaskIds"]!.AsObject();

        Assert.Null(success["$ref"]);
        Assert.Contains("Committed result", Text(success["description"]), StringComparison.Ordinal);
        Assert.Contains("`value` member", Text(affectedTaskIds["description"]), StringComparison.Ordinal);
        Assert.Contains("String node identifier of the task", Text(affectedTaskIds["items"]!["properties"]!["value"]!["description"]), StringComparison.Ordinal);
    }

    [Fact]
    public void External_types_have_reviewed_boundary_descriptions()
    {
        Assert.Contains("RFC 9457", Text(fixture.Schema("ProblemDetails")["description"]), StringComparison.Ordinal);
        Assert.Contains("Any JSON value", Text(fixture.Schema("JsonElement")["description"]), StringComparison.Ordinal);
        Assert.Contains("multipart/form-data", Text(fixture.Schema("IFormFile")["description"]), StringComparison.Ordinal);
        Assert.Contains("`Content-Type` header", Text(fixture.Schema("Stream")["description"]), StringComparison.Ordinal);
        Assert.Contains("3 SetInterval", Text(fixture.Schema("GanttScheduleGesture")["description"]), StringComparison.Ordinal);
    }

    [Fact]
    public void Descriptions_are_platform_independent_markdown()
    {
        var descriptions = new List<string>();
        Collect(fixture.Document, descriptions);

        Assert.NotEmpty(descriptions);
        Assert.DoesNotContain(descriptions, text => text.Contains('\r'));
        Assert.DoesNotContain(descriptions, text => text.Contains("&amp;", StringComparison.Ordinal) || text.Contains("&lt;", StringComparison.Ordinal) || text.Contains("&gt;", StringComparison.Ordinal));
        Assert.DoesNotContain(descriptions, text => text.Split('\n').Any(line => line.Length > 0 && char.IsWhiteSpace(line[0])));
        Assert.DoesNotContain(descriptions, text => text != text.Trim());
    }

    private static void Collect(JsonNode? node, List<string> descriptions)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (name, value) in jsonObject)
                {
                    if (name is "description" or "summary" && value is JsonValue text && text.TryGetValue<string>(out var content))
                    {
                        descriptions.Add(content);
                        continue;
                    }

                    Collect(value, descriptions);
                }

                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    Collect(item, descriptions);
                }

                break;
        }
    }

    private static string Text(JsonNode? node)
        => node?.GetValue<string>() ?? string.Empty;
}
