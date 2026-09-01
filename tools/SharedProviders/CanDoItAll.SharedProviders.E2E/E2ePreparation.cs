using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.SharedProviders.E2E;

internal sealed record E2ePreparationOptions(
    string RepositoryRootPath,
    string ArtifactRootPath,
    bool Reset);

internal static class E2ePreparationCommandLine
{
    private const string RepositoryRootOption = "--repository-root";
    private const string ArtifactRootOption = "--artifact-root";
    private const string ResetOption = "--reset";

    public static bool IsPrepareCommand(string[] args)
        => args.Length > 0 && string.Equals(args[0], "prepare", StringComparison.Ordinal);

    public static E2ePreparationOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var values = ParseOptions(args);
        var repositoryRoot = ResolveRequiredPath(values, RepositoryRootOption);
        var artifactRoot = ResolveRequiredPath(values, ArtifactRootOption);
        var reset = values.TryGetValue(ResetOption, out var resetValue) &&
            bool.TryParse(resetValue, out var parsedReset)
                ? parsedReset
                : values.ContainsKey(ResetOption)
                    ? throw new E2eSafeException("Option '--reset' must be true or false.")
                    : false;
        return new E2ePreparationOptions(repositoryRoot, artifactRoot, reset);
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];
            if (option is not (RepositoryRootOption or ArtifactRootOption or ResetOption))
            {
                throw new E2eSafeException("The prepare command contains an unsupported option.");
            }

            if (index == args.Length - 1 || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new E2eSafeException($"Option '{option}' requires a value.");
            }

            if (!values.TryAdd(option, args[index + 1]))
            {
                throw new E2eSafeException($"Option '{option}' cannot be specified more than once.");
            }
        }

        return values;
    }

    private static string ResolveRequiredPath(
        IReadOnlyDictionary<string, string> values,
        string option)
    {
        if (!values.TryGetValue(option, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new E2eSafeException($"Option '{option}' is required.");
        }

        var path = Path.GetFullPath(value.Trim());
        if (string.Equals(path, Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase))
        {
            throw new E2eSafeException($"Option '{option}' cannot target a filesystem root.");
        }

        return Path.TrimEndingDirectorySeparator(path);
    }
}

internal sealed class E2ePreparationService
{
    internal const string RootMarkerFileName = ".shared-providers-e2e-root";
    internal const string RootMarkerValue = "CanDoItAll.SharedProviders.E2E/v1";
    internal const string ToolStateMarkerFileName = ".shared-providers-e2e-tool-state";
    internal const string ToolStateMarkerValue = "CanDoItAll.SharedProviders.E2E/tool-state/v1";

    private const string RuntimeSecretsDirectory = "runtime-secrets";
    private const string ToolStateDirectory = "tool-state";
    private const int MaximumOwnedTreeEntries = 100_000;

    private static readonly IReadOnlyList<string> RuntimeSecretFiles =
    [
        "db-admin-password",
        "db-central-password",
        "db-client-a-password",
        "db-client-b-password",
        "db-central-connection-string",
        "db-client-a-connection-string",
        "db-client-b-connection-string",
        "api-central-signing-key",
        "api-client-a-signing-key",
        "api-client-b-signing-key",
        "upstream-data-token",
        "upstream-control-token",
        "personal-upstream-data-token",
        "personal-upstream-control-token"
    ];

