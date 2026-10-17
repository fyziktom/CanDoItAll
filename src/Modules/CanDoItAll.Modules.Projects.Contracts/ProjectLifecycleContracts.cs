using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectLifecycleOperation {
    public ProjectLifecycleOperation(Guid databaseProfileId, DatasetNamespaceId @namespace,
        ProjectOperationEpoch epoch, ProjectCallerBinding callerBinding, Guid id) {
        if (databaseProfileId == Guid.Empty || id == Guid.Empty) {
            throw new ArgumentException("A lifecycle operation requires an original profile and nonempty operation identity.");
        }
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(epoch);
        ArgumentNullException.ThrowIfNull(callerBinding);
        DatabaseProfileId = databaseProfileId;
        Namespace = @namespace;
        Epoch = epoch;
        CallerBinding = callerBinding;
        Id = id;
    }

    public Guid DatabaseProfileId { get; }
    public DatasetNamespaceId Namespace { get; }
    public ProjectOperationEpoch Epoch { get; }
    public ProjectCallerBinding CallerBinding { get; }
    public Guid Id { get; }
}

public sealed record ProjectLifecycleInterpretation {
    public static ProjectLifecycleInterpretation VersionOne { get; } = new(1, 1);

    public ProjectLifecycleInterpretation(int schema, int normalization) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schema);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(normalization);
        Schema = schema;
        Normalization = normalization;
    }

    public int Schema { get; }
    public int Normalization { get; }
}

public enum ProjectLifecycleCommandKind { Delete = 1, EnsureLink = 2, RemoveLink = 3, ReconnectLink = 4 }

public sealed record ProjectHierarchyEdgeExpectation {
    public ProjectHierarchyEdgeExpectation(Guid edgeId, ProjectWriteAdmission parent, ProjectWriteAdmission child) {
        if (edgeId == Guid.Empty) {
            throw new ArgumentException("An observed hierarchy edge requires its original identity.", nameof(edgeId));
        }
        ProjectLifecycleContractRules.RequireDistinctEndpoints(parent, child);
        EdgeId = edgeId;
        Parent = parent;
        Child = child;
    }

    public Guid EdgeId { get; }
    public ProjectWriteAdmission Parent { get; }
    public ProjectWriteAdmission Child { get; }
}

public abstract record ProjectLifecycleCommand {
    private ProjectLifecycleCommand() {
    }

    public abstract ProjectLifecycleCommandKind Kind { get; }
    public abstract Guid DatabaseProfileId { get; }

    public sealed record Delete : ProjectLifecycleCommand {
        public Delete(ProjectDefinitionExpectation expected) {
            ArgumentNullException.ThrowIfNull(expected);
            Expected = expected;
        }

        public ProjectDefinitionExpectation Expected { get; }
        public override ProjectLifecycleCommandKind Kind => ProjectLifecycleCommandKind.Delete;
        public override Guid DatabaseProfileId => Expected.Project.DatabaseProfileId;
    }

    public sealed record EnsureLink : ProjectLifecycleCommand {
        public EnsureLink(ProjectWriteAdmission parent, ProjectWriteAdmission child) {
            ProjectLifecycleContractRules.RequireDistinctEndpoints(parent, child);
            Parent = parent;
            Child = child;
        }

        public ProjectWriteAdmission Parent { get; }
        public ProjectWriteAdmission Child { get; }
        public override ProjectLifecycleCommandKind Kind => ProjectLifecycleCommandKind.EnsureLink;
        public override Guid DatabaseProfileId => Parent.DatabaseProfileId;
    }

    public sealed record RemoveLink : ProjectLifecycleCommand {
        public RemoveLink(ProjectHierarchyEdgeExpectation expected) {
            ArgumentNullException.ThrowIfNull(expected);
            Expected = expected;
        }

        public ProjectHierarchyEdgeExpectation Expected { get; }
        public override ProjectLifecycleCommandKind Kind => ProjectLifecycleCommandKind.RemoveLink;
        public override Guid DatabaseProfileId => Expected.Parent.DatabaseProfileId;
    }

    public sealed record ReconnectLink : ProjectLifecycleCommand {
        public ReconnectLink(ProjectHierarchyEdgeExpectation expected, ProjectWriteAdmission newParent) {
            ArgumentNullException.ThrowIfNull(expected);
            ProjectLifecycleContractRules.RequireDistinctEndpoints(expected.Child, newParent);
            ProjectLifecycleContractRules.RequireDistinctEndpoints(expected.Parent, newParent);
            Expected = expected;
            NewParent = newParent;
        }

        public ProjectHierarchyEdgeExpectation Expected { get; }
        public ProjectWriteAdmission NewParent { get; }
        public override ProjectLifecycleCommandKind Kind => ProjectLifecycleCommandKind.ReconnectLink;
        public override Guid DatabaseProfileId => Expected.Parent.DatabaseProfileId;
    }
}

public sealed record ProjectLifecycleSubmission {
    public ProjectLifecycleSubmission(ProjectLifecycleOperation operation, ProjectLifecycleInterpretation interpretation,
        ProjectLifecycleCommand command) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(interpretation);
        ArgumentNullException.ThrowIfNull(command);
        if (command is not ProjectLifecycleCommand.Delete and not ProjectLifecycleCommand.EnsureLink and
                not ProjectLifecycleCommand.RemoveLink and not ProjectLifecycleCommand.ReconnectLink) {
            throw new ArgumentException("A lifecycle submission requires a supported concrete command.", nameof(command));
        }
        if (operation.DatabaseProfileId != command.DatabaseProfileId) {
            throw new ArgumentException("A lifecycle command must retain its operation's original profile.");
        }
        Operation = operation;
        Interpretation = interpretation;
        Command = command;
    }

    public ProjectLifecycleOperation Operation { get; }
    public ProjectLifecycleInterpretation Interpretation { get; }
    public ProjectLifecycleCommand Command { get; }
}

internal static class ProjectLifecycleContractRules {
    internal static void RequireDistinctEndpoints(ProjectWriteAdmission first, ProjectWriteAdmission second) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.DatabaseProfileId != second.DatabaseProfileId || first.ProjectId == second.ProjectId) {
            throw new ArgumentException("Hierarchy endpoints must be distinct projects in the same original profile.");
        }
    }
}
