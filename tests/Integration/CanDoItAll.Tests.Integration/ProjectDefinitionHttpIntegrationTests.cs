using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class ProjectDefinitionHttpIntegrationTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };

    [Fact]
    public async Task Protected_HTTP_create_replace_scalar_and_lookup_preserve_exact_historical_acknowledgements() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var context = await ContextAsync(host.Client);
        Assert.Equal(ProjectMutationCallerScope.OpenApi.Bind(context.Namespace), context.CallerBinding);
        var create = Request(context, Creation("  Initial accepted project  "));
        var created = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post, "/api/projects/definitions", create, HttpStatusCode.Created);
        Assert.False(created.Replayed);
        Assert.Equal("Initial accepted project", created.Receipt.Acknowledgement.Name);
        Assert.NotEqual(Guid.Empty, Assert.Single(created.Receipt.Acknowledgement.Phases).Id);
        Assert.NotEqual(Guid.Empty, Assert.Single(created.Receipt.Acknowledgement.Options).Id);
        var projectId = created.Receipt.Acknowledgement.Project.ProjectId;
        var read = await host.Client.GetFromJsonAsync<ProjectDefinitionReadApiResponse>($"/api/projects/{projectId:D}/definition", Json);
        Assert.NotNull(read);
        Assert.Equal(created.Receipt.Acknowledgement.Definition, read.Expectation);
        Assert.Equal(context.Namespace, read.WriteContext.Namespace);
        Assert.Equal(context.CallerBinding, read.WriteContext.CallerBinding);

        var replace = Request(read.WriteContext, new ReplaceProjectDefinitionCommand(read.Expectation,
            read.Scalars with { Name = "Accepted replacement" },
            read.Phases.Select(row => new ProjectDefinitionPhaseInput(row.Id, "Updated phase", row.Goal, row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
            read.Options.Select(row => new ProjectDefinitionOptionInput(row.Id, row.Category, "Updated option", row.Notes)).ToArray(), null));
        var replaced = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Put,
            $"/api/projects/{projectId:D}/definition", replace, HttpStatusCode.OK);
        Assert.Equal(Assert.Single(created.Receipt.Acknowledgement.Phases).Id, Assert.Single(replaced.Receipt.Acknowledgement.Phases).Id);
        var scalar = Request(context, new UpdateProjectDefinitionScalarsCommand(replaced.Receipt.Acknowledgement.Definition,
            read.Scalars with { Name = "Latest scalar edit" }));
        var scalarAccepted = await SendAcceptedAsync<ProjectDefinitionScalarsAcknowledgement>(host.Client, HttpMethod.Put,
            $"/api/projects/{projectId:D}/definition/scalars", scalar, HttpStatusCode.OK);
        var latest = (await host.Client.GetFromJsonAsync<ProjectDefinitionReadApiResponse>($"/api/projects/{projectId:D}/definition", Json))!;
        Assert.Equal(scalarAccepted.Receipt.Acknowledgement.Definition, latest.Expectation);
        AssertChildren(replaced.Receipt.Acknowledgement, latest);

        var replay = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post, "/api/projects/definitions", create, HttpStatusCode.OK);
        Assert.True(replay.Replayed);
        Assert.Equal(Serialize(created.Receipt), Serialize(replay.Receipt));
        using var lookup = await host.Client.GetAsync(Lookup(create.Operation));
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var found = await lookup.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("foundFull", found.GetProperty("status").GetString());
        Assert.Equal(Serialize(created.Receipt), found.GetProperty("receipt").GetRawText());
        using var scalarLookup = await host.Client.GetAsync(Lookup(scalar.Operation));
        var scalarFound = await scalarLookup.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("foundScalars", scalarFound.GetProperty("status").GetString());
        Assert.Equal(Serialize(scalarAccepted.Receipt), scalarFound.GetProperty("receipt").GetRawText());
        Assert.Equal("Latest scalar edit", latest.Scalars.Name);

        var altered = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(create.Operation, create.Version, Creation("Different payload"));
        await AssertRejectedAsync(host.Client, HttpMethod.Post, "/api/projects/definitions", altered,
            HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionOperationMismatch);
        var stale = Request(context, replace.Command);
        await AssertRejectedAsync(host.Client, HttpMethod.Put, $"/api/projects/{projectId:D}/definition", stale,
            HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionConflict);
    }

    [Theory]
    [InlineData("caller", HttpStatusCode.Conflict)]
    [InlineData("namespace", HttpStatusCode.Conflict)]
    [InlineData("route", HttpStatusCode.BadRequest)]
    [InlineData("version", HttpStatusCode.BadRequest)]
    [InlineData("missingPhases", HttpStatusCode.BadRequest)]
    [InlineData("nullPhases", HttpStatusCode.BadRequest)]
    [InlineData("missingOptions", HttpStatusCode.BadRequest)]
    [InlineData("nullOptions", HttpStatusCode.BadRequest)]
    [InlineData("clientActor", HttpStatusCode.BadRequest)]
    [InlineData("emptyOperation", HttpStatusCode.BadRequest)]
    public async Task Protected_HTTP_rejects_invalid_identity_original_target_version_and_complete_replacement_shape(string defect, HttpStatusCode expected) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var context = await ContextAsync(host.Client);
        var created = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post, "/api/projects/definitions",
            Request(context, Creation("Boundary source")), HttpStatusCode.Created);
        var ack = created.Receipt.Acknowledgement;
        var replacement = Request(context, new ReplaceProjectDefinitionCommand(ack.Definition,
            new("Must not commit", "", "", ProjectStatus.Draft, "", null), [], [], null));
        var body = JsonSerializer.SerializeToNode(replacement, Json)!.AsObject();
        var route = ack.Project.ProjectId;
        switch (defect) {
            case "caller":
                body["operation"]!["callerBinding"] = ProjectMutationCallerScope.LocalWorkspace.Bind(context.Namespace).Value;
                break;
            case "namespace":
                body["operation"]!["namespace"] = Guid.NewGuid().ToString("D");
                break;
            case "route":
                route = Guid.NewGuid();
                break;
            case "version":
                body["version"]!["schema"] = 2;
                break;
            case "missingPhases":
                body["command"]!.AsObject().Remove("phases");
                break;
            case "nullPhases":
                body["command"]!["phases"] = null;
                break;
            case "missingOptions":
                body["command"]!.AsObject().Remove("options");
                break;
            case "nullOptions":
                body["command"]!["options"] = null;
                break;
            case "clientActor":
                body["caller"] = new JsonObject { ["kind"] = "OpenApi", ["subject"] = "trusted-operator" };
                break;
            case "emptyOperation":
                body["operation"]!["id"] = Guid.Empty.ToString("D");
                break;
        }
        using var response = await host.Client.PutAsync($"/api/projects/{route:D}/definition",
            new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(expected, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("rejected", result.GetProperty("status").GetString());
        var current = (await host.Client.GetFromJsonAsync<ProjectDefinitionReadApiResponse>($"/api/projects/{ack.Project.ProjectId:D}/definition", Json))!;
        Assert.Equal(ack.Definition, current.Expectation);
        AssertChildren(ack, current);
    }

    [Fact]
    public async Task Protected_HTTP_retained_receipt_replays_after_current_deployment_limits_are_tightened() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var context = await ContextAsync(host.Client);
        var command = Request(context, Creation("A retained accepted name longer than the next configured bound"));
        var accepted = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post, "/api/projects/definitions", command, HttpStatusCode.Created);
        host.App.Services.GetRequiredService<IOptions<ProjectDefinitionOperationOptions>>().Value.MaxFieldUtf8Bytes = 16;

        var replay = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post, "/api/projects/definitions", command, HttpStatusCode.OK);
        Assert.True(replay.Replayed);
        Assert.Equal(Serialize(accepted.Receipt), Serialize(replay.Receipt));
        Assert.Equal(16, (await ContextAsync(host.Client)).Limits.MaxFieldUtf8Bytes);
        await AssertRejectedAsync(host.Client, HttpMethod.Post, "/api/projects/definitions", Request(context, command.Command),
            HttpStatusCode.RequestEntityTooLarge, ProjectErrorCodes.DefinitionLimitExceeded);
    }

    [Fact]
    public async Task Protected_HTTP_canonical_byte_boundary_accepts_exactly_and_preserves_replay_after_limit_reduction() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var context = await ContextAsync(host.Client);
        var request = Request(context, Creation("Canonical \"é東京\" boundary"));
        var command = request.Command;
        var intent = new ProjectDefinitionNormalizedIntent(request.Version, ProjectDefinitionCommandKind.Create, null,
            command.Scalars, command.Phases, command.Options, command.ExternalIdentity, new(null, null, null),
            ProjectDefinitionEffectBinding.None, ProjectDefinitionLimits.VersionOneMaximum);
        _ = ProjectDefinitionOperationCodec.ComputeDigest(intent, request.Operation, ProjectMutationCallerScope.OpenApi,
            ProjectDefinitionLimits.VersionOneMaximum, out var canonicalByteLength);
        var options = host.App.Services.GetRequiredService<IOptions<ProjectDefinitionOperationOptions>>().Value;
        options.MaxCanonicalCommandBytes = canonicalByteLength;
        await using var scope = host.App.Services.CreateAsyncScope();
        await using var database = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var before = await database.Set<Project>().CountAsync();

        var accepted = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post,
            "/api/projects/definitions", request, HttpStatusCode.Created);
        options.MaxCanonicalCommandBytes = canonicalByteLength - 1;
        var replay = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post,
            "/api/projects/definitions", request, HttpStatusCode.OK);
        Assert.True(replay.Replayed);
        Assert.Equal(Serialize(accepted.Receipt), Serialize(replay.Receipt));

        var fresh = Request(context, command);
        await AssertRejectedAsync(host.Client, HttpMethod.Post, "/api/projects/definitions", fresh,
            HttpStatusCode.RequestEntityTooLarge, ProjectErrorCodes.DefinitionLimitExceeded);
        Assert.Equal(before + 1, await database.Set<Project>().CountAsync());
        Assert.Single(await database.Set<ProjectDefinitionReceiptRecord>().Where(row => row.OperationId == request.Operation.Id).ToArrayAsync());
        Assert.False(await database.Set<ProjectDefinitionReceiptRecord>().AnyAsync(row => row.OperationId == fresh.Operation.Id));
        var current = (await host.Client.GetFromJsonAsync<ProjectDefinitionReadApiResponse>(
            $"/api/projects/{accepted.Receipt.Acknowledgement.Project.ProjectId:D}/definition", Json))!;
        Assert.Equal(accepted.Receipt.Acknowledgement.Definition, current.Expectation);
    }

    [Fact]
    public async Task Protected_HTTP_verified_subject_survives_token_renewal_while_current_read_and_write_revocation_take_effect() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);
        const string subject = "definition-caller";
        var issuer = host.App.Services.GetRequiredService<IApiTokenService>();
        var first = issuer.IssueToken(new() { Subject = subject, Scopes = [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects] });
        SetToken(host.Client, first);
        var context = await ContextAsync(host.Client);
        var configuredIssuer = host.App.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value.Authorization.Issuer;
        Assert.Equal(ProjectMutationCallerScope.FromVerifiedSubject(configuredIssuer, subject).Bind(context.Namespace), context.CallerBinding);
        Assert.NotEqual(ProjectMutationCallerScope.OpenApi.Bind(context.Namespace), context.CallerBinding);
        var request = Request(context, Creation("Authenticated original"));
        var accepted = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post, "/api/projects/definitions", request, HttpStatusCode.Created);
        var renewed = issuer.IssueToken(new() { Subject = subject, Scopes = [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects] });
        Assert.NotEqual(first.Token, renewed.Token);
        SetToken(host.Client, renewed);
        Assert.Equal(context.CallerBinding, (await ContextAsync(host.Client)).CallerBinding);
        var replay = await SendAcceptedAsync<ProjectEditorAcknowledgement>(host.Client, HttpMethod.Post, "/api/projects/definitions", request, HttpStatusCode.OK);
        Assert.Equal(Serialize(accepted.Receipt), Serialize(replay.Receipt));

        var readOnly = issuer.IssueToken(new() { Subject = subject, Scopes = [ApiAccessScopeNames.ReadProjects] });
        SetToken(host.Client, readOnly);
        using (var found = await host.Client.GetAsync(Lookup(request.Operation))) {
            Assert.Equal(HttpStatusCode.OK, found.StatusCode);
            Assert.Equal("foundFull", (await found.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        }
        using (var denied = await host.Client.PostAsJsonAsync("/api/projects/definitions", request, Json)) {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        var writeOnly = issuer.IssueToken(new() { Subject = subject, Scopes = [ApiAccessScopeNames.WriteProjects] });
        SetToken(host.Client, writeOnly);
        using (var denied = await host.Client.PostAsJsonAsync("/api/projects/definitions", request, Json)) {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        SetToken(host.Client, renewed);
        var registry = host.App.Services.GetRequiredService<IApiTokenRegistry>();
        var records = (await registry.SearchAsync(new(subject))).Items;
        foreach (var record in records) {
            var retained = (await registry.FindAsync(record.Id))!;
            await registry.DeleteAsync(retained.Id);
            registry.Register(retained with { Scopes = [ApiAccessScopeNames.WriteProjects] });
        }
        using (var denied = await host.Client.GetAsync(Lookup(request.Operation))) {
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using (var denied = await host.Client.PostAsJsonAsync("/api/projects/definitions", request, Json)) {
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        SetToken(host.Client, issuer.IssueToken(new() { Subject = "another-definition-caller", Scopes = [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects] }));
        Assert.NotEqual(context.CallerBinding, (await ContextAsync(host.Client)).CallerBinding);
        await AssertRejectedAsync(host.Client, HttpMethod.Post, "/api/projects/definitions", request,
            HttpStatusCode.Conflict, ProjectErrorCodes.DefinitionCallerChanged);
    }

    [Fact]
    public async Task Protected_HTTP_enabled_authorization_never_turns_absent_or_invalid_credentials_into_open_access() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);
        using (var absent = await host.Client.GetAsync("/api/projects/definition-write-context")) {
            Assert.Equal(HttpStatusCode.Unauthorized, absent.StatusCode);
        }
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-client-selected-identity");
        using var invalid = await host.Client.GetAsync("/api/projects/definition-write-context");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    private static async Task<ProjectDefinitionWriteContext> ContextAsync(HttpClient client)
        => (await client.GetFromJsonAsync<ProjectDefinitionWriteContext>("/api/projects/definition-write-context", Json))!;

    private static ProjectDefinitionRequest<T> Request<T>(ProjectDefinitionWriteContext context, T command) where T : class
        => new(new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding), ProjectDefinitionRequestVersion.VersionOne, command);

    private static CreateProjectDefinitionCommand Creation(string name)
        => new(new(name, "Description", "Objective", ProjectStatus.Draft, "Plan", null),
            [new(null, "Phase", "Goal", ProjectPhaseStatus.Planned, null, null)],
            [new(null, ProjectOptionCategory.Language, "C#", "Option notes")], null);

    private static string Lookup(ProjectDefinitionOperation operation)
        => $"/api/projects/definition-operations/{operation.Namespace.Value:D}/{operation.Epoch.Value}/{operation.Id:D}?callerBinding={Uri.EscapeDataString(operation.CallerBinding.Value)}";

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static void AssertChildren(ProjectEditorAcknowledgement acknowledgement, ProjectDefinitionReadApiResponse current) {
        Assert.Equal(acknowledgement.Phases.Select(row => new ProjectDefinitionPhase(row.Id, row.Name, row.Goal, row.Status, row.StartDateUtc, row.EndDateUtc)), current.Phases);
        Assert.Equal(acknowledgement.Options.Select(row => new ProjectDefinitionOption(row.Id, row.Category, row.OptionName, row.Notes)), current.Options);
    }

    private static async Task<ProjectDefinitionAcceptedApiResponse<T>> SendAcceptedAsync<T>(HttpClient client, HttpMethod method,
        string path, object body, HttpStatusCode expected) where T : class {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body, options: Json) };
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        var wire = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("accepted", wire.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, wire.GetProperty("receipt").GetProperty("operation").GetProperty("epoch").ValueKind);
        return wire.Deserialize<ProjectDefinitionAcceptedApiResponse<T>>(Json)!;
    }

    private static async Task AssertRejectedAsync(HttpClient client, HttpMethod method, string path, object body,
        HttpStatusCode expected, string code) {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body, options: Json) };
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        var wire = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("rejected", wire.GetProperty("status").GetString());
        Assert.Contains(wire.GetProperty("errors").EnumerateArray(), item => item.GetProperty("code").GetString() == code);
    }

    private static void SetToken(HttpClient client, ApiTokenIssueResult token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
}