    private static readonly IReadOnlyList<string> BindDirectories =
    [
        Path.Combine("central", "data"),
        Path.Combine("client-a", "data"),
        Path.Combine("client-b", "data"),
        "logs",
        "tool-publish",
        ToolStateDirectory,
        Path.Combine(ToolStateDirectory, "credentials"),
        Path.Combine(ToolStateDirectory, "handoff"),
        Path.Combine(ToolStateDirectory, "logs"),
        Path.Combine(ToolStateDirectory, "scenario-results")
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly DurableFileWriter writer = new(new PhysicalFileSystemPathPolicyFactory());

    public async Task PrepareAsync(
        E2ePreparationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRoots(options);

        var markerPath = Path.Combine(options.ArtifactRootPath, RootMarkerFileName);
        if (options.Reset)
        {
            ResetArtifactRoot(options.ArtifactRootPath, markerPath);
        }

        Directory.CreateDirectory(options.ArtifactRootPath);
        EnsureRootIsNotReparsePoint(options.ArtifactRootPath);
        writer.EnsureDirectory(
            options.ArtifactRootPath,
            options.ArtifactRootPath,
            requirePrivateUnixMode: true);
        var markerExists = File.Exists(markerPath);
        if (markerExists)
        {
            ValidateMarker(markerPath);
        }
        else if (Directory.EnumerateFileSystemEntries(options.ArtifactRootPath).Any())
        {
            throw new E2eSafeException(
                "The exact E2E artifact root is non-empty and does not contain the expected marker.");
        }

        EnsureTreeHasNoReparsePoints(options.ArtifactRootPath);
        HardenWindowsArtifactTree(options.ArtifactRootPath);
        EnsureDirectories(options.ArtifactRootPath);
        HardenWindowsArtifactTree(options.ArtifactRootPath);
        if (!markerExists)
        {
            await writer.WriteTextAsync(
                options.ArtifactRootPath,
                markerPath,
                RootMarkerValue,
                DurableFileWriteOptions.CreateNew,
                cancellationToken);
        }

        var toolStateMarkerPath = Path.Combine(
            options.ArtifactRootPath,
            ToolStateDirectory,
            ToolStateMarkerFileName);
        if (!File.Exists(toolStateMarkerPath))
        {
            await writer.WriteTextAsync(
                options.ArtifactRootPath,
                toolStateMarkerPath,
                ToolStateMarkerValue,
                DurableFileWriteOptions.CreateNew,
                cancellationToken);
        }
        else if (!string.Equals(
                     ReadBoundedMarker(
                         toolStateMarkerPath,
                         "The E2E tool-state ownership marker could not be read."),
                     ToolStateMarkerValue,
                     StringComparison.Ordinal))
        {
            throw new E2eSafeException("The E2E tool-state ownership marker is invalid.");
        }

        var secretPaths = RuntimeSecretFiles
            .Select(fileName => Path.Combine(options.ArtifactRootPath, RuntimeSecretsDirectory, fileName))
            .ToArray();
        var existingSecretCount = secretPaths.Count(File.Exists);
        var generated = existingSecretCount == 0;
        if (existingSecretCount is > 0 && existingSecretCount != secretPaths.Length)
        {
            throw new E2eSafeException(
                "The E2E runtime-secret set is incomplete. Re-run prepare with --reset true.");
        }

        if (generated)
        {
            await GenerateRuntimeSecretsAsync(options.ArtifactRootPath, cancellationToken);
        }
        else
        {
            foreach (var secretPath in secretPaths)
            {
                writer.HardenPrivateFile(options.ArtifactRootPath, secretPath);
            }
        }

        await WritePreparationStateAsync(options, generated, cancellationToken);
    }

    private static void ValidateRoots(E2ePreparationOptions options)
    {
        if (!Directory.Exists(options.RepositoryRootPath) ||
            !File.Exists(Path.Combine(options.RepositoryRootPath, "compose.shared-providers.e2e.yaml")) ||
            !File.Exists(Path.Combine(
                options.RepositoryRootPath,
                "src",
                "App",
                "CanDoItAll.Web",
                "CanDoItAll.Web.csproj")) ||
            !Directory.Exists(Path.Combine(options.RepositoryRootPath, ".git")) &&
            !File.Exists(Path.Combine(options.RepositoryRootPath, ".git")))
        {
            throw new E2eSafeException(
                "The repository root is not the CanDoItAll checkout root required by this E2E lane.");
        }

        var expectedArtifactRoot = Path.GetFullPath(Path.Combine(
            options.RepositoryRootPath,
            ".artifacts",
            "shared-providers-e2e"));
        if (!string.Equals(
                expectedArtifactRoot,
                options.ArtifactRootPath,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new E2eSafeException(
                "The artifact root must be exactly <repository>/.artifacts/shared-providers-e2e.");
        }

        EnsureArtifactPathAncestorsAreNotReparsePoints(
            options.RepositoryRootPath,
            expectedArtifactRoot);
    }

    private static void EnsureArtifactPathAncestorsAreNotReparsePoints(
        string repositoryRoot,
        string artifactRoot)
    {
        foreach (var path in new[]
                 {
                     repositoryRoot,
                     Path.Combine(repositoryRoot, ".artifacts"),
                     artifactRoot
                 })
        {
            if (Directory.Exists(path) &&
                (File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
            {
                throw new E2eSafeException(
                    "The repository, artifact parent, and E2E artifact root must not contain reparse-point ancestors.");
            }
        }
    }

    private static void ResetArtifactRoot(string artifactRoot, string markerPath)
    {
        if (!Directory.Exists(artifactRoot))
        {
            return;
        }

        EnsureRootIsNotReparsePoint(artifactRoot);
        if (Directory.EnumerateFileSystemEntries(artifactRoot).Any())
        {
            if (!File.Exists(markerPath))
            {
                throw new E2eSafeException(
                    "Reset refused because the exact E2E artifact root has no ownership marker.");
            }

            ValidateMarker(markerPath);
            EnsureTreeHasNoReparsePoints(artifactRoot);
        }

        Directory.Delete(artifactRoot, recursive: true);
    }

    private static void ValidateMarker(string markerPath)
    {
        var marker = ReadBoundedMarker(
            markerPath,
            "The E2E artifact ownership marker could not be read.");

        if (!string.Equals(marker, RootMarkerValue, StringComparison.Ordinal))
        {
            throw new E2eSafeException("The E2E artifact ownership marker is invalid.");
        }
    }

    private static string ReadBoundedMarker(string path, string safeFailure)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists ||
                info.Attributes.HasFlag(FileAttributes.Directory) ||
                info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                info.Attributes.HasFlag(FileAttributes.Device) ||
                info.Length is <= 0 or > 256)
            {
                throw new E2eSafeException(safeFailure);
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 256,
                FileOptions.SequentialScan);
            if (!stream.CanSeek || stream.Length is <= 0 or > 256)
            {
                throw new E2eSafeException(safeFailure);
            }

            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 256,
                leaveOpen: false);
            var buffer = new char[257];
            var read = reader.ReadBlock(buffer, 0, buffer.Length);
            if (read == buffer.Length || reader.Read() >= 0)
            {
                throw new E2eSafeException(safeFailure);
            }

            return new string(buffer, 0, read).Trim();
        }
        catch (E2eSafeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new E2eSafeException(safeFailure, exception);
        }
    }

