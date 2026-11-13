using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed record UserUiPreferencesAccount {
    private UserUiPreferencesAccount(Guid? userId) => UserId = userId;

    public Guid? UserId { get; }
    public bool IsConfiguredAdministrator => UserId is null;
    public static UserUiPreferencesAccount ConfiguredAdministrator { get; } = new((Guid?)null);

    public static UserUiPreferencesAccount ForUser(Guid userId) => userId != Guid.Empty
        ? new(userId)
        : throw new ArgumentException("A preference account requires a nonempty user identity.", nameof(userId));
}

public interface IUserUiPreferencesStore {
    Task<UserUiPreferences> ReadAsync(UserUiPreferencesAccount account, CancellationToken cancellationToken = default);

    Task<UserUiPreferences> UpdateAsync(UserUiPreferencesAccount account, UserUiPreferencesPatch patch,
        Func<CancellationToken, Task> authorizeCommit, CancellationToken cancellationToken = default);
}
