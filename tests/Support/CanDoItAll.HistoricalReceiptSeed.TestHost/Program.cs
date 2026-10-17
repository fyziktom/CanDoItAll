using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Tests.HistoricalReceipts;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

return await HistoricalSeedChild.RunAsync(args);

internal static class HistoricalSeedChild {
    internal static async Task<int> RunAsync(string[] arguments) {
        try {
            if (arguments.Length != 4 || !Enum.TryParse<HistoricalReceiptSeedKind>(arguments[0], false, out var kind) ||
                !Enum.IsDefined(kind) || arguments[0] != kind.ToString() || !Guid.TryParseExact(arguments[1], "D", out var nonce) || nonce == Guid.Empty ||
                !Guid.TryParseExact(arguments[2], "D", out var profileId) || profileId == Guid.Empty) {
                throw new ArgumentException("The seed child requires one supported kind, nonce, profile and output path.");
            }
            var outputRoot = RequireRoot(HistoricalReceiptSeedProtocol.OutputRootEnvironment);
            var environmentRoot = RequireRoot(HistoricalReceiptSeedProtocol.EnvironmentRootEnvironment);
            var relativeEnvironment = Path.GetRelativePath(outputRoot, environmentRoot);
            if (relativeEnvironment == "." || Path.IsPathRooted(relativeEnvironment) || relativeEnvironment == ".." ||
                relativeEnvironment.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                throw new InvalidOperationException("The seed environment must be inside its recorded output root.");
            }
            var output = Path.GetFullPath(arguments[3]);
            if (!Path.IsPathFullyQualified(arguments[3]) || Path.GetDirectoryName(output) != outputRoot || File.Exists(output)) {
                throw new ArgumentException("The seed result must be a new file directly under the recorded output root.");
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            HistoricalReceiptSeeded frame;
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var registrations = new ServiceCollection();
            TestApplicationBootstrap.ConfigureDefaultServices(registrations, configuration,
                new TestHostEnvironment(environmentRoot, "CanDoItAll.HistoricalReceiptSeed.TestHost"));
            registrations.AddRouting();
            registrations.AddCanDoItAllApi(configuration);
            registrations.AddLogging(logging => logging.ClearProviders());
            await using (var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true })) {
                await TestApplicationBootstrap.InitializeSchemaAsync(provider, TestSchemaBootstrapModules.Full, deadline.Token);
                await using var scope = provider.CreateAsyncScope();
                var canonical = scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
                if (canonical.Profile.Id != profileId || canonical.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
                    throw new InvalidOperationException("The seed child resolved a different owned profile.");
                }
                using var self = Process.GetCurrentProcess();
                frame = await HistoricalReceiptSeedScenario.CreateAsync(scope.ServiceProvider, kind, nonce,
                    new DateTimeOffset(self.StartTime.ToUniversalTime()), deadline.Token);
            }
            HistoricalReceiptSeedProtocol.Validate(frame);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, HistoricalReceiptSeedProtocol.Json);
            if (bytes.Length > HistoricalReceiptSeedProtocol.MaximumFrameBytes) {
                throw new InvalidDataException("The seed result exceeds its transport bound.");
            }
            await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await stream.WriteAsync(bytes, deadline.Token);
            await stream.FlushAsync(deadline.Token);
            stream.Flush(flushToDisk: true);
            return 0;
        } catch (Exception failure) {
            await Console.Error.WriteLineAsync(failure.GetType().FullName);
            return 2;
        }
    }

    private static string RequireRoot(string key) {
        var configured = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured)) {
            throw new InvalidOperationException("The owned seed root was not supplied.");
        }
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        if (!Directory.Exists(root)) {
            throw new DirectoryNotFoundException("The owned seed root is unavailable.");
        }
        for (DirectoryInfo? current = new(root); current is not null; current = current.Parent) {
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                throw new InvalidOperationException("An owned seed root cannot traverse a reparse point.");
            }
        }
        return root;
    }
}
