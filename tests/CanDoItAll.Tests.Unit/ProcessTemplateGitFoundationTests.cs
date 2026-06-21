using System.Text.Json;
using CanDoItAll.Git;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessTemplateGitFoundationTests
{
    [Fact]
    public void Git_path_authorization_rejects_paths_outside_repository()
    {
        var root = new GitRepositoryPath(Path.Combine(Path.GetTempPath(), "repo-root"));
        var outside = Path.Combine(Path.GetTempPath(), "other-root", "file.txt");

        var result = GitPathAuthorizer.Authorize(root, outside);

        Assert.False(result.IsAuthorized);
        Assert.Equal("GitPath.OutsideRepository", result.ErrorCode);
    }

    [Fact]
    public void Git_path_authorization_normalizes_repository_relative_paths()
    {
        var root = new GitRepositoryPath(Path.Combine(Path.GetTempPath(), "repo-root"));

        var result = GitPathAuthorizer.Authorize(root, "templates\\component.json");

        Assert.True(result.IsAuthorized);
        Assert.Equal("templates/component.json", result.Path?.RepositoryRelativePath);
    }

    [Fact]
    public async Task Git_repository_client_uses_argument_specs_and_sanitizes_commit_messages()
    {
        var executor = new RecordingGitCommandExecutor();
        var client = new GitRepositoryClient(new GitRepositoryPath(Path.GetTempPath()), executor);

        await client.CommitAsync("contains private context");

        var spec = Assert.Single(executor.Specs);
        Assert.Equal(["commit", "-m", "contains private context"], spec.Arguments.Select(argument => argument.Value));
        Assert.Equal("git commit -m ***", spec.SanitizedCommand);
    }

    [Fact]
    public async Task Git_repository_client_adds_authorized_paths_after_separator()
    {
        var executor = new RecordingGitCommandExecutor();
        var root = new GitRepositoryPath(Path.GetTempPath());
        var authorized = GitPathAuthorizer.Authorize(root, "Templates/Processes/component.json").Path!;
        var client = new GitRepositoryClient(root, executor);

        await client.AddAsync([authorized]);

        var spec = Assert.Single(executor.Specs);
        Assert.Equal(["add", "--", "Templates/Processes/component.json"], spec.Arguments.Select(argument => argument.Value));
    }

    [Fact]
    public void Template_content_hash_is_independent_of_property_order()
    {
        using var first = JsonDocument.Parse("""{"b":2,"a":{"d":4,"c":3}}""");
        using var second = JsonDocument.Parse("""{"a":{"c":3,"d":4},"b":2}""");

        var firstHash = ProcessTemplateContentHasher.ComputeCanonicalHash(first.RootElement);
        var secondHash = ProcessTemplateContentHasher.ComputeCanonicalHash(second.RootElement);

        Assert.Equal(firstHash, secondHash);
        Assert.StartsWith("sha256:", firstHash, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_migration_registry_requires_sequential_intermediate_migrations()
    {
        var registry = new ProcessTemplateMigrationRegistry(
            ["schema/1.0", "schema/1.1", "schema/1.2"],
            [new IdentityMigration("migrate-1", "schema/1.0", "schema/1.1")]);

        var result = registry.CreatePlan("schema/1.0", "schema/1.2");

        Assert.False(result.Succeeded);
        Assert.Equal("TemplateMigration.MissingIntermediate", result.ErrorCode);
    }

    [Fact]
    public void Template_migration_registry_plans_every_intermediate_step()
    {
        var registry = new ProcessTemplateMigrationRegistry(
            ["schema/1.0", "schema/1.1", "schema/1.2"],
            [
                new IdentityMigration("migrate-1", "schema/1.0", "schema/1.1"),
                new IdentityMigration("migrate-2", "schema/1.1", "schema/1.2")
            ]);

        var result = registry.CreatePlan("schema/1.0", "schema/1.2");

        Assert.True(result.Succeeded);
        Assert.Equal(["migrate-1", "migrate-2"], result.Migrations.Select(migration => migration.MigrationId));
    }

    [Fact]
    public void Template_merge_marks_local_patch_conflicts_on_changed_global_pointers()
    {
        using var value = JsonDocument.Parse("""{"label":"Local"}""");
        var operation = new ProcessTemplatePatchOperation(
            ProcessTemplatePatchOperationKind.Replace,
            "/label",
            value.RootElement.Clone());

        var result = ProcessTemplateThreeWayMerge.DetectConflicts([operation], new HashSet<string>(["/label"], StringComparer.Ordinal));

        Assert.True(result.HasConflicts);
        Assert.Empty(result.AutoAppliedOperations);
        Assert.Equal("/label", Assert.Single(result.Conflicts).JsonPointer);
    }

    [Fact]
    public void Projection_metadata_reports_source_hash_drift()
    {
        var metadata = new ProcessTemplateProjectionMetadata(
            ProcessTemplateSchemaMarker.ProjectionMetadataSchemaV1,
            ProcessTemplateProjectionKind.Markdown,
            "sha256:old",
            "generator/1",
            new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.True(ProcessTemplateProjectionRules.HasSourceDrift(metadata, "sha256:new"));
        Assert.False(ProcessTemplateProjectionRules.HasSourceDrift(metadata, "sha256:old"));
    }

    [Fact]
    public void Template_component_documents_are_serializable_with_source_generated_context()
    {
        using var content = JsonDocument.Parse("""{"kind":"step","title":"Review"}""");
        var document = new ProcessTemplateComponentDocument(
            ProcessTemplateSchemaMarker.ComponentSchemaV1,
            "1.0.0",
            "step.review",
            ProcessTemplateComponentType.Step,
            ProcessTemplateContentHasher.ComputeCanonicalHash(content.RootElement),
            new ProcessTemplateComponentReference(
                TemplateComponentId.New(),
                "step.base",
                "1.0.0",
                "sha256:base"),
            new ProcessTemplateCompatibility("1.0", "2.x"),
            content.RootElement.Clone());

        var json = JsonSerializer.Serialize(document, ProcessTemplateJsonContext.Default.ProcessTemplateComponentDocument);
        var roundTrip = JsonSerializer.Deserialize(json, ProcessTemplateJsonContext.Default.ProcessTemplateComponentDocument);

        Assert.NotNull(roundTrip);
        Assert.Equal(ProcessTemplateSchemaMarker.ComponentSchemaV1, roundTrip.SchemaVersion);
        Assert.Equal("step.review", roundTrip.Key);
    }

    private sealed class RecordingGitCommandExecutor : IGitCommandExecutor
    {
        public List<GitCommandSpec> Specs { get; } = [];

        public Task<GitCommandResult> ExecuteAsync(
            GitCommandSpec spec,
            CancellationToken cancellationToken = default)
        {
            Specs.Add(spec);
            return Task.FromResult(new GitCommandResult(true, 0, string.Empty, string.Empty, spec.SanitizedCommand));
        }
    }

    private sealed class IdentityMigration : IProcessTemplateMigration
    {
        public IdentityMigration(string migrationId, string fromSchemaVersion, string toSchemaVersion)
        {
            MigrationId = migrationId;
            FromSchemaVersion = fromSchemaVersion;
            ToSchemaVersion = toSchemaVersion;
        }

        public string MigrationId { get; }

        public string FromSchemaVersion { get; }

        public string ToSchemaVersion { get; }

        public JsonDocument Migrate(JsonDocument source)
        {
            return JsonDocument.Parse(source.RootElement.GetRawText());
        }
    }
}
