namespace CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

public interface IApiAccessConfigurationOwner {
    Task<ApiAccessConfiguration> ReadAsync(CancellationToken cancellationToken);
    ValueTask<bool> CanManageAsync(CancellationToken cancellationToken);
}

public interface IApiTokenOwner {
    Task<ApiTokenDisclosure> IssueAsync(ApiTokenIntent intent, CancellationToken cancellationToken);
    Task<ApiPage<ApiTokenMetadata>> SearchAsync(ApiPageQuery query, CancellationToken cancellationToken);
    Task<ApiTokenMetadata?> ObserveAsync(Guid id, CancellationToken cancellationToken);
    Task<ApiWriteResult> ApplyAsync(ApiTokenAction action, CancellationToken cancellationToken);
}

public interface IApiAccountOwner {
    Task<ApiPage<ApiAccountMetadata>> SearchAsync(ApiPageQuery query, CancellationToken cancellationToken);
    Task<ApiAccountMetadata?> ObserveAsync(Guid id, CancellationToken cancellationToken);
    Task<ApiAccountWriteResult> ApplyAsync(ApiAccountIntent intent, CancellationToken cancellationToken);
}
