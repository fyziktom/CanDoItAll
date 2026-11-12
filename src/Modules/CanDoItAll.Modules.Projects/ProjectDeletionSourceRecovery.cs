using CanDoItAll.Infrastructure;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public interface IProjectDeletionSourceRecoveryLease {
    ProjectDeletionSourceRecovery CaptureDeletionRecoverySource();
}

public enum ProjectDeletionExecutionWorkspaceKind { Sandbox = 1, Project = 2 }

public sealed record ProjectDeletionWorkspaceLocation {
    public ProjectDeletionWorkspaceLocation(HostBoundPathRecord root) {
        ArgumentNullException.ThrowIfNull(root);
        FormatVersion = root.FormatVersion;
        PlatformFamily = root.PlatformFamily;
        PathSyntax = root.PathSyntax;
        HostBindingId = root.HostBindingId;
        RootPath = root.Path;
        State = root.State;
        if (!HostBoundPathPolicy.TryResolve(ToPathRecord(), new(PlatformFamily, HostBindingId), out var resolved, out _) ||
                !string.Equals(resolved, RootPath, StringComparison.Ordinal)) {
            throw new ArgumentException("Recovery requires an active, normalized host-bound workspace root.", nameof(root));
        }
    }

    public int FormatVersion { get; }
    public HostPlatformFamily PlatformFamily { get; }
    public PhysicalPathSyntax PathSyntax { get; }
    public string HostBindingId { get; }
    public string RootPath { get; }
    public HostBoundPathState State { get; }

    public HostBoundPathRecord ToPathRecord() => new() {
        FormatVersion = FormatVersion,
        PlatformFamily = PlatformFamily,
        PathSyntax = PathSyntax,
        HostBindingId = HostBindingId,
        Path = RootPath,
        State = State
    };

    public void RequireCurrentHost() {
        if (!HostBoundPathPolicy.TryResolve(ToPathRecord(), HostPathContext.CaptureCurrent(), out var resolved, out _) ||
                !string.Equals(resolved, RootPath, StringComparison.Ordinal)) {
            throw new InvalidOperationException("The original workspace root is not bound to the current host.");
        }
    }

    public override string ToString() => "Original host-bound workspace location";
}

public abstract record ProjectDeletionSourceRecovery {
    private ProjectDeletionSourceRecovery() {
    }

    public sealed record NoExternalSourceLease : ProjectDeletionSourceRecovery;

    public sealed record CanonicalOrganizationCatalog : ProjectDeletionSourceRecovery {
        public CanonicalOrganizationCatalog(Guid databaseProfileId, HostBoundPathRecord root) {
            if (databaseProfileId == Guid.Empty) {
                throw new ArgumentException("Catalog recovery requires its original database profile.", nameof(databaseProfileId));
            }
            DatabaseProfileId = databaseProfileId;
            Location = new(root);
        }

        public Guid DatabaseProfileId { get; }
        public ProjectDeletionWorkspaceLocation Location { get; }
        public int FormatVersion => Location.FormatVersion;
        public HostPlatformFamily PlatformFamily => Location.PlatformFamily;
        public PhysicalPathSyntax PathSyntax => Location.PathSyntax;
        public string HostBindingId => Location.HostBindingId;
        public string RootPath => Location.RootPath;
        public HostBoundPathState State => Location.State;
        public HostBoundPathRecord ToPathRecord() => Location.ToPathRecord();
        public void RequireCurrentHost() => Location.RequireCurrentHost();
        public override string ToString() => $"Original organization catalog for profile {DatabaseProfileId:D}";
    }

    public sealed record CanonicalInvocation : ProjectDeletionSourceRecovery {
        public CanonicalInvocation(CanonicalOrganizationCatalog catalog, ProjectDeletionWorkspaceLocation execution,
            ProjectDeletionExecutionWorkspaceKind scope, string key) {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(execution);
            ArgumentNullException.ThrowIfNull(key);
            if (!Enum.IsDefined(scope) || key.Length > 256 || key != key.ToLowerInvariant() ||
                    key.Any(character => !char.IsLetterOrDigit(character) && character != '-') ||
                    key.StartsWith('-') || key.EndsWith('-') || key.Contains("--", StringComparison.Ordinal) ||
                    scope == ProjectDeletionExecutionWorkspaceKind.Project && key.Length == 0 ||
                    catalog.PlatformFamily != execution.PlatformFamily || catalog.HostBindingId != execution.HostBindingId) {
                throw new ArgumentException("Invocation recovery requires its exact same-host execution workspace and normalized bounded scope.");
            }
            Catalog = catalog;
            Execution = execution;
            Scope = scope;
            Key = key;
        }

        public CanonicalOrganizationCatalog Catalog { get; }
        public ProjectDeletionWorkspaceLocation Execution { get; }
        public ProjectDeletionExecutionWorkspaceKind Scope { get; }
        public string Key { get; }
        public override string ToString() => $"Original catalog and execution publication for profile {Catalog.DatabaseProfileId:D}";
    }
}
