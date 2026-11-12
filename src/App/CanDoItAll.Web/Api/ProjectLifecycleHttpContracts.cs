using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Web.Api;

internal sealed record ProjectLifecycleApiSubmission(ProjectLifecycleOperation Operation,
    ProjectLifecycleInterpretation Interpretation, ProjectLifecycleApiCommand Command) {
    internal ProjectLifecycleSubmission ToSubmission() {
        ArgumentNullException.ThrowIfNull(Command);
        return new(Operation, Interpretation, Command.ToCommand());
    }

    internal static ProjectLifecycleApiSubmission From(ProjectLifecycleSubmission original)
        => new(original.Operation, original.Interpretation, ProjectLifecycleApiCommand.From(original.Command));
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ProjectLifecycleApiCommand.Delete), "delete")]
[JsonDerivedType(typeof(ProjectLifecycleApiCommand.EnsureLink), "ensureLink")]
[JsonDerivedType(typeof(ProjectLifecycleApiCommand.RemoveLink), "removeLink")]
[JsonDerivedType(typeof(ProjectLifecycleApiCommand.ReconnectLink), "reconnectLink")]
internal abstract record ProjectLifecycleApiCommand {
    private ProjectLifecycleApiCommand() {
    }

    internal abstract ProjectLifecycleCommand ToCommand();

    public sealed record Delete(ProjectDefinitionExpectation Expected) : ProjectLifecycleApiCommand {
        internal override ProjectLifecycleCommand ToCommand() => new ProjectLifecycleCommand.Delete(Expected);
    }

    public sealed record EnsureLink(ProjectWriteAdmission Parent, ProjectWriteAdmission Child) : ProjectLifecycleApiCommand {
        internal override ProjectLifecycleCommand ToCommand() => new ProjectLifecycleCommand.EnsureLink(Parent, Child);
    }

    public sealed record RemoveLink(ProjectHierarchyEdgeExpectation Expected) : ProjectLifecycleApiCommand {
        internal override ProjectLifecycleCommand ToCommand() => new ProjectLifecycleCommand.RemoveLink(Expected);
    }

    public sealed record ReconnectLink(ProjectHierarchyEdgeExpectation Expected, ProjectWriteAdmission NewParent) : ProjectLifecycleApiCommand {
        internal override ProjectLifecycleCommand ToCommand() => new ProjectLifecycleCommand.ReconnectLink(Expected, NewParent);
    }

    internal static ProjectLifecycleApiCommand From(ProjectLifecycleCommand command) => command switch {
        ProjectLifecycleCommand.Delete value => new Delete(value.Expected),
        ProjectLifecycleCommand.EnsureLink value => new EnsureLink(value.Parent, value.Child),
        ProjectLifecycleCommand.RemoveLink value => new RemoveLink(value.Expected),
        ProjectLifecycleCommand.ReconnectLink value => new ReconnectLink(value.Expected, value.NewParent),
        _ => throw new InvalidOperationException("The lifecycle owner returned an unsupported command.")
    };
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ProjectLifecycleApiAcknowledgement.DeletionCommitted), "deletionCommitted")]
[JsonDerivedType(typeof(ProjectLifecycleApiAcknowledgement.LinkEnsured), "linkEnsured")]
[JsonDerivedType(typeof(ProjectLifecycleApiAcknowledgement.LinkRemoved), "linkRemoved")]
[JsonDerivedType(typeof(ProjectLifecycleApiAcknowledgement.LinkReconnected), "linkReconnected")]
internal abstract record ProjectLifecycleApiAcknowledgement {
    private ProjectLifecycleApiAcknowledgement() {
    }

    public sealed record DeletionCommitted(ProjectDeletionCompletionBinding Completion) : ProjectLifecycleApiAcknowledgement;
    public sealed record LinkEnsured(Guid EdgeId, bool Created) : ProjectLifecycleApiAcknowledgement;
    public sealed record LinkRemoved : ProjectLifecycleApiAcknowledgement;
    public sealed record LinkReconnected(Guid DestinationEdgeId, bool Created) : ProjectLifecycleApiAcknowledgement;

