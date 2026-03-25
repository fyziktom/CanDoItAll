using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.LocalRuntime.Processes;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Mcp.DotNetWatch.Configuration;

public sealed class RuntimeConfiguration
{
    public RuntimeConfiguration(IOptions<McpServerOptions> optionsAccessor)
    {
        var options = optionsAccessor.Value;

        ServerName = options.Server.Name.Trim();
        WorkspaceRoot = ResolvePath(Environment.CurrentDirectory, options.Server.WorkspaceRoot);
        SolutionPath = ResolvePath(WorkspaceRoot, options.Server.SolutionPath);
        ServerAssemblyPath = ResolveServerAssemblyPath();
        BinaryVersionMarker = ComputeBinaryVersionMarker(ServerAssemblyPath);

        DefaultApp = new RuntimeDefaultAppConfiguration(
            ResolvePath(WorkspaceRoot, options.DefaultApp.ProjectPath),
            ResolvePath(WorkspaceRoot, options.DefaultApp.WorkingDirectory ?? Path.GetDirectoryName(options.DefaultApp.ProjectPath)!),
            options.DefaultApp.Mode,
            options.DefaultApp.Configuration,
            NullIfWhiteSpace(options.DefaultApp.Framework),
            NullIfWhiteSpace(options.DefaultApp.LaunchProfile),
            options.DefaultApp.Arguments,
            options.DefaultApp.Urls,
            new Dictionary<string, string>(options.DefaultApp.EnvironmentOverlay, StringComparer.OrdinalIgnoreCase));

        HealthEnabled = options.Health.Enabled;
        HealthUrls = options.Health.Urls.Select(static url => new Uri(url, UriKind.Absolute)).ToArray();
        HealthTimeout = TimeSpan.FromMilliseconds(options.Health.TimeoutMs);
        HealthPollInterval = TimeSpan.FromMilliseconds(options.Health.PollIntervalMs);
        StableHealthSuccessCount = Math.Max(1, options.Health.StableSuccessCount);
        AcceptInsecureLocalhostHttps = options.Health.AcceptInsecureLocalhostHttps;
        AllowedHealthHosts = new HashSet<string>(options.Health.AllowedHosts, StringComparer.OrdinalIgnoreCase);

        BuildDefaultTargetPath = ResolvePath(WorkspaceRoot, options.Build.DefaultTargetPath);
        BuildDefaultWhenAppRunning = options.Build.DefaultWhenAppRunning;
        BuildDefaultTimeout = TimeSpan.FromMilliseconds(options.Build.DefaultTimeoutMs);
        BuildExtraArguments = options.Build.ExtraArguments;

        TestDefaultTargetPath = string.IsNullOrWhiteSpace(options.Tests.DefaultTargetPath)
            ? null
            : ResolvePath(WorkspaceRoot, options.Tests.DefaultTargetPath);
        TestDefaultWhenAppRunning = options.Tests.DefaultWhenAppRunning;
        TestDefaultTimeout = TimeSpan.FromMilliseconds(options.Tests.DefaultTimeoutMs);
        TestRunnerPreference = string.IsNullOrWhiteSpace(options.Tests.RunnerPreference) ? "Auto" : options.Tests.RunnerPreference;
        TestDefaultFilter = NullIfWhiteSpace(options.Tests.DefaultFilter);
        TestProjectPaths = options.Tests.Projects.Select(project => ResolvePath(WorkspaceRoot, project)).ToArray();

        LogBufferCapacity = options.Logs.BufferCapacity;
        PersistLogsToFile = options.Logs.PersistToFile;
        LogFolder = ResolvePath(WorkspaceRoot, options.Logs.Folder);
        BootstrapDiagnosticsPath = Path.Combine(LogFolder, "mcp-bootstrap-diagnostics.log");
        MaxLogFileSizeBytes = Math.Max(1, options.Logs.MaxFileSizeMb) * 1024L * 1024L;
        RedactionEnabled = options.Logs.RedactionEnabled;
        IncludeSystemEventsInLogs = options.Logs.IncludeSystemEvents;

        GracefulStopTimeout = TimeSpan.FromMilliseconds(options.Process.GracefulStopTimeoutMs);
        ForceKillAfter = TimeSpan.FromMilliseconds(options.Process.ForceKillAfterMs);
        CleanupStaleManagedProcessesOnStartup = options.Process.CleanupStaleManagedProcessesOnStartup;
        RegistryPath = ResolvePath(WorkspaceRoot, options.Process.RegistryPath);
        ServerInstanceDirectory = Path.Combine(Path.GetDirectoryName(RegistryPath) ?? Path.Combine(WorkspaceRoot, ".mcp-state"), "server-instances");
        UsePollingFileWatcher = options.Process.UsePollingFileWatcher;

        BackendEnabled = options.Backend.Enabled;
        BackendBindHost = options.Backend.BindHost.Trim();
        BackendRegistrationPath = ResolvePath(WorkspaceRoot, options.Backend.RegistrationPath);
        BackendLaunchLockPath = ResolvePath(WorkspaceRoot, options.Backend.LaunchLockPath);
        BackendStartupTimeout = TimeSpan.FromMilliseconds(options.Backend.StartupTimeoutMs);
        BackendStartupPollInterval = TimeSpan.FromMilliseconds(options.Backend.StartupPollIntervalMs);
        MachineStateRoot = ResolveMachineStateRoot();
        GlobalBackendCatalogDirectory = Path.Combine(MachineStateRoot, "backend-catalog");

        BridgePingTimeout = TimeSpan.FromMilliseconds(options.Bridge.PingTimeoutMs);
        BridgeRepairRetryCount = Math.Max(0, options.Bridge.RepairRetryCount);

        AtomicRuntimeEnabled = options.AtomicRuntime.Enabled;
        RuntimeSlotRoot = ResolvePath(WorkspaceRoot, options.AtomicRuntime.SlotRoot);
        RollbackRetentionCount = Math.Max(1, options.AtomicRuntime.RollbackRetentionCount);
        DefaultCandidateConfiguration = string.IsNullOrWhiteSpace(options.AtomicRuntime.DefaultCandidateConfiguration)
            ? "Release"
            : options.AtomicRuntime.DefaultCandidateConfiguration;

        EndpointLeasePath = ResolvePath(WorkspaceRoot, options.Endpoints.LeasePath);
        CandidateHttpPortStart = options.Endpoints.CandidateHttpPortStart;
        CandidateHttpPortEnd = Math.Max(options.Endpoints.CandidateHttpPortStart, options.Endpoints.CandidateHttpPortEnd);

        ShadowRetainedBuildCount = Math.Max(1, options.ShadowHost.RetainedBuildCount);

        WorkflowGuidanceEnabled = options.WorkflowGuidance.Enabled;
        WorkflowGuidanceMaxSerializedCharacters = Math.Max(64, options.WorkflowGuidance.MaxSerializedCharacters);
        WorkflowGuidanceToolAllowList = new HashSet<string>(options.WorkflowGuidance.ToolAllowList, StringComparer.OrdinalIgnoreCase);

        DefaultAppWaitTimeout = TimeSpan.FromMilliseconds(options.Waits.DefaultAppWaitTimeoutMs);
        DefaultOperationWaitTimeout = TimeSpan.FromMilliseconds(options.Waits.DefaultOperationWaitTimeoutMs);
        DefaultPollInterval = TimeSpan.FromMilliseconds(options.Waits.DefaultPollIntervalMs);
        DefaultQuietPeriod = TimeSpan.FromMilliseconds(options.Waits.DefaultQuietPeriodMs);

        AllowedProjectRoots = options.Security.AllowedProjectRoots.Select(root => ResolvePath(WorkspaceRoot, root)).ToArray();
        AllowExternalHealthHosts = options.Security.AllowExternalHealthHosts;
        AllowedEnvironmentKeys = new HashSet<string>(options.Security.AllowedEnvironmentKeys, StringComparer.OrdinalIgnoreCase);

        Directory.CreateDirectory(LogFolder);
        var registryDirectory = Path.GetDirectoryName(RegistryPath);
        if (!string.IsNullOrWhiteSpace(registryDirectory))
        {
            Directory.CreateDirectory(registryDirectory);
        }

        Directory.CreateDirectory(ServerInstanceDirectory);
        EnsureParentDirectoryExists(BackendRegistrationPath);
        EnsureParentDirectoryExists(BackendLaunchLockPath);
        Directory.CreateDirectory(GlobalBackendCatalogDirectory);
        Directory.CreateDirectory(RuntimeSlotRoot);
        EnsureParentDirectoryExists(EndpointLeasePath);
    }

