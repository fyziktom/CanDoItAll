using CanDoItAll.SharedKernel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowProjectLifetimeSerializationTests {
    private static readonly Guid Profile = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Project = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Lifetime = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Agent = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_authority_bytes_and_fingerprint_remain_unchanged(bool stringEnums) {
        var options = Options(stringEnums);
        var legacy = LegacyAuthority.Create(WorkflowStructureAuthorityChannel.AgentExecution);
        var before = JsonSerializer.Serialize(legacy, options);
        var read = JsonSerializer.Deserialize<WorkflowStructureAuthority>(before, options)!;
        Assert.Null(read.ProjectScope);
        Assert.Equal(before, JsonSerializer.Serialize(read, options));
        Assert.Equal(ProjectWorkflowContributionFingerprint.Hash(JsonSerializer.Serialize(legacy, Options(false))),
            WorkflowStructureAuthorityFingerprint.Create(read));
        Assert.Equal(Agent, read.AgentGovernance!.AgentId);
        Assert.NotEqual(Guid.Empty, read.AgentGovernance.AuthorityId.Value);
        Assert.NotEmpty(read.AgentGovernance.AllowedOperations);
    }

    [Theory]
    [InlineData(WorkflowStructureAuthorityChannel.LocalOperator)]
    [InlineData(WorkflowStructureAuthorityChannel.AuthenticatedOperator)]
    [InlineData(WorkflowStructureAuthorityChannel.AgentExecution)]
    public void New_authority_round_trip_preserves_original_channel_and_nondefault_scope(WorkflowStructureAuthorityChannel channel) {
        var authority = Scoped(channel);
        var json = JsonSerializer.Serialize(authority, Options(false));
        var saved = JsonSerializer.Deserialize<WorkflowStructureAuthority>(json, Options(false))!;
        Assert.Equal(channel, saved.Channel);
        Assert.Equal(authority.Principal, saved.Principal);
        Assert.Equal(new WorkflowProjectLifetime(Profile, Project, Lifetime), Assert.Single(saved.ProjectScope!.Projects));
        Assert.Equal(Project, Assert.Single(saved.ProjectScope.AdmissionProjectIds));
        Assert.Equal(authority.ProjectScope!.WorkflowStartCapabilityId, saved.ProjectScope.WorkflowStartCapabilityId);
        Assert.Equal(WorkflowStructureAuthorityFingerprint.Create(authority), WorkflowStructureAuthorityFingerprint.Create(saved));
        Assert.Contains("project-scope-v1", json, StringComparison.Ordinal);
        if (channel == WorkflowStructureAuthorityChannel.AgentExecution) {
            Assert.NotEqual(Guid.Empty, saved.ProjectScope.WorkflowStartCapabilityId);
            Assert.Equal(Agent, saved.AgentGovernance!.AgentId);
            Assert.Equal(new[] { "alpha", "omega" }, saved.AgentGovernance.AllowedOperations.Order(StringComparer.Ordinal));
            Assert.Equal(new[] { "cap-a", "cap-z" }, saved.AgentGovernance.AllowedCapabilityKeys.Order(StringComparer.Ordinal));
            Assert.Equal(new[] { "read-a", "read-z" }, saved.AgentGovernance.ReadOnlyExternalTargetAliases.Order(StringComparer.Ordinal));
        } else {
            Assert.Null(saved.AgentGovernance);
        }
    }

    [Theory]
    [InlineData(WorkflowStructureAuthorityChannel.LocalOperator)]
    [InlineData(WorkflowStructureAuthorityChannel.AuthenticatedOperator)]
    [InlineData(WorkflowStructureAuthorityChannel.AgentExecution)]
    public void Legacy_reader_rejects_authority_bearing_rows_even_when_enum_strings_are_enabled(WorkflowStructureAuthorityChannel channel) {
        var persisted = JsonSerializer.Serialize(Scoped(channel), Options(false));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LegacyAuthority>(persisted, Options(false)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LegacyAuthority>(persisted, Options(true)));
    }

    [Fact]
    public void Numeric_legacy_channel_cannot_smuggle_new_lifetime_authority() {
        var payload = JsonNode.Parse(JsonSerializer.Serialize(Scoped(WorkflowStructureAuthorityChannel.LocalOperator), Options(false)))!;
        payload["channel"] = (int)WorkflowStructureAuthorityChannel.LocalOperator;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowStructureAuthority>(payload.ToJsonString(), Options(false)));
    }

    [Fact]
    public void Scope_marker_without_saved_scope_cannot_be_read_as_legacy_authority() {
        var payload = JsonNode.Parse(JsonSerializer.Serialize(Scoped(WorkflowStructureAuthorityChannel.LocalOperator), Options(false)))!;
        payload.AsObject().Remove("projectScope");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkflowStructureAuthority>(payload.ToJsonString(), Options(false)));
    }

    [Fact]
    public void Scope_rejects_unknown_version_duplicate_projects_and_default_identifiers() {
        var target = new WorkflowProjectLifetime(Profile, Project, Lifetime);
        Assert.Throws<ArgumentException>(() => new WorkflowStructureProjectScope([target], [Project], schemaVersion: 2));
        Assert.Throws<ArgumentException>(() => new WorkflowStructureProjectScope([target, target]));
        Assert.Throws<ArgumentException>(() => new WorkflowProjectLifetime(Profile, Project, Guid.Empty));
        Assert.Throws<ArgumentException>(() => new WorkflowStructureProjectScope([target], [Guid.NewGuid()]));
    }

    [Fact]
    public void Equivalent_grant_and_target_order_has_identical_persisted_bytes_after_restart() {
        var original = Scoped(WorkflowStructureAuthorityChannel.AgentExecution);
        var secondProject = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var secondLifetime = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var first = original with { AllProjects = true, ProjectIds = [secondProject, Project], ProjectScope = new([
            new(Profile, secondProject, secondLifetime), new(Profile, Project, Lifetime)], [secondProject, Project]) };
        var second = original with { AllProjects = true, ProjectIds = [Project, secondProject], AgentGovernance = Governance(reverse: true),
            ProjectScope = new([new(Profile, Project, Lifetime), new(Profile, secondProject, secondLifetime)], [Project, secondProject]) };
        var firstBytes = JsonSerializer.Serialize(first, Options(false));
        var secondBytes = JsonSerializer.Serialize(second, Options(false));
        Assert.Equal(firstBytes, secondBytes);
        var reloaded = JsonSerializer.Deserialize<WorkflowStructureAuthority>(firstBytes, Options(false))!;
        Assert.Equal(firstBytes, JsonSerializer.Serialize(reloaded, Options(false)));
        Assert.Equal(WorkflowStructureAuthorityFingerprint.Create(first), WorkflowStructureAuthorityFingerprint.Create(reloaded));
    }

    [Fact]
    public void Native_plan_domain_binds_original_lifetime_and_source_without_changing_legacy_hash() {
        var definition = WorkflowVersionId.New();
        var identity = new WorkflowStructureOutputIdentity(WorkflowExecutionOccurrence.Start(WorkflowRunId.New()).Advance(definition, new("effect")), 0);
        var legacy = new WorkflowStructureOutputPlan(identity, definition, new("effect"), Project, new("parent"), "binding",
            WorkflowStructureOutputKind.Task, WorkflowStructureOutputRole.RequiredResult, string.Empty);
        var request = new ProjectObjectCreateRequest(ProjectObjectType.WorkItem, "Original", "", "Original notes", "parent", ObjectSubtype: "task");
        var oldHash = ProjectWorkflowContributionFingerprint.Create(legacy, request);
        var authority = Scoped(WorkflowStructureAuthorityChannel.LocalOperator);
        var current = legacy with { ProjectLifetime = new(Profile, Project, Lifetime),
            SourceAuthorityFingerprint = WorkflowStructureAuthorityFingerprint.Create(authority) };
        var currentHash = ProjectWorkflowContributionFingerprint.Create(current, request);
        Assert.NotEqual(oldHash, currentHash);
        Assert.NotEqual(currentHash, ProjectWorkflowContributionFingerprint.Create(current with {
            ProjectLifetime = new(Profile, Project, Guid.NewGuid()) }, request));
        Assert.NotEqual(currentHash, ProjectWorkflowContributionFingerprint.Create(current with {
            SourceAuthorityFingerprint = new string('A', 64) }, request));
        var legacyReaderProjection = current with { ProjectLifetime = null, SourceAuthorityFingerprint = null };
        Assert.Equal(oldHash, ProjectWorkflowContributionFingerprint.Create(legacyReaderProjection, request));
        Assert.NotEqual(currentHash, ProjectWorkflowContributionFingerprint.Create(legacyReaderProjection, request));
    }

    private static WorkflowStructureAuthority Scoped(WorkflowStructureAuthorityChannel channel) {
        var legacy = LegacyAuthority.Create(channel);
        var authority = JsonSerializer.Deserialize<WorkflowStructureAuthority>(JsonSerializer.Serialize(legacy, Options(false)), Options(false))!;
        return authority with { ProjectScope = new([new(Profile, Project, Lifetime)], [Project],
            workflowStartCapabilityId: channel == WorkflowStructureAuthorityChannel.AgentExecution
                ? Guid.Parse("88888888-8888-8888-8888-888888888888") : null) };
    }

    private static AgentExecutionGovernanceSnapshot Governance(bool reverse = false) {
        string[] Order(string first, string second) => reverse ? [second, first] : [first, second];
        return new(new(Guid.Parse("77777777-7777-7777-7777-777777777777")), Agent, Profile, new(12),
            WorkspaceScopeDescriptor.Organization(Profile.ToString("N")), true, true, "fixture-v1", "policy-fixture",
            Order("alpha", "omega"), Order("cap-a", "cap-z"), Order("write-a", "write-z"),
            Order("read-a", "read-z"), Order("artifact-a", "artifact-z"));
    }

    private static JsonSerializerOptions Options(bool stringEnums) {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        if (stringEnums) {
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }
        return options;
    }

    private sealed record LegacyAuthority(WorkflowStructureAuthorityChannel Channel, WorkflowLaunchActor Principal,
        Guid DatabaseProfileId, Guid ProjectId, bool CanCreateTasks, bool CanCreateAssets,
        DateTimeOffset? ExpiresAtUtc, string PolicyFingerprint) {
        public WorkflowStructureOperatorSurface OperatorSurface { get; init; }
        public AgentExecutionGovernanceSnapshot? AgentGovernance { get; init; }
        public WorkflowStructureProcessAuthority? ProcessAuthority { get; init; }
        public WorkflowStructureSchedulerAuthority? SchedulerAuthority { get; init; }
        public bool AllProjects { get; init; }
        public IReadOnlyList<Guid> ProjectIds { get; init; } = [];

        public static LegacyAuthority Create(WorkflowStructureAuthorityChannel channel) => new(channel,
            new(channel == WorkflowStructureAuthorityChannel.AgentExecution ? WorkflowLaunchActorKind.Agent : WorkflowLaunchActorKind.User,
                channel == WorkflowStructureAuthorityChannel.AgentExecution ? Agent.ToString("D") : "original-user"),
            Profile, Project, true, true, channel == WorkflowStructureAuthorityChannel.AuthenticatedOperator
                ? DateTimeOffset.Parse("2035-01-02T03:04:05Z") : null, "policy-fixture") {
            OperatorSurface = channel == WorkflowStructureAuthorityChannel.AuthenticatedOperator ? WorkflowStructureOperatorSurface.Api : WorkflowStructureOperatorSurface.UserInterface,
            AgentGovernance = channel == WorkflowStructureAuthorityChannel.AgentExecution ? Governance() : null,
            ProjectIds = [Project]
        };
    }
}
