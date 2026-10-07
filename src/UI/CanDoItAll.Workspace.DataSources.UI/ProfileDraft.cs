using System.ComponentModel.DataAnnotations;
using CanDoItAll.Modules.Workspace.DataSources.Contracts;

namespace CanDoItAll.Workspace.DataSources.UI;

public sealed class ProfileDraft {
    public Guid? Id { get; internal set; }
    [Required] public string DisplayName { get; set; } = "PostgreSQL workspace";
    public string? WorkspaceRoot { get; set; }
    [Required] public string PostgresHost { get; set; } = "localhost";
    [Range(1, 65535)] public int PostgresPort { get; set; } = 5432;
    [Required] public string PostgresDatabaseName { get; set; } = "candoitall";
    [Required] public string PostgresUsername { get; set; } = "postgres";
    public string? PostgresAdminDatabaseName { get; set; }
    public bool PostgresTrustServerCertificate { get; set; }
    public DataSourceProvider ProviderKind { get; internal set; }
    public bool IsRuntimeLocked { get; internal set; }
    public bool HasPassword { get; internal set; }

    public ProfileValues Capture() => new(Id, DisplayName, WorkspaceRoot, PostgresHost, PostgresPort,
        PostgresDatabaseName, PostgresUsername, PostgresAdminDatabaseName, PostgresTrustServerCertificate);

    internal static ProfileDraft From(ProfileEditor editor) => new() {
        Id = editor.Values.Id, DisplayName = editor.Values.DisplayName, WorkspaceRoot = editor.Values.WorkspaceRoot,
        PostgresHost = editor.Values.PostgresHost, PostgresPort = editor.Values.PostgresPort,
        PostgresDatabaseName = editor.Values.PostgresDatabaseName, PostgresUsername = editor.Values.PostgresUsername,
        PostgresAdminDatabaseName = editor.Values.PostgresAdminDatabaseName,
        PostgresTrustServerCertificate = editor.Values.PostgresTrustServerCertificate,
        ProviderKind = editor.ProviderKind, IsRuntimeLocked = editor.IsRuntimeLocked, HasPassword = editor.HasPassword
    };
}
