using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.HistoricalReceipts;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Tests.Integration;

internal sealed class AgentHistoricalReceiptRestartFixture(string rowRoot, Guid nonce, HistoricalReceiptSeedKind kind) {
    private readonly CancellationTokenSource rowDeadline = new(TimeSpan.FromMinutes(10));
    private readonly string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private readonly List<OwnedChild> children = [];
    private CanDoItAllTestEnvironment? environment;
    private TestDatabaseProfile? profile;
    private Guid profileId;
    private JsonSerializerOptions json = null!;
    private Dictionary<string, string> originalFiles = [];
    private string? processState;
    private OwnedChild? web;
    private HttpClient? client;
    private int sequence;
    private bool environmentAllocationAttempted;
    private bool databaseAllocationAttempted;
    internal HistoricalReceiptSeeded Seed { get; private set; } = null!;

    internal static async Task RunAsync(HistoricalReceiptSeedKind kind, Func<AgentHistoricalReceiptRestartFixture, Task> test) {
        var parent = Environment.GetEnvironmentVariable(HistoricalReceiptSeedProtocol.OutputRootEnvironment);
        Assert.True(!string.IsNullOrWhiteSpace(parent) && Path.IsPathFullyQualified(parent) && Directory.Exists(parent),
            "The parent must supply an existing owned historical restart output root.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent!));
        RequireNoLinks(root);
        var nonce = Guid.NewGuid();
        var rowRoot = Path.Combine(root, $"historical-{kind}-{nonce:N}");
        Assert.False(Directory.Exists(rowRoot));
        await WriteNewAsync(Path.Combine(root, $"historical-{nonce:N}.intent.json"), new {
            Owner = nameof(AgentHistoricalReceiptRestartTests), Nonce = nonce, Kind = kind, RowRoot = rowRoot,
            ParentProcessId = Environment.ProcessId, HardRowDeadlineSeconds = 600, RecordedAtUtc = DateTimeOffset.UtcNow
        });
        Directory.CreateDirectory(rowRoot);
        var fixture = new AgentHistoricalReceiptRestartFixture(rowRoot, nonce, kind);
        Exception? primary = null;
        try {
            await fixture.InitializeAsync();
            await test(fixture);
        } catch (Exception failure) {
            primary = failure;
        }
        try {
            await fixture.CleanupAsync(primary?.GetType().FullName);
        } catch (Exception cleanup) {
            throw primary is null ? cleanup : new AggregateException(primary, cleanup);
        }
        if (primary is not null) {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }

    private async Task InitializeAsync() {
        var databasePrefix = Guid.NewGuid().ToString("N")[..20];
        await RecordAsync("allocation-intent", new { DatabasePrefix = $"cditall_{databasePrefix}_", MaximumDatabases = 1, EnvironmentParent = rowRoot });
        environmentAllocationAttempted = true;
        environment = CanDoItAllTestEnvironment.CreateUnder(rowRoot, "environment");
        RequireContained(environment.RootPath);
        await RecordAsync("environment-created", new { environment.RootPath, environment.ControlPlaneRootPath });
        databaseAllocationAttempted = true;
        profile = environment.CreatePostgreSqlProfile(databasePrefix);
        var databaseName = new NpgsqlConnectionStringBuilder(profile.ConnectionString).Database;
        Assert.Matches($"^cditall_{databasePrefix}_[0-9a-f]{{32}}$", databaseName);
        await RecordAsync("database-created", new { DatabaseName = databaseName, profile.ProfileKey, profile.ProfileRootPath, profile.WorkspaceRootPath });
        await WithServicesAsync(async services => {
            profileId = services.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile().Profile.Id;
            json = services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
            await Task.CompletedTask;
        });
        await RecordAsync("profile-resolved", new { ProfileId = profileId });
        var assembly = Path.Combine(IntegrationTestPaths.RepositoryRoot, "tests", "Support", "CanDoItAll.HistoricalReceiptSeed.TestHost",
            "bin", IntegrationTestPaths.BuildConfiguration, "net10.0", "CanDoItAll.HistoricalReceiptSeed.TestHost.dll");
        var output = Path.Combine(rowRoot, "seeded.json");
        var seed = await LaunchAsync("seed-A", assembly, [kind.ToString(), nonce.ToString("D"), profileId.ToString("D"), output], null);
        using (var seedDeadline = CancellationTokenSource.CreateLinkedTokenSource(rowDeadline.Token)) {
            seedDeadline.CancelAfter(TimeSpan.FromSeconds(180));
            await seed.Process.WaitForExitAsync(seedDeadline.Token);
        }
        await CompleteChildAsync(seed, intentionalTeardown: false);
        Assert.Equal(0, seed.Process.ExitCode);
        var frame = new FileInfo(output);
        Assert.True(frame.Exists && frame.Length is > 0 and <= HistoricalReceiptSeedProtocol.MaximumFrameBytes);
        Seed = JsonSerializer.Deserialize<HistoricalReceiptSeeded>(await File.ReadAllBytesAsync(output, rowDeadline.Token), HistoricalReceiptSeedProtocol.Json)!;
        HistoricalReceiptSeedProtocol.Validate(Seed);
        Assert.Equal(nonce, Seed.Nonce);
        Assert.Equal(kind, Seed.Kind);
        Assert.Equal(profileId, Seed.ProfileId);
        Assert.Equal(seed.Process.Id, Seed.ProcessId);
        Assert.Equal(seed.StartedAtUtc, Seed.ProcessStartedAtUtc);
        Assert.True(Seed.OriginalGeneration > 0);
        Assert.Equal(Seed.RunFileSha256, await HashAsync(Layout.RunPath(Seed.Request.ExecutionRunId)));
        originalFiles = await CaptureFilesAsync();
        await AssertNoClientsAsync();
        await AssertOriginalEvidenceAsync();
    }

    internal async Task StartWebAsync() {
        Assert.Null(web);
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Server.ExclusiveAddressUse = true;
        reservation.Start(1);
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        var endpoint = new Uri($"http://127.0.0.1:{port}");
        var assembly = IntegrationTestPaths.ResolveProjectOutputAssembly(Path.Combine("App", "CanDoItAll.Web"), "CanDoItAll.Web.dll");
        reservation.Stop();
        var name = children.Count == 1 ? "web-B" : "web-B2";
        web = await LaunchAsync(name, assembly, [], endpoint);
        Assert.NotEqual(Seed.ProcessId, web.Process.Id);
        Assert.NotEqual(Seed.ProcessStartedAtUtc, web.StartedAtUtc);
        client = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(15) };
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(rowDeadline.Token);
        startup.CancelAfter(TimeSpan.FromSeconds(90));
        while (true) {
            Assert.False(web.Process.HasExited, "The owned Web observer exited during startup; raw diagnostics remain suppressed.");
            try {
                using var health = await client.GetAsync("/health", startup.Token);
                if (health.StatusCode == HttpStatusCode.OK) {
                    break;
                }
            } catch (HttpRequestException) {
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), startup.Token);
        }
        await WithServicesAsync(async services => {
            var token = services.GetRequiredService<IApiTokenService>().IssueToken(new() {
                Subject = "historical-restart-fixture", Scopes = [ApiAccessScopeNames.ReadAgents], LifetimeMinutes = 10
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            await Task.CompletedTask;
        });
        await RecordAsync("web-ready", new { web.Name, web.Process.Id, web.StartedAtUtc, Endpoint = endpoint.AbsoluteUri });
        Assert.Equal(Seed.RunFileSha256, await HashAsync(Layout.RunPath(Seed.Request.ExecutionRunId)));
    }

    internal async Task StopWebAsync() {
        if (web is null) {
            return;
        }
        client?.Dispose();
        client = null;
        var stopped = web;
        await StopChildAsync(stopped, ChildStopReason.ObserverComplete);
        web = null;
        Assert.NotNull(stopped.Endpoint);
        using var socket = new TcpClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            await socket.ConnectAsync(IPAddress.Loopback, stopped.Endpoint.Port, deadline.Token);
        } catch (SocketException) {
        }
        Assert.False(socket.Connected, "The owned Web listener remained reachable after process exit.");
        await AssertNoClientsAsync();
    }

    internal async Task<AgentHistoricalReceiptReview> ReadAsync(AgentHistoricalReceiptReviewRequest request) {
        Assert.NotNull(client);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(rowDeadline.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var body = new StringContent(JsonSerializer.Serialize(request, json), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"/api/agents/execution-runs/{request.ExecutionRunId:D}/saved-receipts/read", body, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
        Assert.True(bytes.Length <= AgentHistoricalReceiptReview.MaximumUtf8Bytes);
        var result = JsonSerializer.Deserialize<AgentHistoricalReceiptReview>(bytes, json)!;
        Assert.Equal(request, result.Request);
        Assert.Equal(profileId, result.CurrentContext.ProfileId);
        Assert.Equal(0, result.CurrentContext.Generation.Value);
        await AssertOriginalEvidenceAsync();
        return result;
    }

    internal void AssertAcceptedPage(AgentHistoricalReceiptReview page, bool firstPage) {
        Assert.Equal(firstPage ? AgentHistoricalReceiptReviewStop.LookupLimit : AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, page.Stop);
        Assert.Equal(firstPage ? 4 : 1, page.Observations.Count);
        var expected = firstPage ? Seed.Receipts.Take(4) : Seed.Receipts.Skip(4);
        Assert.Equal(expected.Select(item => item.OperationId), page.Observations.Select(item => item.Binding.IntentId.Value));
        Assert.All(page.Observations, item => Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, item.Knowledge));
        if (firstPage) {
            Assert.Equal(4, page.NextCursor!.NextProposalOrdinal);
        } else {
            Assert.Null(page.NextCursor);
        }
    }

    internal AgentHistoricalReceiptReviewRequest Next(AgentHistoricalReceiptReview page)
        => new(Seed.Request.ExecutionRunId, Seed.Request.ChatSessionId, Seed.Request.AgentId, Seed.Request.ExecutionCreatedAtUtc,
            Assert.IsType<AgentHistoricalReceiptCursor>(page.NextCursor));

    internal void AssertRefused(AgentHistoricalReceiptReview page, AgentHistoricalReceiptReviewStop stop) {
        Assert.Equal(stop, page.Stop);
        Assert.Empty(page.Observations);
        Assert.Null(page.NextCursor);
        Assert.Null(page.RunRevision);
        Assert.Null(page.JournalRevision);
    }

    internal Task EditCurrentTargetAsync() => WithServicesAsync(async services => {
        var projects = services.GetRequiredService<ProjectsService>();
        var current = await projects.GetAsync(Seed.ProjectId, rowDeadline.Token);
        current.Name = "Later current definition, not the original accepted effect";
        Assert.True((await projects.SaveAsync(current, rowDeadline.Token)).IsSuccess);
    });

    internal Task ReplaceTargetLifetimeAsync() => WithServicesAsync(async services => {
        var projects = services.GetRequiredService<ProjectsService>();
        var deleted = await projects.DeleteAsync(Seed.ProjectId, rowDeadline.Token);
        Assert.Equal(Seed.ProjectId, deleted.ProjectId);
        Assert.Empty(deleted.Warnings);
        Assert.True((await projects.CreateAsync(Seed.ProjectId, new() { Name = "Replacement target lifetime" }, rowDeadline.Token)).IsSuccess);
        var admission = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(Seed.ProjectId, rowDeadline.Token);
        Assert.NotNull(admission);
        Assert.NotEqual(Seed.ProjectLifetimeId, admission.LifetimeId);
    });

    internal Task SetSourceReadAsync(bool allowed) => WithServicesAsync(async services => {
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        await store.UpdateCatalogAsync(current => current with { Agents = current.Agents.Select(agent => agent.Id != Seed.SourceAgentId ? agent : agent with {
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, allowed ? new() {
                CanRead = true, CanWrite = true, CanCreateProjects = true, CanCreateSubprojects = true,
                AllowedProjectIds = [Seed.ProjectId], AllowedProjectLifetimes = [new(Seed.ProfileId, Seed.ProjectId, Seed.ProjectLifetimeId)]
            } : new())
        }).ToArray() }, rowDeadline.Token);
    });

    internal async Task ExpireOriginalClaimAsync() {
        Assert.Equal(HistoricalReceiptSeedKind.Process, kind);
        await WithServicesAsync(async services => {
            await using var context = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync(rowDeadline.Token);
            var claim = await context.DispatchClaims.SingleAsync(item => item.RunId == Seed.ProcessRunId && item.StepInstanceId == Seed.ProcessStepId, rowDeadline.Token);
            var expiry = DateTimeOffset.UtcNow;
            Assert.True(Seed.Request.ExecutionCreatedAtUtc < expiry);
            Assert.True(claim.ExpiresAtUtc > expiry);
            claim.ExpiresAtUtc = expiry;
            await context.SaveChangesAsync(rowDeadline.Token);
            var assignment = await context.RuntimeStepAssignments.AsNoTracking().SingleAsync(item => item.RunId == Seed.ProcessRunId && item.StepInstanceId == Seed.ProcessStepId, rowDeadline.Token);
            var observation = await services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>().ObserveOriginalForReceiptReadAsync(new(
                Seed.Request.ExecutionRunId, Seed.Request.AgentId, new(Seed.ProcessRunId!.Value), new(Seed.ProcessStepId!.Value),
                claim.ClaimToken, assignment.StepKey, Seed.Request.ExecutionCreatedAtUtc, true), rowDeadline.Token);
            Assert.NotNull(observation.Snapshot);
            Assert.False(observation.Snapshot.ObservedCurrentDispatch);
        });
        processState = await ProcessStateAsync();
    }

    internal async Task ReactivateAsync() {
        Assert.Null(web);
        await AssertNoClientsAsync();
        await WithServicesAsync(async services => {
            var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
            var result = await services.GetRequiredService<DatasetActivationCoordinator>().ReactivateAsync(profile,
                new(Seed.NamespaceId), Guid.NewGuid(), rowDeadline.Token);
            Assert.NotEqual(Seed.NamespaceId, result.Namespace.Value);
        });
        await AssertNoClientsAsync();
        await RecordAsync("dataset-reactivated", new { PriorNamespace = Seed.NamespaceId, WritersStopped = true });
    }

    internal async Task AssertOriginalEvidenceAsync() {
        Assert.Equal(originalFiles.OrderBy(item => item.Key), (await CaptureFilesAsync()).OrderBy(item => item.Key));
        Assert.Equal(Seed.RunFileSha256, await HashAsync(Layout.RunPath(Seed.Request.ExecutionRunId)));
        await WithServicesAsync(async services => {
            await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(rowDeadline.Token);
            var rows = await context.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().ToArrayAsync(rowDeadline.Token);
            Assert.Equal(5, rows.Length);
            foreach (var receipt in Seed.Receipts) {
                var row = Assert.Single(rows, item => item.OperationId == receipt.OperationId);
                Assert.Equal(Seed.NamespaceId, row.NamespaceId);
                Assert.Equal(Seed.ProjectLifetimeId, row.LifetimeId);
                Assert.Equal(receipt.Sha256, Convert.ToHexString(SHA256.HashData(row.AcceptedPayload)));
            }
        });
        if (processState is not null) {
            Assert.Equal(processState, await ProcessStateAsync());
        }
    }

    private FileSandboxWorkspaceStorageLayout Layout => new(profile!.WorkspaceRootPath,
        WorkspaceScopeDescriptor.Organization(profileId.ToString("N")));

    private async Task<Dictionary<string, string>> CaptureFilesAsync() {
        var paths = Directory.EnumerateFiles(Layout.ExecutionStorageRoot, "*", SearchOption.AllDirectories).Take(129).ToList();
        Assert.True(paths.Count <= 128, "The small seeded execution workspace exceeded its file observation bound.");
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (var path in paths) {
            RequireContained(path);
            result.Add(Path.GetRelativePath(rowRoot, path), await HashAsync(path));
        }
        return result;
    }

    private async Task<string> ProcessStateAsync() {
        string? result = null;
        await WithServicesAsync(async services => {
            await using var context = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync(rowDeadline.Token);
            result = JsonSerializer.Serialize(new {
                RunCount = await context.RuntimeStates.CountAsync(rowDeadline.Token),
                ClaimCount = await context.DispatchClaims.CountAsync(rowDeadline.Token),
                RecordCount = await context.RunRecords.CountAsync(rowDeadline.Token),
                State = await context.RuntimeStates.AsNoTracking().SingleAsync(item => item.RunId == Seed.ProcessRunId, rowDeadline.Token),
                Steps = await context.RuntimeSteps.AsNoTracking().Where(item => item.RunId == Seed.ProcessRunId).OrderBy(item => item.StepInstanceId).ToArrayAsync(rowDeadline.Token),
                Claims = await context.DispatchClaims.AsNoTracking().Where(item => item.RunId == Seed.ProcessRunId).OrderBy(item => item.ClaimToken).ToArrayAsync(rowDeadline.Token)
            });
        });
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(result!)));
    }

    private async Task WithServicesAsync(Func<IServiceProvider, Task> operation) {
        Assert.NotNull(environment);
        Assert.NotNull(profile);
        var registrations = new ServiceCollection();
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile, Configuration());
        TestApplicationBootstrap.ConfigureDefaultServices(registrations, configuration, environment.CreateHostEnvironment("HistoricalReceiptRestartParent"));
        registrations.AddRouting();
        registrations.AddCanDoItAllApi(configuration);
        registrations.AddLogging(logging => logging.ClearProviders());
        await using var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.False(provider.GetRequiredService<IOptions<ProcessRuntimeDispatchQueueOptions>>().Value.EnableRecovery);
        await using var scope = provider.CreateAsyncScope();
        rowDeadline.Token.ThrowIfCancellationRequested();
        await operation(scope.ServiceProvider);
        rowDeadline.Token.ThrowIfCancellationRequested();
    }

    private IReadOnlyDictionary<string, string?> Configuration() => new Dictionary<string, string?> {
        ["ControlPlane:RootPath"] = environment!.ControlPlaneRootPath,
        ["ControlPlane:StateRootPath"] = Path.Combine(environment.RootPath, "state"),
        ["ControlPlane:LogsRootPath"] = Path.Combine(environment.RootPath, "logs"),
        ["ControlPlane:RuntimeTemporaryRootPath"] = Path.Combine(environment.RootPath, "runtime"),
        ["Database:PasswordFile"] = string.Empty,
        [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind,
        [$"{ProcessRuntimeDispatchQueueOptions.ConfigurationSectionName}:{nameof(ProcessRuntimeDispatchQueueOptions.EnableRecovery)}"] = "false",
        ["Workflows:ExampleSeed:Enabled"] = "false",
        ["DevelopmentManager:TuningModeEnabled"] = "false",
        ["WebHost:HttpsRedirectionEnabled"] = "false",
        ["Api:Enabled"] = "true",
        ["Api:Authorization:Enabled"] = "true",
        ["Api:Authorization:Issuer"] = "HistoricalReceiptRestartTests",
        ["Api:Authorization:Audience"] = "HistoricalReceiptRestartTests",
        ["Api:Authorization:SigningKey"] = signingKey
    };

    private async Task<OwnedChild> LaunchAsync(string name, string assembly, string[] arguments, Uri? endpoint) {
        Assert.True(File.Exists(assembly), "The standard configuration child output must be built before this opt-in row.");
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = environment!.RootPath, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        string[] prefixes = ["Database__", "ControlPlane__", "Storage__", "SecretVault__", "DataProtection__", "Kestrel__", "Api__", "WebHost__", "AgentFramework__Providers__", "Workflows__ExampleSeed__"];
        foreach (var key in start.Environment.Keys.Where(key => prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToArray()) {
            start.Environment.Remove(key);
        }
        foreach (var pair in profile!.CreateEnvironmentVariables(Configuration())) {
            start.Environment[pair.Key] = pair.Value;
        }
        start.Environment[HistoricalReceiptSeedProtocol.OutputRootEnvironment] = rowRoot;
        start.Environment[HistoricalReceiptSeedProtocol.EnvironmentRootEnvironment] = environment.RootPath;
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = endpoint?.AbsoluteUri ?? string.Empty;
        start.Environment["ASPNETCORE_HTTP_PORTS"] = string.Empty;
        start.Environment["ASPNETCORE_HTTPS_PORTS"] = string.Empty;
        start.Environment["AgentFramework__Providers__SeedDefaults"] = endpoint is null ? "true" : "false";
        start.ArgumentList.Add(assembly);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }
        await RecordAsync("child-intent", new { Name = name, Assembly = assembly, Sha256 = await HashAsync(assembly), Arguments = arguments,
            ProfileId = profileId, Endpoint = endpoint?.AbsoluteUri });
        var process = new Process { StartInfo = start };
        var child = new OwnedChild(name, process, endpoint);
        children.Add(child);
        if (!process.Start()) {
            process.Dispose();
            throw new InvalidOperationException("The owned historical child could not start.");
        }
        child.Started = true;
        child.StartedAtUtc = new(process.StartTime.ToUniversalTime());
        child.Output = DrainAsync(process.StandardOutput.BaseStream, child.Pumps.Token);
        child.Error = DrainAsync(process.StandardError.BaseStream, child.Pumps.Token);
        await RecordAsync("child-started", new { child.Name, process.Id, child.StartedAtUtc, Endpoint = endpoint?.AbsoluteUri });
        return child;
    }

    private async Task StopChildAsync(OwnedChild child, ChildStopReason reason) {
        if (child.Completed || !child.Started) {
            return;
        }
        var forced = !child.Process.HasExited;
        if (forced) {
            child.Process.Kill(entireProcessTree: true);
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await child.Process.WaitForExitAsync(timeout.Token);
        await CompleteChildAsync(child, forced, reason);
    }

    private async Task CompleteChildAsync(OwnedChild child, bool intentionalTeardown, ChildStopReason reason = ChildStopReason.NaturalExit) {
        var counts = await FinishPumpsAsync(child, cancel: false);
        child.Completed = true;
        await RecordAsync("child-exited", new { child.Name, child.Process.Id, child.StartedAtUtc, child.Process.ExitCode,
            IntentionalTeardown = intentionalTeardown, StopReason = reason.ToString(), StdoutBytes = counts[0], StderrBytes = counts[1] });
        Assert.True(counts.All(count => count <= 16 * 1024 * 1024), "Child output exceeded its bounded diagnostic allowance.");
    }

    private static async Task<long> DrainAsync(Stream reader, CancellationToken cancellationToken) {
        var buffer = new byte[4096];
        long count = 0;
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) is var read && read != 0) {
            count = checked(count + read);
        }
        return count;
    }

    private static async Task<long[]> FinishPumpsAsync(OwnedChild child, bool cancel) {
        var combined = Task.WhenAll(child.Output, child.Error);
        Exception? primary = null;
        if (!cancel) {
            try {
                var result = await combined.WaitAsync(TimeSpan.FromSeconds(10));
                child.PumpsDrained = true;
                return result;
            } catch (Exception failure) {
                primary = failure;
            }
        }
        List<Exception> failures = primary is null ? [] : [primary];
        try {
            await child.Pumps.CancelAsync();
        } catch (Exception failure) {
            failures.Add(failure);
        }
        foreach (var pipe in new[] { child.Process.StandardOutput, child.Process.StandardError }) {
            try {
                pipe.Dispose();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        try {
            await combined;
        } catch (Exception failure) {
            IEnumerable<Exception> diagnostics = combined.Exception is { } aggregate ? aggregate.InnerExceptions : [failure];
            foreach (var diagnostic in diagnostics) {
                if (child.Pumps.IsCancellationRequested && diagnostic is (OperationCanceledException or ObjectDisposedException)) {
                    continue;
                }
                failures.Add(diagnostic);
            }
        } finally {
            child.PumpsDrained = true;
        }
        if (failures.Count != 0) {
            throw new AggregateException("Owned child pump completion failed after cancellation and pipe closure.", failures);
        }
        return combined.Status == TaskStatus.RanToCompletion ? combined.Result : [0, 0];
    }

    private async Task AssertNoClientsAsync() {
        Assert.NotNull(profile);
        using (var pool = new NpgsqlConnection(profile.ConnectionString)) {
            NpgsqlConnection.ClearPool(pool);
        }
        var owned = new NpgsqlConnectionStringBuilder(profile.ConnectionString);
        var name = owned.Database;
        owned.Database = "postgres";
        owned.Pooling = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var connection = new NpgsqlConnection(owned.ConnectionString);
        await connection.OpenAsync(deadline.Token);
        while (true) {
            await using var query = connection.CreateCommand();
            query.CommandText = "SELECT count(*) FROM pg_stat_activity WHERE datname = @database AND backend_type = 'client backend'";
            query.Parameters.AddWithValue("database", name!);
            if (Convert.ToInt64(await query.ExecuteScalarAsync(deadline.Token)) == 0) {
                break;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
        }
        await RecordAsync("database-clients-absent", new { DatabaseName = name });
    }

    private async Task AssertDatabaseRemovedAsync() {
        var owned = new NpgsqlConnectionStringBuilder(profile!.ConnectionString);
        var name = owned.Database;
        owned.Database = "postgres";
        owned.Pooling = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var connection = new NpgsqlConnection(owned.ConnectionString);
        await connection.OpenAsync(deadline.Token);
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT count(*) FROM pg_database WHERE datname = @database";
        query.Parameters.AddWithValue("database", name!);
        Assert.Equal(0, Convert.ToInt64(await query.ExecuteScalarAsync(deadline.Token)));
        await RecordAsync("database-removed", new { DatabaseName = name });
    }

    private async Task CleanupAsync(string? workloadFailureType) {
        List<Exception> failures = [];
        try {
            client?.Dispose();
            foreach (var child in children.AsEnumerable().Reverse()) {
                try {
                    await StopChildAsync(child, workloadFailureType is null ? ChildStopReason.ObserverComplete : ChildStopReason.CleanupAfterFailure);
                } catch (Exception failure) {
                    failures.Add(failure);
                } finally {
                    if (child.Started && !child.PumpsDrained) {
                        try {
                            await FinishPumpsAsync(child, cancel: true);
                        } catch (Exception failure) {
                            failures.Add(failure);
                        }
                    }
                }
            }
            if (profile is not null && children.All(child => !child.Started || child.Process.HasExited)) {
                try {
                    await AssertNoClientsAsync();
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            }
            if (environment is not null && children.All(child => !child.Started || child.Process.HasExited)) {
                try {
                    RequireContained(environment.RootPath);
                    await environment.DisposeAsync();
                    Assert.False(Directory.Exists(environment.RootPath));
                    if (profile is not null) {
                        await AssertDatabaseRemovedAsync();
                    }
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            } else if (environment is not null) {
                failures.Add(new InvalidOperationException("The owned environment is retained because a child did not exit."));
            }
            var allocationUnknown = environmentAllocationAttempted && environment is null || databaseAllocationAttempted && profile is null;
            if (allocationUnknown) {
                failures.Add(new InvalidOperationException("An interrupted fixture allocation requires exact manifest reconciliation."));
            }
            await RecordAsync("cleanup", new { WorkloadFailureType = workloadFailureType, CleanupSucceeded = failures.Count == 0,
                AllocationMayRequireManifestReconciliation = allocationUnknown, FailureTypes = failures.Select(item => item.GetType().FullName).ToArray() });
        } finally {
            foreach (var child in children) {
                child.Process.Dispose();
                child.Pumps.Dispose();
            }
            rowDeadline.Dispose();
        }
        if (failures.Count != 0) {
            throw new AggregateException("Owned historical restart cleanup is incomplete.", failures);
        }
    }

    private Task RecordAsync(string phase, object evidence)
        => WriteNewAsync(Path.Combine(rowRoot, $"{++sequence:D3}-{phase}.json"), new {
            Owner = nameof(AgentHistoricalReceiptRestartTests), Nonce = nonce, Kind = kind, Phase = phase,
            ObservedAtUtc = DateTimeOffset.UtcNow, Evidence = evidence
        });

    private static async Task WriteNewAsync(string path, object value) {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(output, value, HistoricalReceiptSeedProtocol.Json);
        await output.FlushAsync();
        output.Flush(flushToDisk: true);
    }

    private void RequireContained(string path) {
        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(rowRoot, full);
        Assert.False(Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        RequireNoLinks(Directory.Exists(full) ? full : Path.GetDirectoryName(full)!);
    }

    private static void RequireNoLinks(string root) {
        for (DirectoryInfo? current = new(Path.GetFullPath(root)); current is not null; current = current.Parent) {
            Assert.False(current.Attributes.HasFlag(FileAttributes.ReparsePoint), "An owned fixture path must not traverse a reparse point.");
        }
    }

    private async Task<string> HashAsync(string path)
        => Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path, rowDeadline.Token)));

    private enum ChildStopReason { NaturalExit, ObserverComplete, CleanupAfterFailure }

    private sealed class OwnedChild(string name, Process process, Uri? endpoint) {
        internal string Name { get; } = name;
        internal Process Process { get; } = process;
        internal DateTimeOffset StartedAtUtc { get; set; }
        internal Uri? Endpoint { get; } = endpoint;
        internal Task<long> Output { get; set; } = Task.FromResult(0L);
        internal Task<long> Error { get; set; } = Task.FromResult(0L);
        internal CancellationTokenSource Pumps { get; } = new();
        internal bool PumpsDrained { get; set; }
        internal bool Started { get; set; }
        internal bool Completed { get; set; }
    }
}