    private void EnsureDirectories(string artifactRoot)
    {
        writer.EnsureDirectory(
            artifactRoot,
            Path.Combine(artifactRoot, RuntimeSecretsDirectory),
            requirePrivateUnixMode: true);
        foreach (var relativePath in BindDirectories)
        {
            var privateDirectory = relativePath.StartsWith(
                ToolStateDirectory,
                StringComparison.Ordinal);
            writer.EnsureDirectory(
                artifactRoot,
                Path.Combine(artifactRoot, relativePath),
                requirePrivateUnixMode: privateDirectory);
        }
    }

    private async Task GenerateRuntimeSecretsAsync(
        string artifactRoot,
        CancellationToken cancellationToken)
    {
        var adminPassword = CreateRandomToken(32);
        var centralPassword = CreateRandomToken(32);
        var clientAPassword = CreateRandomToken(32);
        var clientBPassword = CreateRandomToken(32);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["db-admin-password"] = adminPassword,
            ["db-central-password"] = centralPassword,
            ["db-client-a-password"] = clientAPassword,
            ["db-client-b-password"] = clientBPassword,
            ["db-central-connection-string"] = CreateConnectionString(
                "candoitall_e2e_central",
                "candoitall_e2e_central",
                centralPassword),
            ["db-client-a-connection-string"] = CreateConnectionString(
                "candoitall_e2e_client_a",
                "candoitall_e2e_client_a",
                clientAPassword),
            ["db-client-b-connection-string"] = CreateConnectionString(
                "candoitall_e2e_client_b",
                "candoitall_e2e_client_b",
                clientBPassword),
            ["api-central-signing-key"] = CreateRandomToken(64),
            ["api-client-a-signing-key"] = CreateRandomToken(64),
            ["api-client-b-signing-key"] = CreateRandomToken(64),
            ["upstream-data-token"] = CreateRandomToken(48),
            ["upstream-control-token"] = CreateRandomToken(48),
            ["personal-upstream-data-token"] = CreateRandomToken(48),
            ["personal-upstream-control-token"] = CreateRandomToken(48)
        };
        if (values.Values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new E2eSafeException("The generated E2E credential set was not unique.");
        }

