using System.Globalization;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static partial class OpenApiContractDescriptions {
    private const string OriginalExpectation = "Original project admission and definition revision from a coherent read. Preserve both unchanged with the draft; a newer token does not reconcile older values.";
    private const string Operation = "Complete original dataset, epoch, operation ID and caller binding. Preserve every field through retry and lookup; this address grants no authority.";
    private const string Scalars = "Complete project scalar values. Scalar-only commands preserve child collections; full replacement commands replace the submitted collections too.";
    private const string ExternalIdentity = "Normalized immutable external namespace/key pair, or null when unassigned. It identifies the project independently of its display name.";
    private const string PhaseRows = "Complete ordered phase collection. Full replacement removes omitted stored phases; an explicit empty array removes all phases.";
    private const string OptionRows = "Complete option collection. Full replacement removes omitted stored options; blank option rows are omitted from the accepted collection.";
    private const string Receipt = "Exact historical accepted receipt for the original operation. It is not a read of the project's current definition and must not overwrite later local edits.";
    private const string Timestamp = "UTC date/time or null when unset. Preserve the original wire timestamp, including fractional precision, when the value is not edited.";
    private const string PhaseStatus = "Phase status as a JSON integer: 0 Planned, 1 Active, 2 Blocked, 3 Completed.";
    private const string OptionCategory = "Technology option category as a JSON integer: 0 Language, 1 Database, 2 Ui, 3 ExternalApi, 4 Storage, 5 Deployment, 6 Testing, 7 Other.";
    private const string ProjectStatusDescription = "Project lifecycle label as a JSON integer: 0 Draft, 1 Active, 2 OnHold, 3 Completed, 4 Archived. The label does not itself lock or delete a project.";

    private static IReadOnlyDictionary<Type, Description> CreateDefinitionDescriptions() {
        Dictionary<Type, Description> descriptions = new() {
            [typeof(DatasetNamespaceId)] = new("Nonempty dataset activation identifier. Restore or replacement changes this identity even if project rows and revisions reappear."),
            [typeof(ProjectCallerBinding)] = new("Opaque server-derived caller binding for operation recovery. Preserve its supported pc1 value; it is not a credential or permission."),
            [typeof(ProjectDefinitionExpectation)] = new(OriginalExpectation,
                (nameof(ProjectDefinitionExpectation.Project), "Original database profile, project and lifetime from the coherent owner read."),
                (nameof(ProjectDefinitionExpectation.Revision), "Original definition revision; a current read cannot refresh an older draft's expectation.")),
            [typeof(ProjectDefinitionRequestVersion)] = new("Request schema and normalization versions interpreted together; preserve the original pair through retries."),
            [typeof(ProjectDefinitionOperation)] = new(Operation,
                (nameof(ProjectDefinitionOperation.Namespace), "Original dataset namespace retained with this operation."),
                (nameof(ProjectDefinitionOperation.Epoch), ProjectOperationEpochHttpContract.Description),
                (nameof(ProjectDefinitionOperation.Id), "Original operation identifier, chosen once before dispatch."),
                (nameof(ProjectDefinitionOperation.CallerBinding), "Original opaque caller binding retained with this operation; not an authorization credential.")),
            [typeof(ProjectDefinitionRequest<>)] = new("One frozen definition command and its original operation address and interpretation version.",
                (nameof(ProjectDefinitionRequest<object>.Operation), Operation),
                (nameof(ProjectDefinitionRequest<object>.Version), "Original request schema and normalization versions. Retry with the same versions and command."),
                (nameof(ProjectDefinitionRequest<object>.Command), "Original typed command. Do not alter its values, ordering or expectation while retrying this operation.")),
            [typeof(ProjectDefinitionResultVersion)] = new("Version of the retained accepted-result representation.",
                (nameof(ProjectDefinitionResultVersion.Value), "Positive accepted-result schema version; supported version 1.")),
            [typeof(ProjectDefinitionScalars)] = new("Project definition scalar fields, excluding children and external identity.", ScalarProperties()),
            [typeof(ProjectDefinitionExternalIdentity)] = new(ExternalIdentity,
                (nameof(ProjectDefinitionExternalIdentity.Namespace), "Stable external namespace, trimmed and lowercased; paired with key and immutable after assignment."),
                (nameof(ProjectDefinitionExternalIdentity.Key), "Stable key within the external namespace, trimmed and lowercased; the pair must be unique in the active dataset.")),
            [typeof(CreateProjectDefinitionCommand)] = new("Create one project definition with complete initial scalars, phases and options.",
                (nameof(CreateProjectDefinitionCommand.Scalars), Scalars),
                (nameof(CreateProjectDefinitionCommand.Phases), PhaseRows),
                (nameof(CreateProjectDefinitionCommand.Options), OptionRows),
                (nameof(CreateProjectDefinitionCommand.ExternalIdentity), "Optional immutable external identity to assign during creation. " + ExternalIdentity)),
            [typeof(ReplaceProjectDefinitionCommand)] = new("Conditionally replace every definition scalar and both complete child collections using the original expectation.",
                (nameof(ReplaceProjectDefinitionCommand.Expected), OriginalExpectation),
                (nameof(ReplaceProjectDefinitionCommand.Scalars), Scalars),
                (nameof(ReplaceProjectDefinitionCommand.Phases), PhaseRows),
                (nameof(ReplaceProjectDefinitionCommand.Options), OptionRows),
                (nameof(ReplaceProjectDefinitionCommand.AssignExternalIdentity), "Optional identity assignment for a currently unassigned project; null preserves an existing identity and cannot clear it.")),
            [typeof(UpdateProjectDefinitionScalarsCommand)] = new("Conditionally replace all scalar fields while preserving phases, options and external identity.",
                (nameof(UpdateProjectDefinitionScalarsCommand.Expected), OriginalExpectation),
                (nameof(UpdateProjectDefinitionScalarsCommand.Scalars), Scalars)),
            [typeof(ProjectDefinitionPhaseInput)] = new("One phase in a complete replacement command. Existing child IDs must belong to the original project.", PhaseProperties(input: true)),
            [typeof(ProjectDefinitionPhase)] = new("One persisted phase from the same coherent definition snapshot.", PhaseProperties(input: false)),
            [typeof(ProjectDefinitionOptionInput)] = new("One option in a complete replacement command. Existing child IDs must belong to the original project.", OptionProperties(input: true)),
            [typeof(ProjectDefinitionOption)] = new("One persisted option from the same coherent definition snapshot.", OptionProperties(input: false)),
            [typeof(ProjectDefinitionSnapshot)] = new("Coherent current definition and its exact original expectation from one database snapshot.", SnapshotProperties()),
            [typeof(ProjectDefinitionReadApiResponse)] = new("Coherent current definition plus a separately acquired current operation context. The dataset/caller binding is checked across the read.",
                [.. SnapshotProperties(), (nameof(ProjectDefinitionReadApiResponse.WriteContext), "Current operation admission context, not acceptance or a refreshed definition expectation.")]),
            [typeof(ProjectDefinitionScalarsAcknowledgement)] = new("Exact accepted scalar values and resulting expectation; it contains no child replacement acknowledgement.",
                (nameof(ProjectDefinitionScalarsAcknowledgement.Definition), "Accepted project lifetime and resulting definition revision. It describes this acceptance, not a later current read."),
                (nameof(ProjectDefinitionScalarsAcknowledgement.Scalars), "Normalized scalar values accepted by this operation."),
                (nameof(ProjectDefinitionScalarsAcknowledgement.ExternalIdentity), ExternalIdentity)),
            [typeof(ProjectEditorAcknowledgement)] = new("Exact accepted full definition, with submitted-index mappings for phase and option IDs.",
                [.. ScalarProperties(),
                    (nameof(ProjectEditorAcknowledgement.Definition), "Accepted project lifetime and resulting revision. Reconcile against the frozen submission before adopting it."),
                    (nameof(ProjectEditorAcknowledgement.Phases), "Accepted phases with one mapping for every submitted phase, in submitted order."),
                    (nameof(ProjectEditorAcknowledgement.Options), "Accepted nonblank options with their original submitted indexes; omitted blank rows have no persisted ID."),
                    (nameof(ProjectEditorAcknowledgement.ExternalIdentity), ExternalIdentity)]),
            [typeof(ProjectPhaseAcknowledgement)] = new("Accepted phase identity and values mapped to the frozen submitted collection.",
                [.. PhaseProperties(input: false),
                    (nameof(ProjectPhaseAcknowledgement.SubmittedIndex), "Zero-based index in the original submitted phase array. Validate complete, unique mapping before reconciling the draft.")]),
            [typeof(ProjectOptionAcknowledgement)] = new("Accepted option identity and values mapped to a nonblank frozen submitted row.",
                [.. OptionProperties(input: false),
                    (nameof(ProjectOptionAcknowledgement.SubmittedIndex), "Zero-based index in the original submitted option array; blank rows can be omitted from acceptance.")]),
            [typeof(ProjectDefinitionAccepted<>)] = new(Receipt,
                (nameof(ProjectDefinitionAccepted<object>.Operation), Operation),
                (nameof(ProjectDefinitionAccepted<object>.Version), "Version of this exact retained result, independent of the request interpretation version."),
                (nameof(ProjectDefinitionAccepted<object>.Kind), EnumText<ProjectDefinitionCommandKind>("Kind of the originally accepted command.")),
                (nameof(ProjectDefinitionAccepted<object>.AcceptedAtUtc), "Original acceptance timestamp in UTC. Preserve its wire precision; replay does not replace it with the lookup time."),
                (nameof(ProjectDefinitionAccepted<object>.ReplayUntilUtc), "Original minimum replay-retention promise in UTC. Availability still requires current authorization and an unretired dataset/epoch."),
                (nameof(ProjectDefinitionAccepted<object>.Acknowledgement), "Exact normalized values, revision and child mapping accepted for the original frozen command.")),
            [typeof(ProjectDefinitionAcceptedApiResponse<>)] = new("This operation is accepted; receipt is historical evidence and replayed describes only this response attempt.",
                (nameof(ProjectDefinitionAcceptedApiResponse<object>.Receipt), Receipt),
                (nameof(ProjectDefinitionAcceptedApiResponse<object>.Replayed), "True when this response returns an earlier acceptance. Exclude this attempt-local flag from persisted receipt identity."),
                (nameof(ProjectDefinitionAcceptedApiResponse<object>.Status), "Outcome discriminator; always accepted.")),
            [typeof(ProjectDefinitionRejectedApiResponse)] = new("This request attempt was rejected. It does not resolve an earlier unconfirmed attempt at the same operation address.",
                (nameof(ProjectDefinitionRejectedApiResponse.Operation), "Original operation when parsed successfully, otherwise null. Do not mint a new address to resolve prior uncertainty."),
                (nameof(ProjectDefinitionRejectedApiResponse.Errors), "Nonempty safe errors. Branch on stable codes rather than human-readable messages."),
                (nameof(ProjectDefinitionRejectedApiResponse.Status), "Outcome discriminator; always rejected.")),
            [typeof(ProjectDefinitionUnconfirmedApiResponse)] = new("Commit outcome is unresolved. Retain the original frozen request and use authorized lookup or an explicit exact retry.",
                (nameof(ProjectDefinitionUnconfirmedApiResponse.Operation), Operation),
                (nameof(ProjectDefinitionUnconfirmedApiResponse.Reason), EnumText<ProjectDefinitionRecoveryReason>("Why this response cannot establish the original outcome.")),
                (nameof(ProjectDefinitionUnconfirmedApiResponse.Status), "Outcome discriminator; always unconfirmed. HTTP status alone is not outcome evidence.")),
            [typeof(ProjectDefinitionWriteContext)] = new("Current caller-bound admission context and limits for preparing a new operation; no mutation has been accepted.",
                (nameof(ProjectDefinitionWriteContext.Namespace), "Current dataset activation identifier. Never rebind an older draft or dispatched request after dataset replacement."),
                (nameof(ProjectDefinitionWriteContext.CallerBinding), "Current opaque caller binding. Retain the original binding with the pending request."),
                (nameof(ProjectDefinitionWriteContext.Epoch), ProjectOperationEpochHttpContract.Description),
                (nameof(ProjectDefinitionWriteContext.AdmissionDeadlineUtc), "UTC deadline for admitting new effects in this issued epoch. It does not extend when an operation is retried."),
                (nameof(ProjectDefinitionWriteContext.ReplayUntilUtc), "Minimum replay-retention promise for accepted results in this epoch, subject to current authorization and dataset identity."),
                (nameof(ProjectDefinitionWriteContext.RequestVersions), "Supported request schema/normalization pairs for new requests. Preserve the chosen pair on retry."),
                (nameof(ProjectDefinitionWriteContext.ResultVersions), "Supported retained-result versions the client must understand before reconciling acceptance."),
                (nameof(ProjectDefinitionWriteContext.Limits), "Current new-request limits. Retained results use their original supported interpretation, not newly tightened admission limits.")),
            [typeof(ProjectDefinitionLimits)] = new("Owner-enforced limits for new definition submissions; byte limits are UTF-8 or encoded-owner sizes as specified.",
                (nameof(ProjectDefinitionLimits.MaxPhaseCount), "Maximum number of submitted phases, including all replacement rows."),
                (nameof(ProjectDefinitionLimits.MaxOptionCount), "Maximum number of submitted option rows before blank-row normalization."),
                (nameof(ProjectDefinitionLimits.MaxFieldUtf8Bytes), "Maximum UTF-8 byte length of one raw submitted text field."),
                (nameof(ProjectDefinitionLimits.MaxRawRequestBytes), "Maximum aggregate raw command input bytes before normalized command capture; the HTTP body also has its own bound."),
                (nameof(ProjectDefinitionLimits.MaxCanonicalCommandBytes), "Maximum owner-canonical encoded command bytes; this is not a client digest algorithm."),
                (nameof(ProjectDefinitionLimits.MaxAcceptedResultBytes), "Maximum encoded owner receipt bytes. It is not the size of the complete HTTP response envelope.")),
            [typeof(ProjectEditorModel)] = new("Mutable compatibility editor model; existing updates require the original complete definition expectation.",
                (nameof(ProjectEditorModel.ExpectedDefinition), OriginalExpectation)),
            [typeof(ProjectEditorSaveRequest)] = new("Compatibility create/full-replacement request without durable operation recovery.",
                [.. ScalarProperties(),
                    (nameof(ProjectEditorSaveRequest.Id), "Existing project ID for replacement, or null to create. An unknown existing ID is rejected."),
                    (nameof(ProjectEditorSaveRequest.ExpectedDefinition), OriginalExpectation),
                    (nameof(ProjectEditorSaveRequest.ExpectedLifetimeId), "Optional legacy lifetime mirror; when supplied it must equal expectedDefinition.project.lifetimeId."),
                    (nameof(ProjectEditorSaveRequest.ExternalNamespace), "Optional stable external namespace, supplied together with externalKey. Omit both to preserve an assigned pair."),
                    (nameof(ProjectEditorSaveRequest.ExternalKey), "Stable key within externalNamespace. An assigned pair cannot be changed or cleared."),
                    (nameof(ProjectEditorSaveRequest.Phases), PhaseRows + " Required and non-null for an existing project."),
                    (nameof(ProjectEditorSaveRequest.Options), OptionRows + " Required and non-null for an existing project.")]),
            [typeof(Error)] = new("Safe application error with a stable code and human-readable explanation.",
                (nameof(Error.Code), "Stable machine-readable reason. Use it together with the response outcome discriminator."),
                (nameof(Error.Message), "Human-readable explanation; wording is not a stable protocol and must not be parsed."),
                (nameof(Error.Severity), EnumText<ErrorSeverity>("Severity of this error.")))
        };
        AddLookupDescriptions(descriptions);
        AddEnum<ProjectDefinitionCommandKind>("Definition command kind.");
        AddEnum<ProjectDefinitionRecoveryReason>("Reason an attempted write remains unconfirmed.");
        AddEnum<ProjectOperationObservationState>("State of an epoch with no currently observed receipt. Neither state proves rollback.");
        AddEnum<ProjectDefinitionDetailUnavailableReason>("Reason historical accepted detail cannot currently be disclosed. It does not prove absence.");
        AddEnum<ProjectDefinitionInvalidContextReason>("Reason the original operation context cannot be resolved.");
        return descriptions;

        void AddEnum<T>(string text) where T : struct, Enum => descriptions.Add(typeof(T), new(EnumText<T>(text)));
    }

    private static void AddLookupDescriptions(Dictionary<Type, Description> descriptions) {
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse), new("Authorized observation of an original operation, discriminated by status. Only a found receipt or fully retained closedAbsent resolves prior uncertainty."));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.FoundFull), new("Exact retained full-definition acceptance; current project values may already differ.",
            (nameof(ProjectDefinitionLookupApiResponse.FoundFull.Receipt), Receipt)));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.FoundScalars), new("Exact retained scalar acceptance; children were preserved by this command.",
            (nameof(ProjectDefinitionLookupApiResponse.FoundScalars.Receipt), Receipt)));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.NotObserved), new("No receipt was observed in an open or draining epoch. An in-flight attempt may still commit; this is not absence proof.",
            (nameof(ProjectDefinitionLookupApiResponse.NotObserved.Operation), Operation),
            (nameof(ProjectDefinitionLookupApiResponse.NotObserved.State), EnumText<ProjectOperationObservationState>("Observed epoch state."))));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.ClosedAbsent), new("The closed epoch was fully retained and contained no receipt for this operation at the coherent observation; no new effect can enter that epoch.",
            (nameof(ProjectDefinitionLookupApiResponse.ClosedAbsent.Operation), Operation),
            (nameof(ProjectDefinitionLookupApiResponse.ClosedAbsent.ClosedAtUtc), "UTC instant when new effect admission to the epoch closed."),
            (nameof(ProjectDefinitionLookupApiResponse.ClosedAbsent.ObservedAtUtc), "UTC time of this coherent retained-absence observation."),
            (nameof(ProjectDefinitionLookupApiResponse.ClosedAbsent.RetainedUntilUtc), "Minimum guaranteed retention bound, not an expiry test. A still-unretired epoch can provide valid absence proof after this timestamp.")));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.Expired), new("The original epoch is retired; detail and absence can no longer be proven. Preserve uncertainty rather than minting a replacement operation.",
            (nameof(ProjectDefinitionLookupApiResponse.Expired.Operation), Operation)));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.DatasetChanged), new("The active dataset differs from the operation's original namespace. No outcome in the original dataset is established.",
            (nameof(ProjectDefinitionLookupApiResponse.DatasetChanged.Operation), Operation)));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.CallerContextChanged), new("The current authorized caller differs from the operation's original caller binding. No original outcome is disclosed.",
            (nameof(ProjectDefinitionLookupApiResponse.CallerContextChanged.Operation), Operation)));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.InvalidContext), new("The original context cannot be observed as a supported issued epoch. This is not permission to replace its identity.",
            (nameof(ProjectDefinitionLookupApiResponse.InvalidContext.Operation), Operation),
            (nameof(ProjectDefinitionLookupApiResponse.InvalidContext.Reason), EnumText<ProjectDefinitionInvalidContextReason>("Invalid operation context reason."))));
        descriptions.Add(typeof(ProjectDefinitionLookupApiResponse.DetailUnavailable), new("Accepted detail cannot currently be read or decoded. This does not establish rollback or safe resubmission.",
            (nameof(ProjectDefinitionLookupApiResponse.DetailUnavailable.Operation), Operation),
            (nameof(ProjectDefinitionLookupApiResponse.DetailUnavailable.Reason), EnumText<ProjectDefinitionDetailUnavailableReason>("Why receipt detail is unavailable."))));
    }

    private static (string, string)[] ScalarProperties() => [
        (nameof(ProjectDefinitionScalars.Name), "Required project display name; normalized by trimming."),
        (nameof(ProjectDefinitionScalars.Description), "Project description text; normalized by trimming."),
        (nameof(ProjectDefinitionScalars.Objective), "Project objective text; normalized by trimming."),
        (nameof(ProjectDefinitionScalars.Status), ProjectStatusDescription),
        (nameof(ProjectDefinitionScalars.CurrentPhase), "Current phase as free text, independent of the ordered phase collection."),
        (nameof(ProjectDefinitionScalars.TargetDateUtc), Timestamp)
    ];

    private static (string, string)[] SnapshotProperties() => [
        (nameof(ProjectDefinitionSnapshot.Expectation), OriginalExpectation),
        (nameof(ProjectDefinitionSnapshot.Scalars), "Normalized scalar values from this same coherent snapshot."),
        (nameof(ProjectDefinitionSnapshot.ExternalIdentity), ExternalIdentity),
        (nameof(ProjectDefinitionSnapshot.Phases), "Persisted phases in their stored order from the same snapshot."),
        (nameof(ProjectDefinitionSnapshot.Options), "Persisted option rows from the same snapshot; no synthetic blank editor rows.")
    ];

    private static (string, string)[] PhaseProperties(bool input) => [
        (nameof(ProjectDefinitionPhase.Id), input ? "Original persisted phase ID, or null for a new row. A non-null ID must belong to the target project." : "Persisted phase ID assigned by the owner."),
        (nameof(ProjectDefinitionPhase.Name), "Required phase name; normalized by trimming."),
        (nameof(ProjectDefinitionPhase.Goal), "Phase goal text; normalized by trimming."),
        (nameof(ProjectDefinitionPhase.Status), PhaseStatus),
        (nameof(ProjectDefinitionPhase.StartDateUtc), "Planned phase start. " + Timestamp),
        (nameof(ProjectDefinitionPhase.EndDateUtc), "Planned phase end. " + Timestamp)
    ];

    private static (string, string)[] OptionProperties(bool input) => [
        (nameof(ProjectDefinitionOption.Id), input ? "Original persisted option ID, or null for a new row. A non-null ID must belong to the target project." : "Persisted option ID assigned by the owner."),
        (nameof(ProjectDefinitionOption.Category), OptionCategory),
        (nameof(ProjectDefinitionOption.OptionName), "Selected option name; normalized by trimming. A blank name omits that submitted row from persistence."),
        (nameof(ProjectDefinitionOption.Notes), "Notes about the selected option; normalized by trimming.")
    ];

}