    internal static ProjectLifecycleApiAcknowledgement From(ProjectLifecycleAcknowledgement acknowledgement) => acknowledgement switch {
        ProjectLifecycleAcknowledgement.DeletionCommitted value => new DeletionCommitted(value.Completion),
        ProjectLifecycleAcknowledgement.LinkEnsured value => new LinkEnsured(value.EdgeId, value.Created),
        ProjectLifecycleAcknowledgement.LinkRemoved => new LinkRemoved(),
        ProjectLifecycleAcknowledgement.LinkReconnected value => new LinkReconnected(value.DestinationEdgeId, value.Created),
        _ => throw new InvalidOperationException("The lifecycle owner returned an unsupported acknowledgement.")
    };
}

internal sealed record ProjectLifecycleApiReceipt(ProjectLifecycleApiSubmission Original, int ResultVersion,
    DateTimeOffset AcceptedAtUtc, DateTimeOffset ReplayUntilUtc, ProjectLifecycleApiAcknowledgement Acknowledgement) {
    internal static ProjectLifecycleApiReceipt From(ProjectLifecycleAccepted receipt)
        => new(ProjectLifecycleApiSubmission.From(receipt.Original), receipt.ResultVersion, receipt.AcceptedAtUtc,
            receipt.ReplayUntilUtc, ProjectLifecycleApiAcknowledgement.From(receipt.Acknowledgement));
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "status")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.Accepted), "accepted")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.Refused), "refused")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.Unconfirmed), "unconfirmed")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.Found), "found")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.NotObserved), "notObserved")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.ClosedAbsent), "closedAbsent")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.Expired), "expired")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.UnknownFutureEpoch), "unknownFutureEpoch")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.ContextChanged), "contextChanged")]
[JsonDerivedType(typeof(ProjectLifecycleApiResponse.Unavailable), "unavailable")]
internal abstract record ProjectLifecycleApiResponse {
    private ProjectLifecycleApiResponse() {
    }

    public sealed record Accepted(ProjectLifecycleApiReceipt Receipt, bool Replayed,
        ProjectLifecycleCompletionIssue? CompletionIssue, bool DiagnosticsUnavailable) : ProjectLifecycleApiResponse;
    public sealed record Refused(ProjectLifecycleApiSubmission Original, ProjectLifecycleRefusal Reason,
        bool DiagnosticsUnavailable) : ProjectLifecycleApiResponse;
    public sealed record Unconfirmed(ProjectLifecycleApiSubmission Original, ProjectLifecycleRecoveryReason Reason,
        bool DiagnosticsUnavailable) : ProjectLifecycleApiResponse;
    public sealed record Found(ProjectLifecycleApiReceipt Receipt) : ProjectLifecycleApiResponse;
    public sealed record NotObserved(ProjectLifecycleOperation Original, ProjectOperationObservationState State) : ProjectLifecycleApiResponse;
    public sealed record ClosedAbsent(ProjectLifecycleOperation Original, DateTimeOffset ClosedAtUtc,
        DateTimeOffset ObservedAtUtc, DateTimeOffset ReplayUntilUtc) : ProjectLifecycleApiResponse;
    public sealed record Expired(ProjectLifecycleOperation Original) : ProjectLifecycleApiResponse;
    public sealed record UnknownFutureEpoch(ProjectLifecycleOperation Original) : ProjectLifecycleApiResponse;
    public sealed record ContextChanged(ProjectLifecycleOperation Original, ProjectLifecycleContextChange Change) : ProjectLifecycleApiResponse;
    public sealed record Unavailable(ProjectLifecycleOperation Original, ProjectLifecycleUnavailableReason Reason) : ProjectLifecycleApiResponse;
}