    public string ServerName { get; }

    public string WorkspaceRoot { get; }

    public string SolutionPath { get; }

    public string ServerAssemblyPath { get; }

    public string BinaryVersionMarker { get; }

    public RuntimeDefaultAppConfiguration DefaultApp { get; }

    public bool HealthEnabled { get; }

    public IReadOnlyList<Uri> HealthUrls { get; }

    public TimeSpan HealthTimeout { get; }

    public TimeSpan HealthPollInterval { get; }

    public int StableHealthSuccessCount { get; }

    public bool AcceptInsecureLocalhostHttps { get; }

    public HashSet<string> AllowedHealthHosts { get; }

    public string BuildDefaultTargetPath { get; }

    public WhenAppRunningPolicy BuildDefaultWhenAppRunning { get; }

    public TimeSpan BuildDefaultTimeout { get; }

    public IReadOnlyList<string> BuildExtraArguments { get; }

    public string? TestDefaultTargetPath { get; }

    public WhenAppRunningPolicy TestDefaultWhenAppRunning { get; }

    public TimeSpan TestDefaultTimeout { get; }

    public string TestRunnerPreference { get; }

    public string? TestDefaultFilter { get; }

    public IReadOnlyList<string> TestProjectPaths { get; }

    public int LogBufferCapacity { get; }

