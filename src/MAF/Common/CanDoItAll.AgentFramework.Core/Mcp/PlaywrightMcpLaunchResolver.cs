using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.Core;

public sealed record PlaywrightMcpLaunchResolution(
    string Command,
    IReadOnlyList<string> Arguments);

public static partial class PlaywrightMcpLaunchResolver
{
    private const string PackageName = "@playwright/mcp";
    private const string EvidenceFileName = ".candoitall-install.json";

    public static async Task<PlaywrightMcpLaunchResolution?> TryResolveAsync(
        string workspaceRoot,
        string command,
        IReadOnlyList<string> arguments,
        IWorkspaceProcessHost processHost,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(processHost);
        if (!IsNpxCommand(command) ||
            !TrySplitPlaywrightMcpArguments(arguments, out var packageSpec, out var version, out var serverArguments))
        {
            return null;
        }

        WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(workspaceRoot);
        var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var managedRoot = Path.Combine(
            fullWorkspaceRoot,
            ".agent-tools",
            "npm",
            "playwright-mcp");
        var versionRoot = Path.Combine(managedRoot, version);
        EnsureManagedPathHasNoReparsePoints(fullWorkspaceRoot, versionRoot);
        var cliPath = ResolveCliPath(versionRoot);
        var nodePath = new WorkspaceExecutableLocator().ResolveExecutablePath(["node"], workspaceRoot);
        if (!await HasValidEvidenceAsync(
                versionRoot,
                packageSpec,
                version,
                nodePath,
                cancellationToken).ConfigureAwait(false))
        {
            if (Directory.Exists(versionRoot))
            {
                throw new InvalidOperationException(
                    $"The managed Playwright MCP installation for version '{version}' failed integrity validation. Quarantine or remove that version before retrying.");
            }

            var npmLaunch = ResolveNpmLaunch(fullWorkspaceRoot, nodePath);
            await InstallAtomicallyAsync(
                    managedRoot,
                    versionRoot,
                    fullWorkspaceRoot,
                    packageSpec,
                    version,
                    nodePath,
                    npmLaunch,
                    processHost,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!await HasValidEvidenceAsync(
                versionRoot,
                packageSpec,
                version,
                nodePath,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                $"The managed Playwright MCP installation for version '{version}' could not be verified after setup.");
        }

        EnsureManagedPathHasNoReparsePoints(fullWorkspaceRoot, versionRoot);

        return new PlaywrightMcpLaunchResolution(
            nodePath,
            [cliPath, ..serverArguments]);
    }

    public static bool IsPinnedVisionLaunch(
        string command,
        IReadOnlyList<string> arguments)
    {
        try
        {
            return IsNpxCommand(command) &&
                   TrySplitPlaywrightMcpArguments(
                       arguments,
                       out _,
                       out _,
                       out var serverArguments) &&
                   HasVisionCapability(serverArguments);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task InstallAtomicallyAsync(
        string managedRoot,
        string versionRoot,
        string workspaceRoot,
        string packageSpec,
        string version,
        string nodePath,
        NpmLaunch npmLaunch,
        IWorkspaceProcessHost processHost,
        CancellationToken cancellationToken)
    {
        EnsureManagedPathHasNoReparsePoints(workspaceRoot, managedRoot);
        Directory.CreateDirectory(managedRoot);
        EnsureManagedPathHasNoReparsePoints(workspaceRoot, managedRoot);
        var stagingRoot = Path.Combine(managedRoot, $".install-{version}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        EnsureManagedPathHasNoReparsePoints(workspaceRoot, stagingRoot);
        try
        {
            var environment = new WorkspaceCommandEnvironmentPolicy()
                .MergeEnvironmentVariables(environmentVariables: null, "playwright_mcp_npm_install");
            var result = await processHost.ExecuteAsync(
                    new WorkspaceProcessExecutionRequest(
                        ToolName: "playwright_mcp_npm_install",
                        RecipeId: $"playwright-mcp-{version}",
                        ExecutablePath: npmLaunch.Command,
                        Arguments:
                        [
                            ..npmLaunch.PrefixArguments,
                            "install",
                            "--prefix",
                            stagingRoot,
                            "--no-audit",
                            "--fund=false",
                            "--ignore-scripts",
                            "--package-lock=true",
                            packageSpec
                        ],
                        WorkingDirectory: stagingRoot,
                        EnvironmentVariables: environment,
                        TimeoutSeconds: 300,
                        StdoutLimitCharacters: 16 * 1024,
                        StderrLimitCharacters: 16 * 1024),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.TerminationReason == WorkspaceProcessTerminationReason.CallerCanceled)
            {
                throw new OperationCanceledException(
                    "Playwright MCP package setup was canceled.",
                    cancellationToken);
            }

            if (result.TerminationReason == WorkspaceProcessTerminationReason.TimedOut)
            {
                throw new TimeoutException(
                    $"Timed out preparing Playwright MCP package version '{version}'.");
            }

            if (!result.Started || result.ExitCode != 0 || result.ResidualProcessPossible)
            {
                throw new InvalidOperationException(
                    $"npm failed to prepare Playwright MCP package version '{version}' with exit code {result.ExitCode}.");
            }

            await WriteEvidenceAsync(
                    stagingRoot,
                    packageSpec,
                    version,
                    nodePath,
                    cancellationToken)
                .ConfigureAwait(false);
            EnsureManagedPathHasNoReparsePoints(workspaceRoot, stagingRoot);
            EnsureManagedPathHasNoReparsePoints(workspaceRoot, versionRoot);
            try
            {
                Directory.Move(stagingRoot, versionRoot);
            }
            catch (IOException)
            {
                if (!Directory.Exists(versionRoot) ||
                    !await HasValidEvidenceAsync(
                            versionRoot,
                            packageSpec,
                            version,
                            nodePath,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw;
                }
            }

            EnsureManagedPathHasNoReparsePoints(workspaceRoot, versionRoot);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static NpmLaunch ResolveNpmLaunch(
        string workingDirectory,
        string nodePath)
    {
        var locator = new WorkspaceExecutableLocator();
        var npmPath = locator.ResolveExecutablePath(["npm"], workingDirectory);
        if (string.Equals(Path.GetExtension(npmPath), ".js", StringComparison.OrdinalIgnoreCase))
        {
            return new NpmLaunch(nodePath, [npmPath]);
        }

        var npmCliPath = Path.Combine(
            Path.GetDirectoryName(npmPath) ?? string.Empty,
            "node_modules",
            "npm",
            "bin",
            "npm-cli.js");
        if (File.Exists(npmCliPath))
        {
            return new NpmLaunch(nodePath, [npmCliPath]);
        }

        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The installed npm runtime does not expose npm-cli.js for shell-neutral execution.");
        }

        return new NpmLaunch(npmPath, []);
    }

    private static async Task WriteEvidenceAsync(
        string versionRoot,
        string packageSpec,
        string version,
        string nodePath,
        CancellationToken cancellationToken)
    {
        var cliPath = ResolveCliPath(versionRoot);
        var packageJsonPath = Path.Combine(
            versionRoot,
            "node_modules",
            "@playwright",
            "mcp",
            "package.json");
        var packageLockPath = Path.Combine(versionRoot, "package-lock.json");
        if (!File.Exists(cliPath) ||
            !File.Exists(packageJsonPath) ||
            !File.Exists(packageLockPath))
        {
            throw new InvalidOperationException(
                $"Playwright MCP package version '{version}' did not produce its required runtime files.");
        }

        using (var packageDocument = JsonDocument.Parse(await File.ReadAllTextAsync(packageJsonPath, cancellationToken).ConfigureAwait(false)))
        {
            if (!packageDocument.RootElement.TryGetProperty("version", out var versionElement) ||
                !string.Equals(versionElement.GetString(), version, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Playwright MCP package metadata did not match required version '{version}'.");
            }
        }

        await using var cliStream = File.OpenRead(cliPath);
        var cliHash = Convert.ToHexString(await SHA256.HashDataAsync(cliStream, cancellationToken).ConfigureAwait(false));
        await using var packageLockStream = File.OpenRead(packageLockPath);
        var packageLockHash = Convert.ToHexString(
            await SHA256.HashDataAsync(packageLockStream, cancellationToken).ConfigureAwait(false));
        await using var nodeStream = File.OpenRead(nodePath);
        var nodeHash = Convert.ToHexString(
            await SHA256.HashDataAsync(nodeStream, cancellationToken).ConfigureAwait(false));
        var nodeMode = GetModeIdentity(nodePath);
        var contentTreeHash = await ComputeContentTreeSha256Async(
                versionRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var evidence = new InstallEvidence(
            3,
            packageSpec,
            version,
            cliHash,
            packageLockHash,
            nodeHash,
            nodeMode,
            contentTreeHash);
        await File.WriteAllTextAsync(
                Path.Combine(versionRoot, EvidenceFileName),
                JsonSerializer.Serialize(evidence),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<bool> HasValidEvidenceAsync(
        string versionRoot,
        string packageSpec,
        string version,
        string nodePath,
        CancellationToken cancellationToken)
    {
        var cliPath = ResolveCliPath(versionRoot);
        var evidencePath = Path.Combine(versionRoot, EvidenceFileName);
        var packageLockPath = Path.Combine(versionRoot, "package-lock.json");
        if (!File.Exists(cliPath) ||
            !File.Exists(evidencePath) ||
            !File.Exists(packageLockPath))
        {
            return false;
        }

        try
        {
            var evidence = JsonSerializer.Deserialize<InstallEvidence>(
                await File.ReadAllTextAsync(evidencePath, cancellationToken).ConfigureAwait(false));
            if (evidence is null ||
                evidence.SchemaVersion != 3 ||
                !string.Equals(evidence.PackageSpec, packageSpec, StringComparison.Ordinal) ||
                !string.Equals(evidence.Version, version, StringComparison.Ordinal))
            {
                return false;
            }

            await using var cliStream = File.OpenRead(cliPath);
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(cliStream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(actualHash, evidence.CliSha256, StringComparison.Ordinal))
            {
                return false;
            }

            await using var packageLockStream = File.OpenRead(packageLockPath);
            var packageLockHash = Convert.ToHexString(
                await SHA256.HashDataAsync(packageLockStream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(
                    packageLockHash,
                    evidence.PackageLockSha256,
                    StringComparison.Ordinal))
            {
                return false;
            }

            await using var nodeStream = File.OpenRead(nodePath);
            var nodeHash = Convert.ToHexString(
                await SHA256.HashDataAsync(nodeStream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(nodeHash, evidence.NodeExecutableSha256, StringComparison.Ordinal))
            {
                return false;
            }


            if (!string.Equals(
                    GetModeIdentity(nodePath),
                    evidence.NodeExecutableMode,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var contentTreeHash = await ComputeContentTreeSha256Async(
                    versionRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            return string.Equals(
                contentTreeHash,
                evidence.ContentTreeSha256,
                StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<string> ComputeContentTreeSha256Async(
        string versionRoot,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(versionRoot);
        var evidencePath = Path.Combine(root, EvidenceFileName);
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var entries = EnumerateContentEntries(root)
            .Where(entry => !string.Equals(
                Path.GetFullPath(entry.FullPath),
                evidencePath,
                pathComparison))
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ToArray();
        using var treeHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var linkTarget = entry.IsDirectory
                ? null
                : new FileInfo(entry.FullPath).LinkTarget;
            if (!entry.IsDirectory && linkTarget is not null)
            {
                var finalTarget = File.ResolveLinkTarget(entry.FullPath, returnFinalTarget: true)
                    ?? throw new InvalidOperationException(
                        "Managed Playwright MCP installation contains an unresolved file link.");
                if (!IsContainedPath(root, finalTarget.FullName))
                {
                    throw new InvalidOperationException(
                        "Managed Playwright MCP installation contains a file link outside its version root.");
                }
            }

            AppendUtf8(treeHash, entry.RelativePath);
            treeHash.AppendData([0]);
            AppendUtf8(treeHash, entry.IsDirectory ? "directory" : linkTarget is null ? "file" : "file-link");
            treeHash.AppendData([0]);
            AppendUtf8(treeHash, GetModeIdentity(entry.FullPath));
            treeHash.AppendData([0]);
            AppendUtf8(treeHash, linkTarget?.Replace('\\', '/') ?? string.Empty);
            treeHash.AppendData([0]);
            if (!entry.IsDirectory)
            {
                await using var stream = File.OpenRead(entry.FullPath);
                treeHash.AppendData(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            }

            treeHash.AppendData([10]);
        }

        return Convert.ToHexString(treeHash.GetHashAndReset());
    }

    private static IReadOnlyList<ContentEntry> EnumerateContentEntries(string root)
    {
        var entries = new List<ContentEntry>
        {
            new(root, ".", IsDirectory: true)
        };
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);
        while (pendingDirectories.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                var relativePath = Path.GetRelativePath(root, entry).Replace('\\', '/');
                if (!isDirectory)
                {
                    entries.Add(new ContentEntry(entry, relativePath, IsDirectory: false));
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.ReparsePoint) ||
                    new DirectoryInfo(entry).LinkTarget is not null)
                {
                    throw new InvalidOperationException(
                        "Managed Playwright MCP installations cannot contain directory links.");
                }

                entries.Add(new ContentEntry(entry, relativePath, IsDirectory: true));
                pendingDirectories.Push(entry);
            }
        }

        return entries;
    }

    private static string GetModeIdentity(string path)
        => OperatingSystem.IsWindows()
            ? "windows"
            : ((int)File.GetUnixFileMode(path)).ToString(CultureInfo.InvariantCulture);

    private static void EnsureManagedPathHasNoReparsePoints(
        string workspaceRoot,
        string targetPath)
    {
        var root = Path.GetFullPath(workspaceRoot);
        var target = Path.GetFullPath(targetPath);
        if (!string.Equals(root, target, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal) &&
            !IsContainedPath(root, target))
        {
            throw new InvalidOperationException(
                "The managed Playwright MCP path is outside the resolved workspace root.");
        }

        RejectReparsePointIfPresent(root);
        var relative = Path.GetRelativePath(root, target);
        if (relative == ".")
        {
            return;
        }

        var current = root;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!RejectReparsePointIfPresent(current))
            {
                break;
            }
        }
    }

    private static bool RejectReparsePointIfPresent(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException(
                    "Managed Playwright MCP paths cannot traverse links or reparse points.");
            }

            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static void AppendUtf8(
        IncrementalHash hash,
        string value)
        => hash.AppendData(System.Text.Encoding.UTF8.GetBytes(value));

    private static bool IsContainedPath(
        string root,
        string path)
    {
        var relative = Path.GetRelativePath(root, Path.GetFullPath(path));
        return !Path.IsPathRooted(relative) &&
               relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool IsNpxCommand(string command)
        => !string.IsNullOrWhiteSpace(command) &&
           new WorkspaceExecutableAuthorizationPolicy()
               .IsAllowedCommandName(command, ["npx"]);

    private static bool TrySplitPlaywrightMcpArguments(
        IReadOnlyList<string> arguments,
        out string packageSpec,
        out string version,
        out IReadOnlyList<string> serverArguments)
    {
        packageSpec = string.Empty;
        version = string.Empty;
        serverArguments = [];
        var packageIndex = Array.FindIndex(
            arguments.ToArray(),
            item => item.StartsWith(PackageName, StringComparison.Ordinal));
        if (packageIndex < 0)
        {
            return false;
        }

        packageSpec = arguments[packageIndex];
        var versionPrefix = PackageName + "@";
        version = packageSpec.StartsWith(versionPrefix, StringComparison.Ordinal)
            ? packageSpec[versionPrefix.Length..]
            : string.Empty;
        if (!ExactPackageVersion().IsMatch(version))
        {
            throw new InvalidOperationException(
                $"Playwright MCP must use an exact package version such as '{PackageName}@0.0.78'; tags and version ranges are not accepted.");
        }

        if (arguments.Take(packageIndex).Any(item => !IsNpxOnlyArgument(item)))
        {
            throw new InvalidOperationException(
                "Playwright MCP uses an unsupported npx option before the package selector.");
        }

        serverArguments = arguments.Skip(packageIndex + 1).ToArray();
        return true;
    }

    private static bool HasVisionCapability(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--caps", StringComparison.OrdinalIgnoreCase))
            {
                return index + 1 < arguments.Count &&
                       ArgumentContainsCapability(arguments[index + 1], "vision");
            }

            if (argument.StartsWith("--caps=", StringComparison.OrdinalIgnoreCase) &&
                ArgumentContainsCapability(argument["--caps=".Length..], "vision"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ArgumentContainsCapability(string argument, string capability)
        => argument
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => string.Equals(item, capability, StringComparison.OrdinalIgnoreCase));

    private static string ResolveCliPath(string versionRoot)
        => Path.Combine(
            versionRoot,
            "node_modules",
            "@playwright",
            "mcp",
            "cli.js");

    private static bool IsNpxOnlyArgument(string argument)
        => string.Equals(argument, "--yes", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(argument, "-y", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$")]
    private static partial Regex ExactPackageVersion();

    private sealed record NpmLaunch(
        string Command,
        IReadOnlyList<string> PrefixArguments);

    private sealed record InstallEvidence(
        int SchemaVersion,
        string PackageSpec,
        string Version,
        string CliSha256,
        string PackageLockSha256,
        string NodeExecutableSha256,
        string NodeExecutableMode,
        string ContentTreeSha256);

    private sealed record ContentEntry(
        string FullPath,
        string RelativePath,
        bool IsDirectory);
}
