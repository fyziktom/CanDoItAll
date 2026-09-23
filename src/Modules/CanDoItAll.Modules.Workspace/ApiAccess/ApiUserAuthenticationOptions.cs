namespace CanDoItAll.Modules.Workspace.ApiAccess;

public sealed class ApiUserAuthenticationOptions {
    public bool Enabled { get; set; }
    public int TokenLifetimeMinutes { get; set; } = 60;
    public bool AllowLoopbackHttp { get; set; }
}

public sealed class ApiAccessManagementOptions {
    public bool Enabled { get; set; }
}

public sealed class ApiBootstrapAdminOptions {
    public const string Subject = "configured-api-administrator";
    public string UserName { get; set; } = "admin";
    public string PasswordHash { get; set; } = string.Empty;
}