    public bool PersistLogsToFile { get; }

    public string LogFolder { get; }

    public string BootstrapDiagnosticsPath { get; }

    public long MaxLogFileSizeBytes { get; }

    public bool RedactionEnabled { get; }

    public bool IncludeSystemEventsInLogs { get; }

    public TimeSpan GracefulStopTimeout { get; }

    public TimeSpan ForceKillAfter { get; }

    public bool CleanupStaleManagedProcessesOnStartup { get; }

    public string RegistryPath { get; }

    public string ServerInstanceDirectory { get; }

    public bool UsePollingFileWatcher { get; }

    public bool BackendEnabled { get; }

    public string BackendBindHost { get; }

    public string BackendRegistrationPath { get; }

    public string BackendLaunchLockPath { get; }

    public TimeSpan BackendStartupTimeout { get; }

    public TimeSpan BackendStartupPollInterval { get; }

    public string MachineStateRoot { get; }

    public string GlobalBackendCatalogDirectory { get; }

    public TimeSpan BridgePingTimeout { get; }

    public int BridgeRepairRetryCount { get; }

    public bool AtomicRuntimeEnabled { get; }

    public string RuntimeSlotRoot { get; }

    public int RollbackRetentionCount { get; }

    public string DefaultCandidateConfiguration { get; }

    public string EndpointLeasePath { get; }

    public int CandidateHttpPortStart { get; }

    public int CandidateHttpPortEnd { get; }

    public int ShadowRetainedBuildCount { get; }

    public bool WorkflowGuidanceEnabled { get; }

    public int WorkflowGuidanceMaxSerializedCharacters { get; }

    public HashSet<string> WorkflowGuidanceToolAllowList { get; }

    public TimeSpan DefaultAppWaitTimeout { get; }

    public TimeSpan DefaultOperationWaitTimeout { get; }

    public TimeSpan DefaultPollInterval { get; }

    public TimeSpan DefaultQuietPeriod { get; }

    public IReadOnlyList<string> AllowedProjectRoots { get; }

    public bool AllowExternalHealthHosts { get; }

    public HashSet<string> AllowedEnvironmentKeys { get; }

    public FileLogStoreOptions CreateFileLogStoreOptions()
    {
        return new FileLogStoreOptions
        {
            Enabled = PersistLogsToFile,
            RootDirectory = LogFolder,
            MaxFileSizeBytes = MaxLogFileSizeBytes
        };
    }

    public SecretRedactionOptions CreateSecretRedactionOptions()
    {
        return new SecretRedactionOptions
        {
            Enabled = RedactionEnabled
        };
    }

    public LocalProcessRuntimeOptions CreateLocalProcessRuntimeOptions()
    {
        return new LocalProcessRuntimeOptions
        {
            WorkspaceRoot = WorkspaceRoot,
            RegistryPath = RegistryPath,
            ServerInstanceDirectory = ServerInstanceDirectory,
            GracefulStopTimeout = GracefulStopTimeout,
            ForceKillAfter = ForceKillAfter
        };
    }

