using CanDoItAll.SharedKernel;

namespace CanDoItAll.AppComponents;

public enum UserUiPreferencesFailure {
    None,
    Load,
    Save
}

public interface IUserUiPreferencesState {
    UserUiPreferences Current { get; }
    bool IsReady { get; }
    bool IsBusy { get; }
    UserUiPreferencesFailure Failure { get; }
    bool CanEdit => IsReady && !IsBusy && Failure == UserUiPreferencesFailure.None;
    event Action? Changed;
    Task UpdateAsync(UserUiPreferencesPatch patch);
    Task RetryAsync();
}
