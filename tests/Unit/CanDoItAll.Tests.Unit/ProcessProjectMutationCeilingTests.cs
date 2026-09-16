using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessProjectMutationCeilingTests {
    private const string LegacyAgentAuthorityJson = """
        {"principal":{"$source":"agent-execution","ceiling":{"authorityId":"11111111-1111-1111-1111-111111111111","agentId":"22222222-2222-2222-2222-222222222222","databaseProfileGeneration":7,"workspaceScopeKind":"Project","workspaceScopeKey":"44444444-4444-4444-4444-444444444444","readAllowed":true,"mutationAllowed":true,"policyVersion":"policy-version","policyFingerprint":"policy-fingerprint","allowedOperations":["a.operation","m.operation","z.operation"],"allowedCapabilityKeys":["a.capability","m.capability","z.capability"],"writableExternalTargetAliases":["a-target","m-target","z-target"],"readOnlyExternalTargetAliases":["a-read","m-read","z-read"],"allowedManagedArtifactReadRefs":["managed:a/item.txt","managed:m/item.txt","managed:z/item.txt"]},"operation":"StructureStart"},"databaseProfileId":"33333333-3333-3333-3333-333333333333","projectAdmission":{"databaseProfileId":"33333333-3333-3333-3333-333333333333","projectId":"44444444-4444-4444-4444-444444444444","lifetimeId":"55555555-5555-5555-5555-555555555555"},"canCreateTasks":true,"canCreateAssets":false,"policyFingerprint":"policy-fingerprint"}
        """;

    [Fact]
    public void Legacy_agent_authority_preserves_frozen_bytes_and_preparation_hash() {
        var options = ProcessInstancePlanPersistenceMapper.CreateSerializerOptions();
        Assert.Equal(1112, Encoding.UTF8.GetByteCount(LegacyAgentAuthorityJson));
        Assert.Equal("sha256:cdb4e252850950617700a087ffddf82cdf2e62cbb153b986343174eec079638c", Hash(LegacyAgentAuthorityJson));
        var authority = JsonSerializer.Deserialize<ProcessLaunchAuthority>(LegacyAgentAuthorityJson, options)!;
        authority.Validate();
        Assert.Equal(LegacyAgentAuthorityJson, JsonSerializer.Serialize(authority, options));
        var source = Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(authority.Principal);
        Assert.Equal(ProcessLaunchAgentCeiling.LegacySchemaVersion, source.Ceiling.EffectiveSchemaVersion);
        Assert.Null(source.Ceiling.SourceProjectAdmission);
        var entity = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
        Assert.Equal(Hash(entity.PayloadJson), entity.PreparationFingerprint);
        Assert.Equal(LegacyAgentAuthorityJson, JsonSerializer.Serialize(ProcessPreparedLaunchCodec.Read(entity).Preparation.Authority, options));
    }

    [Fact]
    public void New_agent_source_lifetime_survives_preparation_and_rejects_the_legacy_hash_domain() {
        var options = ProcessInstancePlanPersistenceMapper.CreateSerializerOptions();
        var legacy = JsonSerializer.Deserialize<ProcessLaunchAuthority>(LegacyAgentAuthorityJson, options)!;
        var source = Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(legacy.Principal);
        var authority = legacy with { Principal = source with { Ceiling = source.Ceiling with {
            SchemaVersion = ProcessLaunchAgentCeiling.CurrentSchemaVersion, SourceProjectAdmission = legacy.ProjectAdmission
        } } };
        var entity = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
        var restored = Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(ProcessPreparedLaunchCodec.Read(entity).Preparation.Authority!.Principal);
        Assert.Equal(ProcessLaunchAgentCeiling.CurrentSchemaVersion, restored.Ceiling.EffectiveSchemaVersion);
        Assert.Equal(legacy.ProjectAdmission, restored.Ceiling.SourceProjectAdmission);
        Assert.NotEqual(Hash(entity.PayloadJson), entity.PreparationFingerprint);
        entity.PreparationFingerprint = Hash(entity.PayloadJson);
        Assert.Throws<InvalidOperationException>(() => ProcessPreparedLaunchCodec.Read(entity));
        Assert.Throws<InvalidOperationException>(() => (authority with { Principal = source with { Ceiling = source.Ceiling with {
            SchemaVersion = ProcessLaunchAgentCeiling.CurrentSchemaVersion
        } } }).Validate());
    }

    [Fact]
    public void Legacy_null_project_ceiling_preserves_authority_bytes_and_preparation_hash() {
        var authority = ProcessPreparedLaunchFixture.Local(Guid.NewGuid());
        var options = ProcessInstancePlanPersistenceMapper.CreateSerializerOptions();
        var legacy = JsonSerializer.Serialize(new {
            authority.Principal, authority.DatabaseProfileId, authority.ProjectAdmission, authority.CanCreateTasks,
            authority.CanCreateAssets, authority.PolicyFingerprint
        }, options);
        Assert.Equal(legacy, JsonSerializer.Serialize(authority, options));
        var entity = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
        Assert.Equal(Hash(entity.PayloadJson), entity.PreparationFingerprint);
        Assert.Null(ProcessPreparedLaunchCodec.Read(entity).Preparation.Authority!.ProjectMutations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Saved_project_ceiling_round_trips_and_requires_a_new_reader_even_when_all_grants_are_false(bool grants) {
        var profile = Guid.NewGuid();
        var project = new ProcessProjectAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var authority = ProcessPreparedLaunchFixture.Local(profile, project) with { ProjectMutations = new(grants, grants, grants, grants) };
        var entity = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
        var restored = ProcessPreparedLaunchCodec.Read(entity).Preparation.Authority!;
        Assert.Equal(authority.ProjectMutations, restored.ProjectMutations);
        Assert.Equal(project, restored.ProjectAdmission);
        Assert.NotEqual(Guid.Empty, restored.ProjectAdmission!.LifetimeId);
        Assert.NotEqual(Hash(entity.PayloadJson), entity.PreparationFingerprint);
        Assert.NotEqual(Hash("process-tool-source-v1\n" + entity.PayloadJson), entity.PreparationFingerprint);
        entity.PreparationFingerprint = Hash(entity.PayloadJson);
        Assert.Throws<InvalidOperationException>(() => ProcessPreparedLaunchCodec.Read(entity));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void Unsupported_project_ceiling_version_cannot_be_persisted(int version) {
        var authority = ProcessPreparedLaunchFixture.Local(Guid.NewGuid()) with { ProjectMutations = new(true, true, true, true, version) };
        Assert.Throws<InvalidOperationException>(() => ProcessPreparedLaunchCodec.ToEntity(
            ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()))));
    }

    [Fact]
    public void Creation_permissions_do_not_change_caller_identity_or_borrow_another_principal() {
        var authority = ProcessPreparedLaunchFixture.Local(Guid.NewGuid());
        var bounded = authority with { ProjectMutations = new(true, false, false, false) };
        Assert.Equal(ProcessLaunchIntentFingerprint.CallerFingerprint(authority), ProcessLaunchIntentFingerprint.CallerFingerprint(bounded));
        Assert.True(bounded.ProjectMutations!.IsWithin(new(true, true, true, true)));
        Assert.False(bounded.ProjectMutations.IsWithin(new(false, true, true, true)));
        Assert.IsType<ProcessLaunchPrincipal.LocalOperator>(bounded.Principal);
    }

    [Fact]
    public void Project_scoped_Agent_ceiling_cannot_acquire_root_creation_permission() {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var source = new ProcessLaunchPrincipal.AgentExecution(new(Guid.NewGuid(), Guid.NewGuid(), 3,
            ProcessLaunchSourceScopeKind.Project, project.ProjectId.ToString("D"), true, true, "v1", "source-policy", [], [], [], [], []),
            ProcessLaunchAgentOperation.StructureStart);
        var authority = new ProcessLaunchAuthority(source, project.DatabaseProfileId, project, false, false, "source-policy",
            new(false, true, true, true));
        authority.Validate();
        Assert.Throws<InvalidOperationException>(() => (authority with { ProjectMutations = new(true, true, true, true) }).Validate());
    }

    private static string Hash(string value)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
