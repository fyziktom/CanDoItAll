namespace CanDoItAll.Modules.Projects;

public abstract record ProjectDefinitionSubmission {
    private ProjectDefinitionSubmission(ProjectDefinitionOperation operation, ProjectDefinitionRequestVersion version,
        ProjectDefinitionCommandKind kind, Guid? targetProjectId) {
        Operation = operation;
        Version = version;
        Kind = kind;
        TargetProjectId = targetProjectId;
    }

    public ProjectDefinitionOperation Operation { get; }
    public ProjectDefinitionRequestVersion Version { get; }
    public ProjectDefinitionCommandKind Kind { get; }
    public Guid? TargetProjectId { get; }

    public sealed record Create : ProjectDefinitionSubmission {
        public Create(ProjectDefinitionRequest<CreateProjectDefinitionCommand> request)
            : base(request.Operation, request.Version, ProjectDefinitionCommandKind.Create, null) => Request = request;
        public ProjectDefinitionRequest<CreateProjectDefinitionCommand> Request { get; }
    }

    public sealed record Replace : ProjectDefinitionSubmission {
        public Replace(ProjectDefinitionRequest<ReplaceProjectDefinitionCommand> request)
            : base(request.Operation, request.Version, ProjectDefinitionCommandKind.Replace, request.Command.Expected.Project.ProjectId) => Request = request;
        public ProjectDefinitionRequest<ReplaceProjectDefinitionCommand> Request { get; }
    }

    public sealed record Scalars : ProjectDefinitionSubmission {
        public Scalars(ProjectDefinitionRequest<UpdateProjectDefinitionScalarsCommand> request)
            : base(request.Operation, request.Version, ProjectDefinitionCommandKind.UpdateScalars, request.Command.Expected.Project.ProjectId) => Request = request;
        public ProjectDefinitionRequest<UpdateProjectDefinitionScalarsCommand> Request { get; }
    }
}

public sealed record ProjectDefinitionFrozenSubmission {
    public ProjectDefinitionFrozenSubmission(ProjectDefinitionSubmission submission, string payload) {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentException.ThrowIfNullOrEmpty(payload);
        Submission = submission;
        Payload = payload;
    }

    public ProjectDefinitionSubmission Submission { get; }
    public string Payload { get; }
}

public abstract record ProjectDefinitionAcceptedReceipt {
    private ProjectDefinitionAcceptedReceipt(ProjectDefinitionOperation operation, ProjectDefinitionCommandKind kind) {
        Operation = operation;
        Kind = kind;
    }

    public ProjectDefinitionOperation Operation { get; }
    public ProjectDefinitionCommandKind Kind { get; }

    public sealed record Full : ProjectDefinitionAcceptedReceipt {
        public Full(ProjectDefinitionAccepted<ProjectEditorAcknowledgement> result) : base(result.Operation, result.Kind) => Result = result;
        public ProjectDefinitionAccepted<ProjectEditorAcknowledgement> Result { get; }
    }

    public sealed record Scalars : ProjectDefinitionAcceptedReceipt {
        public Scalars(ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement> result) : base(result.Operation, result.Kind) => Result = result;
        public ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement> Result { get; }
    }
}

public sealed record ProjectDefinitionReceiptPayload {
    public ProjectDefinitionReceiptPayload(ProjectDefinitionAcceptedReceipt receipt, string payload) {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentException.ThrowIfNullOrEmpty(payload);
        Receipt = receipt;
        Payload = payload;
    }

    public ProjectDefinitionAcceptedReceipt Receipt { get; }
    public string Payload { get; }
}

public interface IProjectDefinitionRecoveryCodec {
    ProjectDefinitionFrozenSubmission Freeze(ProjectDefinitionSubmission submission, ProjectDefinitionLimits limits);
    ProjectDefinitionFrozenSubmission ReadSubmission(string payload, ProjectDefinitionLimits limits);
    ProjectDefinitionReceiptPayload EncodeAccepted(ProjectDefinitionSubmission submission,
        ProjectDefinitionAcceptedReceipt receipt, ProjectDefinitionLimits limits);
    ProjectDefinitionReceiptPayload ReadAccepted(ProjectDefinitionSubmission submission, string payload, ProjectDefinitionLimits limits);
}
