using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectDefinitionGovernedBinding(ProjectMutationCallerScope Caller, ProjectDefinitionEffectBinding Effect);

internal static class ProjectDefinitionGovernedBindings {
    internal static ProjectDefinitionGovernedBinding ForAgent(ProjectAgentMutationAdmission original,
        AgentToolSessionObservation verifiedSession, AgentToolAdmittedInvocation verifiedProposal) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(verifiedSession);
        ArgumentNullException.ThrowIfNull(verifiedProposal);
        var session = verifiedSession.Session;
        var governance = original.Governance;
        if (original.ProjectIds.Count > 1024 || original.ProjectLifetimes.Count > 1024 ||
            governance is null || verifiedSession.Governance is null || session.Reference.BackgroundSource is not null ||
            session.Purpose != AgentRuntimeContextPurpose.InteractiveChat || session.AgentId != original.AgentId ||
            session.Profile.ProfileId != original.DatabaseProfileId || session.Reference.AuthorityId != governance.AuthorityId ||
            governance.AgentId != original.AgentId || governance.DatabaseProfileId != original.DatabaseProfileId ||
            session.Profile.Generation != governance.DatabaseProfileGeneration || verifiedProposal.Session != session.Reference ||
            original.Operation != verifiedProposal.Payload.ToolName || !MatchesDefinitionTool(original) ||
            !Encode("projects:agent-governance:v1", writer => Governance(writer, governance)).AsSpan().SequenceEqual(
                Encode("projects:agent-governance:v1", writer => Governance(writer, verifiedSession.Governance)))) {
            throw Invalid("The verified Agent session, original source and exact proposal do not match.");
        }
        RequireProposal(verifiedProposal);
        var effect = Encode("projects:agent-effect:v1", writer => {
            writer.Id(original.AgentId);
            writer.Id(original.DatabaseProfileId);
            writer.Number((int)original.Domain);
            writer.Text(original.Operation!);
            writer.Flag(original.CanRead);
            writer.Flag(original.CanCreateProjects);
            writer.Flag(original.CanCreateSubprojects);
            writer.Flag(original.CanWriteTasks);
            writer.Flag(original.CanWriteStructure);
            writer.Flag(original.AllowAllProjects);
            writer.Collection(original.ProjectIds.Order(), writer.Id);
            writer.Collection(original.ProjectLifetimes.OrderBy(item => item.DatabaseProfileId).ThenBy(item => item.ProjectId).ThenBy(item => item.LifetimeId),
                item => Project(writer, new(item.DatabaseProfileId, item.ProjectId, item.LifetimeId)));
            Project(writer, original.SourceProject);
            Reservation(writer, original.CreatedProjectReservation);
            Governance(writer, governance);
            writer.Text(session.Profile.Fingerprint);
            writer.Number(session.Profile.Generation.Value);
            Proposal(writer, verifiedProposal);
        });
        return new(AgentCaller(session), new(ProjectDefinitionEffectKind.Agent, 1, effect));
    }

    internal static ProjectDefinitionGovernedBinding ForProcess(ProjectProcessMutationAdmission original,
        ProjectProcessProjectOperation operation, AgentToolAdmittedInvocation? verifiedProposal = null) {
        ArgumentNullException.ThrowIfNull(original);
        var dispatch = original.Dispatch;
        var evidence = dispatch.Evidence;
        var tool = operation switch {
            ProjectProcessProjectOperation.Update => ProjectStructureToolPolicy.ProjectStructureProjectUpdate,
            ProjectProcessProjectOperation.CreateRoot => ProjectStructureToolPolicy.ProjectStructureProjectCreate,
            ProjectProcessProjectOperation.CreateChild => ProjectStructureToolPolicy.ProjectStructureSubprojectCreate,
            _ => throw Invalid("The Process operation is not a supported project definition effect.")
        };
        if (original.CreatedProjects.Length > 1024 || dispatch.SourceAuthority is null || dispatch.ProjectReference is null ||
            string.IsNullOrWhiteSpace(dispatch.OwnerFingerprint) || string.IsNullOrWhiteSpace(dispatch.ReadinessHash) ||
            dispatch.ProjectId != original.ProjectAdmission.ProjectId || dispatch.ProjectReference.RunId != evidence.RunId ||
            dispatch.ProjectReference.StepInstanceId != evidence.StepInstanceId ||
            verifiedProposal is not null && (verifiedProposal.Payload.ToolName != tool ||
                verifiedProposal.Session.ExecutionRunId != evidence.ExecutionRunId ||
                verifiedProposal.Session.BackgroundSource is not { } background || background.SourceId != evidence.StepKey ||
                background.OwnerFingerprint != AgentToolProtocolEnvelope.ComputeDigest(dispatch.OwnerFingerprint))) {
            throw Invalid("The Process source, saved execution and exact proposal do not match.");
        }
        dispatch.SourceAuthority.Validate();
        if (verifiedProposal is not null) {
            RequireProposal(verifiedProposal);
        }
        var effect = Encode("projects:process-effect:v1", writer => {
            writer.Number((int)operation);
            writer.Id(evidence.ExecutionRunId);
            writer.Id(evidence.ExecutorAgentId);
            writer.Id(evidence.RunId.Value);
            writer.Id(evidence.StepInstanceId.Value);
            writer.Id(evidence.DispatchClaimToken);
            writer.Text(evidence.StepKey);
            writer.Time(evidence.ExecutionCreatedAtUtc);
            writer.Id(dispatch.RootRunId.Value);
            writer.Text(dispatch.OwnerFingerprint);
            writer.Text(dispatch.ReadinessHash);
            writer.Set(dispatch.AllowedOperations);
            writer.Text(dispatch.OperationTargetScope);
            writer.Id(dispatch.ProjectReference.AdmissionId.Value);
            writer.Text(dispatch.ProjectReference.PreparationFingerprint);
            writer.Text(dispatch.ProjectReference.ReadinessHash);
            Source(writer, dispatch.SourceAuthority);
            CapabilityScope(writer, dispatch.CapabilityScope);
            writer.Collection(original.CreatedProjects.OrderBy(item => item.Id), item => Reservation(writer, item));
            writer.Flag(verifiedProposal is not null);
            if (verifiedProposal is not null) {
                Proposal(writer, verifiedProposal);
            }
        });
        return new(ProcessCaller(dispatch), new(ProjectDefinitionEffectKind.Process, 1, effect));
    }

    internal static ProjectMutationCallerScope AgentCaller(AgentToolSessionAdmission verified) {
        if (verified.Purpose != AgentRuntimeContextPurpose.InteractiveChat || verified.Reference.BackgroundSource is not null) {
            throw Invalid("The saved session is not an interactive Agent execution.");
        }
        return ProjectMutationCallerScope.FromAdmittedExecution(new(Encode("projects:agent-caller:v1", writer => {
            writer.Id(verified.Profile.ProfileId);
            writer.Id(verified.AgentId);
            writer.Id(verified.Reference.ExecutionRunId);
            writer.Id(verified.Reference.ChatSessionId);
            writer.Id(verified.Reference.AuthorityId.Value);
        })));
    }

    internal static ProjectMutationCallerScope ProcessCaller(ProcessExecutionDispatchAuthority verified)
        => ProjectMutationCallerScope.FromAdmittedExecution(new(Encode("projects:process-caller:v1", writer => {
            writer.Id(verified.SourceAuthority?.DatabaseProfileId ?? throw Invalid("The saved Process source is unavailable."));
            writer.Id(verified.Evidence.ExecutorAgentId);
            writer.Id(verified.Evidence.ExecutionRunId);
            writer.Id(verified.Evidence.RunId.Value);
            writer.Id(verified.Evidence.StepInstanceId.Value);
        })));

    internal static void RequireSourceBounds(AgentExecutionGovernanceSnapshot? agent, ProcessExecutionDispatchAuthority? process) {
        _ = Encode("projects:source-bounds:v1", writer => {
            if (agent is not null) {
                Governance(writer, agent);
            }
            if (process is not null) {
                Source(writer, process.SourceAuthority ?? throw Invalid("The saved Process source is unavailable."));
                CapabilityScope(writer, process.CapabilityScope);
                writer.Set(process.AllowedOperations);
            }
        });
    }

    private static bool MatchesDefinitionTool(ProjectAgentMutationAdmission original) => (original.Operation, original.Domain) switch {
        (ProjectStructureToolPolicy.ProjectStructureProjectUpdate, ProjectAgentMutationDomain.NonTaskStructure) or
        (ProjectStructureToolPolicy.ProjectStructureProjectCreate, ProjectAgentMutationDomain.ProjectCreation) or
        (ProjectStructureToolPolicy.ProjectStructureSubprojectCreate, ProjectAgentMutationDomain.SubprojectCreation) => true,
        _ => false
    };

    private static void RequireProposal(AgentToolAdmittedInvocation proposal) {
        if (proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != proposal.Payload.Digest ||
            proposal.BatchId.Value == Guid.Empty || proposal.IntentId.Value == Guid.Empty ||
            proposal.Payload.Effect != AgentToolProposalEffect.Mutation) {
            throw Invalid("A governed definition effect requires its exact approved mutation proposal.");
        }
    }

    private static void Proposal(PrimitiveWriter writer, AgentToolAdmittedInvocation proposal) {
        writer.Id(proposal.BatchId.Value);
        writer.Id(proposal.IntentId.Value);
        writer.Text(proposal.Payload.ToolName);
        writer.Number(proposal.Payload.SemanticVersion);
        writer.Text(proposal.Payload.Digest.Value);
        writer.Number((int)proposal.Payload.Effect);
        writer.Number((int)proposal.Payload.Recovery);
        writer.Number((int)proposal.ApprovalStatus);
        writer.Text(proposal.ApprovedDigest!.Value.Value);
        writer.Flag(proposal.Session.BackgroundSource is not null);
        if (proposal.Session.BackgroundSource is { } background) {
            writer.Text(background.SourceKind);
            writer.Text(background.SourceId);
            writer.Text(background.OwnerFingerprint.Value);
            writer.Text(background.ExecutionFingerprint.Value);
        }
    }

    private static void Governance(PrimitiveWriter writer, AgentExecutionGovernanceSnapshot value) {
        writer.Id(value.AuthorityId.Value);
        writer.Id(value.AgentId);
        writer.Id(value.DatabaseProfileId);
        writer.Number(value.DatabaseProfileGeneration.Value);
        writer.Number((int)value.WorkspaceScope.Kind);
        writer.Text(value.WorkspaceScope.Key);
        writer.Number(value.EffectiveSchemaVersion);
        Project(writer, value.SourceProjectLifetime is { } source ? new(source.DatabaseProfileId, source.ProjectId, source.LifetimeId) : null);
        writer.Flag(value.ReadAllowed);
        writer.Flag(value.MutationAllowed);
        writer.Text(value.PolicyVersion);
        writer.Text(value.PolicyFingerprint);
        writer.Set(value.AllowedOperations);
        writer.Set(value.AllowedCapabilityKeys);
        writer.Set(value.WritableExternalTargetAliases);
        writer.Set(value.ReadOnlyExternalTargetAliases);
        writer.Set(value.AllowedManagedArtifactReadRefs);
    }

    private static void Source(PrimitiveWriter writer, ProcessLaunchAuthority source) {
        writer.Id(source.DatabaseProfileId);
        Project(writer, source.ProjectAdmission is { } project ? new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId) : null);
        writer.Flag(source.CanCreateTasks);
        writer.Flag(source.CanCreateAssets);
        writer.Text(source.PolicyFingerprint);
        writer.Flag(source.ProjectMutations is not null);
        if (source.ProjectMutations is { } mutations) {
            writer.Number(mutations.SchemaVersion);
            writer.Flag(mutations.CanCreateProjects);
            writer.Flag(mutations.CanCreateSubprojects);
            writer.Flag(mutations.CanChangeHierarchy);
            writer.Flag(mutations.CanMoveNodesToSubproject);
        }
        switch (source.Principal) {
            case ProcessLaunchPrincipal.LocalOperator local:
                writer.Number(1);
                writer.Number((int)local.Surface);
                break;
            case ProcessLaunchPrincipal.AuthenticatedOperator authenticated:
                writer.Number(2);
                writer.Text(authenticated.SubjectId);
                writer.Time(authenticated.ExpiresAtUtc);
                break;
            case ProcessLaunchPrincipal.AgentExecution agent:
                writer.Number(3);
                writer.Number((int)agent.Operation);
                var ceiling = agent.Ceiling;
                writer.Id(ceiling.AuthorityId);
                writer.Id(ceiling.AgentId);
                writer.Number(ceiling.DatabaseProfileGeneration);
                writer.Number((int)ceiling.WorkspaceScopeKind);
                writer.Text(ceiling.WorkspaceScopeKey);
                writer.Number(ceiling.EffectiveSchemaVersion);
                Project(writer, ceiling.SourceProjectAdmission is { } sourceProject ?
                    new(sourceProject.DatabaseProfileId, sourceProject.ProjectId, sourceProject.LifetimeId) : null);
                writer.Flag(ceiling.ReadAllowed);
                writer.Flag(ceiling.MutationAllowed);
                writer.Text(ceiling.PolicyVersion);
                writer.Text(ceiling.PolicyFingerprint);
                writer.Set(ceiling.AllowedOperations);
                writer.Set(ceiling.AllowedCapabilityKeys);
                writer.Set(ceiling.WritableExternalTargetAliases);
                writer.Set(ceiling.ReadOnlyExternalTargetAliases);
                writer.Set(ceiling.AllowedManagedArtifactReadRefs);
                break;
            default:
                throw Invalid("The Process source principal kind is unsupported.");
        }
    }

    private static void CapabilityScope(PrimitiveWriter writer, ProcessCapabilityScope scope) {
        ArgumentNullException.ThrowIfNull(scope);
        writer.Collection(scope.Directives, item => {
            writer.Number((int)item.Kind);
            writer.Number((int)item.Target.Kind);
            writer.Text(item.Target.Value);
            writer.Text(item.Target.SecondaryValue);
            writer.Text(item.Reason);
        }, 128);
        writer.Collection(scope.InstructionFragments, item => {
            writer.Text(item.Key);
            writer.Text(item.Title);
            writer.Text(item.Content);
            writer.Number((int)item.Placement);
        }, 128);
        writer.Collection(scope.RequiredReceipts, item => {
            writer.Text(item.Key);
            writer.Number((int)item.Kind);
            writer.Text(item.ToolName);
            writer.Text(item.RuntimeToolProviderKey);
            writer.Text(item.McpServerKey);
            writer.Number(item.MinimumCount);
            writer.Flag(item.RequireSuccessfulExit);
            writer.Flag(item.RequireCurrentRun);
            writer.Number((int)item.Activation);
            writer.Number((int)item.Purpose);
            writer.Set(item.ApplicableBranchOutcomeKeys, 32);
            writer.Text(item.Reason);
        }, 1024);
    }

    private static void Project(PrimitiveWriter writer, ProjectWriteAdmission? project) {
        writer.Flag(project is not null);
        if (project is null) {
            return;
        }
        writer.Id(project.DatabaseProfileId);
        writer.Id(project.ProjectId);
        writer.Id(project.LifetimeId);
    }

    private static void Reservation(PrimitiveWriter writer, ProjectCreationReservation? reservation) {
        writer.Flag(reservation is not null);
        if (reservation is null) {
            return;
        }
        writer.Id(reservation.Id);
        writer.Id(reservation.DatabaseProfileId);
        writer.Id(reservation.ProjectId);
        writer.Id(reservation.LifetimeId);
        writer.Id(reservation.RequesterId);
        writer.OptionalId(reservation.ParentProjectId);
        writer.OptionalId(reservation.ParentLifetimeId);
    }

    private static byte[] Encode(string domain, Action<PrimitiveWriter> encode) {
        using var writer = new PrimitiveWriter();
        writer.Text(domain);
        encode(writer);
        return writer.Finish();
    }

    private static InvalidOperationException Invalid(string message) => new(message);

    private sealed class PrimitiveWriter : IDisposable {
        private const int MaximumFieldBytes = 65_536;
        private const int MaximumTotalBytes = 1_048_576;
        private static readonly UTF8Encoding Utf8 = new(false, true);
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private int total;

        internal void Text(string value) {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > MaximumFieldBytes || Utf8.GetByteCount(value) > MaximumFieldBytes) {
                throw Invalid("A governed binding field exceeds its supported byte limit.");
            }
            var count = Utf8.GetByteCount(value);
            RequireBytes(4 + count);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, count);
            hash.AppendData(length);
            hash.AppendData(Utf8.GetBytes(value));
        }

        internal void Number(long value) {
            RequireBytes(8);
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
            hash.AppendData(bytes);
        }

        internal void Id(Guid value) {
            if (value == Guid.Empty) {
                throw Invalid("A governed binding requires nonempty admitted identifiers.");
            }
            Text(value.ToString("D"));
        }

        internal void OptionalId(Guid? value) {
            Flag(value.HasValue);
            if (value.HasValue) {
                Id(value.Value);
            }
        }

        internal void Flag(bool value) => Number(value ? 1 : 0);

        internal void Time(DateTimeOffset value) {
            if (value.Offset != TimeSpan.Zero) {
                throw Invalid("A governed binding requires UTC timestamps.");
            }
            Number(value.UtcTicks);
        }

        internal void Set(IEnumerable<string> values, int maximum = 500) {
            var source = Capture(values, maximum);
            long bytes = 8;
            foreach (var value in source) {
                if (value.Length > MaximumFieldBytes) {
                    throw Invalid("A governed binding field exceeds its supported byte limit.");
                }
                var count = Utf8.GetByteCount(value);
                bytes += 4L + count;
                if (count > MaximumFieldBytes || bytes > MaximumTotalBytes - total) {
                    throw Invalid("A governed binding set exceeds its supported byte limit.");
                }
            }
            var captured = source.Select(value => value.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            Number(captured.Length);
            foreach (var value in captured) {
                Text(value);
            }
        }

        internal void Collection<T>(IEnumerable<T> values, Action<T> encode, int maximum = 1024) {
            var captured = Capture(values, maximum);
            Number(captured.Count);
            foreach (var value in captured) {
                encode(value);
            }
        }

        private static List<T> Capture<T>(IEnumerable<T> values, int maximum) {
            ArgumentNullException.ThrowIfNull(values);
            if (values.TryGetNonEnumeratedCount(out var count) && count > maximum) {
                throw Invalid("A governed binding collection exceeds its supported limit.");
            }
            List<T> captured = [];
            foreach (var value in values) {
                if (captured.Count == maximum || value is null) {
                    throw Invalid("A governed binding collection is invalid or exceeds its supported limit.");
                }
                captured.Add(value);
            }
            return captured;
        }

        private void RequireBytes(int count) {
            if (count > MaximumTotalBytes - total) {
                throw Invalid("The governed binding exceeds its supported aggregate byte limit.");
            }
            total += count;
        }

        internal byte[] Finish() => hash.GetHashAndReset();
        public void Dispose() => hash.Dispose();
    }
}
