using CanDoItAll.Tests.Support;
using Xunit.Sdk;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
public sealed class HistoricalFileObservationTests {
    [Fact]
    public async Task Repeated_barriers_do_not_report_their_own_probe_changes() {
        await using var environment = CanDoItAllTestEnvironment.Create("historical-file-observation");
        using var observation = await HistoricalFileObservation.StartAsync(environment.RootPath);
        for (var pass = 0; pass < 5; pass++) {
            await observation.AssertNoChangesAsync();
        }
        Assert.Empty(Directory.EnumerateFiles(environment.RootPath, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(WatcherChangeTypes.Created)]
    [InlineData(WatcherChangeTypes.Changed)]
    [InlineData(WatcherChangeTypes.Renamed)]
    [InlineData(WatcherChangeTypes.Deleted)]
    public async Task Observation_rejects_real_mutations_including_transient_files(WatcherChangeTypes mutation) {
        await using var environment = CanDoItAllTestEnvironment.Create("historical-file-observation");
        var path = Path.Combine(environment.RootPath, "observed.txt");
        if (mutation != WatcherChangeTypes.Created) {
            await File.WriteAllTextAsync(path, "original");
        }
        using var observation = await HistoricalFileObservation.StartAsync(environment.RootPath);
        switch (mutation) {
            case WatcherChangeTypes.Created:
                await File.WriteAllTextAsync(path, "transient");
                File.Delete(path);
                break;
            case WatcherChangeTypes.Changed:
                await File.AppendAllTextAsync(path, "changed");
                break;
            case WatcherChangeTypes.Renamed:
                File.Move(path, path + ".renamed");
                break;
            case WatcherChangeTypes.Deleted:
                File.Delete(path);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        var failure = await Assert.ThrowsAsync<TrueException>(observation.AssertNoChangesAsync);
        Assert.Contains("Historical no-write observation found", failure.Message, StringComparison.Ordinal);
        Assert.Contains("observed.txt", failure.Message, StringComparison.Ordinal);
    }
}