    public IReadOnlyDictionary<string, object?> CreateRedactedSnapshot()
    {
        return new Dictionary<string, object?>
        {
            ["serverName"] = ServerName,
            ["binaryVersionMarker"] = BinaryVersionMarker,
            ["workspaceRoot"] = WorkspaceRoot,
            ["workspaceRootRelative"] = ".",
            ["solutionPath"] = SolutionPath,
            ["solutionPathRelative"] = GetRelativePath(SolutionPath),
            ["backend"] = new Dictionary<string, object?>
            {
                ["enabled"] = BackendEnabled,
                ["bindHost"] = BackendBindHost,
                ["registrationPath"] = BackendRegistrationPath,
                ["registrationPathRelative"] = GetRelativePath(BackendRegistrationPath),
                ["launchLockPath"] = BackendLaunchLockPath,
                ["launchLockPathRelative"] = GetRelativePath(BackendLaunchLockPath),
                ["machineStateRoot"] = MachineStateRoot,
                ["globalBackendCatalogDirectory"] = GlobalBackendCatalogDirectory
            },
            ["bridge"] = new Dictionary<string, object?>
            {
                ["pingTimeoutMs"] = (int)BridgePingTimeout.TotalMilliseconds,
                ["repairRetryCount"] = BridgeRepairRetryCount
            },
            ["atomicRuntime"] = new Dictionary<string, object?>
            {
                ["enabled"] = AtomicRuntimeEnabled,
                ["slotRoot"] = RuntimeSlotRoot,
                ["slotRootRelative"] = GetRelativePath(RuntimeSlotRoot),
                ["rollbackRetentionCount"] = RollbackRetentionCount,
                ["defaultCandidateConfiguration"] = DefaultCandidateConfiguration
            },
            ["endpoints"] = new Dictionary<string, object?>
            {
                ["leasePath"] = EndpointLeasePath,
                ["leasePathRelative"] = GetRelativePath(EndpointLeasePath),
                ["candidateHttpPortStart"] = CandidateHttpPortStart,
                ["candidateHttpPortEnd"] = CandidateHttpPortEnd
            },
            ["shadowHost"] = new Dictionary<string, object?>
            {
                ["retainedBuildCount"] = ShadowRetainedBuildCount
            },
            ["workflowGuidance"] = new Dictionary<string, object?>
            {
                ["enabled"] = WorkflowGuidanceEnabled,
                ["maxSerializedCharacters"] = WorkflowGuidanceMaxSerializedCharacters,
                ["toolAllowList"] = WorkflowGuidanceToolAllowList.ToArray()
            },
            ["defaultApp"] = new Dictionary<string, object?>
            {
                ["projectPath"] = DefaultApp.ProjectPath,
                ["projectPathRelative"] = GetRelativePath(DefaultApp.ProjectPath),
                ["workingDirectory"] = DefaultApp.WorkingDirectory,
                ["workingDirectoryRelative"] = GetRelativePath(DefaultApp.WorkingDirectory),
                ["mode"] = DefaultApp.Mode.ToString(),
                ["configuration"] = DefaultApp.Configuration,
                ["framework"] = DefaultApp.Framework,
                ["launchProfile"] = DefaultApp.LaunchProfile,
                ["arguments"] = DefaultApp.Arguments,
                ["urls"] = DefaultApp.Urls,
                ["environmentOverlay"] = DefaultApp.EnvironmentOverlay.ToDictionary(static pair => pair.Key, static _ => "***redacted***", StringComparer.OrdinalIgnoreCase)
            },
            ["healthUrls"] = HealthUrls.Select(url => url.ToString()).ToArray(),
            ["buildDefaultTargetPath"] = BuildDefaultTargetPath,
            ["buildDefaultTargetPathRelative"] = GetRelativePath(BuildDefaultTargetPath),
            ["testDefaultTargetPath"] = TestDefaultTargetPath,
            ["testDefaultTargetPathRelative"] = string.IsNullOrWhiteSpace(TestDefaultTargetPath) ? null : GetRelativePath(TestDefaultTargetPath),
            ["testProjects"] = TestProjectPaths.ToArray(),
            ["testProjectsRelative"] = TestProjectPaths.Select(GetRelativePath).ToArray(),
            ["allowedProjectRoots"] = AllowedProjectRoots.ToArray(),
            ["allowedProjectRootsRelative"] = AllowedProjectRoots.Select(GetRelativePath).ToArray(),
            ["allowedEnvironmentKeys"] = AllowedEnvironmentKeys.ToArray()
        };
    }

    public string GetRelativePath(string path)
    {
        return Path.GetRelativePath(WorkspaceRoot, path);
    }

    private static string ResolvePath(string basePath, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }

    private static void EnsureParentDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string ResolveMachineStateRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "CanDoItAll.Mcp.DotNetWatch");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, ".candoitall-mcp-dotnetwatch");
        }

        return Path.Combine(Path.GetTempPath(), "CanDoItAll.Mcp.DotNetWatch");
    }

    private static string ResolveServerAssemblyPath()
    {
        return Path.GetFullPath(Assembly.GetEntryAssembly()?.Location ?? Environment.ProcessPath ?? throw new InvalidOperationException("Could not resolve server assembly path."));
    }

    private static string ComputeBinaryVersionMarker(string assemblyPath)
    {
        var fileInfo = new FileInfo(assemblyPath);
        var stamp = $"{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stamp));
        return Convert.ToHexString(hash);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public sealed record RuntimeDefaultAppConfiguration(
    string ProjectPath,
    string WorkingDirectory,
    AppRunMode Mode,
    string Configuration,
    string? Framework,
    string? LaunchProfile,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> Urls,
    IReadOnlyDictionary<string, string> EnvironmentOverlay);
