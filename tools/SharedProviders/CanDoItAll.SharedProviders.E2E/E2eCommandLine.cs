namespace CanDoItAll.SharedProviders.E2E;

internal enum E2eCommand
{
    SeedCentral,
    SeedClientA,
    SeedClientB,
    Snapshot,
    UnpublishText,
    RepublishText,
    SyncClientA,
    SyncClientB,
    SyncClientAExpectOffline,
    SyncClientBExpectOffline,
    PointClientAAtClientB,
    RestoreClientASource
}

internal enum E2eRole
{
    Central,
    ClientA,
    ClientB
}

internal sealed record E2eInvocation(
    E2eCommand Command,
    E2eRole Role,
    E2eOptions Options);

internal sealed class E2eOptions
{
    public required E2eRole Role { get; init; }

    public required string ArtifactRootPath { get; init; }

    public required string InstanceRootPath { get; init; }

    public required string DatabaseConnectionStringFilePath { get; init; }

    public required string ApiSigningKeyFilePath { get; init; }

    public string? UpstreamTokenFilePath { get; init; }

    public required Uri UpstreamBaseUri { get; init; }

    public required Uri ComfyUiBaseUri { get; init; }

    public required Uri CentralBaseUri { get; init; }

    public required Uri ClientBBaseUri { get; init; }

    public required string HostBindingId { get; init; }
}

internal static class E2eCommandLine
{
    private const string ArtifactRootOption = "--artifact-root";
    private const string InstanceRootOption = "--instance-root";
    private const string ConnectionStringFileOption = "--connection-string-file";
    private const string ApiSigningKeyFileOption = "--api-signing-key-file";
    private const string UpstreamTokenFileOption = "--upstream-token-file";
    private const string UpstreamUriOption = "--upstream-uri";
    private const string ComfyUiUriOption = "--comfyui-uri";
    private const string CentralUriOption = "--central-uri";
    private const string ClientBUriOption = "--client-b-uri";
    private const string HostBindingIdOption = "--host-binding-id";
    private const string RoleOption = "--role";

    private const string ArtifactRootEnvironment = "SHARED_PROVIDERS_E2E_ROOT";
    private const string InstanceRootEnvironment = "SHARED_PROVIDERS_E2E_INSTANCE_ROOT";
    private const string ConnectionStringFileEnvironment =
        "SHARED_PROVIDERS_E2E_DATABASE_CONNECTION_STRING_FILE";
    private const string ApiSigningKeyFileEnvironment =
        "SHARED_PROVIDERS_E2E_API_SIGNING_KEY_FILE";
    private const string UpstreamTokenFileEnvironment =
        "SHARED_PROVIDERS_E2E_UPSTREAM_TOKEN_FILE";
    private const string UpstreamUriEnvironment = "SHARED_PROVIDERS_E2E_UPSTREAM_URI";
    private const string ComfyUiUriEnvironment = "SHARED_PROVIDERS_E2E_COMFYUI_URI";
    private const string CentralUriEnvironment = "SHARED_PROVIDERS_E2E_CENTRAL_URI";
    private const string ClientBUriEnvironment = "SHARED_PROVIDERS_E2E_CLIENT_B_URI";
    private const string HostBindingIdEnvironment = "SHARED_PROVIDERS_E2E_HOST_BINDING_ID";

    private const string DefaultUpstreamUri = "http://deterministic-upstream:8080/v1";
    private const string DefaultComfyUiUri = "http://deterministic-upstream:8080";
    private const string DefaultCentralUri = "http://central:8080";
    private const string DefaultClientBUri = "http://client-b:8080";

    private static readonly IReadOnlyDictionary<string, E2eCommand> Commands =
        new Dictionary<string, E2eCommand>(StringComparer.Ordinal)
        {
            ["seed-central"] = E2eCommand.SeedCentral,
            ["seed-client-a"] = E2eCommand.SeedClientA,
            ["seed-client-b"] = E2eCommand.SeedClientB,
            ["snapshot"] = E2eCommand.Snapshot,
            ["unpublish-text"] = E2eCommand.UnpublishText,
            ["republish-text"] = E2eCommand.RepublishText,
            ["sync-client-a"] = E2eCommand.SyncClientA,
            ["sync-client-b"] = E2eCommand.SyncClientB,
            ["sync-client-a-expect-offline"] = E2eCommand.SyncClientAExpectOffline,
            ["sync-client-b-expect-offline"] = E2eCommand.SyncClientBExpectOffline,
            ["point-client-a-at-client-b"] = E2eCommand.PointClientAAtClientB,
            ["restore-client-a-source"] = E2eCommand.RestoreClientASource
        };

    private static readonly IReadOnlySet<string> KnownOptions = new HashSet<string>(
    [
        ArtifactRootOption,
        InstanceRootOption,
        ConnectionStringFileOption,
        ApiSigningKeyFileOption,
        UpstreamTokenFileOption,
        UpstreamUriOption,
        ComfyUiUriOption,
        CentralUriOption,
        ClientBUriOption,
        HostBindingIdOption,
        RoleOption
    ], StringComparer.Ordinal);