        foreach (var (fileName, value) in values)
        {
            await writer.WriteTextAsync(
                artifactRoot,
                Path.Combine(artifactRoot, RuntimeSecretsDirectory, fileName),
                value,
                new DurableFileWriteOptions
                {
                    CommitMode = DurableFileCommitMode.CreateNew,
                    RequirePrivateUnixMode = true
                },
                cancellationToken);
        }
    }

    private async Task WritePreparationStateAsync(
        E2ePreparationOptions options,
        bool generated,
        CancellationToken cancellationToken)
    {
        var state = new E2ePreparationState(
            SchemaVersion: 1,
            PreparedAtUtc: DateTimeOffset.UtcNow,
            RootMarker: RootMarkerValue,
            RuntimeSecretFileCount: RuntimeSecretFiles.Count,
            RuntimeSecretsGenerated: generated,
            BindDirectories: BindDirectories);
        await writer.WriteTextAsync(
            options.ArtifactRootPath,
            Path.Combine(
                options.ArtifactRootPath,
                ToolStateDirectory,
                "handoff",
                "prepare-state.json"),
            JsonSerializer.Serialize(state, JsonOptions),
            DurableFileWriteOptions.Default,
            cancellationToken);
    }

    private static string CreateRandomToken(int byteCount)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string CreateConnectionString(
        string database,
        string username,
        string password)
        => $"Host=db;Port=5432;Database={database};Username={username};Password={password};" +
            "Pooling=true;Timeout=15;Command Timeout=30;SSL Mode=Disable;GSS Encryption Mode=Disable";

    private static void EnsureRootIsNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
        {
            throw new E2eSafeException("The E2E artifact root cannot be a symbolic link or reparse point.");
        }
    }

    private static void EnsureTreeHasNoReparsePoints(string root)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));
        var entryCount = 0;
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                entryCount++;
                if (entryCount > MaximumOwnedTreeEntries)
                {
                    throw new E2eSafeException(
                        "The E2E artifact tree exceeded its bounded entry limit.");
                }

                if ((entry.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                {
                    throw new E2eSafeException(
                        "The E2E artifact tree contains a symbolic link, reparse point, or device.");
                }

                if (entry is DirectoryInfo childDirectory)
                {
                    pending.Push(childDirectory);
                }
            }
        }
    }

    private static void HardenWindowsArtifactTree(string artifactRoot)
    {
        if (OperatingSystem.IsWindows())
        {
            HardenWindowsArtifactTreeCore(artifactRoot);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void HardenWindowsArtifactTreeCore(string artifactRoot)
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new E2eSafeException("The current Windows identity has no security identifier.");
        var allowedSids = new[]
        {
            currentSid,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null)
        };

        SetRestrictedDirectoryAcl(new DirectoryInfo(artifactRoot), allowedSids);
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(artifactRoot));
        var entryCount = 0;
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                entryCount++;
                if (entryCount > MaximumOwnedTreeEntries)
                {
                    throw new E2eSafeException(
                        "The E2E artifact tree exceeded its bounded entry limit.");
                }

                if ((entry.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                {
                    throw new E2eSafeException(
                        "The E2E artifact tree contains a symbolic link, reparse point, or device.");
                }

                if (entry is DirectoryInfo childDirectory)
                {
                    SetRestrictedDirectoryAcl(childDirectory, allowedSids);
                    pending.Push(childDirectory);
                }
                else if (entry is FileInfo file)
                {
                    SetRestrictedFileAcl(file, allowedSids);
                }
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetRestrictedDirectoryAcl(
        DirectoryInfo directory,
        IReadOnlyCollection<SecurityIdentifier> allowedSids)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in allowedSids)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        directory.SetAccessControl(security);
        ValidateRestrictedAcl(directory.GetAccessControl(AccessControlSections.Access), allowedSids);
    }

    [SupportedOSPlatform("windows")]
    private static void SetRestrictedFileAcl(
        FileInfo file,
        IReadOnlyCollection<SecurityIdentifier> allowedSids)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in allowedSids)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                FileSystemRights.FullControl,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        file.SetAccessControl(security);
        ValidateRestrictedAcl(file.GetAccessControl(AccessControlSections.Access), allowedSids);
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateRestrictedAcl(
        FileSystemSecurity security,
        IReadOnlyCollection<SecurityIdentifier> allowedSids)
    {
        if (!security.AreAccessRulesProtected)
        {
            throw new E2eSafeException("The E2E artifact ACL still permits inherited access.");
        }

        var allowedValues = allowedSids
            .Select(sid => sid.Value)
            .ToHashSet(StringComparer.Ordinal);
        var rules = security
            .GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToArray();
        var allowRules = rules
            .Where(rule => rule.AccessControlType == AccessControlType.Allow)
            .ToArray();
        if (allowRules.Length == 0 ||
            allowRules.Any(rule =>
                rule.IdentityReference is not SecurityIdentifier sid ||
                !allowedValues.Contains(sid.Value)) ||
            allowedSids.Any(sid => !allowRules.Any(rule =>
                rule.IdentityReference is SecurityIdentifier ruleSid &&
                string.Equals(ruleSid.Value, sid.Value, StringComparison.Ordinal) &&
                (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)))
        {
            throw new E2eSafeException(
                "The E2E artifact ACL contains an unexpected or incomplete allow rule.");
        }
    }
}

internal sealed record E2ePreparationState(
    int SchemaVersion,
    DateTimeOffset PreparedAtUtc,
    string RootMarker,
    int RuntimeSecretFileCount,
    bool RuntimeSecretsGenerated,
    IReadOnlyList<string> BindDirectories);
