using CanDoItAll.AppComponents;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

internal sealed class UserUiPreferencesTestState : IUserUiPreferencesState {
    public UserUiPreferences Current { get; set; } = UserUiPreferences.Default;
    public bool IsReady { get; set; } = true;
    public bool IsBusy { get; set; }
    public UserUiPreferencesFailure Failure { get; set; }
    public List<UserUiPreferencesPatch> Updates { get; } = [];
    public int RetryCalls { get; private set; }
    public event Action? Changed;

    public Task UpdateAsync(UserUiPreferencesPatch patch) {
        Updates.Add(patch);
        Current = Current.Apply(patch);
        NotifyChanged();
        return Task.CompletedTask;
    }

    public Task RetryAsync() {
        RetryCalls++;
        return Task.CompletedTask;
    }

    public void NotifyChanged() => Changed?.Invoke();
}
