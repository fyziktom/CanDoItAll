using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Composition;

public sealed class ProjectDeletionRecoverySession : IAsyncDisposable {
    private readonly ExistingDatabaseProfileSession profileSession;

    internal ProjectDeletionRecoverySession(ExistingDatabaseProfileSession profileSession, ProjectDeletionRecoveryCoordinator coordinator,
        ProjectDeletionLegacyInstaller legacyInstaller) {
        this.profileSession = profileSession;
        Coordinator = coordinator;
        LegacyInstaller = legacyInstaller;
    }

    public ProjectDeletionRecoveryCoordinator Coordinator { get; }
    public ProjectDeletionLegacyInstaller LegacyInstaller { get; }
    public ValueTask DisposeAsync() => profileSession.DisposeAsync();
}

public static class ProjectDeletionRecoveryComposition {
    public static async Task<ProjectDeletionRecoverySession> OpenAsync(IConfiguration configuration, IHostEnvironment host,
        Guid profileId, ILogger<ProjectDeletionRecoveryCoordinator> logger, CancellationToken cancellationToken = default) {
        var session = await ExistingDatabaseProfileSession.OpenAsync(configuration, host, profileId, cancellationToken);
        try {
            var profile = session.Profile;
            string connection = session.ConnectionString;
            var transactions = CoordinatedDatabaseTransaction.ForProfile(profile);
            var options = new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(connection).Options;
            var agentOptions = new DbContextOptionsBuilder<AgentProjectAccessDbContext>().UseNpgsql(connection).Options;
            var workbenchOptions = new DbContextOptionsBuilder<WorkbenchDbContext>().UseNpgsql(connection).Options;
            var environment = new RecoveryEnvironment(session.Snapshot, session.Reader, new(session.Files));
            var activation = new DatasetActivationService(session.Paths, session.Files, new PhysicalFileSystemPathPolicyFactory());
            IProjectDeletionLegacyParticipant[] participants = [new AgentProjectDeletionRecoveryInspector(agentOptions, transactions),
                new ProjectWorkbenchDeletionRecoveryInspector(workbenchOptions, transactions)];
            var coordinator = new ProjectDeletionRecoveryCoordinator(profile, options, transactions,
                activation, environment, participants, TimeProvider.System, logger);
            var legacy = new ProjectDeletionLegacyInstaller(profile, options, transactions, activation, environment,
                () => new(profile.Profile.Id, session.Snapshot.CreateResolvedProfile().Profile.Storage.WorkspacePath
                    ?? throw new InvalidOperationException("Legacy installation requires an existing host-bound canonical workspace.")),
                participants, TimeProvider.System, logger);
            return new(session, coordinator, legacy);
        } catch (Exception primary) {
            try {
                await session.DisposeAsync();
            } catch (Exception release) {
                throw new AggregateException("Recovery composition and secret-reader release both failed.", primary, release);
            }
            throw;
        }
    }

    private sealed class RecoveryEnvironment(ExistingDatabaseProfileSnapshot original,
        ExistingDatabaseProfileReader profiles, FileSandboxWorkspaceMaintenanceExclusion workspaces) : IProjectDeletionRecoveryEnvironment {
        public ProjectDeletionRecoveryDigest ProfileIdentity { get; } = new(original.IdentitySha256);

        public async Task RequireOriginalProfileAsync(CancellationToken cancellationToken)
            => original.RequireSame(await profiles.ReadAsync(original.ProfileId, cancellationToken));

        public Task RequireOriginalSourceAsync(ProjectDeletionSourceRecovery source, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            switch (source) {
                case ProjectDeletionSourceRecovery.NoExternalSourceLease:
                    break;
                case ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog catalog:
                    RequireCatalog(catalog);
                    break;
                case ProjectDeletionSourceRecovery.CanonicalInvocation invocation:
                    RequireCatalog(invocation.Catalog);
                    invocation.Execution.RequireCurrentHost();
                    RequireOriginalDirectory(invocation.Execution.RootPath);
                    break;
                default:
                    throw new NotSupportedException("This original source has no offline exclusion adapter.");
            }
            return Task.CompletedTask;
        }

        public async ValueTask<IAsyncDisposable> AcquireSourceExclusionAsync(ProjectDeletionSourceRecovery source,
            CancellationToken cancellationToken) {
            await RequireOriginalSourceAsync(source, cancellationToken);
            var held = new SourceLeases();
            try {
                var catalog = source switch {
                    ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog direct => direct,
                    ProjectDeletionSourceRecovery.CanonicalInvocation captured => captured.Catalog,
                    ProjectDeletionSourceRecovery.NoExternalSourceLease => null,
                    _ => throw new NotSupportedException("This original source has no offline exclusion adapter.")
                };
                if (catalog is not null) {
                    held.Add(await workspaces.AcquireAsync(catalog.RootPath,
                        WorkspaceScopeDescriptor.Organization(catalog.DatabaseProfileId.ToString("N")), cancellationToken));
                }
                if (source is ProjectDeletionSourceRecovery.CanonicalInvocation invocation) {
                    WorkspaceScopeDescriptor scope = invocation.Scope switch {
                        ProjectDeletionExecutionWorkspaceKind.Project => WorkspaceScopeDescriptor.Project(invocation.Key),
                        ProjectDeletionExecutionWorkspaceKind.Sandbox => new(WorkspaceScopeKind.Sandbox, invocation.Key),
                        _ => throw new NotSupportedException("The original execution scope is unsupported.")
                    };
                    held.Add(await workspaces.AcquireAsync(invocation.Execution.RootPath, scope, cancellationToken));
                }
                return held;
            } catch (Exception primary) {
                try {
                    await held.DisposeAsync();
                } catch (Exception release) {
                    throw new AggregateException("Source exclusion admission and terminal release both failed.", primary, release);
                }
                throw;
            }
        }

        private void RequireCatalog(ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog catalog) {
            catalog.RequireCurrentHost();
            var expected = new ProjectDeletionWorkspaceLocation(original.CreateResolvedProfile().Profile.Storage.WorkspacePath!);
            if (catalog.DatabaseProfileId != original.ProfileId || catalog.Location != expected) {
                throw new InvalidOperationException("The original catalog path binding no longer matches its explicit profile.");
            }
            RequireOriginalDirectory(catalog.RootPath);
        }

        private static void RequireOriginalDirectory(string root) {
            var policy = new PhysicalFileSystemPathPolicyFactory().Create(root, PhysicalFileSystemCaseSensitivity.Sensitive);
            policy.EnsureSafePath(root);
            if ((File.GetAttributes(root) & FileAttributes.Directory) == 0) {
                throw new InvalidDataException("The original source workspace directory is unavailable.");
            }
        }
    }

    private sealed class SourceLeases : IAsyncDisposable {
        private readonly List<IAsyncDisposable> leases = [];
        private bool disposalAttempted;

        internal void Add(IAsyncDisposable lease) => leases.Add(lease);

        public async ValueTask DisposeAsync() {
            if (disposalAttempted) {
                throw new InvalidOperationException("Source exclusion cleanup cannot be attempted concurrently or repeated.");
            }
            disposalAttempted = true;
            var failures = new List<Exception>();
            for (int index = leases.Count - 1; index >= 0; index--) {
                try {
                    await leases[index].DisposeAsync();
                } catch (Exception release) {
                    failures.Add(release);
                }
            }
            if (failures.Count > 0) {
                throw new AggregateException("Source exclusion cleanup is unconfirmed.", failures);
            }
        }
    }
}