    public static E2eInvocation Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || !Commands.TryGetValue(args[0], out var command))
        {
            throw new E2eSafeException("A supported E2E command is required.");
        }

        var values = ParseOptions(args);
        var role = ResolveRole(command, GetOptional(values, RoleOption));
        var artifactRoot = ResolveRequiredPath(values, ArtifactRootOption, ArtifactRootEnvironment);
        var instanceRoot = ResolveRequiredPath(values, InstanceRootOption, InstanceRootEnvironment);
        EnsureDistinctRoots(artifactRoot, instanceRoot);

        var options = new E2eOptions
        {
            Role = role,
            ArtifactRootPath = artifactRoot,
            InstanceRootPath = instanceRoot,
            DatabaseConnectionStringFilePath = ResolveRequiredFile(
                values,
                ConnectionStringFileOption,
                ConnectionStringFileEnvironment),
            ApiSigningKeyFilePath = ResolveRequiredFile(
                values,
                ApiSigningKeyFileOption,
                ApiSigningKeyFileEnvironment),
            UpstreamTokenFilePath = ResolveOptionalFile(
                values,
                UpstreamTokenFileOption,
                UpstreamTokenFileEnvironment),
            UpstreamBaseUri = ResolveUri(
                values,
                UpstreamUriOption,
                UpstreamUriEnvironment,
                DefaultUpstreamUri),
            ComfyUiBaseUri = ResolveUri(
                values,
                ComfyUiUriOption,
                ComfyUiUriEnvironment,
                DefaultComfyUiUri),
            CentralBaseUri = ResolveUri(
                values,
                CentralUriOption,
                CentralUriEnvironment,
                DefaultCentralUri),
            ClientBBaseUri = ResolveUri(
                values,
                ClientBUriOption,
                ClientBUriEnvironment,
                DefaultClientBUri),
            HostBindingId = ResolveHostBindingId(values, role)
        };

        return new E2eInvocation(command, role, options);
    }

    public static string ToToken(E2eRole role) => role switch
    {
        E2eRole.Central => "central",
        E2eRole.ClientA => "client-a",
        E2eRole.ClientB => "client-b",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static string ToToken(E2eCommand command)
        => Commands.Single(pair => pair.Value == command).Key;

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];
            if (!KnownOptions.Contains(option))
            {
                throw new E2eSafeException("The E2E command contains an unsupported option.");
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

    private static E2eRole ResolveRole(E2eCommand command, string? roleValue)
    {
        if (command == E2eCommand.Snapshot)
        {
            return roleValue switch
            {
                "central" => E2eRole.Central,
                "client-a" => E2eRole.ClientA,
                "client-b" => E2eRole.ClientB,
                _ => throw new E2eSafeException(
                    "The snapshot command requires --role central, client-a, or client-b.")
            };
        }

        if (roleValue is not null)
        {
            throw new E2eSafeException("The --role option is valid only for snapshot.");
        }

        return command switch
        {
            E2eCommand.SeedCentral or
            E2eCommand.UnpublishText or
            E2eCommand.RepublishText => E2eRole.Central,
            E2eCommand.SeedClientA or
            E2eCommand.SyncClientA or
            E2eCommand.SyncClientAExpectOffline or
            E2eCommand.PointClientAAtClientB or
            E2eCommand.RestoreClientASource => E2eRole.ClientA,
            E2eCommand.SeedClientB or
            E2eCommand.SyncClientB or
            E2eCommand.SyncClientBExpectOffline => E2eRole.ClientB,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
        };
    }

    private static string ResolveRequiredPath(
        IReadOnlyDictionary<string, string> values,
        string option,
        string environmentVariable)
    {
        var value = ResolveValue(values, option, environmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new E2eSafeException($"Option '{option}' or its environment equivalent is required.");
        }

        var fullPath = Path.GetFullPath(value);
        if (string.Equals(fullPath, Path.GetPathRoot(fullPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new E2eSafeException($"Option '{option}' cannot target a filesystem root.");
        }

        return fullPath;
    }

    private static string ResolveRequiredFile(
        IReadOnlyDictionary<string, string> values,
        string option,
        string environmentVariable)
    {
        var path = ResolveRequiredPath(values, option, environmentVariable);
        if (!File.Exists(path))
        {
            throw new E2eSafeException($"The secret file configured by '{option}' does not exist.");
        }

        return path;
    }

    private static string? ResolveOptionalFile(
        IReadOnlyDictionary<string, string> values,
        string option,
        string environmentVariable)
    {
        var value = ResolveValue(values, option, environmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var path = Path.GetFullPath(value);
        if (!File.Exists(path))
        {
            throw new E2eSafeException($"The secret file configured by '{option}' does not exist.");
        }

        return path;
    }

    private static Uri ResolveUri(
        IReadOnlyDictionary<string, string> values,
        string option,
        string environmentVariable,
        string fallback)
    {
        var value = ResolveValue(values, option, environmentVariable) ?? fallback;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new E2eSafeException($"Option '{option}' must be an HTTP(S) base URI without credentials, query, or fragment.");
        }

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    private static string ResolveHostBindingId(
        IReadOnlyDictionary<string, string> values,
        E2eRole role)
    {
        var value = ResolveValue(values, HostBindingIdOption, HostBindingIdEnvironment)
            ?? $"shared-providers-e2e-{ToToken(role)}";
        if (value.Length is < 8 or > 128 ||
            value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            throw new E2eSafeException(
                "The E2E host binding id must contain 8-128 ASCII letters, digits, hyphens, or underscores.");
        }

        return value;
    }

    private static string? ResolveValue(
        IReadOnlyDictionary<string, string> values,
        string option,
        string environmentVariable)
    {
        if (values.TryGetValue(option, out var configured))
        {
            return configured.Trim();
        }

        var environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(environmentValue)
            ? null
            : environmentValue.Trim();
    }

    private static string? GetOptional(
        IReadOnlyDictionary<string, string> values,
        string option)
        => values.TryGetValue(option, out var value) ? value.Trim() : null;

    private static void EnsureDistinctRoots(string artifactRoot, string instanceRoot)
    {
        if (string.Equals(artifactRoot, instanceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new E2eSafeException("The artifact root and instance root must be different directories.");
        }
    }
}

internal sealed class E2eSafeException : InvalidOperationException
{
    public E2eSafeException(string message)
        : base(message)
    {
    }

    public E2eSafeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
