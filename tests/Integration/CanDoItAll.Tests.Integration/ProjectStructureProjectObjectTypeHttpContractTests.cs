using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureProjectObjectTypeHttpContractTests
{
    private const string ObjectTypeErrorCode = "ProjectStructureObjectTypeInvalid";
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    [Fact]
    public async Task Node_and_asset_endpoints_round_trip_project_object_types_as_canonical_symbols()
    {
        await using var host = await CreateHostAsync("project-object-type-symbol-round-trip");
        var fixture = await CreateFixtureAsync(host.Client);

        using var nodeResponse = await PostRawJsonAsync(
            host.Client,
            $"/api/project-structure/projects/{fixture.ProjectId:D}/nodes",
            NodePayload("ProjectBlock", "Symbolic project block", fixture));
        Assert.Equal(HttpStatusCode.OK, nodeResponse.StatusCode);
        var nodeId = await AssertObjectTypeAsync(nodeResponse, "ProjectBlock");

        using var fileResponse = await PostRawJsonAsync(
            host.Client,
            $"/api/project-structure/projects/{fixture.ProjectId:D}/assets",
            AssetPayload(
                "File",
                "Symbolic file",
                "symbolic.md",
                "text/markdown",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("# Symbolic file")),
                fixture));
        await AssertStatusAsync(host, fileResponse, HttpStatusCode.OK);
        var fileId = await AssertObjectTypeAsync(fileResponse, "File");

        using var imageResponse = await PostRawJsonAsync(
            host.Client,
            $"/api/project-structure/projects/{fixture.ProjectId:D}/assets",
            AssetPayload(
                "ImageAsset",
                "Symbolic image",
                "symbolic.png",
                "image/png",
                TinyPngBase64,
                fixture));
        await AssertStatusAsync(host, imageResponse, HttpStatusCode.OK);
        var imageId = await AssertObjectTypeAsync(imageResponse, "ImageAsset");

        using var canonicalResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{fixture.ProjectId:D}/structure/read",
            new ProjectStructureReadRequest(IncludeAssets: true));
        Assert.Equal(HttpStatusCode.OK, canonicalResponse.StatusCode);
        using var canonical = JsonDocument.Parse(await canonicalResponse.Content.ReadAsStringAsync());
        var typesById = canonical.RootElement.GetProperty("nodes")
            .EnumerateArray()
            .ToDictionary(
                node => node.GetProperty("id").GetString()!,
                node => node.GetProperty("objectType").GetString()!);
        Assert.Equal("ProjectBlock", typesById[nodeId]);
        Assert.Equal("File", typesById[fileId]);
        Assert.Equal("ImageAsset", typesById[imageId]);

        await AssertAssetReadTypeAsync(host.Client, fixture.ProjectId, fileId, "File");
        await AssertAssetReadTypeAsync(host.Client, fixture.ProjectId, imageId, "ImageAsset");
    }

    [Fact]
    public async Task Defined_numeric_project_object_types_are_accepted_but_are_emitted_as_canonical_symbols()
    {
        await using var host = await CreateHostAsync("project-object-type-numeric-compatibility");
        var fixture = await CreateFixtureAsync(host.Client);

        using var nodeResponse = await PostRawJsonAsync(
            host.Client,
            $"/api/project-structure/projects/{fixture.ProjectId:D}/nodes",
            NodePayload((int)ProjectObjectType.ProjectBlock, "Numeric project block", fixture));
        Assert.Equal(HttpStatusCode.OK, nodeResponse.StatusCode);
        await AssertObjectTypeAsync(nodeResponse, "ProjectBlock");

        using var assetResponse = await PostRawJsonAsync(
            host.Client,
            $"/api/project-structure/projects/{fixture.ProjectId:D}/assets",
            AssetPayload(
                (int)ProjectObjectType.File,
                "Numeric file",
                "numeric.md",
                "text/markdown",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("# Numeric file")),
                fixture));
        await AssertStatusAsync(host, assetResponse, HttpStatusCode.OK);
        await AssertObjectTypeAsync(assetResponse, "File");
    }

    [Fact]
    public async Task OpenApi_documents_directional_request_and_response_contracts()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        using var openApiResponse = await host.Client.GetAsync("/openapi/v1.json");
        var openApiJson = await openApiResponse.Content.ReadAsStringAsync();
        Assert.True(openApiResponse.IsSuccessStatusCode, openApiJson);
        using var document = JsonDocument.Parse(openApiJson);
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        var nodeCreate = paths
            .GetProperty("/api/project-structure/projects/{projectId}/nodes")
            .GetProperty("post");
        var nodeEdit = paths
            .GetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}")
            .GetProperty("put");
        var nodeType = paths
            .GetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/type")
            .GetProperty("post");
        var assetCreate = paths
            .GetProperty("/api/project-structure/projects/{projectId}/assets")
            .GetProperty("post");
        var canonicalRead = paths
            .GetProperty("/api/project-structure/projects/{projectId}/structure/read")
            .GetProperty("post");
        var assetRead = paths
            .GetProperty("/api/project-structure/projects/{projectId}/assets/{nodeId}")
            .GetProperty("get");
        var assetContentRead = paths
            .GetProperty("/api/project-structure/projects/{projectId}/assets/{nodeId}/content")
            .GetProperty("get");
        var assetRevision = paths
            .GetProperty("/api/project-structure/projects/{projectId}/assets/{nodeId}/revisions")
            .GetProperty("post");
        var workflowAddOptions = paths
            .GetProperty("/api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow-add-options")
            .GetProperty("post");
        var nodeCatalog = paths
            .GetProperty("/api/project-structure/node-catalog")
            .GetProperty("get");
        var checklist = paths
            .GetProperty("/api/project-structure/projects/{projectId}/checklists/query")
            .GetProperty("post");
        var dependencies = paths
            .GetProperty("/api/project-structure/projects/{projectId}/dependencies/query")
            .GetProperty("post");

        foreach (var requestOperation in new[]
                 {
                     nodeCreate,
                     nodeEdit,
                     nodeType,
                     assetCreate,
                     canonicalRead,
                     checklist
                 })
        {
            var content = requestOperation
                .GetProperty("requestBody")
                .GetProperty("content");
            Assert.True(content.TryGetProperty("application/json", out _));
            Assert.True(content.TryGetProperty("application/*+json", out _));
            Assert.False(content.TryGetProperty("*/*", out _));
        }

        AssertObjectTypeInputSchema(
            root,
            GetPropertySchema(root, GetRequestSchema(nodeCreate), "objectType"),
            allowsNodeKindAliases: true,
            allowsNull: false);
        AssertObjectTypeInputSchema(
            root,
            GetPropertySchema(root, GetRequestSchema(nodeEdit), "objectType"),
            allowsNodeKindAliases: true,
            allowsNull: true);
        AssertObjectTypeInputSchema(
            root,
            GetPropertySchema(root, GetRequestSchema(nodeType), "objectType"),
            allowsNodeKindAliases: false,
            allowsNull: false);
        AssertObjectTypeInputSchema(
            root,
            GetPropertySchema(root, GetRequestSchema(assetCreate), "objectType"),
            allowsNodeKindAliases: false,
            allowsNull: false);
        AssertObjectTypeInputSchema(
            root,
            GetNullableArrayItemSchema(
                root,
                GetPropertySchema(root, GetRequestSchema(canonicalRead), "objectTypes")),
            allowsNodeKindAliases: false,
            allowsNull: false);
        AssertObjectTypeInputSchema(
            root,
            GetNullableArrayItemSchema(
                root,
                GetPropertySchema(root, GetRequestSchema(checklist), "objectTypes")),
            allowsNodeKindAliases: false,
            allowsNull: false);

        foreach (var responseOperation in new[]
                 {
                     nodeCatalog,
                     nodeCreate,
                     nodeEdit,
                     nodeType,
                     canonicalRead,
                     checklist,
                     dependencies,
                     assetCreate,
                     assetRead,
                     assetContentRead,
                     assetRevision,
                     workflowAddOptions
                 })
        {
            AssertResponseObjectTypesAreCanonical(
                root,
                GetResponseSchema(responseOperation));
        }

        var sharedObjectTypeSchema = root
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(nameof(ProjectObjectType));
        Assert.Equal(
            "integer",
            ResolveSchema(root, sharedObjectTypeSchema).GetProperty("type").GetString());
        foreach (var sharedResponseSchemaName in new[]
                 {
                     nameof(ProjectStructureNodeSummary),
                     nameof(ProjectStructureAssetDescriptor),
                     nameof(ProjectStructureAssetContentDescriptor),
                     nameof(ProjectStructureWorkflowAddOptionsResult)
                 })
        {
            AssertSharedResponseObjectTypesRemainNumeric(
                root,
                root.GetProperty("components")
                    .GetProperty("schemas")
                    .GetProperty(sharedResponseSchemaName));
        }
    }

    [Theory]
    [InlineData(false, "UnknownProjectObjectType")]
    [InlineData(false, "1")]
    [InlineData(false, 999)]
    [InlineData(true, "UnknownProjectObjectType")]
    [InlineData(true, "1")]
    [InlineData(true, 999)]
    public async Task Unknown_project_object_types_return_a_sanitized_typed_400_without_mutation(
        bool assetEndpoint,
        object objectType)
    {
        await using var host = await CreateHostAsync(
            $"project-object-type-invalid-{assetEndpoint}-{objectType}");
        var fixture = await CreateFixtureAsync(host.Client);
        var path = assetEndpoint
            ? $"/api/project-structure/projects/{fixture.ProjectId:D}/assets"
            : $"/api/project-structure/projects/{fixture.ProjectId:D}/nodes";
        var payload = assetEndpoint
            ? AssetPayload(
                objectType,
                "Rejected asset",
                "rejected.md",
                "text/markdown",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("rejected")),
                fixture)
            : NodePayload(objectType, "Rejected node", fixture);

        using var response = await PostRawJsonAsync(host.Client, path, payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var error = JsonDocument.Parse(body);
        Assert.Equal(
            ObjectTypeErrorCode,
            error.RootElement.GetProperty("error").GetProperty("errorCode").GetString());
        Assert.DoesNotContain("UnknownProjectObjectType", body, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonException", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);

        using var canonicalResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{fixture.ProjectId:D}/structure/read",
            new ProjectStructureReadRequest(IncludeAssets: true));
        Assert.Equal(HttpStatusCode.OK, canonicalResponse.StatusCode);
        using var canonical = JsonDocument.Parse(await canonicalResponse.Content.ReadAsStringAsync());
        Assert.Single(canonical.RootElement.GetProperty("nodes").EnumerateArray());
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
                "ProjectObjectType HTTP contract",
                "Verify one Project Structure HTTP enum contract.",
                "Keep HTTP serialization isolated from domain and MAF serialization.",
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
                "Verify ProjectObjectType HTTP contract",
                15));
        leaseResponse.EnsureSuccessStatusCode();
        using var lease = JsonDocument.Parse(await leaseResponse.Content.ReadAsStringAsync());
        return new Fixture(
            projectId,
            $"project:{projectId:D}",
            lease.RootElement.GetProperty("leaseToken").GetString()!);
    }

    private static object NodePayload(object objectType, string title, Fixture fixture)
        => new
        {
            objectType,
            title,
            subtitle = "HTTP contract node",
            notes = "Created by the R22 integration contract.",
            parentNodeKey = fixture.RootNodeId,
            objectSubtype = "architecture",
            leaseToken = fixture.LeaseToken
        };

    private static object AssetPayload(
        object objectType,
        string title,
        string fileName,
        string contentType,
        string base64Data,
        Fixture fixture)
        => new
        {
            objectType,
            title,
            subtitle = "HTTP contract asset",
            notes = "Created by the R22 integration contract.",
            media = new
            {
                fileName,
                contentType,
                base64Data
            },
            parentNodeKey = fixture.RootNodeId,
            objectSubtype = contentType == "image/png" ? "generated" : "markdown",
            leaseToken = fixture.LeaseToken
        };

    private static async Task<HttpResponseMessage> PostRawJsonAsync(
        HttpClient client,
        string path,
        object payload)
        => await client.PostAsync(
            path,
            new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"));

    private static async Task<string> AssertObjectTypeAsync(
        HttpResponseMessage response,
        string expectedObjectType)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedObjectType, document.RootElement.GetProperty("objectType").GetString());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task AssertStatusAsync(
        ProjectStructureAgentApiTestHost host,
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        if (response.StatusCode == expectedStatus)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var failure = await dbContext.Set<ProjectStructureOperationAnalyticsRecord>()
            .AsNoTracking()
            .Where(record => !record.Succeeded)
            .OrderByDescending(record => record.OccurredAtUtc)
            .FirstOrDefaultAsync();
        var diagnostic = failure is null
            ? "No failed operation analytics were recorded."
            : $"{failure.OperationName}: {failure.ErrorCode}: {failure.ErrorMessage}";
        Assert.Fail(
            $"Expected HTTP {(int)expectedStatus}, received {(int)response.StatusCode}. " +
            $"Body: {body} Diagnostic: {diagnostic}");
    }

    private static async Task AssertAssetReadTypeAsync(
        HttpClient client,
        Guid projectId,
        string nodeId,
        string expectedObjectType)
    {
        using var response = await client.GetAsync(
            $"/api/project-structure/projects/{projectId:D}/assets/{Uri.EscapeDataString(nodeId)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedObjectType, document.RootElement.GetProperty("objectType").GetString());
    }

    private static JsonElement GetRequestSchema(JsonElement operation)
        => operation.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

    private static JsonElement GetResponseSchema(JsonElement operation)
        => operation.GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

    private static JsonElement GetPropertySchema(
        JsonElement root,
        JsonElement ownerSchema,
        string propertyName)
        => ResolveSchema(root, ownerSchema)
            .GetProperty("properties")
            .GetProperty(propertyName);

    private static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        while (schema.TryGetProperty("$ref", out var reference))
        {
            var referenceValue = reference.GetString()
                ?? throw new InvalidOperationException("OpenAPI schema reference was empty.");
            var schemaName = referenceValue[(referenceValue.LastIndexOf('/') + 1)..];
            schema = root.GetProperty("components")
                .GetProperty("schemas")
                .GetProperty(schemaName);
        }

        return schema;
    }

    private static JsonElement GetNullableArrayItemSchema(
        JsonElement root,
        JsonElement schema)
    {
        schema = ResolveSchema(root, schema);
        var alternatives = schema.GetProperty("oneOf").EnumerateArray().ToArray();
        Assert.Equal(2, alternatives.Length);
        Assert.Contains(
            alternatives,
            alternative => alternative.GetProperty("type").GetString() == "null");
        var arraySchema = alternatives.Single(
            alternative => alternative.GetProperty("type").GetString() == "array");
        return arraySchema.GetProperty("items");
    }

    private static void AssertObjectTypeInputSchema(
        JsonElement root,
        JsonElement schema,
        bool allowsNodeKindAliases,
        bool allowsNull)
    {
        schema = ResolveSchema(root, schema);
        var alternatives = schema.GetProperty("oneOf").EnumerateArray().ToArray();
        Assert.Equal(allowsNull ? 3 : 2, alternatives.Length);
        var stringSchema = alternatives.Single(
            item => item.GetProperty("type").GetString() == "string");
        var integerSchema = alternatives.Single(
            item => item.GetProperty("type").GetString() == "integer");
        Assert.Equal(
            allowsNull,
            alternatives.Any(item => item.GetProperty("type").GetString() == "null"));

        if (allowsNodeKindAliases)
        {
            Assert.False(stringSchema.TryGetProperty("enum", out _));
            Assert.Contains(
                "node-kind alias",
                schema.GetProperty("description").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var documentedSymbols = stringSchema.GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();
            Assert.Equal(Enum.GetNames<ProjectObjectType>(), documentedSymbols);
        }

        var documentedNumbers = integerSchema.GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();
        Assert.Equal(
            Enum.GetValues<ProjectObjectType>().Select(value => (int)value),
            documentedNumbers);
    }

    private static void AssertResponseObjectTypesAreCanonical(
        JsonElement root,
        JsonElement responseSchema)
    {
        var schemas = new List<JsonElement>();
        CollectPropertySchemas(
            root,
            responseSchema,
            "objectType",
            schemas,
            new HashSet<string>(StringComparer.Ordinal));
        Assert.NotEmpty(schemas);
        foreach (var schema in schemas)
        {
            var resolved = ResolveSchema(root, schema);
            Assert.Equal("string", resolved.GetProperty("type").GetString());
            var documentedSymbols = resolved.GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();
            Assert.Equal(Enum.GetNames<ProjectObjectType>(), documentedSymbols);
            Assert.False(resolved.TryGetProperty("oneOf", out _));
        }
    }

    private static void AssertSharedResponseObjectTypesRemainNumeric(
        JsonElement root,
        JsonElement responseSchema)
    {
        var schemas = new List<JsonElement>();
        CollectPropertySchemas(
            root,
            responseSchema,
            "objectType",
            schemas,
            new HashSet<string>(StringComparer.Ordinal));
        Assert.NotEmpty(schemas);
        foreach (var schema in schemas)
        {
            Assert.Equal(
                "integer",
                ResolveSchema(root, schema).GetProperty("type").GetString());
        }
    }

    private static void CollectPropertySchemas(
        JsonElement root,
        JsonElement schema,
        string propertyName,
        ICollection<JsonElement> matches,
        ISet<string> visitedReferences)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var referenceValue = reference.GetString()
                ?? throw new InvalidOperationException("OpenAPI schema reference was empty.");
            if (!visitedReferences.Add(referenceValue))
            {
                return;
            }

            CollectPropertySchemas(
                root,
                ResolveSchema(root, schema),
                propertyName,
                matches,
                visitedReferences);
            return;
        }

        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                {
                    matches.Add(property.Value);
                }

                CollectPropertySchemas(
                    root,
                    property.Value,
                    propertyName,
                    matches,
                    visitedReferences);
            }
        }

        if (schema.TryGetProperty("items", out var items))
        {
            CollectPropertySchemas(
                root,
                items,
                propertyName,
                matches,
                visitedReferences);
        }

        foreach (var compositionName in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (!schema.TryGetProperty(compositionName, out var composition))
            {
                continue;
            }

            foreach (var item in composition.EnumerateArray())
            {
                CollectPropertySchemas(
                    root,
                    item,
                    propertyName,
                    matches,
                    visitedReferences);
            }
        }
    }

    private sealed record Fixture(Guid ProjectId, string RootNodeId, string LeaseToken);
}
