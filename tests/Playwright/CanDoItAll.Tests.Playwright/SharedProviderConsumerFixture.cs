using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

internal sealed class SharedProviderConsumerFixture : IAsyncDisposable {
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static readonly JsonSerializerOptions ReadJson = new(Json) {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly IPlaywright playwright;
    private readonly IBrowser browser;
    private readonly string root;
    private readonly JsonElement metadata;
    private readonly List<string> browserErrors = [];
    private readonly List<object> requestFailures = [];
    private readonly string traceId = Guid.NewGuid().ToString("N");
    public SharedProviderNativeDefaultsUiTests.Settings Settings { get; }
    public IPage Page { get; }
    public HttpClient Api { get; }

    private SharedProviderConsumerFixture(IPlaywright playwright, IBrowser browser, IPage page, HttpClient api,
        SharedProviderNativeDefaultsUiTests.Settings settings, string root, JsonElement metadata) {
        this.playwright = playwright;
        this.browser = browser;
        this.root = root;
        this.metadata = metadata;
        Settings = settings;
        Page = page;
        Api = api;
        Page.PageError += (_, error) => browserErrors.Add(error);
        Page.Console += (_, message) => {
            if (message.Type == "error") {
                browserErrors.Add(message.Text);
            }
        };
        Page.RequestFailed += (_, request) => requestFailures.Add(new {
            Path = new Uri(request.Url).AbsolutePath, request.Method, request.ResourceType, request.Failure
        });
    }

    public static async Task<SharedProviderConsumerFixture> StartAsync() {
        var settings = SharedProviderNativeDefaultsUiTests.Settings.Load();
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("CANDOITALL_SHARED_PP2_FIXTURE_ROOT")!);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "host-run-metadata.json")));
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chrome" });
        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
        var page = await context.NewPageAsync();
        var token = await SharedProviderNativeDefaultsUiTests.IssueTokenAsync(page, settings.Clients[0], false, [
            ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects, ApiAccessScopeNames.WriteProjectStructure,
            ApiAccessScopeNames.WriteAgents, ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows,
            ApiAccessScopeNames.ExecuteWorkflows
        ]);
        var api = SharedProviderNativeDefaultsUiTests.Api(settings.Clients[0], token);
        var authority = new Uri(settings.Clients[0]).Authority;
        await context.RouteAsync("**/*", route => new Uri(route.Request.Url).Authority == authority
            ? route.ContinueAsync() : route.AbortAsync());
        await context.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" });
        return new(playwright, browser, page, api, settings, root, document.RootElement.Clone());
    }

    public Task NavigateAsync(string relative) => SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(Page, Settings.Clients[0] + relative);
    public Task ScreenshotAsync(string name) => Page.ScreenshotAsync(new() { Path = Path.Combine(Settings.Evidence, name + ".png") });
    public Task EvidenceAsync(string name, object evidence) => File.WriteAllTextAsync(Path.Combine(Settings.Evidence, name + ".json"), JsonSerializer.Serialize(evidence, Json));
    public static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public async Task<JsonElement> GetAsync(string path) {
        using var response = await Api.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    public async Task<T> PostAsync<T>(string path, object request) {
        using var response = await Api.PostAsJsonAsync(path, request, Json);
        Assert.True(response.IsSuccessStatusCode, $"Native {path} returned {(int)response.StatusCode}.");
        return (await response.Content.ReadFromJsonAsync<T>(ReadJson))!;
    }

    public async Task ScriptAsync(string sourceModel, string marker, params object[] steps) {
        await RunOracleAsync("e2e-runner", ["consumer-upstream", "script"], JsonSerializer.Serialize(new { model = sourceModel, marker, steps }, Json));
    }

    public async Task AssertScriptCompleteAsync(int expected) {
        var progress = await RunOracleAsync("e2e-runner", ["consumer-upstream", "progress"]);
        Assert.Equal(expected, progress.GetProperty("total").GetInt32());
        Assert.Equal(expected, progress.GetProperty("consumed").GetInt32());
    }

    public Task<JsonElement> ReadCapturesAsync() => RunOracleAsync("e2e-runner", ["consumer-upstream", "captures"]);
    public Task<JsonElement> ClearScriptAsync() => RunOracleAsync("e2e-runner", ["consumer-upstream", "clear"]);

    public Task<JsonElement> ReadAgentAsync(Guid agentId) => RunOracleAsync("e2e-client-a", ["read-consumer-agent", agentId.ToString("D"), "--role", "client-a"]);

    public async Task RestartOwnedAppsAsync() {
        var project = metadata.GetProperty("composeProjectName").GetString()!;
        using var images = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "tool-state", "handoff", "image-reuse.json")));
        var expectedImage = images.RootElement.GetProperty("appImageId").GetString();
        var before = new List<AppContainer>();
        foreach (var role in new[] { "central", "client-a", "client-b" }) {
            var identities = (await DockerAsync("ps", "--all", "--filter", "label=com.docker.compose.project=" + project,
                "--filter", "label=com.docker.compose.service=" + role, "--format", "{{.ID}}"))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var container = await InspectAsync(Assert.Single(identities));
            Assert.Equal(project, container.Project);
            Assert.Equal(role, container.Role);
            Assert.Equal(metadata.GetProperty("appImage").GetString(), container.Image);
            Assert.Equal(expectedImage, container.ImageId);
            before.Add(container);
        }
        await Page.GotoAsync("about:blank");
        await DockerAsync(["restart", .. before.Select(container => container.Id)]);
        var after = new List<AppContainer>();
        foreach (var previous in before) {
            var current = await InspectAsync(previous.Id);
            Assert.Equal(previous with { StartedAt = current.StartedAt }, current);
            Assert.True(current.StartedAt > previous.StartedAt);
            after.Add(current);
        }
        await EvidenceAsync("custom-restart-containers", new { Before = before, After = after });

        async Task<AppContainer> InspectAsync(string id) {
            var fields = (await DockerAsync("inspect", "--format",
                "{{.Id}}|{{.Image}}|{{.Config.Image}}|{{index .Config.Labels \"com.docker.compose.project\"}}|{{index .Config.Labels \"com.docker.compose.service\"}}|{{.State.StartedAt}}", id)).Trim().Split('|');
            Assert.Equal(6, fields.Length);
            return new(fields[0], fields[1], fields[2], fields[3], fields[4], DateTimeOffset.Parse(fields[5]));
        }
    }

    private sealed record AppContainer(string Id, string ImageId, string Image, string Project, string Role, DateTimeOffset StartedAt);

    private static async Task<string> DockerAsync(params string[] arguments) {
        var start = new ProcessStartInfo("docker") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("--context");
        start.ArgumentList.Add("default");
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The owned restart command did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, $"The owned restart command failed: {await stderr}");
        return await stdout;
    }

    private async Task<JsonElement> RunOracleAsync(string service, string[] arguments, string? input = null) {
        var repository = TestRepositoryRoot.Find();
        var start = new ProcessStartInfo("docker") {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "--context", "default", "compose", "--ansi", "never", "--project-name", metadata.GetProperty("composeProjectName").GetString()!,
            "--env-file", ".env.shared-providers.e2e.example", "--file", "compose.shared-providers.e2e.yaml", "--profile", "orchestrator",
            "run", "--rm", "--no-deps", "-T", service }.Concat(arguments)) {
            start.ArgumentList.Add(argument);
        }
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("COMPOSE_", StringComparison.Ordinal) || key.StartsWith("E2E_", StringComparison.Ordinal) || key.StartsWith("DOCKER_", StringComparison.Ordinal)).ToArray()) {
            start.Environment.Remove(key);
        }
        foreach (var line in await File.ReadAllLinesAsync(Path.Combine(repository, ".env.shared-providers.e2e.example"))) {
            var pair = line.Split('=', 2);
            if (pair.Length == 2 && pair[0].StartsWith("E2E_", StringComparison.Ordinal) && pair[0].EndsWith("_FILE", StringComparison.Ordinal)) {
                start.Environment[pair[0]] = Path.Combine(root, "runtime-secrets", Path.GetFileName(pair[1]));
            }
        }
        start.Environment["E2E_ARTIFACT_ROOT"] = root;
        start.Environment["E2E_RUN_MARKER"] = metadata.GetProperty("runMarker").GetString();
        start.Environment["E2E_APP_IMAGE"] = metadata.GetProperty("appImage").GetString();
        start.Environment["E2E_UPSTREAM_IMAGE"] = metadata.GetProperty("upstreamImage").GetString();
        var prefix = metadata.GetProperty("ingressPrefix").GetString()!.Split('.');
        string[] networks = ["LOCAL_INGRESS", "APP_MESH", "CENTRAL_DB", "CLIENT_A_DB", "CLIENT_B_DB", "UPSTREAM_DATA", "UPSTREAM_CONTROL", "CLIENT_A_PERSONAL", "PERSONAL_CONTROL"];
        for (var index = 0; index < networks.Length; index++) {
            start.Environment[$"E2E_{networks[index]}_SUBNET"] = $"{prefix[0]}.{prefix[1]}.{int.Parse(prefix[2]) + index}.0/24";
        }
        start.Environment["E2E_LOCAL_INGRESS_GATEWAY"] = string.Join('.', prefix) + ".1";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The native fixture oracle did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (input is not null) {
            await process.StandardInput.WriteAsync(input);
        }
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, $"The owned consumer oracle failed ({service}/{arguments[0]}). {await stdout}");
        Assert.True((await stderr).Length < 16_384, "Unexpected fixture oracle diagnostics.");
        var payload = (await stdout).Split('\n', StringSplitOptions.RemoveEmptyEntries).Last();
        return JsonSerializer.Deserialize<JsonElement>(payload, Json);
    }

    public async ValueTask DisposeAsync() {
        try {
            await ScreenshotAsync("consumer-final-" + traceId);
            await EvidenceAsync("consumer-final-text-" + traceId, await Page.Locator("body").InnerTextAsync());
            await ClearScriptAsync();
            await EvidenceAsync("consumer-browser-" + traceId, new { Errors = browserErrors, RequestFailures = requestFailures });
        } finally {
            Api.Dispose();
            await browser.DisposeAsync();
            playwright.Dispose();
        }
        Assert.Empty(browserErrors);
    }
}
