using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureProjectObjectTypeHttpHardeningTests
{
    private const string ObjectTypeErrorCode = "ProjectStructureObjectTypeInvalid";
    private const string ContentTypeErrorCode = "ProjectStructureContentTypeUnsupported";
    private const string BodyTooLargeErrorCode = "ProjectStructureRequestBodyTooLarge";
    private const string InvalidRequestErrorCode = "ProjectStructureRequestInvalid";
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private static readonly string[] InvalidObjectTypeTokens =
    [
        "\"UnknownProjectObjectType\"",
        "\"1\"",
        "999",
        "2147483648",
        "true",
        "null"
    ];

    [Fact]
    public async Task Node_create_and_edit_preserve_supported_alias_and_subtype_normalization()
    {
        await using var host = await CreateHostAsync("object-type-alias-normalization");
        var fixture = await CreateFixtureAsync(host.Client);

        var feature = await CreateNodeFromRawKindAsync(
            host.Client,
            fixture,
            "FeatureBlock",
            objectSubtype: null,
            "Inferred feature");
        Assert.Equal(ProjectObjectType.ProjectBlock, feature.ObjectType);
        Assert.Equal("feature", feature.ObjectSubtype);

        var folder = await CreateNodeFromRawKindAsync(
            host.Client,
            fixture,
            "FolderNode",
            objectSubtype: null,
            "Inferred folder");
        Assert.Equal(ProjectObjectType.Repository, folder.ObjectType);
        Assert.Equal("folder", folder.ObjectSubtype);

        var editedFeature = await EditNodeFromRawKindAsync(
            host.Client,
            fixture,
            feature.Id,
            "FeatureBlock",
            "ArchitectureBlock",
            "Explicit architecture");
        Assert.Equal(ProjectObjectType.ProjectBlock, editedFeature.ObjectType);
        Assert.Equal("architecture", editedFeature.ObjectSubtype);
    }

    [Fact]
    public void Node_alias_json_converters_preserve_managed_asset_alias_normalization()
    {
        var create = JsonSerializer.Deserialize<ProjectStructureNodeCreateInput>(
            """
            {
              "objectType": "MarkdownFile",
              "objectSubtype": "xlsx",
              "title": "Spreadsheet",
              "subtitle": "Alias converter",
              "notes": "No mutation is performed.",
              "parentNodeKey": "project:test"
            }
            """,
            ProjectStructureHttpContractTestJson.SerializerOptions) ??
            throw new InvalidOperationException("The node-create alias payload was not read.");
        Assert.Equal(ProjectObjectType.File, create.ObjectType);
        Assert.Equal("excel", create.ObjectSubtype);

        var edit = JsonSerializer.Deserialize<ProjectStructureNodeEditInput>(
            """
            {
              "objectType": "MarkdownFile",
              "objectSubtype": "xlsx",
              "title": "Spreadsheet",
              "subtitle": "Alias converter",
              "notes": "No mutation is performed."
            }
            """,
            ProjectStructureHttpContractTestJson.SerializerOptions) ??
            throw new InvalidOperationException("The node-edit alias payload was not read.");
        Assert.Equal(ProjectObjectType.File, edit.ObjectType);
        Assert.Equal("excel", edit.ObjectSubtype);
    }

    [Fact]
    public async Task Strict_routes_accept_case_insensitive_symbols_and_defined_numeric_values()
    {
        await using var host = await CreateHostAsync("object-type-strict-valid-values");
        var fixture = await CreateFixtureAsync(host.Client);
        var target = await CreateNodeFromRawKindAsync(
            host.Client,
            fixture,
            "ProjectBlock",
            "feature",
            "Strict target");

        await AssertSuccessAsync(await PostRawJsonAsync(
            host.Client,
            StructureReadPath(fixture),
            "{\"objectTypes\":[\"projectblock\"],\"includeAssets\":true}"));
        await AssertSuccessAsync(await PostRawJsonAsync(
            host.Client,
            StructureReadPath(fixture),
            $"{{\"objectTypes\":[{(int)ProjectObjectType.ProjectBlock}]}}"));

        await AssertSuccessAsync(await PostRawJsonAsync(
            host.Client,
            ChecklistPath(fixture),
            "{\"objectTypes\":[\"projectblock\"],\"take\":10}"));
        await AssertSuccessAsync(await PostRawJsonAsync(
            host.Client,
            ChecklistPath(fixture),
            $"{{\"objectTypes\":[{(int)ProjectObjectType.ProjectBlock}],\"take\":10}}"));

        using (var symbolicTypeResponse = await PostRawJsonAsync(
                   host.Client,
                   NodeTypePath(fixture, target.Id),
                   JsonSerializer.Serialize(new
                   {
                       objectType = "projectblock",
                       objectSubtype = "feature",
                       leaseToken = fixture.LeaseToken
                   })))
        {
            var summary = await ReadNodeSummaryAsync(symbolicTypeResponse);
            Assert.Equal(ProjectObjectType.ProjectBlock, summary.ObjectType);
            Assert.Equal("feature", summary.ObjectSubtype);
        }

        using (var numericTypeResponse = await PostRawJsonAsync(
                   host.Client,
                   NodeTypePath(fixture, target.Id),
                   JsonSerializer.Serialize(new
                   {
                       objectType = (int)ProjectObjectType.ProjectBlock,
                       objectSubtype = "feature",
                       leaseToken = fixture.LeaseToken
                   })))
        {
            var summary = await ReadNodeSummaryAsync(numericTypeResponse);
            Assert.Equal(ProjectObjectType.ProjectBlock, summary.ObjectType);
        }

        using (var symbolicAssetResponse = await PostRawJsonAsync(
                   host.Client,
                   AssetCreatePath(fixture),
                   AssetBody(fixture, "file", "case-insensitive.md", "text/markdown", "IyBDYXNl")))
        {
            var summary = await ReadNodeSummaryAsync(symbolicAssetResponse);
            Assert.Equal(ProjectObjectType.File, summary.ObjectType);
        }

        using (var numericAssetResponse = await PostRawJsonAsync(
                   host.Client,
                   AssetCreatePath(fixture),
                   AssetBody(
                       fixture,
                       (int)ProjectObjectType.ImageAsset,
                       "numeric.png",
                       "image/png",
                       TinyPngBase64)))
        {
            var summary = await ReadNodeSummaryAsync(numericAssetResponse);
            Assert.Equal(ProjectObjectType.ImageAsset, summary.ObjectType);
        }
    }

    [Fact]
    public async Task Every_enum_body_rejects_invalid_tokens_with_one_typed_400_without_mutation()
    {
        await using var host = await CreateHostAsync("object-type-invalid-matrix");
        var fixture = await CreateFixtureAsync(host.Client);
        var target = await CreateNodeFromRawKindAsync(
            host.Client,
            fixture,
            "ProjectBlock",
            "feature",
            "Unchanged target");
        var before = await CaptureNodeStateAsync(host.Client, fixture);

        foreach (var endpoint in Enum.GetValues<EnumBodyEndpoint>())
        {
            foreach (var invalidToken in InvalidObjectTypeTokens)
            {
                if (endpoint == EnumBodyEndpoint.NodeEdit && invalidToken == "null")
                {
                    continue;
                }

                using var response = await SendEnumBodyAsync(
                    host.Client,
                    fixture,
                    target.Id,
                    endpoint,
                    BuildEndpointBody(fixture, endpoint, invalidToken));
                var body = await response.Content.ReadAsStringAsync();

                Assert.True(
                    response.StatusCode == HttpStatusCode.BadRequest,
                    $"{endpoint} accepted {invalidToken}. Body: {body}");
                AssertTypedError(body, ObjectTypeErrorCode);
            }
        }

        var after = await CaptureNodeStateAsync(host.Client, fixture);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Required_object_type_properties_reject_missing_values_without_mutation()
    {
        await using var host = await CreateHostAsync("object-type-required-property");
        var fixture = await CreateFixtureAsync(host.Client);
        var target = await CreateNodeFromRawKindAsync(
            host.Client,
            fixture,
            "ProjectBlock",
            "feature",
            "Required-property target");
        var before = await CaptureNodeStateAsync(host.Client, fixture);

        foreach (var endpoint in new[]
                 {
                     EnumBodyEndpoint.NodeCreate,
                     EnumBodyEndpoint.NodeType,
                     EnumBodyEndpoint.AssetCreate
                 })
        {
            var body = JsonNode.Parse(BuildEndpointBody(
                fixture,
                endpoint,
                endpoint == EnumBodyEndpoint.AssetCreate
                    ? "\"File\""
                    : "\"ProjectBlock\""))!.AsObject();
            Assert.True(body.Remove("objectType"));

            using var response = await SendEnumBodyAsync(
                host.Client,
                fixture,
                target.Id,
                endpoint,
                body.ToJsonString());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            AssertTypedError(
                await response.Content.ReadAsStringAsync(),
                ObjectTypeErrorCode);
        }

        var after = await CaptureNodeStateAsync(host.Client, fixture);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Case_insensitive_duplicate_object_type_properties_are_rejected_as_ambiguous()
    {
        await using var host = await CreateHostAsync("object-type-duplicate-property");
        var fixture = await CreateFixtureAsync(host.Client);
        var before = await CaptureNodeStateAsync(host.Client, fixture);
        var nodeBody = BuildEndpointBody(
            fixture,
            EnumBodyEndpoint.NodeCreate,
            "\"ProjectBlock\"");
        nodeBody = nodeBody.Replace(
            "\"objectType\":\"ProjectBlock\"",
            "\"objectType\":\"ProjectBlock\",\"ObjectType\":\"ProjectBlock\"",
            StringComparison.Ordinal);
        Assert.Contains("\"ObjectType\":", nodeBody, StringComparison.Ordinal);

        using (var nodeResponse = await SendEnumBodyAsync(
                   host.Client,
                   fixture,
                   fixture.RootNodeId,
                   EnumBodyEndpoint.NodeCreate,
                   nodeBody))
        {
            Assert.Equal(HttpStatusCode.BadRequest, nodeResponse.StatusCode);
            AssertTypedError(
                await nodeResponse.Content.ReadAsStringAsync(),
                InvalidRequestErrorCode);
        }

        using (var readResponse = await PostRawJsonAsync(
                   host.Client,
                   StructureReadPath(fixture),
                   "{\"objectTypes\":[\"ProjectBlock\"],\"ObjectTypes\":[\"ProjectBlock\"]}"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, readResponse.StatusCode);
            AssertTypedError(
                await readResponse.Content.ReadAsStringAsync(),
                InvalidRequestErrorCode);
        }

        var after = await CaptureNodeStateAsync(host.Client, fixture);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Node_edit_accepts_explicit_null_object_type_without_reclassification()
    {
        await using var host = await CreateHostAsync("object-type-null-edit");
        var fixture = await CreateFixtureAsync(host.Client);
        var target = await CreateNodeFromRawKindAsync(
            host.Client,
            fixture,
            "FeatureBlock",
            "feature",
            "Null edit target");

        using var response = await PutRawJsonAsync(
            host.Client,
            NodeEditPath(fixture, target.Id),
            JsonSerializer.Serialize(new
            {
                title = "Null edit accepted",
                subtitle = "Optional object type",
                notes = "An explicit null preserves the current classification.",
                objectType = (string?)null,
                objectSubtype = target.ObjectSubtype,
                leaseToken = fixture.LeaseToken
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await ReadNodeSummaryAsync(response);
        Assert.Equal(ProjectObjectType.ProjectBlock, edited.ObjectType);
        Assert.Equal("feature", edited.ObjectSubtype);
        Assert.Equal("Null edit accepted", edited.Title);
    }

    [Fact]
    public async Task Nullable_filter_collections_accept_explicit_null_as_no_filter()
    {
        await using var host = await CreateHostAsync("object-type-null-filter-collections");
        var fixture = await CreateFixtureAsync(host.Client);

        using (var structureResponse = await PostRawJsonAsync(
                   host.Client,
                   StructureReadPath(fixture),
                   "{\"objectTypes\":null,\"includeAssets\":true}"))
        {
            Assert.Equal(HttpStatusCode.OK, structureResponse.StatusCode);
        }

        using (var checklistResponse = await PostRawJsonAsync(
                   host.Client,
                   ChecklistPath(fixture),
                   "{\"objectTypes\":null,\"take\":10}"))
        {
            Assert.Equal(HttpStatusCode.OK, checklistResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Every_enum_body_rejects_missing_or_unsupported_content_type_without_mutation()
    {
        await using var host = await CreateHostAsync("object-type-content-type-matrix");
        var fixture = await CreateFixtureAsync(host.Client);
        var target = await CreateNodeFromRawKindAsync(
            host.Client,
            fixture,
            "ProjectBlock",
            "feature",
            "Content type target");
        var before = await CaptureNodeStateAsync(host.Client, fixture);

        foreach (var endpoint in Enum.GetValues<EnumBodyEndpoint>())
        {
            var body = BuildEndpointBody(
                fixture,
                endpoint,
                endpoint == EnumBodyEndpoint.AssetCreate
                    ? "\"File\""
                    : "\"ProjectBlock\"");

            using var unsupportedContent = new StringContent(
                body,
                Encoding.UTF8,
                "text/plain");
            using var unsupportedResponse = await SendEnumBodyAsync(
                host.Client,
                fixture,
                target.Id,
                endpoint,
                unsupportedContent);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupportedResponse.StatusCode);
            AssertTypedError(
                await unsupportedResponse.Content.ReadAsStringAsync(),
                ContentTypeErrorCode);

            using var missingContentType = new StringContent(body, Encoding.UTF8);
            missingContentType.Headers.ContentType = null;
            using var missingResponse = await SendEnumBodyAsync(
                host.Client,
                fixture,
                target.Id,
                endpoint,
                missingContentType);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, missingResponse.StatusCode);
            AssertTypedError(
                await missingResponse.Content.ReadAsStringAsync(),
                ContentTypeErrorCode);
        }

        using var structuredJsonContent = new StringContent(
            "{\"objectTypes\":[\"ProjectBlock\"]}",
            Encoding.UTF8,
            "application/vnd.candoitall.project-structure+json");
        using var structuredJsonResponse = await host.Client.PostAsync(
            StructureReadPath(fixture),
            structuredJsonContent);
        Assert.Equal(HttpStatusCode.OK, structuredJsonResponse.StatusCode);

        var after = await CaptureNodeStateAsync(host.Client, fixture);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Unknown_length_body_over_route_limit_returns_typed_413_without_mutation()
    {
        await using var host = await CreateHostAsync("object-type-bounded-body");
        var fixture = await CreateFixtureAsync(host.Client);
        var before = await CaptureNodeStateAsync(host.Client, fixture);
        var oversizedJson =
            "{\"objectTypes\":[\"ProjectBlock\"],\"padding\":\"" +
            new string('x', 300 * 1024) +
            "\"}";

        using var content = new UnknownLengthJsonContent(oversizedJson);
        using var response = await host.Client.PostAsync(
            StructureReadPath(fixture),
            content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        AssertTypedError(
            await response.Content.ReadAsStringAsync(),
            BodyTooLargeErrorCode);
        var after = await CaptureNodeStateAsync(host.Client, fixture);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Every_project_object_type_response_family_emits_canonical_symbols()
    {
        await using var host = await CreateHostAsync("object-type-response-families");
        var fixture = await CreateFixtureAsync(host.Client);

        using (var catalog = await host.Client.GetAsync("/api/project-structure/node-catalog"))
        {
            await AssertCanonicalObjectTypesAsync(catalog);
        }

        var node = await CreateNodeFromRawKindAsync(
            host.Client,
            fixture,
            "FeatureBlock",
            null,
            "Response family node");

        using (var edit = await PutRawJsonAsync(
                   host.Client,
                   NodeEditPath(fixture, node.Id),
                   JsonSerializer.Serialize(new
                   {
                       title = "Edited response family node",
                       subtitle = "Edited",
                       notes = "Edited",
                       objectType = "FeatureBlock",
                       leaseToken = fixture.LeaseToken
                   })))
        {
            await AssertCanonicalObjectTypesAsync(edit);
        }

        using (var type = await PostRawJsonAsync(
                   host.Client,
                   NodeTypePath(fixture, node.Id),
                   JsonSerializer.Serialize(new
                   {
                       objectType = "ProjectBlock",
                       objectSubtype = "feature",
                       leaseToken = fixture.LeaseToken
                   })))
        {
            await AssertCanonicalObjectTypesAsync(type);
        }

        var start = DateTimeOffset.UtcNow.AddHours(1);
        using (var task = await host.Client.PostAsJsonAsync(
                   $"/api/project-structure/projects/{fixture.ProjectId:D}/tasks",
                   new ProjectStructureTaskCreateRequest(
                       "Response family task",
                       start,
                       start.AddHours(1))))
        {
            task.EnsureSuccessStatusCode();
        }

        using (var checklist = await PostRawJsonAsync(
                   host.Client,
                   ChecklistPath(fixture),
                   "{\"objectTypes\":[\"WorkItem\"],\"take\":10}"))
        {
            await AssertCanonicalObjectTypesAsync(checklist);
        }

        using (var dependencies = await host.Client.PostAsJsonAsync(
                   $"/api/project-structure/projects/{fixture.ProjectId:D}/dependencies/query",
                   new { take = 10 }))
        {
            await AssertCanonicalObjectTypesAsync(dependencies);
        }

        ProjectStructureNodeSummary asset;
        using (var assetCreate = await PostRawJsonAsync(
                   host.Client,
                   AssetCreatePath(fixture),
                   AssetBody(fixture, "File", "response.md", "text/markdown", "IyBSZXNwb25zZQ==")))
        {
            asset = await ReadNodeSummaryAsync(assetCreate);
        }

        using (var assetRead = await host.Client.GetAsync(
                   $"/api/project-structure/projects/{fixture.ProjectId:D}/assets/{Uri.EscapeDataString(asset.Id)}"))
        {
            await AssertCanonicalObjectTypesAsync(assetRead);
        }

        using (var structureRead = await PostRawJsonAsync(
                   host.Client,
                   StructureReadPath(fixture),
                   "{\"includeAssets\":true}"))
        {
            await AssertCanonicalObjectTypesAsync(structureRead);
        }
    }

    [Fact]
    public void Node_create_and_edit_openapi_dtos_match_runtime_contracts()
    {
        AssertOpenApiDtoParity(
            typeof(ProjectStructureNodeCreateInput),
            "CanDoItAll.Web.ProjectStructureNodeCreateOpenApiRequest");
        AssertOpenApiDtoParity(
            typeof(ProjectStructureNodeEditInput),
            "CanDoItAll.Web.ProjectStructureNodeEditOpenApiRequest");
    }

    [Fact]
    public async Task Default_http_json_contract_remains_numeric_outside_project_structure_boundary()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        var serializerOptions = host.App.Services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value
            .SerializerOptions;
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(ProjectObjectType.ImageAsset, serializerOptions));
        Assert.Equal(JsonValueKind.Number, document.RootElement.ValueKind);
        Assert.Equal((int)ProjectObjectType.ImageAsset, document.RootElement.GetInt32());
    }

    private static Task<ProjectStructureAgentApiTestHost> CreateHostAsync(string key)
        => ProjectStructureAgentApiTestHost.CreateAsync(
            key,
            environment => environment.CreatePostgreSqlProfile(key));

    private static async Task<Fixture> CreateFixtureAsync(HttpClient client)
    {
        using var projectResponse = await client.PostAsJsonAsync(
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "ProjectObjectType HTTP hardening",
                "Exercise every enum-bearing HTTP body.",
                "Keep HTTP conversion isolated from MAF and persistence JSON.",
                "Validation",
                ProjectStatus.Active));
        projectResponse.EnsureSuccessStatusCode();
        using var project = JsonDocument.Parse(await projectResponse.Content.ReadAsStringAsync());
        var projectId = project.RootElement.GetProperty("id").GetGuid();

        using var leaseResponse = await client.PostAsJsonAsync(
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                "Verify the HTTP enum boundary",
                30));
        leaseResponse.EnsureSuccessStatusCode();
        using var lease = JsonDocument.Parse(await leaseResponse.Content.ReadAsStringAsync());
        return new Fixture(
            projectId,
            $"project:{projectId:D}",
            lease.RootElement.GetProperty("leaseToken").GetString()!);
    }

    private static async Task<ProjectStructureNodeSummary> CreateNodeFromRawKindAsync(
        HttpClient client,
        Fixture fixture,
        object objectType,
        string? objectSubtype,
        string title)
    {
        using var response = await PostRawJsonAsync(
            client,
            NodeCreatePath(fixture),
            JsonSerializer.Serialize(new
            {
                objectType,
                title,
                subtitle = "HTTP enum boundary",
                notes = "Alias and canonicalization coverage.",
                parentNodeKey = fixture.RootNodeId,
                objectSubtype,
                leaseToken = fixture.LeaseToken
            }));
        return await ReadNodeSummaryAsync(response);
    }

    private static async Task<ProjectStructureNodeSummary> EditNodeFromRawKindAsync(
        HttpClient client,
        Fixture fixture,
        string nodeId,
        object objectType,
        string? objectSubtype,
        string title)
    {
        using var response = await PutRawJsonAsync(
            client,
            NodeEditPath(fixture, nodeId),
            JsonSerializer.Serialize(new
            {
                title,
                subtitle = "HTTP enum boundary edit",
                notes = "Explicit subtype normalization coverage.",
                objectType,
                objectSubtype,
                leaseToken = fixture.LeaseToken
            }));
        return await ReadNodeSummaryAsync(response);
    }

    private static async Task<ProjectStructureNodeSummary> ReadNodeSummaryAsync(
        HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ProjectStructureNodeSummary>(
                body,
                ProjectStructureHttpContractTestJson.SerializerOptions)
            ?? throw new InvalidOperationException("No node summary was returned.");
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, body);
        }
    }

    private static async Task<HttpResponseMessage> SendEnumBodyAsync(
        HttpClient client,
        Fixture fixture,
        string targetNodeId,
        EnumBodyEndpoint endpoint,
        string json)
        => await SendEnumBodyAsync(
            client,
            fixture,
            targetNodeId,
            endpoint,
            new StringContent(json, Encoding.UTF8, "application/json"));

    private static async Task<HttpResponseMessage> SendEnumBodyAsync(
        HttpClient client,
        Fixture fixture,
        string targetNodeId,
        EnumBodyEndpoint endpoint,
        HttpContent content)
    {
        var request = new HttpRequestMessage(
            endpoint == EnumBodyEndpoint.NodeEdit ? HttpMethod.Put : HttpMethod.Post,
            EndpointPath(fixture, targetNodeId, endpoint))
        {
            Content = content
        };
        return await client.SendAsync(request);
    }

    private static string BuildEndpointBody(
        Fixture fixture,
        EnumBodyEndpoint endpoint,
        string objectTypeToken)
    {
        var body = JsonNode.Parse(endpoint switch
        {
            EnumBodyEndpoint.StructureRead => "{\"includeAssets\":true}",
            EnumBodyEndpoint.NodeCreate => JsonSerializer.Serialize(new
            {
                title = "Rejected node",
                subtitle = "Rejected",
                notes = "Must not mutate.",
                parentNodeKey = fixture.RootNodeId,
                objectSubtype = "feature",
                leaseToken = fixture.LeaseToken
            }),
            EnumBodyEndpoint.NodeEdit => JsonSerializer.Serialize(new
            {
                title = "Rejected edit",
                subtitle = "Rejected",
                notes = "Must not mutate.",
                objectSubtype = "feature",
                leaseToken = fixture.LeaseToken
            }),
            EnumBodyEndpoint.NodeType => JsonSerializer.Serialize(new
            {
                objectSubtype = "feature",
                leaseToken = fixture.LeaseToken
            }),
            EnumBodyEndpoint.ChecklistQuery => "{\"take\":10}",
            EnumBodyEndpoint.AssetCreate => AssetBody(
                fixture,
                "File",
                "rejected.md",
                "text/markdown",
                "cmVqZWN0ZWQ="),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null)
        })!.AsObject();
        var token = JsonNode.Parse(objectTypeToken);
        if (endpoint is EnumBodyEndpoint.StructureRead or EnumBodyEndpoint.ChecklistQuery)
        {
            var values = new JsonArray();
            values.Add(token);
            body["objectTypes"] = values;
        }
        else
        {
            body["objectType"] = token;
        }

        return body.ToJsonString();
    }

    private static string AssetBody(
        Fixture fixture,
        object objectType,
        string fileName,
        string contentType,
        string base64Data)
        => JsonSerializer.Serialize(new
        {
            objectType,
            title = fileName,
            subtitle = "HTTP enum asset",
            notes = "Asset contract coverage.",
            media = new
            {
                fileName,
                contentType,
                base64Data
            },
            parentNodeKey = fixture.RootNodeId,
            objectSubtype = contentType == "image/png" ? "generated" : "markdown",
            leaseToken = fixture.LeaseToken
        });

    private static async Task<IReadOnlyList<NodeState>> CaptureNodeStateAsync(
        HttpClient client,
        Fixture fixture)
    {
        using var response = await PostRawJsonAsync(
            client,
            StructureReadPath(fixture),
            "{\"includeAssets\":true,\"includeNotes\":true,\"includeMetadata\":true}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("nodes")
            .EnumerateArray()
            .Select(node => new NodeState(
                node.GetProperty("id").GetString()!,
                node.GetProperty("parentId").ValueKind == JsonValueKind.Null
                    ? null
                    : node.GetProperty("parentId").GetString(),
                node.GetProperty("objectType").GetString()!,
                node.GetProperty("objectSubtype").GetString()!,
                node.GetProperty("title").GetString()!,
                node.GetProperty("subtitle").GetString()!,
                node.GetProperty("status").GetString()!,
                node.GetProperty("notes").ValueKind == JsonValueKind.Null
                    ? null
                    : node.GetProperty("notes").GetString(),
                node.GetProperty("metadataJson").ValueKind == JsonValueKind.Null
                    ? null
                    : node.GetProperty("metadataJson").GetString()))
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertTypedError(string body, string expectedErrorCode)
    {
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            expectedErrorCode,
            document.RootElement.GetProperty("error").GetProperty("errorCode").GetString());
        Assert.DoesNotContain("JsonException", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertCanonicalObjectTypesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        var values = new List<JsonElement>();
        CollectObjectTypeValues(document.RootElement, values);
        Assert.NotEmpty(values);
        var supported = Enum.GetNames<ProjectObjectType>().ToHashSet(StringComparer.Ordinal);
        foreach (var value in values)
        {
            Assert.Equal(JsonValueKind.String, value.ValueKind);
            Assert.Contains(value.GetString()!, supported);
        }
    }

    private static void CollectObjectTypeValues(
        JsonElement element,
        ICollection<JsonElement> values)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "objectType", StringComparison.Ordinal))
                {
                    values.Add(property.Value);
                }

                CollectObjectTypeValues(property.Value, values);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectObjectTypeValues(item, values);
            }
        }
    }

    private static void AssertOpenApiDtoParity(
        Type runtimeType,
        string openApiTypeName)
    {
        var openApiType = typeof(ProjectStructureAgentApi).Assembly.GetType(openApiTypeName)
            ?? throw new InvalidOperationException($"OpenAPI DTO '{openApiTypeName}' was not found.");
        var runtimeProperties = runtimeType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);
        var openApiProperties = openApiType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);
        var expectedNames = runtimeProperties.Keys
            .Append("Metadata")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedNames,
            openApiProperties.Keys.OrderBy(name => name, StringComparer.Ordinal));

        var nullability = new NullabilityInfoContext();
        foreach (var (name, runtimeProperty) in runtimeProperties)
        {
            var openApiProperty = openApiProperties[name];
            Assert.Equal(runtimeProperty.PropertyType, openApiProperty.PropertyType);
            Assert.Equal(
                nullability.Create(runtimeProperty).ReadState,
                nullability.Create(openApiProperty).ReadState);
        }

        Assert.Equal(typeof(JsonElement?), openApiProperties["Metadata"].PropertyType);
        Assert.NotEmpty(runtimeType.GetCustomAttributes<JsonConverterAttribute>());
    }

    private static Task<HttpResponseMessage> PostRawJsonAsync(
        HttpClient client,
        string path,
        string json)
        => client.PostAsync(
            path,
            new StringContent(json, Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> PutRawJsonAsync(
        HttpClient client,
        string path,
        string json)
        => client.PutAsync(
            path,
            new StringContent(json, Encoding.UTF8, "application/json"));

    private static string EndpointPath(
        Fixture fixture,
        string targetNodeId,
        EnumBodyEndpoint endpoint)
        => endpoint switch
        {
            EnumBodyEndpoint.StructureRead => StructureReadPath(fixture),
            EnumBodyEndpoint.NodeCreate => NodeCreatePath(fixture),
            EnumBodyEndpoint.NodeEdit => NodeEditPath(fixture, targetNodeId),
            EnumBodyEndpoint.NodeType => NodeTypePath(fixture, targetNodeId),
            EnumBodyEndpoint.ChecklistQuery => ChecklistPath(fixture),
            EnumBodyEndpoint.AssetCreate => AssetCreatePath(fixture),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null)
        };

    private static string StructureReadPath(Fixture fixture)
        => $"/api/project-structure/projects/{fixture.ProjectId:D}/structure/read";

    private static string NodeCreatePath(Fixture fixture)
        => $"/api/project-structure/projects/{fixture.ProjectId:D}/nodes";

    private static string NodeEditPath(Fixture fixture, string nodeId)
        => $"/api/project-structure/projects/{fixture.ProjectId:D}/nodes/{Uri.EscapeDataString(nodeId)}";

    private static string NodeTypePath(Fixture fixture, string nodeId)
        => $"{NodeEditPath(fixture, nodeId)}/type";

    private static string ChecklistPath(Fixture fixture)
        => $"/api/project-structure/projects/{fixture.ProjectId:D}/checklists/query";

    private static string AssetCreatePath(Fixture fixture)
        => $"/api/project-structure/projects/{fixture.ProjectId:D}/assets";

    private enum EnumBodyEndpoint
    {
        StructureRead,
        NodeCreate,
        NodeEdit,
        NodeType,
        ChecklistQuery,
        AssetCreate
    }

    private sealed record Fixture(
        Guid ProjectId,
        string RootNodeId,
        string LeaseToken);

    private sealed record NodeState(
        string Id,
        string? ParentId,
        string ObjectType,
        string ObjectSubtype,
        string Title,
        string Subtitle,
        string Status,
        string? Notes,
        string? MetadataJson);

    private sealed class UnknownLengthJsonContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthJsonContent(string json)
        {
            _bytes = Encoding.UTF8.GetBytes(json);
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
            => stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
