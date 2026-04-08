using System.Text.Json;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Unit;

public sealed class BrowserStateStoreTests
{
    [Fact]
    public async Task SaveAsync_uses_a_profile_scoped_storage_key_and_embeds_profile_metadata()
    {
        var profileId = Guid.NewGuid();
        var profile = CreateProfile(profileId, "sqlite:managed:alpha");
        var jsRuntime = new TestJsRuntime();
        var sut = new BrowserWorkspaceStateStore(
            jsRuntime,
            Options.Create(new WorkbenchOptions { BrowserStorageKey = "candoitall.workbench.session" }),
            new TestActiveDatabaseProfileResolver(profile));

        await sut.SaveAsync(new WorkbenchSessionSnapshot(4, "dashboard", []));

        Assert.Equal($"candoitall.workbench.session:{profileId:N}", jsRuntime.SavedKey);
        using var savedJson = JsonDocument.Parse(jsRuntime.SavedPayload!);
        Assert.Equal(profileId, savedJson.RootElement.GetProperty("profileId").GetGuid());
        Assert.Equal(
            profile.Profile.Runtime.Fingerprint,
            savedJson.RootElement.GetProperty("profileFingerprint").GetString());
    }

    [Fact]
    public async Task LoadAsync_returns_null_when_the_saved_profile_fingerprint_does_not_match_the_active_profile()
    {
        var profileId = Guid.NewGuid();
        var profile = CreateProfile(profileId, "sqlite:managed:alpha");
        var jsRuntime = new TestJsRuntime
        {
            LoadedPayload = JsonSerializer.Serialize(new WorkbenchSessionSnapshot(
                4,
                "dashboard",
                [],
                ProfileId: profileId,
                ProfileFingerprint: "sqlite:managed:beta"))
        };
        var sut = new BrowserWorkspaceStateStore(
            jsRuntime,
            Options.Create(new WorkbenchOptions { BrowserStorageKey = "candoitall.workbench.session" }),
            new TestActiveDatabaseProfileResolver(profile));

        var snapshot = await sut.LoadAsync();

        Assert.Null(snapshot);
        Assert.Equal($"candoitall.workbench.session:{profileId:N}", jsRuntime.LoadedKey);
    }

    private static ResolvedDatabaseProfile CreateProfile(Guid profileId, string fingerprint)
    {
        return new ResolvedDatabaseProfile(
            new DatabaseProfileRecord
            {
                Id = profileId,
                DisplayName = "Managed profile",
                ProviderKind = DatabaseProviderKind.Sqlite,
                SourceKind = DatabaseProfileSourceKind.ManagedSqlite,
                Sqlite = new SqliteDatabaseProfileConnection
                {
                    DatabasePath = @"C:\workspace\candoitall.db"
                },
                Storage = new DatabaseProfileStorageDescriptor
                {
                    WorkspaceRoot = @"C:\workspace"
                },
                Runtime = new DatabaseProfileRuntimeMetadata
                {
                    Fingerprint = fingerprint
                }
            },
            DatabaseProfileResolutionSource.PersistedActiveProfile,
            "Data Source=C:\\workspace\\candoitall.db");
    }

    private sealed class TestActiveDatabaseProfileResolver(ResolvedDatabaseProfile profile) : IActiveDatabaseProfileResolver
    {
        public ResolvedDatabaseProfile ResolveCurrentProfile() => profile;
    }

    private sealed class TestJsRuntime : IJSRuntime
    {
        public string? LoadedPayload { get; init; }

        public string? LoadedKey { get; private set; }

        public string? SavedKey { get; private set; }

        public string? SavedPayload { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "CanDoItAll.browserState.load":
                    LoadedKey = Assert.IsType<string>(args![0]);
                    return LoadedPayload is null
                        ? new ValueTask<TValue>(result: default!)
                        : new ValueTask<TValue>((TValue)(object)LoadedPayload);
                case "CanDoItAll.browserState.save":
                    SavedKey = Assert.IsType<string>(args![0]);
                    SavedPayload = Assert.IsType<string>(args[1]);
                    return ValueTask.FromResult(default(TValue)!);
                default:
                    throw new InvalidOperationException($"Unexpected JS interop call '{identifier}'.");
            }
        }
    }
}
