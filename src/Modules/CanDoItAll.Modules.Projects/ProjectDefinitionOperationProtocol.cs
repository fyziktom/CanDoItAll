using System.Security.Cryptography;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectDefinitionProtocolRejectedException(Error error) : InvalidOperationException(error.Message) {
    internal Error Error { get; } = error;
}

public sealed class ProjectDefinitionOperationProtocol(
    DatasetActivationService activation,
    ProjectWriteAdmissionService admissions,
    FiniteOperationEpochStore epochs,
    ProjectDefinitionReceiptReader reader,
    IOptions<ProjectDefinitionOperationOptions> configuredOptions) {
    internal ProjectDefinitionLimits Limits => configuredOptions.Value.GetLimits();
    private FiniteOperationPoolRegistration Pool => ProjectFiniteOperationPool.Create(configuredOptions.Value);

    public async Task<ProjectDefinitionWriteContext> GetWriteContextAsync(ProjectMutationCallerScope caller,
        CancellationToken cancellationToken = default) {
        var binding = await activation.ReadActiveBindingAsync(admissions.DatabaseProfileId, cancellationToken);
        var pool = Pool;
        await epochs.RotateExpiredAsync(binding, pool, cancellationToken);
        var current = await epochs.ReadCurrentAsync(binding, pool, cancellationToken);
        if (current.AdmissionClosesAtUtc <= current.ObservedAtUtc) {
            throw new InvalidOperationException("The Projects operation epoch requires maintenance before new admission.");
        }
        return new(binding.Namespace, caller.Bind(binding.Namespace), new(current.Epoch), current.AdmissionClosesAtUtc,
            current.ReplayUntilUtc, [ProjectDefinitionRequestVersion.VersionOne], [ProjectDefinitionResultVersion.VersionOne], Limits);
    }

    internal async Task<ProjectDefinitionOperationAttempt> PrepareAsync(ProjectDefinitionOperation operation,
        ProjectDefinitionInvocation invocation, ProjectDefinitionNormalizedIntent intent, CancellationToken cancellationToken) {
        var binding = await activation.ReadActiveBindingAsync(admissions.DatabaseProfileId, cancellationToken);
        if (binding.Namespace != operation.Namespace) {
            throw Rejected(ProjectErrorCodes.DefinitionDatasetChanged, "The original operation belongs to another dataset activation.");
        }
        if (!invocation.Caller.Matches(binding.Namespace, operation.CallerBinding)) {
            throw Rejected(ProjectErrorCodes.DefinitionCallerChanged, "The original operation belongs to another caller context.");
        }
        var digest = ProjectDefinitionOperationCodec.ComputeDigest(intent, operation, invocation.Caller,
            ProjectDefinitionLimits.VersionOneMaximum, out var canonicalByteLength);
        return new(binding, operation, invocation, intent, digest, canonicalByteLength);
    }

    internal async Task<bool> TryReadAcceptedAsync(ProjectDefinitionOperationAttempt attempt, CancellationToken cancellationToken) {
        var observed = await reader.ObserveAsync(attempt.Binding, attempt.Operation, attempt.Invocation, cancellationToken);
        if (observed.Evidence?.SemanticDigest is { } digest && !CryptographicOperations.FixedTimeEquals(digest, attempt.Digest.ToArray())) {
            throw Rejected(ProjectErrorCodes.DefinitionOperationMismatch, "This operation identifier already belongs to different definition intent.");
        }
        switch (observed.Lookup) {
            case ProjectDefinitionLookup.FoundFull full:
                attempt.Accepted = new ProjectDefinitionDecodedReceipt.Full(full.Result);
                break;
            case ProjectDefinitionLookup.FoundScalars scalar:
                attempt.Accepted = new ProjectDefinitionDecodedReceipt.Scalars(scalar.Result);
                break;
            case ProjectDefinitionLookup.NotObserved { State: ProjectOperationObservationState.Open }:
                return false;
            case ProjectDefinitionLookup.DatasetChanged:
                throw Rejected(ProjectErrorCodes.DefinitionDatasetChanged, "The dataset activation changed.");
            case ProjectDefinitionLookup.CallerContextChanged:
                throw Rejected(ProjectErrorCodes.DefinitionCallerChanged, "The current caller cannot reuse this operation context.");
            case ProjectDefinitionLookup.Expired:
                throw Rejected(ProjectErrorCodes.DefinitionOperationExpired, "The operation's retained recovery horizon has expired.");
            case ProjectDefinitionLookup.InvalidContext:
                throw Rejected(ProjectErrorCodes.DefinitionOperationUnknown, "The operation identifies an epoch that has not been issued.");
            case ProjectDefinitionLookup.ClosedAbsent:
            case ProjectDefinitionLookup.NotObserved:
                throw Rejected(ProjectErrorCodes.DefinitionOperationClosed, "The operation's admission epoch is closed or draining. Resolve its original outcome before preparing new work.");
            case ProjectDefinitionLookup.DetailUnavailable:
                throw Rejected(ProjectErrorCodes.DefinitionReceiptUnavailable, "The retained operation cannot disclose its accepted detail under current access or result support.");
            default:
                throw new InvalidOperationException("The Projects operation lookup returned an unsupported outcome.");
        }
        attempt.Replayed = true;
        attempt.CreationFingerprint = observed.Evidence!.CreationFingerprint;
        return true;
    }

    internal async Task<bool> EnterBeforeOwnerAdmissionAsync(ProjectsDbContext context, ProjectDefinitionOperationAttempt attempt,
        CancellationToken cancellationToken) {
        attempt.Epoch = await epochs.EnterAsync(context, attempt.Binding, Pool, attempt.Operation.Epoch.Value, cancellationToken);
        var operationKey = FormattableString.Invariant($"projects:definition-operation:{attempt.Operation.Namespace.Value:D}:{attempt.Operation.Epoch.Value}:{(int)attempt.Invocation.Caller.Kind}:{attempt.Invocation.Caller.Key.Hex}:{attempt.Operation.Id:D}");
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context, [operationKey], cancellationToken);
        if (await ProjectDefinitionOperationStore.FindLockedAsync(context, attempt.Operation, attempt.Invocation.Caller, cancellationToken) is { } receipt) {
            if (!CryptographicOperations.FixedTimeEquals(receipt.SemanticDigest, attempt.Digest.ToArray())) {
                throw Rejected(ProjectErrorCodes.DefinitionOperationMismatch, "This operation identifier already belongs to different definition intent.");
            }
            attempt.Accepted = ProjectDefinitionOperationCodec.DecodeAccepted(new(receipt.ResultVersion), receipt.AcceptedPayload,
                ProjectDefinitionLimits.VersionOneMaximum);
            attempt.CreationFingerprint = receipt.CreationFingerprint;
            attempt.ReplayObserved = true;
            return true;
        }
        await RequireAdmissibleAsync(context, attempt, cancellationToken);
        return false;
    }

    internal Task RequireAdmissibleAsync(ProjectsDbContext context, ProjectDefinitionOperationAttempt attempt, CancellationToken cancellationToken)
        => epochs.RequireAdmissibleAsync(context, attempt.Epoch
            ?? throw new InvalidOperationException("The operation has not entered its original epoch."), cancellationToken);

    internal async Task RecordAcceptedAsync(ProjectsDbContext context, ProjectDefinitionOperationAttempt attempt,
        ProjectEditorAcknowledgement acknowledgement, string? creationFingerprint, CancellationToken cancellationToken) {
        var now = await FiniteOperationEpochStore.ReadTimeAsync(context, cancellationToken);
        var accepted = new ProjectDefinitionAccepted<ProjectEditorAcknowledgement>(attempt.Operation, ProjectDefinitionResultVersion.VersionOne,
            attempt.Intent.Kind, now, attempt.Epoch!.ReplayUntilUtc, acknowledgement);
        var payload = ProjectDefinitionOperationCodec.EncodeAccepted(accepted, Limits);
        await InsertAsync(context, attempt, acknowledgement.Definition, now, payload, creationFingerprint, cancellationToken);
        attempt.Accepted = new ProjectDefinitionDecodedReceipt.Full(accepted);
        attempt.CreationFingerprint = creationFingerprint;
    }

    internal async Task RecordAcceptedAsync(ProjectsDbContext context, ProjectDefinitionOperationAttempt attempt,
        ProjectDefinitionScalarsAcknowledgement acknowledgement, CancellationToken cancellationToken) {
        var now = await FiniteOperationEpochStore.ReadTimeAsync(context, cancellationToken);
        var accepted = new ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement>(attempt.Operation, ProjectDefinitionResultVersion.VersionOne,
            attempt.Intent.Kind, now, attempt.Epoch!.ReplayUntilUtc, acknowledgement);
        var payload = ProjectDefinitionOperationCodec.EncodeAccepted(accepted, Limits);
        await InsertAsync(context, attempt, acknowledgement.Definition, now, payload, null, cancellationToken);
        attempt.Accepted = new ProjectDefinitionDecodedReceipt.Scalars(accepted);
    }

    private static async Task InsertAsync(ProjectsDbContext context, ProjectDefinitionOperationAttempt attempt,
        ProjectDefinitionExpectation definition, DateTimeOffset now, ProjectDefinitionEncodedResult payload,
        string? creationFingerprint, CancellationToken cancellationToken) {
        context.Add(new ProjectDefinitionReceiptRecord {
            NamespaceId = attempt.Operation.Namespace.Value,
            Epoch = attempt.Operation.Epoch.Value,
            CallerKind = attempt.Invocation.Caller.Kind,
            CallerKey = attempt.Invocation.Caller.Key.ToArray(),
            OperationId = attempt.Operation.Id,
            SchemaVersion = attempt.Intent.Version.Schema,
            NormalizationVersion = attempt.Intent.Version.Normalization,
            ResultVersion = ProjectDefinitionResultVersion.VersionOne.Value,
            CommandKind = attempt.Intent.Kind,
            SemanticDigest = attempt.Digest.ToArray(),
            ProjectId = definition.Project.ProjectId,
            LifetimeId = definition.Project.LifetimeId,
            AcceptedRevision = definition.Revision,
            AcceptedAtUtc = now,
            AcceptedPayload = payload.ToArray(),
            CreationFingerprint = creationFingerprint
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    internal static ProjectDefinitionProtocolRejectedException Rejected(string code, string message) => new(Error.Failure(message, code));
}

internal sealed class ProjectDefinitionOperationAttempt(DatasetActivationBinding binding, ProjectDefinitionOperation operation,
    ProjectDefinitionInvocation invocation, ProjectDefinitionNormalizedIntent intent, ProjectDefinitionDigest digest, int canonicalByteLength) {
    internal DatasetActivationBinding Binding { get; } = binding;
    internal ProjectDefinitionOperation Operation { get; } = operation;
    internal ProjectDefinitionInvocation Invocation { get; } = invocation;
    internal ProjectDefinitionNormalizedIntent Intent { get; } = intent;
    internal ProjectDefinitionDigest Digest { get; } = digest;
    internal int CanonicalByteLength { get; } = canonicalByteLength;
    internal FiniteOperationEpochSnapshot? Epoch { get; set; }
    internal ProjectDefinitionDecodedReceipt? Accepted { get; set; }
    internal string? CreationFingerprint { get; set; }
    internal bool Replayed { get; set; }
    internal bool ReplayObserved { get; set; }
    internal bool CommitAttempted { get; set; }
    internal bool Committed { get; set; }
    internal ProjectDefinitionWriteAccessDecision? CurrentWriteRefusal { get; set; }
}
