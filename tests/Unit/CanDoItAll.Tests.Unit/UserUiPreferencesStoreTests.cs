using System.Text;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "HostPlatform")]
public sealed class UserUiPreferencesStoreTests {
    [Fact]
    public async Task Missing_preferences_use_defaults_and_saved_fields_survive_a_new_store_instance() {
        await using var environment = CanDoItAllTestEnvironment.Create("ui-preferences-roundtrip");
        var account = UserUiPreferencesAccount.ForUser(Guid.NewGuid());
        var store = CreateStore(environment);
        Assert.Equal(new UserUiPreferences(UiThemeMode.Light, true), await store.ReadAsync(account));
        Assert.Empty(Directory.GetFiles(PreferencesDirectory(environment), "*.json"));
        await store.UpdateAsync(account, new(Theme: UiThemeMode.Dark), Allow);
        var reopened = CreateStore(environment);
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, true), await reopened.ReadAsync(account));
        await reopened.UpdateAsync(account, new(MainMenuCollapsed: false), Allow);
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, false), await store.ReadAsync(account));
        if (!OperatingSystem.IsWindows()) {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(PreferencesPath(environment, account)));
        }
    }

    [Fact]
    public async Task Accounts_and_configured_administrator_have_separate_durable_preferences() {
        await using var environment = CanDoItAllTestEnvironment.Create("ui-preferences-isolation");
        var first = UserUiPreferencesAccount.ForUser(Guid.NewGuid());
        var second = UserUiPreferencesAccount.ForUser(Guid.NewGuid());
        var store = CreateStore(environment);
        await store.UpdateAsync(first, new(Theme: UiThemeMode.Dark), Allow);
        await store.UpdateAsync(UserUiPreferencesAccount.ConfiguredAdministrator, new(MainMenuCollapsed: false), Allow);
        Assert.Equal(UserUiPreferences.Default, await store.ReadAsync(second));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, true), await store.ReadAsync(first));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Light, false), await store.ReadAsync(UserUiPreferencesAccount.ConfiguredAdministrator));
    }

    [Fact]
    public async Task Concurrent_field_patches_merge_after_the_original_writer_releases_its_account_lock() {
        await using var environment = CanDoItAllTestEnvironment.Create("ui-preferences-concurrency");
        var account = UserUiPreferencesAccount.ForUser(Guid.NewGuid());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = CreateStore(environment).UpdateAsync(account, new(Theme: UiThemeMode.Dark), async token => {
            entered.SetResult();
            await release.Task.WaitAsync(token);
        });
        Task<UserUiPreferences>? second = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = CreateStore(environment).UpdateAsync(account, new(MainMenuCollapsed: false), Allow);
            Assert.False(second.IsCompleted);
        } finally {
            release.TrySetResult();
        }
        await first.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, false), await second!.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, false), await CreateStore(environment).ReadAsync(account));
    }

    [Fact]
    public async Task Denied_commit_preserves_the_previous_document() {
        await using var environment = CanDoItAllTestEnvironment.Create("ui-preferences-denied");
        var account = UserUiPreferencesAccount.ForUser(Guid.NewGuid());
        var store = CreateStore(environment);
        await store.UpdateAsync(account, new(Theme: UiThemeMode.Dark), Allow);
        var before = await File.ReadAllBytesAsync(PreferencesPath(environment, account));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.UpdateAsync(account, new(MainMenuCollapsed: false),
            _ => throw new UnauthorizedAccessException("Retired preference owner.")));
        Assert.Equal(before, await File.ReadAllBytesAsync(PreferencesPath(environment, account)));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":2,\"theme\":0,\"mainMenuCollapsed\":true}")]
    [InlineData("{\"schemaVersion\":1,\"theme\":999,\"mainMenuCollapsed\":true}")]
    [InlineData("{\"schemaVersion\":1,\"theme\":0}")]
    [InlineData("{\"schemaVersion\":1,\"theme\":0,\"mainMenuCollapsed\":true,\"unexpected\":1}")]
    public async Task Invalid_documents_fail_without_replacing_the_original_bytes(string json) {
        await using var environment = CanDoItAllTestEnvironment.Create("ui-preferences-invalid");
        var account = UserUiPreferencesAccount.ForUser(Guid.NewGuid());
        var store = CreateStore(environment);
        await store.ReadAsync(account);
        var bytes = Encoding.UTF8.GetBytes(json);
        await Writer().WriteBytesAsync(environment.ControlPlaneRootPath, PreferencesPath(environment, account), bytes, DurableFileWriteOptions.Private);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(account));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.UpdateAsync(account, new(Theme: UiThemeMode.Dark), Allow));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(PreferencesPath(environment, account)));
    }

    [Fact]
    public async Task Oversized_documents_are_rejected_before_deserialization() {
        await using var environment = CanDoItAllTestEnvironment.Create("ui-preferences-size");
        var account = UserUiPreferencesAccount.ForUser(Guid.NewGuid());
        var store = CreateStore(environment);
        await store.ReadAsync(account);
        var bytes = Encoding.UTF8.GetBytes("""{"schemaVersion":1,"theme":0,"mainMenuCollapsed":true}""".PadRight(4097));
        await Writer().WriteBytesAsync(environment.ControlPlaneRootPath, PreferencesPath(environment, account), bytes, DurableFileWriteOptions.Private);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(account));
    }

    [Fact]
    public void Invalid_account_and_theme_are_rejected_and_empty_patch_preserves_existing_values() {
        Assert.Throws<ArgumentException>(() => UserUiPreferencesAccount.ForUser(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UserUiPreferencesPatch((UiThemeMode)999).Validate());
        var current = new UserUiPreferences(UiThemeMode.Dark, false);
        Assert.Equal(current, current.Apply(new()));
    }

    private static Task Allow(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static DurableFileWriter Writer() => new(TestWorkspaceServices.PhysicalPathPolicyFactory);

    private static FileUserUiPreferencesStore CreateStore(CanDoItAllTestEnvironment environment) {
        var writer = Writer();
        var paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = environment.ControlPlaneRootPath }),
            environment.CreateHostEnvironment(nameof(UserUiPreferencesStoreTests)), writer);
        return new(paths, writer, TestWorkspaceServices.PhysicalPathPolicyFactory);
    }

    private static string PreferencesDirectory(CanDoItAllTestEnvironment environment) => Path.Combine(environment.ControlPlaneRootPath, "user-ui-preferences");
    private static string PreferencesPath(CanDoItAllTestEnvironment environment, UserUiPreferencesAccount account)
        => Path.Combine(PreferencesDirectory(environment), $"user-{account.UserId:N}.json");
}
