using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed partial class AgentDecisionDataPolicyOwnerIntegrationTests {
    [Fact]
    public Task Dedicated_CAS_returns_exact_policy_and_next_revision_even_for_equal_explicit_submission() => RunAsync(async fixture => {
        var original = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        Assert.Null(original.Policy);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), original.Expectation, Policy());
        var accepted = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(command, fixture.Token));
        Assert.False(accepted.FollowupRequired);
        Assert.Equal(command.Policy, accepted.Snapshot.Policy);
        Assert.Equal(original.Expectation.CatalogRevision.Next(), accepted.Snapshot.Expectation.CatalogRevision);
        Assert.Equal(accepted.Snapshot, await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token));
        Assert.Equal(AgentDecisionDataPolicyRejection.Conflict,
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await fixture.Owner.ReplaceAsync(command, fixture.Token)).Reason);
        var equal = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), accepted.Snapshot.Expectation, command.Policy);
        var repeated = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(equal, fixture.Token));
        Assert.Equal(accepted.Snapshot.Expectation.CatalogRevision.Next(), repeated.Snapshot.Expectation.CatalogRevision);
        Assert.Equal(accepted.Snapshot.Policy, repeated.Snapshot.Policy);
        Assert.Equal(fixture.Agent.Name, (await fixture.CatalogAsync()).Agents.Single(agent => agent.Id == fixture.AgentId).Name);
    });

    [Fact]
    public Task Raw_catalog_full_document_and_transfer_shaped_replacements_preserve_policy_and_reject_new_identity_injection() => RunAsync(async fixture => {
        await fixture.SavePolicyAsync(fixture.AgentId);
        var before = await fixture.CatalogAsync();
        var agent = before.Agents.Single(item => item.Id == fixture.AgentId);
        var raw = agent.ConfigurationJson;
        var updated = await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(item => item.Id == agent.Id ? item with { Name = "Raw editor", ConfigurationJson = "{\"ordinary\":7}" } : item).ToArray()
        }, fixture.Token);
        Assert.Equal(Policy(), AgentDecisionDataPolicyMetadata.Read(updated.Agents.Single(item => item.Id == agent.Id).ConfigurationJson));
        var full = await fixture.Store.LoadAsync(fixture.Token);
        await fixture.Store.SaveAsync(full with {
            Agents = full.Agents.Select(item => item.Id == agent.Id ? item with { ConfigurationJson = "{}" } : item).ToArray()
        }, fixture.Token);
        var transfer = fixture.NewStore();
        await transfer.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(item => item.Id == agent.Id ? item with {
                ConfigurationJson = AgentDecisionDataPolicyMetadata.Replace("{}", new(ReadBasisMode.StepSnapshot))
            } : item).ToArray()
        }, fixture.Token);
        Assert.Equal(Policy(), AgentDecisionDataPolicyMetadata.Read((await fixture.CatalogAsync()).Agents.Single(item => item.Id == agent.Id).ConfigurationJson));
        var bytes = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
        await Assert.ThrowsAsync<AgentDecisionDataPolicyMetadataException>(() => transfer.UpdateCatalogAsync(catalog => catalog with {
            Agents = [.. catalog.Agents, agent with { Id = Guid.NewGuid(), Name = "Injected new identity", TemplateKey = "", ConfigurationJson = raw }]
        }, fixture.Token));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token));
    });

    [Fact]
    public Task Generation_change_during_actual_key_wait_refuses_without_policy_write_and_releases_guards() => RunAsync(async fixture => {
        var original = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var before = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
        await fixture.HoldAgentAsync(fixture.AgentId);
        using var wait = new KeyWait();
        var pending = fixture.Track(fixture.Owner.ReplaceAsync(new(Guid.NewGuid(), original.Expectation, Policy()), fixture.Token));
        await wait.Entered.Task.WaitAsync(fixture.Token);
        Assert.False(pending.IsCompleted);
        fixture.Generation.Advance();
        await fixture.ReleaseAsync();
        Assert.Equal(AgentDecisionDataPolicyRejection.ContextChanged,
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await pending).Reason);
        Assert.Equal(before, await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token));
        await fixture.HoldAgentAsync(fixture.AgentId);
        await fixture.ReleaseAsync();
        await using var workspace = await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory())
            .AcquireCoordinationAsync(fixture.Root, fixture.Layout.WorkspaceLockPath, TimeSpan.FromSeconds(5), false, fixture.Token);
    });

    [Fact]
    public Task Acknowledged_catalog_with_later_index_failure_keeps_exact_acceptance() => RunAsync(async fixture => {
        var snapshot = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), snapshot.Expectation, Policy());
        var index = await File.ReadAllBytesAsync(fixture.Layout.WorkspaceIndexPath, fixture.Token);
        var checks = 0;
        var result = await WithRestoredFileAsync(fixture.Layout.WorkspaceIndexPath, index, () =>
            fixture.Store.ReplaceAgentDecisionDataPolicyAsync(command, () => {
                if (++checks == 2) {
                    File.Delete(fixture.Layout.WorkspaceIndexPath);
                    Directory.CreateDirectory(fixture.Layout.WorkspaceIndexPath);
                }
            }, fixture.Token));
        var accepted = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(result);
        Assert.True(accepted.FollowupRequired);
        Assert.NotNull(accepted.Failure);
        Assert.Equal(AgentDecisionDataPolicyFailureStage.Publication, accepted.Failure.Stage);
        Assert.NotEmpty(accepted.Failure.ExceptionTypes);
        Assert.Same(command, accepted.Command);
        Assert.Equal(command.Policy, accepted.Snapshot.Policy);
        Assert.Equal(snapshot.Expectation.CatalogRevision.Next(), accepted.Snapshot.Expectation.CatalogRevision);
        Assert.Equal(accepted.Snapshot, await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token));
        Assert.Equal(2, checks);
    });

    [Fact]
    public Task Entered_catalog_write_without_acknowledgement_retains_original_unconfirmed_intent() => RunAsync(async fixture => {
        var snapshot = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), snapshot.Expectation, Policy());
        var original = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
        var checks = 0;
        var result = await WithRestoredFileAsync(fixture.Layout.CatalogPath, original, () =>
            fixture.Store.ReplaceAgentDecisionDataPolicyAsync(command, () => {
                if (++checks == 2) {
                    File.Delete(fixture.Layout.CatalogPath);
                    Directory.CreateDirectory(fixture.Layout.CatalogPath);
                }
            }, fixture.Token));
        var unconfirmed = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Unconfirmed>(result);
        Assert.Same(command, unconfirmed.Command);
        Assert.NotNull(unconfirmed.Failure);
        Assert.Equal(AgentDecisionDataPolicyFailureStage.Publication, unconfirmed.Failure.Stage);
        Assert.NotEmpty(unconfirmed.Failure.ExceptionTypes);
        Assert.Equal(2, checks);
    });

    [Fact]
    public Task Corrupt_reserved_members_survive_ordinary_writes_and_explicit_original_CAS_repairs_them() => RunAsync(async fixture => {
        foreach (var agentId in new[] { fixture.AgentId, HrAgentIdentity.AgentId }) {
            var catalog = await fixture.CatalogAsync();
            var original = catalog.Agents.Single(agent => agent.Id == agentId);
            var ordinary = AgentDecisionDataPolicyMetadata.ProjectOrdinaryConfiguration(original.ConfigurationJson).Trim();
            using var ordinaryDocument = JsonDocument.Parse(ordinary);
            var separator = ordinaryDocument.RootElement.EnumerateObject().Any() ? "," : "";
            var corrupt = ordinary[..^1] + separator + "\"decisionDataPolicyOverride\":{ \"future\" : 9 },\"decisionDataPolicyOverride\":null}";
            catalog = catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == agentId ? agent with { ConfigurationJson = corrupt } : agent).ToArray()
            };
            await File.WriteAllTextAsync(fixture.Layout.CatalogPath,
                JsonSerializer.Serialize(catalog, new FileSandboxWorkspaceJsonStore().SerializerOptions), fixture.Token);
            await Assert.ThrowsAsync<AgentDecisionDataPolicyMetadataException>(() => fixture.Owner.ReadAsync(agentId, fixture.Token));
            var saved = await fixture.Store.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Select(agent => agent.Id == agentId
                    ? agent with { Name = "Corrupt policy retained", ConfigurationJson = ordinary } : agent).ToArray()
            }, fixture.Token);
            using var parsed = JsonDocument.Parse(saved.Agents.Single(agent => agent.Id == agentId).ConfigurationJson);
            Assert.Equal(new[] { "{ \"future\" : 9 }", "null" }, parsed.RootElement.EnumerateObject()
                .Where(property => property.NameEquals(AgentDecisionDataPolicyMetadata.PropertyName)).Select(property => property.Value.GetRawText()));
            var stored = saved.Agents.Single(agent => agent.Id == agentId);
            var projected = AgentDefinitionFactory.Create(saved, new AgentEditorModel {
                Name = stored.Name, TemplateKey = stored.TemplateKey, ConfigurationJson = ordinary
            }, agentId, stored, DateTimeOffset.UtcNow, new ProviderProfileService(), "Policy preservation proof");
            using var projectedJson = JsonDocument.Parse(projected.ConfigurationJson);
            Assert.Equal(new[] { "{ \"future\" : 9 }", "null" }, projectedJson.RootElement.EnumerateObject()
                .Where(property => property.NameEquals(AgentDecisionDataPolicyMetadata.PropertyName)).Select(property => property.Value.GetRawText()));
            var repaired = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(
                new(Guid.NewGuid(), new(fixture.Workspace, agentId, saved.CatalogDataRevision), Policy()), fixture.Token));
            Assert.Equal(repaired.Snapshot, await fixture.Owner.ReadAsync(agentId, fixture.Token));
        }
    });

    [Fact]
    public Task Provisioning_and_package_receipts_bind_the_real_persisted_definition_with_owner_policy() => RunAsync(async fixture => {
        var service = new AgentExternalProvisioningService(fixture.Store, new ProviderProfileService(), fixture.Workspace, fixture.Generation);
        var create = new AgentExternalProvisioningCommand("policy-fixture", "owned", "create", null,
            new AgentEditorModel { Name = "Provisioned policy fixture", Instructions = "Local test", ConfigurationJson = "{}" });
        var created = await service.UpsertAsync(create, fixture.Token);
        await fixture.SavePolicyAsync(created.AgentId);
        var stale = await Assert.ThrowsAsync<AgentExternalProvisioningException>(() => service.UpsertAsync(create with {
            IdempotencyKey = "stale", ExpectedConfigurationVersion = created.ConfigurationVersion
        }, fixture.Token));
        Assert.Equal(AgentExternalProvisioningFailureKind.PreconditionFailed, stale.Kind);
        var refreshed = await service.GetAsync(create.Namespace, create.Key, fixture.Token);
        var changed = await service.UpsertAsync(create with {
            IdempotencyKey = "replace", ExpectedConfigurationVersion = refreshed.ConfigurationVersion,
            Agent = new AgentEditorModel { Name = "Provisioned replacement", Instructions = "Changed", ConfigurationJson = "{\"opaque\":1}" }
        }, fixture.Token);
        var actual = (await fixture.CatalogAsync()).Agents.Single(agent => agent.Id == created.AgentId);
        Assert.Equal(Policy(), AgentDecisionDataPolicyMetadata.Read(actual.ConfigurationJson));
        Assert.Equal((await service.GetAsync(create.Namespace, create.Key, fixture.Token)).ConfigurationVersion, changed.ConfigurationVersion);
        Assert.NotEqual(refreshed.ConfigurationVersion, changed.ConfigurationVersion);
        var imported = new AgentImportResult(actual with { Name = "Package replacement", ConfigurationJson = "{}" }, [], [], [], [], [], []) {
            PackageSha256 = new string('A', 64), PackageSchemaVersion = "1.0"
        };
        var package = new Package(imported);
        using var input = new MemoryStream([1]);
        var receipt = await new AgentPackageImportService(fixture.Store, package, new ProviderProfileService()).ImportAsync(input,
            new(AgentPackageImportMode.ReplaceExactVersion, "package", "package-owned", ExpectedAgentVersion: actual.UpdatedAtUtc), fixture.Token);
        var persisted = (await fixture.CatalogAsync()).Agents.Single(agent => agent.Id == actual.Id);
        Assert.Equal(Policy(), AgentDecisionDataPolicyMetadata.Read(persisted.ConfigurationJson));
        Assert.Equal(AgentConfigurationVersion.Create(persisted), receipt.ConfigurationSha256);
        Assert.Equal(1, package.Reads);
    });

    [Fact]
    public Task Concurrent_policy_change_refuses_a_prepared_package_receipt_without_replaying_package_read() => RunAsync(async fixture => {
        var catalog = await fixture.CatalogAsync();
        var blocker = catalog.Agents.MinBy(agent => agent.Id.ToString("N"), StringComparer.Ordinal)!.Id;
        var selected = fixture.AgentId == blocker ? fixture.OtherAgentId : fixture.AgentId;
        await fixture.SavePolicyAsync(selected);
        var before = await fixture.CatalogAsync();
        var original = before.Agents.Single(agent => agent.Id == selected);
        var package = new Package(new(original with { Name = "Refused package target", ConfigurationJson = "{}" }, [], [], [], [], [], []) {
            PackageSha256 = new string('B', 64), PackageSchemaVersion = "1.0"
        });
        await fixture.HoldAgentAsync(blocker);
        using var wait = new KeyWait();
        using var input = new MemoryStream([1]);
        var pending = fixture.Track(new AgentPackageImportService(fixture.Store, package, new ProviderProfileService())
            .ImportAsync(input, new(AgentPackageImportMode.ReplaceExactVersion, "refused-package", "refused-owned",
                ExpectedAgentVersion: original.UpdatedAtUtc), fixture.Token));
        await wait.Entered.Task.WaitAsync(fixture.Token);
        var policyBefore = await fixture.Owner.ReadAsync(selected, fixture.Token);
        var replacement = new DecisionDataPolicyOverride(ReadBasisMode.StepSnapshot);
        Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(
            new(Guid.NewGuid(), policyBefore.Expectation, replacement), fixture.Token));
        await fixture.ReleaseAsync();
        Assert.Equal(AgentPackageImportFailureKind.Conflict, Assert.IsType<AgentPackageImportException>(await fixture.ObserveFailureAsync(pending)).Kind);
        Assert.Equal(1, package.Reads);
        var after = await fixture.CatalogAsync();
        var actual = after.Agents.Single(agent => agent.Id == original.Id);
        Assert.Equal(original.Name, actual.Name);
        Assert.Equal(replacement, AgentDecisionDataPolicyMetadata.Read(actual.ConfigurationJson));
        Assert.DoesNotContain(after.AgentPackageImportOperations, operation => operation.IdempotencyKey == "refused-package");
        Assert.DoesNotContain(after.AgentExternalBindings, binding => binding.Key == "refused-owned");
    });

    [Fact]
    public Task Concurrent_policy_change_refuses_a_prepared_provisioning_receipt_without_returning_its_configuration_version() => RunAsync(async fixture => {
        var service = new AgentExternalProvisioningService(fixture.Store, new ProviderProfileService(), fixture.Workspace, fixture.Generation);
        var create = new AgentExternalProvisioningCommand("policy-race", "owned", "create-race", null,
            new AgentEditorModel { Name = "Provisioning race", ConfigurationJson = "{}" });
        var created = await service.UpsertAsync(create, fixture.Token);
        await fixture.SavePolicyAsync(created.AgentId);
        var refreshed = await service.GetAsync(create.Namespace, create.Key, fixture.Token);
        var blocker = Fixture.GuardAgentId;
        Assert.True(string.CompareOrdinal(blocker.ToString("N"), created.AgentId.ToString("N")) < 0);
        await fixture.HoldAgentAsync(blocker);
        using var wait = new KeyWait();
        var pending = fixture.Track(service.UpsertAsync(create with {
            IdempotencyKey = "refused-provision", ExpectedConfigurationVersion = refreshed.ConfigurationVersion,
            Agent = new AgentEditorModel { Name = "Refused provisioning target", ConfigurationJson = "{}" }
        }, fixture.Token));
        await wait.Entered.Task.WaitAsync(fixture.Token);
        var snapshot = await fixture.Owner.ReadAsync(created.AgentId, fixture.Token);
        var replacement = new DecisionDataPolicyOverride(ReadBasisMode.StepSnapshot);
        Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(
            new(Guid.NewGuid(), snapshot.Expectation, replacement), fixture.Token));
        await fixture.ReleaseAsync();
        Assert.Equal(AgentExternalProvisioningFailureKind.Conflict,
            Assert.IsType<AgentExternalProvisioningException>(await fixture.ObserveFailureAsync(pending)).Kind);
        var current = await fixture.CatalogAsync();
        var actual = current.Agents.Single(agent => agent.Id == created.AgentId);
        Assert.Equal("Provisioning race", actual.Name);
        Assert.Equal(replacement, AgentDecisionDataPolicyMetadata.Read(actual.ConfigurationJson));
        Assert.DoesNotContain(current.AgentExternalProvisioningOperations, operation => operation.IdempotencyKey == "refused-provision");
        Assert.Equal(created.ConfigurationVersion, current.AgentExternalBindings.Single(binding => binding.AgentId == created.AgentId).ConfigurationVersion);
    });

    [Fact]
    public Task Deletion_recovery_preserves_the_retained_target_policy_and_never_adopts_a_new_default() => RunAsync(async fixture => {
        await fixture.SavePolicyAsync(fixture.AgentId);
        var before = await fixture.CatalogAsync();
        var retained = before.Agents.Single(agent => agent.Id == fixture.AgentId).ConfigurationJson;
        var failing = new FileSandboxWorkspaceStore(fixture.Root, WorkspaceScopeDescriptor.Sandbox,
            chatBackedRunCommitBoundary: null, existingRunDetailCommitBoundary: null,
            genericNewRunCommitBoundary: null, jsonReadDiagnostics: null,
            agentDeletionCommitBoundary: stage => {
                if (stage == AgentDeletionCommitStage.JournalPersisted) {
                    throw new IOException("Owned interruption after retained deletion target publication.");
                }
            });
        await Assert.ThrowsAsync<IOException>(() => failing.DeleteAgentWorkspaceDataAsync(fixture.OtherAgentId, fixture.Token));
        var recovered = await fixture.NewStore().LoadCatalogAsync(fixture.Token);
        Assert.DoesNotContain(recovered.Agents, agent => agent.Id == fixture.OtherAgentId);
        Assert.Equal(retained, recovered.Agents.Single(agent => agent.Id == fixture.AgentId).ConfigurationJson);
        Assert.Equal(Policy(), AgentDecisionDataPolicyMetadata.Read(retained));
        Assert.Equal(before.CatalogDataRevision.Next(), recovered.CatalogDataRevision);
    });

    private static DecisionDataPolicyOverride Policy() => new(ReadBasisMode.RunSnapshot,
        MaximumWorkDuration: TimeSpan.FromMinutes(3), MaximumBasisAge: TimeSpan.FromSeconds(45));

    private static async Task RunAsync(Func<Fixture, Task> test) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var environment = CanDoItAllTestEnvironment.Create("agent-policy-owner");
        var fixture = new Fixture(environment.RootPath, deadline.Token);
        var failures = new List<Exception>();
        try {
            await fixture.SeedAsync();
            await test(fixture);
        } catch (Exception failure) {
            failures.Add(failure);
        } finally {
            try {
                await deadline.CancelAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            try {
                await fixture.ReleaseAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            foreach (var pending in fixture.Pending) {
                try {
                    await pending;
                } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                } catch (Exception failure) {
                    if (!fixture.ObservedFailures.Contains(pending) && !failures.Contains(failure)) {
                        failures.Add(failure);
                    }
                }
            }
            try {
                await environment.DisposeAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        ThrowFailures(failures);
    }

    private static async Task<AgentDecisionDataPolicySaveOutcome> WithRestoredFileAsync(string path, byte[] original,
        Func<Task<AgentDecisionDataPolicySaveOutcome>> action) {
        AgentDecisionDataPolicySaveOutcome? outcome = null;
        var failures = new List<Exception>();
        try {
            outcome = await action();
        } catch (Exception failure) {
            failures.Add(failure);
        }
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path);
            }
        } catch (Exception failure) {
            failures.Add(failure);
        }
        try {
            await File.WriteAllBytesAsync(path, original, CancellationToken.None);
        } catch (Exception failure) {
            failures.Add(failure);
        }
        ThrowFailures(failures);
        return outcome ?? throw new InvalidOperationException("The owned policy operation returned no outcome.");
    }

    private static void ThrowFailures(IReadOnlyList<Exception> failures) {
        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1) {
            throw new AggregateException("Agent policy proof and owned cleanup failed.", failures);
        }
    }

    private sealed class Fixture {
        public static Guid GuardAgentId { get; } = new("00000000-0000-0000-0000-000000000001");
        private IAsyncDisposable? held;
        public Fixture(string root, CancellationToken token) {
            Root = root;
            Token = token;
            Store = NewStore();
            Layout = new(root);
            Owner = new(Store, Workspace, Generation);
        }

        public string Root { get; }
        public CancellationToken Token { get; }
        public FileSandboxWorkspaceStore Store { get; }
        public FileSandboxWorkspaceStorageLayout Layout { get; }
        public AgentDecisionDataPolicyOwner Owner { get; }
        public MutableGeneration Generation { get; } = new();
        public AgentExecutionActivityWorkspaceIdentity Workspace { get; } = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1));
        public Guid AgentId { get; } = Guid.NewGuid();
        public Guid OtherAgentId { get; } = Guid.NewGuid();
        public AgentDefinition Agent { get; private set; } = default!;
        public List<Task> Pending { get; } = [];
        public HashSet<Task> ObservedFailures { get; } = new(ReferenceEqualityComparer.Instance);
        public FileSandboxWorkspaceStore NewStore() => new(Root);
        public Task<SandboxWorkspaceCatalog> CatalogAsync() => NewStore().LoadCatalogAsync(Token);

        public async Task SeedAsync() {
            var catalog = await Store.LoadCatalogAsync(Token);
            Agent = catalog.Agents.First(agent => !agent.IsTemplate) with {
                Id = AgentId, Name = "Policy owner fixture", TemplateKey = "", ConfigurationJson = "{}",
                ProviderProfileId = null, Capabilities = []
            };
            Assert.DoesNotContain(catalog.Agents, agent => agent.Id == GuardAgentId);
            await Store.UpdateCatalogAsync(current => current with {
                Agents = [.. current.Agents, Agent,
                    Agent with { Id = OtherAgentId, Name = "Other policy owner", TemplateKey = "" },
                    Agent with { Id = GuardAgentId, Name = "Policy coordination guard", TemplateKey = "" }]
            }, Token);
        }

        public async Task SavePolicyAsync(Guid agentId) {
            var snapshot = await Owner.ReadAsync(agentId, Token);
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await Owner.ReplaceAsync(new(Guid.NewGuid(), snapshot.Expectation, Policy()), Token));
        }

        public async Task HoldAgentAsync(Guid agentId) {
            var catalog = await CatalogAsync();
            var target = catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == agentId ? agent with { Name = "Key identity only" } : agent).ToArray()
            };
            var path = Assert.Single(FileSandboxWorkspaceWriteSet.Catalog(catalog, target).LockPaths(Layout));
            held = await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory())
                .AcquireCoordinationAsync(Root, path, TimeSpan.FromSeconds(5), false, Token);
        }

        public async Task ReleaseAsync() {
            var owned = held;
            held = null;
            if (owned is not null) {
                await owned.DisposeAsync();
            }
        }

        public async Task<Exception?> ObserveFailureAsync(Task pending) {
            var failure = await Record.ExceptionAsync(() => pending);
            ObservedFailures.Add(pending);
            return failure;
        }

        public Task<T> Track<T>(Task<T> pending) {
            Pending.Add(pending);
            return pending;
        }
    }

    private sealed class MutableGeneration : IAgentExecutionProfileGenerationSource {
        private long value = 1;
        public DatabaseProfileGeneration GetGeneration() => new(Volatile.Read(ref value));
        public void Advance() => Interlocked.Increment(ref value);
    }

    private sealed class KeyWait : IDisposable {
        private readonly Activity root = new Activity("agent-policy-key-wait").SetIdFormat(ActivityIdFormat.W3C).Start();
        private readonly ActivityListener listener;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public KeyWait() {
            listener = new() {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {
                    if (activity.TraceId == root.TraceId && activity.OperationName == "workspace.publication.keys.wait") {
                        Entered.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }
        public void Dispose() {
            listener.Dispose();
            root.Dispose();
        }
    }

    private sealed class Package(AgentImportResult result) : IAgentPackageService {
        public int Reads { get; private set; }
        public Task<AgentExportResult> ExportAsync(SandboxWorkspaceDocument document, AgentDefinition agent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentImportResult> ImportAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentImportResult> ImportAsync(Stream input, AgentPackageReadOptions options, CancellationToken cancellationToken = default) {
            Reads++;
            return Task.FromResult(result);
        }
    }
}
