using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectGovernedInvocationSource {
    internal static ProjectMutationAuthorization Bind(ProjectMutationAuthorization original, AgentToolAdmittedInvocation admitted) {
        var claim = AgentToolInvocationClaim.Current
            ?? throw ProjectDefinitionToolProposalCodec.Denied("The owner mutation has no original live tool claim.");
        if (claim.RunLease.Session != admitted.Session || claim.Binding.BatchId != admitted.BatchId ||
                claim.Binding.IntentId != admitted.IntentId || claim.Proposal.Payload != admitted.Payload) {
            throw ProjectDefinitionToolProposalCodec.Denied("The owner mutation differs from its exact admitted tool claim.");
        }
        return new(new BoundSource(original.SourceAuthority, claim), original.ExpectedProjects);
    }

    private sealed class BoundSource(IProjectMutationSourceAuthority source, AgentToolInvocationClaim claim) : IProjectMutationSourceAuthority {
        public async Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default) {
            await claim.RequireCurrentIdentityAsync(cancellationToken);
            var held = await source.AcquireAsync(target, cancellationToken);
            try {
                if (held is not IProjectGovernedInvocationLease original) {
                    throw ProjectDefinitionToolProposalCodec.Denied("The source cannot validate a tool claim under its held publication exclusion.");
                }
                var publication = await original.AcquireInvocationAsync(claim, cancellationToken);
                return new BoundLease(held, publication);
            } catch (Exception primary) {
                try {
                    await held.DisposeAsync();
                } catch (Exception cleanup) {
                    throw new AggregateException(primary, cleanup);
                }
                throw;
            }
        }
    }

    private sealed class BoundLease(IProjectMutationSourceLease held, AgentToolInvocationPublicationLease publication) : IProjectMutationSourceLease {
        public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => held.SourceProjects;
        public ProjectDeletionSourceRecovery CaptureDeletionRecoverySource() {
            var original = held.CaptureDeletionRecoverySource();
            var execution = publication.CaptureRecovery();
            if (execution is AgentExecutionPublicationRecovery.SharedCatalog) {
                return original;
            }
            if (original is not ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog catalog ||
                    execution is not AgentExecutionPublicationRecovery.Workspace workspace) {
                throw new NotSupportedException("Governed deletion recovery requires the original catalog and execution publication locations.");
            }
            var scope = workspace.Scope.Kind switch {
                WorkspaceScopeKind.Sandbox => ProjectDeletionExecutionWorkspaceKind.Sandbox,
                WorkspaceScopeKind.Project => ProjectDeletionExecutionWorkspaceKind.Project,
                _ => throw new NotSupportedException("The original execution workspace is not supported for deletion recovery.")
            };
            catalog.RequireCurrentHost();
            return new ProjectDeletionSourceRecovery.CanonicalInvocation(catalog,
                new(HostBoundPathPolicy.BindCurrent(workspace.RootPath, DateTimeOffset.UtcNow)), scope, workspace.Scope.Key);
        }

        public async Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
            await held.RequireForMutationAsync(cancellationToken);
            await publication.RequireCurrentAsync(cancellationToken);
        }

        public async Task RequireForDeletionCommitAsync(ProjectDeletionCommitTarget target, CancellationToken cancellationToken = default) {
            await held.RequireForDeletionCommitAsync(target, cancellationToken);
            await publication.RequireCurrentAsync(cancellationToken);
        }

        public async Task RequireForReservedCancellationCommitAsync(ProjectReservationCancellationTarget target,
            CancellationToken cancellationToken = default) {
            await held.RequireForReservedCancellationCommitAsync(target, cancellationToken);
            await publication.RequireCurrentAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            Exception? primary = null;
            try {
                await publication.DisposeAsync();
            } catch (Exception failure) {
                primary = failure;
            }
            try {
                await held.DisposeAsync();
            } catch (Exception cleanup) when (primary is not null) {
                throw new AggregateException(primary, cleanup);
            }
            if (primary is not null) {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
            }
        }
    }
}

internal interface IProjectGovernedInvocationLease {
    ValueTask<AgentToolInvocationPublicationLease> AcquireInvocationAsync(AgentToolInvocationClaim claim, CancellationToken cancellationToken);
}
